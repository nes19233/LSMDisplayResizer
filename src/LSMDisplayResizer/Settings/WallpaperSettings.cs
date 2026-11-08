namespace LSMDisplayResizer.Settings;

/// <summary>Wie das Bild auf die Monitore verteilt wird.</summary>
internal enum WallpaperMode
{
    /// <summary>Nach physischer Größe (mm): Übergänge stimmen auch bei unterschiedlich großen Monitoren.</summary>
    Physical,

    /// <summary>Nach Pixeln, so wie die Monitore in den Windows-Anzeigeeinstellungen angeordnet sind.</summary>
    Pixels,
}

/// <summary>Vertikale Ausrichtung nebeneinander stehender Monitore (nur physischer Modus).</summary>
internal enum WallpaperAlignment
{
    Top,
    Center,
    Bottom,
}

/// <summary>Physische Größe eines Monitors (sichtbare Bildfläche) und Feinjustierung.</summary>
internal sealed class MonitorPhysicalSize
{
    public string MonitorId { get; set; } = string.Empty;

    /// <summary>Nur zur Anzeige / Orientierung in der JSON-Datei.</summary>
    public string MonitorName { get; set; } = string.Empty;

    public double WidthMm { get; set; }

    public double HeightMm { get; set; }

    /// <summary>Vertikaler Versatz in mm (positiv = Monitor steht tiefer).</summary>
    public double OffsetYMm { get; set; }

    public MonitorPhysicalSize Clone() => (MonitorPhysicalSize)MemberwiseClone();
}

/// <summary>Einstellungen für das monitorübergreifende Hintergrundbild.</summary>
internal sealed class WallpaperSettings
{
    /// <summary>Quellbild. Leer = Funktion nicht in Benutzung.</summary>
    public string SourceImage { get; set; } = string.Empty;

    public WallpaperMode Mode { get; set; } = WallpaperMode.Physical;

    /// <summary>
    /// Optionale Rahmenkorrektur: Abstand zwischen den Bildflächen nebeneinander stehender Monitore in mm.
    /// Standard 0 = Bilder stoßen direkt aneinander.
    /// </summary>
    public double GapMm { get; set; } = 0;

    public WallpaperAlignment Alignment { get; set; } = WallpaperAlignment.Center;

    /// <summary>Bei Änderung der Monitoranordnung (oder der Einstellungen) automatisch neu erstellen.</summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>Gespeicherte bzw. angepasste Monitorgrößen. Fehlende Monitore werden aus der EDID gelesen.</summary>
    public List<MonitorPhysicalSize> Monitors { get; set; } = [];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SourceImage);

    public WallpaperSettings Clone()
    {
        var copy = (WallpaperSettings)MemberwiseClone();
        copy.Monitors = Monitors.Select(m => m.Clone()).ToList();
        return copy;
    }

    public void Normalize()
    {
        SourceImage ??= string.Empty;
        if (!Enum.IsDefined(Mode)) Mode = WallpaperMode.Physical;
        if (!Enum.IsDefined(Alignment)) Alignment = WallpaperAlignment.Center;
        GapMm = Math.Clamp(double.IsFinite(GapMm) ? GapMm : 0, 0, 300);
        Monitors = (Monitors ?? []).Where(m => m != null && !string.IsNullOrWhiteSpace(m.MonitorId)).ToList();
        foreach (var m in Monitors)
        {
            m.WidthMm = Math.Clamp(double.IsFinite(m.WidthMm) ? m.WidthMm : 0, 0, 5000);
            m.HeightMm = Math.Clamp(double.IsFinite(m.HeightMm) ? m.HeightMm : 0, 0, 5000);
            m.OffsetYMm = Math.Clamp(double.IsFinite(m.OffsetYMm) ? m.OffsetYMm : 0, -2000, 2000);
            m.MonitorName ??= string.Empty;
        }
        Monitors.RemoveAll(m => m.WidthMm < 50 || m.HeightMm < 50);
    }
}
