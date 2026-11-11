using System.Runtime.InteropServices;
using LSMDisplayResizer.Interop;
using LSMDisplayResizer.Logging;
using static LSMDisplayResizer.Interop.NativeMethods;

namespace LSMDisplayResizer.Monitors;

/// <summary>
/// Liefert Monitorinformationen. Die Identität (Geräte-ID, Name) wird pro GDI-Gerätename
/// zwischengespeichert, Abmessungen und Arbeitsbereich werden bei jedem Aufruf frisch gelesen.
/// </summary>
internal sealed class MonitorManager
{
    private readonly record struct Identity(string DeviceId, string ModelKey, string FriendlyName, string StableKey = "");

    private readonly object _sync = new();
    private Dictionary<string, Identity> _identities = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Verwirft die zwischengespeicherten Identitäten (nach Anzeigeänderungen aufrufen).</summary>
    public void Invalidate()
    {
        lock (_sync)
        {
            _identities = new Dictionary<string, Identity>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public MonitorDescriptor? FromWindow(IntPtr hwnd)
    {
        IntPtr hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        return hmon == IntPtr.Zero ? null : FromHandle(hmon);
    }

    public MonitorDescriptor? FromHandle(IntPtr hmon)
    {
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>(), szDevice = string.Empty };
        if (!GetMonitorInfo(hmon, ref mi)) return null;

        Identity id = GetIdentity(mi.szDevice);
        return new MonitorDescriptor(
            hmon,
            mi.szDevice,
            id.DeviceId,
            id.ModelKey,
            id.FriendlyName,
            mi.rcMonitor.ToRectangle(),
            mi.rcWork.ToRectangle(),
            (mi.dwFlags & MONITORINFOF_PRIMARY) != 0,
            ReadScale(hmon),
            string.IsNullOrEmpty(id.StableKey) ? id.DeviceId : id.StableKey);
    }

    private static double ReadScale(IntPtr hmon)
    {
        try
        {
            return GetDpiForMonitor(hmon, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0 ? dpiX / 96.0 : 1.0;
        }
        catch
        {
            return 1.0; // älteres Windows ohne shcore-Funktion
        }
    }

    public IReadOnlyList<MonitorDescriptor> GetAll()
    {
        var handles = new List<IntPtr>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref RECT _, IntPtr _) =>
        {
            handles.Add(h);
            return true;
        }, IntPtr.Zero);

        return handles
            .Select(FromHandle)
            .OfType<MonitorDescriptor>()
            .OrderBy(m => m.Bounds.Left)
            .ThenBy(m => m.Bounds.Top)
            .ToList();
    }

    private Identity GetIdentity(string gdiDeviceName)
    {
        lock (_sync)
        {
            if (_identities.TryGetValue(gdiDeviceName, out var cached)) return cached;
        }

        // Alle Monitore auf einmal einlesen: Die stabile Kennung hängt von den anderen Monitoren ab
        // (zwei Hälften desselben Monitors werden nach links/rechts unterschieden).
        var all = ReadAllIdentities();
        lock (_sync)
        {
            foreach (var (name, id) in all) _identities[name] = id;
            if (_identities.TryGetValue(gdiDeviceName, out var found)) return found;
        }

        Identity identity = ReadIdentity(gdiDeviceName);
        lock (_sync)
        {
            _identities[gdiDeviceName] = identity;
        }
        return identity;
    }

