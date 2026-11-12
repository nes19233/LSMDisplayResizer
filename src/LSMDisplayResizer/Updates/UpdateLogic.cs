using System.Text.Json;

namespace LSMDisplayResizer.Updates;

/// <summary>Ein veröffentlichtes Release auf GitHub.</summary>
/// <param name="Version">Version ohne "v", z. B. "1.0.9".</param>
/// <param name="PageUrl">Release-Seite im Browser.</param>
/// <param name="Assets">Dateiname → Download-URL.</param>
internal sealed record ReleaseInfo(string Version, string PageUrl, IReadOnlyDictionary<string, string> Assets);

/// <summary>Wie die laufende App installiert ist – bestimmt, ob sie sich selbst aktualisieren darf.</summary>
internal enum InstallKind
{
    /// <summary>MSI für den aktuellen Benutzer (%LocalAppData%\Programs) – Update ohne Adminrechte möglich.</summary>
    PerUser,

    /// <summary>MSI für alle Benutzer (Programme-Ordner) – Updates verteilt die IT, die App weist nur hin.</summary>
    PerMachine,

    /// <summary>Einzelne EXE an beliebigem Ort – nur Hinweis auf die Release-Seite.</summary>
    Portable,
}

/// <summary>Reine Hilfsfunktionen der Update-Prüfung (ohne Netzwerk, testbar).</summary>
internal static class UpdateLogic
{
    /// <summary>"v1.0.9" / "1.0.9" → 1.0.9; null bei ungültigem Text.</summary>
    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string t = text.Trim().TrimStart('v', 'V');
        int cut = t.IndexOfAny(['-', '+']);
        if (cut >= 0) t = t[..cut];
        if (!Version.TryParse(t, out var v)) return null;
        // 1.0 und 1.0.0 gleich behandeln
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
    }

    public static bool IsNewer(string? candidate, string? current)
    {
        var c = ParseVersion(candidate);
        var cur = ParseVersion(current);
        return c != null && cur != null && c > cur;
    }

    /// <summary>Antwort von GET /repos/{owner}/{repo}/releases/latest auswerten. Entwürfe und Vorabversionen → null.</summary>
    public static ReleaseInfo? ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (Bool(root, "draft") || Bool(root, "prerelease")) return null;

        var version = ParseVersion(Str(root, "tag_name"));
        if (version == null) return null;

        var assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in list.EnumerateArray())
            {
                string? name = Str(a, "name");
                string? url = Str(a, "browser_download_url");
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url)) assets[name] = url;
            }
        }
        return new ReleaseInfo(version.ToString(3), Str(root, "html_url") ?? AppInfo.ProjectUrl + "/releases", assets);
    }

    /// <summary>Dateiname des MSI im Release, z. B. "LSMDisplayResizer-1.0.9-win-x64-de.msi".</summary>
    public static string MsiAssetName(string version, bool english) =>
        $"LSMDisplayResizer-{version}-win-x64-{(english ? "en" : "de")}.msi";

    public const string ChecksumAssetName = "SHA256SUMS.txt";

    /// <summary>Prüfsumme (Kleinbuchstaben) für eine Datei aus SHA256SUMS.txt ("&lt;hash&gt;  &lt;name&gt;" bzw. "&lt;hash&gt; *&lt;name&gt;").</summary>
    public static string? FindChecksum(string sums, string fileName)
    {
        foreach (string raw in sums.Split('\n'))
        {
            string line = raw.Trim();
            int space = line.IndexOf(' ');
            if (space != 64) continue;
            string name = line[space..].Trim().TrimStart('*');
            if (string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
                return line[..64].ToLowerInvariant();
        }
        return null;
    }

    /// <summary>Installationsart aus dem Pfad der laufenden EXE.</summary>
    public static InstallKind DetectInstallKind(string? exePath, string localAppData, IEnumerable<string> programFolders)
    {
        if (string.IsNullOrEmpty(exePath)) return InstallKind.Portable;
        string dir = Path.GetDirectoryName(Path.GetFullPath(exePath)) ?? string.Empty;

        static bool Same(string a, string b) =>
            string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);

        if (localAppData.Length > 0 && Same(dir, Path.Combine(localAppData, "Programs", AppInfo.Name)))
            return InstallKind.PerUser;
        foreach (string pf in programFolders)
        {
            if (pf.Length > 0 && Same(dir, Path.Combine(pf, AppInfo.Name))) return InstallKind.PerMachine;
        }
        return InstallKind.Portable;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
