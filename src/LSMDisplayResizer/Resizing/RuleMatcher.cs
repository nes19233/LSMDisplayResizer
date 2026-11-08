using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Settings;

namespace LSMDisplayResizer.Resizing;

/// <summary>Sucht die passendste feste Regel für Programm und Monitor.</summary>
internal static class RuleMatcher
{
    /// <summary>
    /// Bewertung: Programm exakt (2) vor "*" (1); Monitor exakt per Geräte-ID (3) vor gleichem Modell
    /// (2, z. B. nach Umstecken an einen anderen Anschluss) vor "Alle Monitore" (1).
    /// Bei Gleichstand gewinnt die weiter oben stehende Regel.
    /// </summary>
    public static FixedRule? Find(IEnumerable<FixedRule> rules, string processName, string monitorId, string monitorModelKey)
    {
        FixedRule? best = null;
        int bestScore = 0;

        foreach (var rule in rules)
        {
            int processScore = ProcessScore(rule, processName);
            if (processScore == 0) continue;

            int monitorScore = MonitorScore(rule, monitorId, monitorModelKey);
            if (monitorScore == 0) continue;

            int score = monitorScore * 10 + processScore;
            if (score > bestScore)
            {
                best = rule;
                bestScore = score;
            }
        }
        return best;
    }

    public static FixedRule? Find(IEnumerable<FixedRule> rules, string processName, MonitorDescriptor monitor) =>
        Find(rules, processName, monitor.DeviceId, monitor.ModelKey);

    private static int ProcessScore(FixedRule rule, string processName)
    {
        if (rule.ProcessName == FixedRule.Any) return 1;
        return string.Equals(rule.ProcessName, processName, StringComparison.OrdinalIgnoreCase) ? 2 : 0;
    }

    private static int MonitorScore(FixedRule rule, string monitorId, string monitorModelKey)
    {
        if (rule.IsAnyMonitor) return 1;
        if (string.Equals(rule.MonitorId, monitorId, StringComparison.OrdinalIgnoreCase)) return 3;

        string ruleModel = MonitorDescriptor.ExtractModelKey(rule.MonitorId);
        return ruleModel.Length > 0 && string.Equals(ruleModel, monitorModelKey, StringComparison.OrdinalIgnoreCase) ? 2 : 0;
    }
}
