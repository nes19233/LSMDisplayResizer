namespace LSMDisplayResizer.Settings;

/// <summary>Alle Einstellungen der App. Wird als JSON gespeichert.</summary>
internal sealed class AppSettings
{
    /// <summary>Automatische Anpassung aktiv (false = pausiert).</summary>
    public bool Enabled { get; set; } = true;

    public ResizeMode Mode { get; set; } = ResizeMode.Percent;

    /// <summary>Feste Regeln (nur im Modus <see cref="ResizeMode.FixedRules"/>).</summary>
    public List<FixedRule> Rules { get; set; } = [];

    /// <summary>Im Modus "Feste Regeln": Fenster ohne passende Regel prozentual anpassen.</summary>
    public bool PercentFallbackWithoutRule { get; set; }

    /// <summary>
    /// Beim Ziehen mit der Maus bleibt die linke obere Ecke dort, wo das Fenster abgelegt wurde.
    /// Bei false wird auch die Position relativ zum Arbeitsbereich übertragen.
    /// </summary>
    public bool KeepDropPositionOnDrag { get; set; } = true;

    /// <summary>Prozessnamen (z. B. "game.exe"), deren Fenster nie angepasst werden.</summary>
    public List<string> ExcludedProcesses { get; set; } = [];

    /// <summary>Globales Tastenkürzel für die manuelle Anpassung des aktiven Fensters.</summary>
    public bool HotkeyEnabled { get; set; }

    public Keys Hotkey { get; set; } = Keys.Control | Keys.Alt | Keys.R;

    /// <summary>
    /// Fenster, die die volle Höhe des Arbeitsbereichs nutzen (angedockt bzw. von Windows nur aufgezogen statt
    /// maximiert), wie maximierte Fenster behandeln: Größe nicht merken, nicht anpassen.
    /// </summary>
    public bool TreatFullHeightAsSnapped { get; set; } = true;

    /// <summary>
    /// Nach Anzeigeänderungen (An-/Abdocken, Monitor ab-/angesteckt, Standby) Fenster, die Windows auf einen
    /// anderen Monitor geschoben hat, anpassen – ausgehend von ihrem Zustand vor der Änderung.
    /// </summary>
    public bool AdjustAfterDisplayChange { get; set; } = true;

    /// <summary>Wartezeit nach einer Anzeigeänderung, bis Windows die Fenster umgeräumt hat.</summary>
    public int DisplayChangeDelaySeconds { get; set; } = 8;

    /// <summary>Wartezeit nach der letzten Positionsänderung (Tastatur/Programm), bevor geprüft wird.</summary>
    public int DebounceMilliseconds { get; set; } = 300;

    /// <summary>Ausführliches Log, z. B. warum ein Fenster ignoriert wurde.</summary>
    public bool VerboseLog { get; set; }

    /// <summary>Monitorübergreifendes Hintergrundbild.</summary>
    public WallpaperSettings Wallpaper { get; set; } = new();

    public AppSettings Clone()
    {
        var copy = (AppSettings)MemberwiseClone();
        copy.Rules = Rules.Select(r => r.Clone()).ToList();
        copy.ExcludedProcesses = [.. ExcludedProcesses];
        copy.Wallpaper = Wallpaper.Clone();
        return copy;
    }

    /// <summary>Korrigiert unplausible Werte (z. B. nach Handbearbeitung der JSON-Datei).</summary>
    public void Normalize()
    {
        Rules = (Rules ?? []).Where(r => r != null).ToList();
        foreach (var rule in Rules) rule.Normalize();
        Rules.RemoveAll(r => r.ProcessName.Length == 0);

        ExcludedProcesses = (ExcludedProcesses ?? [])
            .Select(NormalizeProcessName)
            .Where(p => p.Length > 0 && p != FixedRule.Any)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        DebounceMilliseconds = Math.Clamp(DebounceMilliseconds, 50, 5000);
        DisplayChangeDelaySeconds = Math.Clamp(DisplayChangeDelaySeconds, 2, 60);
        if (!Enum.IsDefined(Mode)) Mode = ResizeMode.Percent;
        Wallpaper ??= new WallpaperSettings();
        Wallpaper.Normalize();
    }

    /// <summary>"Game" → "game.exe", "C:\x\Game.EXE" → "game.exe", "*" bleibt "*".</summary>
    public static string NormalizeProcessName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        string n = name.Trim().Trim('"');
        if (n == FixedRule.Any) return n;
        n = Path.GetFileName(n);
        if (n.Length == 0) return string.Empty;
        if (!n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n += ".exe";
        return n.ToLowerInvariant();
    }
}
