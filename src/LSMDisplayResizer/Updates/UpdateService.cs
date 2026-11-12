using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using LSMDisplayResizer.Logging;
using LSMDisplayResizer.Settings;
using Microsoft.Win32;

namespace LSMDisplayResizer.Updates;

/// <summary>
/// Sucht bei GitHub nach einer neueren Version, lädt das passende MSI, prüft es gegen SHA256SUMS.txt und startet die
/// Installation. Es werden keine Daten über den Benutzer gesendet – nur eine Anfrage an die öffentliche GitHub-API.
/// Abschaltbar in den Einstellungen und per Installation (MSI-Eigenschaft UPDATECHECK=0).
/// </summary>
internal sealed class UpdateService : IDisposable
{
    private const string LatestReleaseApi = "https://api.github.com/repos/nes19233/LSMDisplayResizer/releases/latest";
    private const string PolicyKey = @"Software\LSM Display Resizer";
    private const string PolicyValue = "UpdateCheck";

    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private static readonly HttpClient Http = CreateClient();

    private readonly Func<AppSettings> _settings;
    private readonly SynchronizationContext _ui;
    private System.Threading.Timer? _timer;
    private int _checking;

    /// <summary>Automatische Prüfung hat eine neue Version gefunden (auf dem UI-Thread).</summary>
    public event Action<ReleaseInfo>? UpdateAvailable;

    public UpdateService(Func<AppSettings> settings, SynchronizationContext ui)
    {
        _settings = settings;
        _ui = ui;
    }

    /// <summary>Von der Installation abgeschaltet (UPDATECHECK=0 → Registrierungswert UpdateCheck = 0).</summary>
    public static bool DisabledByPolicy { get; } = ReadPolicy();

