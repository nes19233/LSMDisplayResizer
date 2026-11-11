using System.Drawing;

namespace LSMDisplayResizer.Monitors;

/// <summary>Ein angeschlossener Monitor mit stabiler Kennung und aktuellen Abmessungen (echte Pixel).</summary>
/// <param name="Handle">HMONITOR (ändert sich bei Anzeigeänderungen).</param>
/// <param name="DeviceName">GDI-Name, z. B. "\\.\DISPLAY1" (nicht stabil).</param>
/// <param name="DeviceId">Stabile Geräte-ID (Geräteschnittstellenpfad aus EnumDisplayDevices).</param>
/// <param name="ModelKey">Hersteller/Modell aus der Geräte-ID, z. B. "DEL4127".</param>
/// <param name="FriendlyName">Lesbarer Name, z. B. "DELL U2720Q".</param>
/// <param name="Bounds">Gesamter Monitorbereich (rcMonitor).</param>
/// <param name="WorkArea">Arbeitsbereich ohne Taskleiste (rcWork).</param>
/// <param name="Scale">Anzeigeskalierung (1,0 = 100 %, 2,0 = 200 %).</param>
/// <param name="StableKey">Kennung, die über An-/Abdocken hinweg gleich bleibt (EDID-Seriennummer, siehe MonitorManager).</param>
internal sealed record MonitorDescriptor(
    IntPtr Handle,
    string DeviceName,
    string DeviceId,
    string ModelKey,
    string FriendlyName,
    Rectangle Bounds,
    Rectangle WorkArea,
    bool IsPrimary,
    double Scale = 1.0,
    string StableKey = "")
{
    /// <summary>Stabile Kennung, ersatzweise die Geräte-ID.</summary>
    public string Key => string.IsNullOrEmpty(StableKey) ? DeviceId : StableKey;

    public string ShortDeviceName => DeviceName.Replace(@"\\.\", string.Empty);

    public string DisplayName =>
        $"{FriendlyName} – {ShortDeviceName} ({Bounds.Width}×{Bounds.Height}){(IsPrimary ? Localization.L.T(", Hauptmonitor") : string.Empty)}";

    /// <summary>
    /// Holt den Modellteil aus einem Geräteschnittstellenpfad:
    /// "\\?\DISPLAY#DEL4127#5&amp;1a2b&amp;0&amp;UID4353#{e6f07b5f-...}" → "DEL4127".
    /// </summary>
    public static string ExtractModelKey(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return string.Empty;
        string[] parts = deviceId.Split('#');
        return parts.Length >= 3 && parts[0].EndsWith("DISPLAY", StringComparison.OrdinalIgnoreCase)
            ? parts[1].ToUpperInvariant()
            : string.Empty;
    }
}
