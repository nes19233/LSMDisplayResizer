using LSMDisplayResizer.Logging;
using Microsoft.Win32;

namespace LSMDisplayResizer.Wallpaper;

/// <summary>Liest die physische Bildgröße eines Monitors aus seinen EDID-Daten (Registry, ohne Adminrechte).</summary>
internal static class EdidReader
{
    /// <summary>
    /// Größe der Bildfläche in mm in der nativen Ausrichtung des Monitors, oder null.
    /// </summary>
    /// <param name="deviceInterfacePath">"\\?\DISPLAY#DEL4127#5&amp;1a2b&amp;0&amp;UID4353#{GUID}"</param>
    public static (double WidthMm, double HeightMm)? ReadSize(string deviceInterfacePath)
    {
        try
        {
            string[] parts = deviceInterfacePath.Split('#');
            if (parts.Length < 3 || !parts[0].EndsWith("DISPLAY", StringComparison.OrdinalIgnoreCase)) return null;

            string keyPath = $@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{parts[1]}\{parts[2]}\Device Parameters";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            return key?.GetValue("EDID") is byte[] edid ? Parse(edid) : null;
        }
        catch (Exception ex)
        {
            Log.Debug($"EDID für {deviceInterfacePath} nicht lesbar: {ex.Message}");
            return null;
        }
    }

    /// <summary>Wertet einen EDID-Block aus: bevorzugt mm aus dem ersten Detailed-Timing-Deskriptor, sonst cm aus dem Basisblock.</summary>
    public static (double WidthMm, double HeightMm)? Parse(byte[] edid)
    {
        if (edid.Length < 128) return null;
        if (edid[0] != 0x00 || edid[1] != 0xFF || edid[7] != 0x00) return null; // EDID-Header 00 FF FF FF FF FF FF 00

        // Detailed Timing Descriptor 1 ab Byte 54 (Pixeltakt != 0 → Timing-Deskriptor)
        if (edid[54] != 0 || edid[55] != 0)
        {
            int wMm = edid[66] | ((edid[68] & 0xF0) << 4);
            int hMm = edid[67] | ((edid[68] & 0x0F) << 8);
            if (IsPlausible(wMm, hMm)) return (wMm, hMm);
        }

        int wCm = edid[21], hCm = edid[22];
        if (IsPlausible(wCm * 10, hCm * 10)) return (wCm * 10, hCm * 10);
        return null;
    }

    // Unter 10 cm oder über 4 m sind Platzhalterwerte (z. B. Projektoren melden 0).
    private static bool IsPlausible(int wMm, int hMm) => wMm is >= 100 and <= 4000 && hMm is >= 100 and <= 4000;
}
