namespace LSMDisplayResizer.Settings;

/// <summary>Alle Einstellungen der App. Wird als JSON gespeichert.</summary>
internal sealed class AppSettings
{
    /// <summary>Automatische Anpassung aktiv (false = pausiert).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Prozessnamen (z. B. "game.exe"), deren Fenster nie angepasst werden.</summary>
    public List<string> ExcludedProcesses { get; set; } = [];

    /// <summary>Wartezeit nach der letzten Positionsänderung (Tastatur/Programm), bevor geprüft wird.</summary>
    public int DebounceMilliseconds { get; set; } = 300;

    /// <summary>Ausführliches Log, z. B. warum ein Fenster ignoriert wurde.</summary>
    public bool VerboseLog { get; set; }

    public AppSettings Clone()
    {
        var copy = (AppSettings)MemberwiseClone();
        copy.ExcludedProcesses = [.. ExcludedProcesses];
        return copy;
    }

    /// <summary>Korrigiert unplausible Werte (z. B. nach Handbearbeitung der JSON-Datei).</summary>
    public void Normalize()
    {
        ExcludedProcesses = (ExcludedProcesses ?? [])
            .Select(NormalizeProcessName)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        DebounceMilliseconds = Math.Clamp(DebounceMilliseconds, 50, 5000);
    }

    /// <summary>"Game" → "game.exe", "C:\x\Game.EXE" → "game.exe".</summary>
    public static string NormalizeProcessName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        string n = name.Trim().Trim('"');
        if (n == "*") return n;
        n = Path.GetFileName(n);
        if (!n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n += ".exe";
        return n.ToLowerInvariant();
    }
}
