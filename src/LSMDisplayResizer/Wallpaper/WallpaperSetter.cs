using System.ComponentModel;
using System.Text.Json;
using LSMDisplayResizer.Logging;
using Microsoft.Win32;
using static LSMDisplayResizer.Interop.NativeMethods;

namespace LSMDisplayResizer.Wallpaper;

/// <summary>Setzt das Hintergrundbild im Modus „Spannen“ (nur für den aktuellen Benutzer, ohne Adminrechte).</summary>
internal static class WallpaperSetter
{
    private const string DesktopKey = @"Control Panel\Desktop";
    private const string SpanStyle = "22";

    /// <param name="Wallpaper">Gesicherte Kopie der Bilddatei.</param>
    /// <param name="OriginalPath">Ursprünglicher Pfad (wird bevorzugt, solange die Datei existiert).</param>
    private sealed record Backup(string Wallpaper, string WallpaperStyle, string TileWallpaper, string? OriginalPath = null);

    public static string OutputDirectory => Path.Combine(AppInfo.DataDirectory, "Hintergrund");

    private static string BackupFile => Path.Combine(OutputDirectory, "vorheriger-hintergrund.json");

    public static bool HasBackup => File.Exists(BackupFile);

    /// <summary>Hinweis, falls eine Gruppenrichtlinie das Hintergrundbild vorgibt, sonst null.</summary>
    public static string? GetPolicyHint()
    {
        try
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using var system = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System");
                if (system?.GetValue("Wallpaper") is string s && s.Length > 0)
                    return "Eine Gruppenrichtlinie gibt das Hintergrundbild vor – es wird beim nächsten Anmelden überschrieben.";

                using var active = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop");
                if (active?.GetValue("NoChangingWallPaper") is int i && i != 0)
                    return "Eine Gruppenrichtlinie verbietet das Ändern des Hintergrundbilds.";
            }
        }
        catch
        {
            // Richtlinien nicht lesbar → kein Hinweis
        }
        return null;
    }

    /// <summary>Setzt <paramref name="imagePath"/> als über alle Monitore gespanntes Hintergrundbild.</summary>
    public static void ApplySpanned(string imagePath)
    {
        BackupCurrentIfForeign();

        using (var key = Registry.CurrentUser.CreateSubKey(DesktopKey, writable: true))
        {
            key.SetValue("WallpaperStyle", SpanStyle, RegistryValueKind.String);
            key.SetValue("TileWallpaper", "0", RegistryValueKind.String);
        }

        if (!SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE))
            throw new Win32Exception();
    }

    /// <summary>Stellt das Hintergrundbild wieder her, das vor der ersten Verwendung gesetzt war.</summary>
    public static bool RestoreBackup()
    {
        if (!File.Exists(BackupFile)) return false;
        var backup = JsonSerializer.Deserialize<Backup>(File.ReadAllText(BackupFile));
        if (backup == null) return false;

        using (var key = Registry.CurrentUser.CreateSubKey(DesktopKey, writable: true))
        {
            key.SetValue("WallpaperStyle", backup.WallpaperStyle, RegistryValueKind.String);
            key.SetValue("TileWallpaper", backup.TileWallpaper, RegistryValueKind.String);
        }

        // Originaldatei bevorzugen – außer Windows' eigene Zwischendatei, die inzwischen unser Bild enthält.
        string path =
            backup.OriginalPath is { Length: > 0 } original && File.Exists(original) && !IsTranscoded(original) ? original :
            File.Exists(backup.Wallpaper) ? backup.Wallpaper :
            string.Empty;
        if (!SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE))
            throw new Win32Exception();

        File.Delete(BackupFile);
        Log.Info($"Vorheriges Hintergrundbild wiederhergestellt: {(path.Length > 0 ? path : "(keins)")}");
        return true;
    }

    private static bool IsTranscoded(string path) =>
        Path.GetFileName(path).StartsWith("TranscodedWallpaper", StringComparison.OrdinalIgnoreCase);

    /// <summary>Löscht ältere erzeugte Bilder bis auf <paramref name="keep"/>.</summary>
    public static void CleanupOldImages(string keep)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(OutputDirectory, "gespannt-*.png"))
            {
                if (!string.Equals(file, keep, StringComparison.OrdinalIgnoreCase)) File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Alte Hintergrundbilder nicht gelöscht: {ex.Message}");
        }
    }

    /// <summary>Sichert den aktuellen Hintergrund – aber nur, wenn er nicht von dieser App stammt.</summary>
    private static void BackupCurrentIfForeign()
    {
        if (File.Exists(BackupFile)) return;

        using var key = Registry.CurrentUser.OpenSubKey(DesktopKey);
        string wallpaper = key?.GetValue("Wallpaper") as string ?? string.Empty;
        if (wallpaper.StartsWith(OutputDirectory, StringComparison.OrdinalIgnoreCase)) return;

        Directory.CreateDirectory(OutputDirectory);

        // Bilddatei kopieren: Windows verweist oft auf "TranscodedWallpaper", das beim Setzen überschrieben wird.
        string savedCopy = string.Empty;
        if (wallpaper.Length > 0 && File.Exists(wallpaper))
        {
            string ext = Path.GetExtension(wallpaper);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            savedCopy = Path.Combine(OutputDirectory, "vorheriger-hintergrund" + ext);
            File.Copy(wallpaper, savedCopy, overwrite: true);
        }

        var backup = new Backup(
            savedCopy,
            key?.GetValue("WallpaperStyle") as string ?? "10",
            key?.GetValue("TileWallpaper") as string ?? "0",
            wallpaper);

        File.WriteAllText(BackupFile, JsonSerializer.Serialize(backup));
        Log.Info($"Bisheriges Hintergrundbild gesichert: {(wallpaper.Length > 0 ? wallpaper : "(keins)")}");
    }
}
