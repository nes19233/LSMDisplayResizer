using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using LSMDisplayResizer.Interop;
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
    /// <param name="BackgroundType">Hintergrundtyp vorher (0 = Bild, 1 = Volltonfarbe, 2 = Diashow, 3 = Blickpunkt).</param>
    private sealed record Backup(string Wallpaper, string WallpaperStyle, string TileWallpaper, string? OriginalPath = null,
        int? BackgroundType = null);

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

    /// <summary>
    /// Setzt <paramref name="imagePath"/> als über alle Monitore gespanntes Hintergrundbild.
    /// Bevorzugt über IDesktopWallpaper: Nur so wird auch von „Volltonfarbe“ auf „Bild“ umgeschaltet.
    /// Muss auf einem STA-Thread (UI-Thread) aufgerufen werden.
    /// </summary>
    public static void ApplySpanned(string imagePath)
    {
        BackupCurrentIfForeign();

        if (TryViaCom(dw =>
            {
                // Reihenfolge wichtig: Enable(true) vor dem Setzen lädt das bisherige Bild des
                // virtuellen Desktops und scheitert, wenn dort keins eingetragen ist (0x80070002).
                Step("SetPosition", () => dw.SetPosition(DesktopWallpaperPosition.Span));
                Step("SetWallpaper", () => SetForAllMonitors(dw, imagePath));
                Step("Enable", () => dw.Enable(true));                         // Volltonfarbe → Bild
            }, out var error))
        {
            return;
        }

        Log.Warn($"IDesktopWallpaper nicht verfügbar ({error?.Message}) – Rückfall auf SystemParametersInfo");
        SetViaLegacyApi(imagePath, SpanStyle, "0");
    }

    /// <summary>Stellt das Hintergrundbild wieder her, das vor der ersten Verwendung gesetzt war.</summary>
    public static bool RestoreBackup()
    {
        if (!File.Exists(BackupFile)) return false;
        var backup = JsonSerializer.Deserialize<Backup>(File.ReadAllText(BackupFile));
        if (backup == null) return false;

        // Originaldatei bevorzugen – außer Windows' eigene Zwischendatei, die inzwischen unser Bild enthält.
        string path =
            backup.OriginalPath is { Length: > 0 } original && File.Exists(original) && !IsTranscoded(original) ? original :
            File.Exists(backup.Wallpaper) ? backup.Wallpaper :
            string.Empty;

        // Vorher "Volltonfarbe" (1) oder gar kein Bild → wieder Volltonfarbe anzeigen.
        bool solidColor = backup.BackgroundType == 1 || path.Length == 0;
        var position = ToPosition(backup.WallpaperStyle, backup.TileWallpaper);

        if (!TryViaCom(dw =>
            {
                Step("SetPosition", () => dw.SetPosition(position));
                if (path.Length > 0) Step("SetWallpaper", () => SetForAllMonitors(dw, path));
                Step("Enable", () => dw.Enable(!solidColor));
            }, out var error))
        {
            Log.Warn($"IDesktopWallpaper nicht verfügbar ({error?.Message}) – Rückfall auf SystemParametersInfo");
            SetViaLegacyApi(solidColor ? string.Empty : path, backup.WallpaperStyle, backup.TileWallpaper);
        }

        File.Delete(BackupFile);
        Log.Info(solidColor
            ? "Vorheriger Hintergrund wiederhergestellt: Volltonfarbe"
            : $"Vorheriges Hintergrundbild wiederhergestellt: {path}");
        return true;
    }

    private static bool TryViaCom(Action<IDesktopWallpaper> action, out Exception? error)
    {
        error = null;
        IDesktopWallpaper? dw = null;
        try
        {
            dw = (IDesktopWallpaper)new DesktopWallpaperClass();
            action(dw);
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
        finally
        {
            if (dw != null) Marshal.ReleaseComObject(dw);
        }
    }

    /// <summary>
    /// Setzt das Bild für alle Monitore. Windows merkt sich auch längst abgesteckte Monitore; mit ihnen scheitert
    /// SetWallpaper(null, …) mit 0x80070002. Dann wird jeder angeschlossene Monitor einzeln gesetzt.
    /// </summary>
    private static void SetForAllMonitors(IDesktopWallpaper dw, string path)
    {
        try
        {
            dw.SetWallpaper(null, path);
            return;
        }
        catch (Exception ex)
        {
            Log.Debug($"SetWallpaper für alle Monitore fehlgeschlagen ({ex.Message}) – setze einzeln");
        }

        int ok = 0;
        uint count = dw.GetMonitorDevicePathCount();
        for (uint i = 0; i < count; i++)
        {
            string id;
            try
            {
                id = dw.GetMonitorDevicePathAt(i);
                if (string.IsNullOrEmpty(id)) continue;
                dw.GetMonitorRECT(id); // wirft bei nicht angeschlossenen Monitoren
            }
            catch
            {
                continue;
            }
            dw.SetWallpaper(id, path);
            ok++;
        }
        if (ok == 0) throw new InvalidOperationException("Kein angeschlossener Monitor gefunden");
    }

    /// <summary>Führt einen COM-Schritt aus und nennt ihn im Fehlerfall.</summary>
    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{name}: {ex.Message}", ex);
        }
    }

    private static void SetViaLegacyApi(string path, string style, string tile)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(DesktopKey, writable: true))
        {
            key.SetValue("WallpaperStyle", style, RegistryValueKind.String);
            key.SetValue("TileWallpaper", tile, RegistryValueKind.String);
        }
        if (!SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE))
            throw new Win32Exception();
    }

    /// <summary>Registry-Werte WallpaperStyle/TileWallpaper → Position.</summary>
    private static DesktopWallpaperPosition ToPosition(string style, string tile) => style switch
    {
        "22" => DesktopWallpaperPosition.Span,
        "10" => DesktopWallpaperPosition.Fill,
        "6" => DesktopWallpaperPosition.Fit,
        "2" => DesktopWallpaperPosition.Stretch,
        "0" when tile == "1" => DesktopWallpaperPosition.Tile,
        "0" => DesktopWallpaperPosition.Center,
        _ => DesktopWallpaperPosition.Fill,
    };

    /// <summary>Hintergrundtyp aus den Windows-Einstellungen: 0 = Bild, 1 = Volltonfarbe, 2 = Diashow, 3 = Blickpunkt.</summary>
    private static int? ReadBackgroundType()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers");
            return key?.GetValue("BackgroundType") is int i ? i : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsTranscoded(string path) =>
        Path.GetFileName(path).StartsWith("TranscodedWallpaper", StringComparison.OrdinalIgnoreCase);

    /// <summary>Aktuell eingestellter Hintergrund – um ihn bei einem fehlgeschlagenen Setzen wiederherzustellen.</summary>
    internal readonly record struct WallpaperState(string Path, string Style, string Tile, int? BackgroundType);

    public static WallpaperState CaptureCurrent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DesktopKey);
            return new WallpaperState(
                key?.GetValue("Wallpaper") as string ?? string.Empty,
                key?.GetValue("WallpaperStyle") as string ?? "10",
                key?.GetValue("TileWallpaper") as string ?? "0",
                ReadBackgroundType());
        }
        catch
        {
            return new WallpaperState(string.Empty, "10", "0", null);
        }
    }

    /// <summary>Stellt einen zuvor gesicherten Hintergrund wieder her (nur, wenn dessen Bilddatei noch existiert).</summary>
    public static void Revert(WallpaperState state)
    {
        try
        {
            bool hasImage = state.Path.Length > 0 && File.Exists(state.Path);
            bool solidColor = state.BackgroundType == 1 || !hasImage;
            var position = ToPosition(state.Style, state.Tile);

            if (!TryViaCom(dw =>
                {
                    Step("SetPosition", () => dw.SetPosition(position));
                    if (hasImage) Step("SetWallpaper", () => SetForAllMonitors(dw, state.Path));
                    Step("Enable", () => dw.Enable(!solidColor));
                }, out _))
            {
                SetViaLegacyApi(hasImage && !solidColor ? state.Path : string.Empty, state.Style, state.Tile);
            }
            Log.Info($"Bisheriger Hintergrund wiederhergestellt: {(solidColor ? "Volltonfarbe" : state.Path)}");
        }
        catch (Exception ex)
        {
            Log.Error("Bisheriger Hintergrund konnte nicht wiederhergestellt werden", ex);
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // egal
        }
    }

    /// <summary>Zeitstempel von Windows' Kopie des aktuellen Hintergrundbilds (ändert sich bei jeder Übernahme).</summary>
    public static DateTime GetTranscodedTimestamp()
    {
        try
        {
            string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\TranscodedWallpaper");
            return File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>Löscht ältere erzeugte Bilder bis auf <paramref name="keep"/>.</summary>
    public static void CleanupOldImages(string keep)
    {
        try
        {
            // Nie die Datei löschen, die Windows gerade als Hintergrund eingetragen hat.
            string current = CaptureCurrent().Path;
            foreach (var file in Directory.EnumerateFiles(OutputDirectory, "gespannt-*.png"))
            {
                if (string.Equals(file, keep, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(file, current, StringComparison.OrdinalIgnoreCase)) continue;
                File.Delete(file);
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
            wallpaper,
            ReadBackgroundType());

        File.WriteAllText(BackupFile, JsonSerializer.Serialize(backup));
        Log.Info($"Bisheriger Hintergrund gesichert: {(backup.BackgroundType == 1 ? "Volltonfarbe" : wallpaper.Length > 0 ? wallpaper : "(keins)")}");
    }
}