    private static Dictionary<string, Identity> ReadAllIdentities()
    {
        var monitors = new List<(string Gdi, int Left, int Top, Identity Id, string? Serial)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>(), szDevice = string.Empty };
            if (GetMonitorInfo(h, ref mi))
            {
                var id = ReadIdentity(mi.szDevice);
                monitors.Add((mi.szDevice, mi.rcMonitor.Left, mi.rcMonitor.Top, id, Wallpaper.EdidReader.ReadDeviceSerial(id.DeviceId)));
            }
            return true;
        }, IntPtr.Zero);

        var keys = ComputeStableKeys(monitors.Select(m => (m.Gdi, m.Left, m.Top, m.Serial, m.Id.ModelKey, m.Id.DeviceId)).ToList());
        return monitors.ToDictionary(m => m.Gdi, m => m.Id with { StableKey = keys[m.Gdi] }, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Stabile Kennung je Monitor für die Wiedererkennung über An-/Abdocken hinweg. Windows vergibt Geräte-IDs je nach
    /// Anschluss, Dock und Modus neu (ein Dell U3425WE meldete sich mit 13 verschiedenen IDs) – die EDID-Seriennummer
    /// bleibt gleich. Bildflächen mit derselben Seriennummer (Bild-neben-Bild) werden von links nach rechts nummeriert.
    /// Ohne Seriennummer: Modell, sonst Geräte-ID.
    /// </summary>
    public static Dictionary<string, string> ComputeStableKeys(
        IReadOnlyList<(string Gdi, int Left, int Top, string? Serial, string ModelKey, string DeviceId)> monitors)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in monitors.GroupBy(m => !string.IsNullOrEmpty(m.Serial) ? "S:" + m.Serial
                                                     : !string.IsNullOrEmpty(m.ModelKey) ? "M:" + m.ModelKey
                                                     : "D:" + m.DeviceId, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(m => m.Left).ThenBy(m => m.Top).ToList();
            for (int i = 0; i < ordered.Count; i++)
                result[ordered[i].Gdi] = ordered.Count == 1 ? group.Key : $"{group.Key}#{i + 1}";
        }
        return result;
    }

    private static Identity ReadIdentity(string gdiDeviceName)
    {
        string deviceId = string.Empty;
        string deviceString = string.Empty;

        try
        {
            // Monitore am Ausgang "\\.\DISPLAYn" durchgehen; mit EDD_GET_DEVICE_INTERFACE_NAME liefert
            // DeviceID den stabilen Schnittstellenpfad "\\?\DISPLAY#DEL4127#...#{GUID}".
            for (uint i = 0; ; i++)
            {
                var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                if (!EnumDisplayDevices(gdiDeviceName, i, ref dd, EDD_GET_DEVICE_INTERFACE_NAME)) break;
                if (string.IsNullOrEmpty(dd.DeviceID)) continue;

                bool active = (dd.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0;
                if (deviceId.Length == 0 || active)
                {
                    deviceId = dd.DeviceID;
                    deviceString = dd.DeviceString;
                }
                if (active) break;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"EnumDisplayDevices für {gdiDeviceName} fehlgeschlagen", ex);
        }

        var (friendly, devicePath) = ReadDisplayConfigName(gdiDeviceName);
        if (deviceId.Length == 0) deviceId = devicePath;
        if (deviceId.Length == 0) deviceId = gdiDeviceName; // letzte Rückfallebene

        string name = !string.IsNullOrWhiteSpace(friendly) ? friendly
            : !string.IsNullOrWhiteSpace(deviceString) ? deviceString
            : "Monitor";

        return new Identity(deviceId, MonitorDescriptor.ExtractModelKey(deviceId), name);
    }

    /// <summary>Liest über QueryDisplayConfig den Anzeigenamen des Monitors (z. B. "DELL U2720Q").</summary>
    private static (string FriendlyName, string DevicePath) ReadDisplayConfigName(string gdiDeviceName)
    {
        try
        {
            DISPLAYCONFIG_PATH_INFO[] paths;
            uint pathCount, modeCount;
            int result;
            do
            {
                result = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount);
                if (result != ERROR_SUCCESS) return (string.Empty, string.Empty);
                paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
                result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            }
            while (result == ERROR_INSUFFICIENT_BUFFER);

            if (result != ERROR_SUCCESS) return (string.Empty, string.Empty);

            for (int i = 0; i < pathCount; i++)
            {
                var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                        size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                        adapterId = paths[i].sourceInfo.adapterId,
                        id = paths[i].sourceInfo.id,
                    },
                    viewGdiDeviceName = string.Empty,
                };
                if (DisplayConfigGetDeviceInfo(ref source) != ERROR_SUCCESS) continue;
                if (!string.Equals(source.viewGdiDeviceName, gdiDeviceName, StringComparison.OrdinalIgnoreCase)) continue;

                var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                        size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                        adapterId = paths[i].targetInfo.adapterId,
                        id = paths[i].targetInfo.id,
                    },
                    monitorFriendlyDeviceName = string.Empty,
                    monitorDevicePath = string.Empty,
                };
                if (DisplayConfigGetDeviceInfo(ref target) != ERROR_SUCCESS) continue;

                return (target.monitorFriendlyDeviceName ?? string.Empty, target.monitorDevicePath ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"QueryDisplayConfig für {gdiDeviceName} fehlgeschlagen", ex);
        }
        return (string.Empty, string.Empty);
    }
}
