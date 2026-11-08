namespace LSMDisplayResizer.Settings;

/// <summary>Wie die Zielgröße berechnet wird.</summary>
internal enum ResizeMode
{
    /// <summary>Größe und Position relativ zum Arbeitsbereich übertragen.</summary>
    Percent,

    /// <summary>Zielgröße pro Programm und Monitor aus <see cref="AppSettings.Rules"/>.</summary>
    FixedRules,
}

internal enum SizeUnit
{
    Percent,
    Pixels,
}

/// <summary>Feste Zielgröße für ein Programm auf einem Monitor.</summary>
internal sealed class FixedRule
{
    /// <summary>Wert für <see cref="MonitorId"/> bzw. <see cref="ProcessName"/>: gilt für alle.</summary>
    public const string Any = "*";

    /// <summary>Prozessname, z. B. "chrome.exe", oder "*".</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>Stabile Monitor-ID (siehe MonitorDescriptor.DeviceId) oder "*".</summary>
    public string MonitorId { get; set; } = Any;

    /// <summary>Anzeigename des Monitors zum Zeitpunkt der Regelerstellung (nur zur Anzeige).</summary>
    public string MonitorName { get; set; } = string.Empty;

    /// <summary>Breite des sichtbaren Fensterrahmens.</summary>
    public double Width { get; set; } = 60;

    /// <summary>Höhe des sichtbaren Fensterrahmens.</summary>
    public double Height { get; set; } = 70;

    public SizeUnit Unit { get; set; } = SizeUnit.Percent;

    public bool IsAnyMonitor => string.IsNullOrEmpty(MonitorId) || MonitorId == Any;

    public FixedRule Clone() => (FixedRule)MemberwiseClone();

    public string UnitText => Unit == SizeUnit.Percent ? "%" : "px";

    public string MonitorText => IsAnyMonitor ? "Alle Monitore" : (string.IsNullOrEmpty(MonitorName) ? MonitorId : MonitorName);

    public void Normalize()
    {
        ProcessName = AppSettings.NormalizeProcessName(ProcessName);
        if (string.IsNullOrWhiteSpace(MonitorId)) MonitorId = Any;
        MonitorName ??= string.Empty;
        if (Unit == SizeUnit.Percent)
        {
            Width = Math.Clamp(Width, 5, 100);
            Height = Math.Clamp(Height, 5, 100);
        }
        else
        {
            Width = Math.Clamp(Math.Round(Width), 50, 20000);
            Height = Math.Clamp(Math.Round(Height), 50, 20000);
        }
    }
}