    public static InstallKind InstallKind { get; } = UpdateLogic.DetectInstallKind(
        Environment.ProcessPath,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        ]);

    public void Start()
    {
        CleanupDownloads();
        if (DisabledByPolicy)
        {
            Log.Info("Update-Prüfung ist von der Installation abgeschaltet");
            return;
        }
        _timer = new System.Threading.Timer(_ => _ = AutoCheckAsync(), null, FirstCheckDelay, CheckInterval);
    }

    private async Task AutoCheckAsync()
    {
        if (!_settings().CheckForUpdates || DisabledByPolicy) return;
        if (Interlocked.Exchange(ref _checking, 1) == 1) return;
        try
        {
            var release = await CheckAsync(CancellationToken.None).ConfigureAwait(false);
            if (release != null) _ui.Post(_ => UpdateAvailable?.Invoke(release), null);
        }
        catch (Exception ex)
        {
            // Offline, Proxy, GitHub nicht erreichbar … – beim nächsten Mal wieder versuchen.
            Log.Warn($"Update-Prüfung fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _checking, 0);
        }
    }

    /// <summary>Fragt das neueste Release ab. Rückgabe: neuere Version oder null, wenn die App aktuell ist.</summary>
    public static async Task<ReleaseInfo?> CheckAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        var release = UpdateLogic.ParseRelease(json);
        if (release == null) return null;
        bool newer = UpdateLogic.IsNewer(release.Version, AppInfo.Version);
        Log.Info(newer
            ? $"Update verfügbar: {release.Version} (installiert: {AppInfo.Version})"
            : $"Update-Prüfung: {AppInfo.Version} ist aktuell (neueste Version: {release.Version})");
        return newer ? release : null;
    }

    /// <summary>
    /// Lädt das MSI in der Sprache der Oberfläche herunter und prüft die SHA-256-Prüfsumme.
    /// Rückgabe: Pfad zur geprüften Datei. Bei Fehlern oder falscher Prüfsumme eine Ausnahme mit lesbarer Meldung.
    /// </summary>
    public static async Task<string> DownloadAsync(ReleaseInfo release, IProgress<int> progress, CancellationToken ct)
    {
        string msiName = UpdateLogic.MsiAssetName(release.Version, Localization.L.IsEnglish);
        if (!release.Assets.TryGetValue(msiName, out string? msiUrl))
            throw new InvalidOperationException(Localization.L.F("Im Release fehlt die Datei {0}.", msiName));
        if (!release.Assets.TryGetValue(UpdateLogic.ChecksumAssetName, out string? sumsUrl))
            throw new InvalidOperationException(Localization.L.F("Im Release fehlt die Datei {0}.", UpdateLogic.ChecksumAssetName));

        string sums = await Http.GetStringAsync(sumsUrl, ct).ConfigureAwait(false);
        string expected = UpdateLogic.FindChecksum(sums, msiName)
                          ?? throw new InvalidOperationException(Localization.L.F("Für {0} ist keine Prüfsumme hinterlegt.", msiName));

        string dir = Path.Combine(Path.GetTempPath(), AppInfo.Name + " Update");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, msiName);
        string partial = target + ".download";

        using (var response = await Http.GetAsync(msiUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? -1;
            await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            long done = 0;
            int lastPercent = -1;
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                done += read;
                if (total > 0)
                {
                    int percent = (int)(done * 100 / total);
                    if (percent != lastPercent) progress.Report(lastPercent = percent);
                }
            }
        }

        string actual;
        await using (var check = File.OpenRead(partial))
        {
            actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(check, ct).ConfigureAwait(false));
        }
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(partial);
            Log.Warn($"Update {release.Version}: Prüfsumme stimmt nicht (erwartet {expected}, erhalten {actual}) – Datei verworfen");
            throw new InvalidOperationException(Localization.L.T("Die heruntergeladene Datei ist beschädigt oder wurde verändert (Prüfsumme stimmt nicht). Sie wurde verworfen."));
        }

        File.Move(partial, target, overwrite: true);
        Log.Info($"Update {release.Version} heruntergeladen und geprüft: {target}");
        return target;
    }

    /// <summary>
    /// Startet die Installation in einem eigenen Prozess (die App muss sich danach selbst beenden). Nach der
    /// Installation – auch wenn sie abgebrochen wurde – startet das Skript die App wieder.
    /// </summary>
    public static void LaunchInstaller(string msiPath)
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("EXE-Pfad unbekannt");
        string dir = Path.GetDirectoryName(msiPath)!;
        string logFile = Path.Combine(dir, "install.log");
        string script = Path.Combine(dir, "update.cmd");
        // Autostart so lassen, wie der Benutzer ihn eingestellt hat (das MSI würde ihn sonst wieder einschalten).
        string autostart = Autostart.IsEnabled() ? "1" : "0";

        File.WriteAllText(script,
            "@echo off\r\n" +
            "chcp 65001 >nul\r\n" +
            $"msiexec /i \"{msiPath}\" /passive /norestart AUTOSTART={autostart} /l*v \"{logFile}\"\r\n" +
            $"start \"\" \"{exe}\"\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c \"\"{script}\"\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = dir,
        });
        Log.Info($"Update-Installation gestartet: {msiPath}");
    }

    public static void OpenReleasePage(ReleaseInfo? release)
    {
        try
        {
            Process.Start(new ProcessStartInfo(release?.PageUrl ?? AppInfo.ProjectUrl + "/releases/latest") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("Release-Seite konnte nicht geöffnet werden", ex);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        // GitHub verlangt einen User-Agent; nur App-Name und Version.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LSMDisplayResizer", AppInfo.Version));
        return client;
    }

    private static bool ReadPolicy()
    {
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(PolicyKey);
            using var user = Registry.CurrentUser.OpenSubKey(PolicyKey);
            return IsOff(machine?.GetValue(PolicyValue)) || IsOff(user?.GetValue(PolicyValue));
        }
        catch (Exception ex)
        {
            Log.Error("Update-Richtlinie konnte nicht gelesen werden", ex);
            return false;
        }

        static bool IsOff(object? value) => value switch
        {
            int i => i == 0,
            string s => s.Trim() == "0",
            _ => false,
        };
    }

    /// <summary>Heruntergeladene MSIs vom letzten Update entfernen (das Installationsprotokoll bleibt).</summary>
    private static void CleanupDownloads()
    {
        try
        {
            string dir = Path.Combine(Path.GetTempPath(), AppInfo.Name + " Update");
            if (!Directory.Exists(dir)) return;
            foreach (string f in Directory.EnumerateFiles(dir, "*.msi*")) TryDelete(f);
            TryDelete(Path.Combine(dir, "update.cmd"));
        }
        catch (Exception ex)
        {
            Log.Debug($"Update-Ordner konnte nicht aufgeräumt werden: {ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // egal
        }
    }

    public void Dispose() => _timer?.Dispose();
}
