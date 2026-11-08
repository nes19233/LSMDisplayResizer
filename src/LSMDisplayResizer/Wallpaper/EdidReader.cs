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
    public static (double WidthMm, double HeightMm)? ReadSize(string deviceInterfacePath) =>
        ReadEdid(deviceInterfacePath) is { } edid ? Parse(edid) : null;

    /// <summary>
    /// Kennung des physischen Geräts (Hersteller + Seriennummer), oder null. Bei Bild-neben-Bild (PbP) melden beide
    /// Eingänge eines Monitors dieselbe Seriennummer, aber unterschiedliche Produktcodes.
    /// </summary>
    public static string? ReadDeviceSerial(string deviceInterfacePath) =>
        ReadEdid(deviceInterfacePath) is { } edid ? ParseDeviceSerial(edid) : null;

    private static byte[]? ReadEdid(string deviceInterfacePath)
    {
        try
        {
            string[] parts = deviceInterfacePath.Split('#');
            if (parts.Length < 3 || !parts[0].EndsWith("DISPLAY", StringComparison.OrdinalIgnoreCase)) return null;

            string keyPath = $@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{parts[1]}\{parts[2]}\Device Parameters";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            return key?.GetValue("EDID") as byte[];
        }
        catch (Exception ex)
        {
            Log.Debug($"EDID für {deviceInterfacePath} nicht lesbar: {ex.Message}");
            return null;
        }
    }

    /// <summary>Hersteller (3 Buchstaben) + Seriennummer: bevorzugt der Text-Deskriptor 0xFF, sonst die 32-Bit-Nummer.</summary>
    public static string? ParseDeviceSerial(byte[] edid)
    {
        if (edid.Length < 128 || edid[0] != 0x00 || edid[1] != 0xFF || edid[7] != 0x00) return null;

        int m = (edid[8] << 8) | edid[9];
        string manufacturer = new([(char)('A' - 1 + ((m >> 10) & 0x1F)), (char)('A' - 1 + ((m >> 5) & 0x1F)), (char)('A' - 1 + (m & 0x1F))]);

        foreach (int off in new[] { 54, 72, 90, 108 })
        {
            if (edid[off] == 0 && edid[off + 1] == 0 && edid[off + 2] == 0 && edid[off + 3] == 0xFF)
            {
                string text = System.Text.Encoding.ASCII.GetString(edid, off + 5, 13).Split('\n')[0].Trim();
                if (text.Length > 0) return $"{manufacturer}:{text}";
            }
        }

        uint number = BitConverter.ToUInt32(edid, 12);
        // 0 und 0x01010101 sind übliche Platzhalter – damit lassen sich Geräte nicht unterscheiden.
        return number is 0 or 0x01010101 ? null : $"{manufacturer}:#{number}";
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
