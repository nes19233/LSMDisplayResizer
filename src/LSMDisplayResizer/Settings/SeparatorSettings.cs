using System.Globalization;

namespace LSMDisplayResizer.Settings;

/// <summary>Feine Trennlinie zwischen den Monitoren (optional).</summary>
internal sealed class SeparatorSettings
{
    public bool Enabled { get; set; }

    /// <summary>Farbe als "#RRGGBB".</summary>
    public string Color { get; set; } = "#808080";

    /// <summary>Stärke in Pixeln (1–4) – bewusst dünn, damit die Linie nicht stört.</summary>
    public int Thickness { get; set; } = 1;

    /// <summary>Deckkraft in Prozent (10–100).</summary>
    public int OpacityPercent { get; set; } = 60;

    public SeparatorSettings Clone() => (SeparatorSettings)MemberwiseClone();

    public void Normalize()
    {
        Thickness = Math.Clamp(Thickness, 1, 4);
        OpacityPercent = Math.Clamp(OpacityPercent, 10, 100);
        Color = ToHex(ParseColor(Color));
    }

    public System.Drawing.Color GetColor() => ParseColor(Color);

    public static System.Drawing.Color ParseColor(string? hex)
    {
        string h = (hex ?? string.Empty).Trim().TrimStart('#');
        return h.Length == 6 && int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)
            ? System.Drawing.Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF)
            : System.Drawing.Color.FromArgb(0x80, 0x80, 0x80);
    }

    public static string ToHex(System.Drawing.Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
