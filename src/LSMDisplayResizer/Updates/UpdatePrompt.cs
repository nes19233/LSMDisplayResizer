using LSMDisplayResizer.Localization;
using LSMDisplayResizer.Logging;

namespace LSMDisplayResizer.Updates;

/// <summary>Dialoge zur Update-Prüfung: Hinweis, Download mit Fortschritt, Fehler.</summary>
/// <remarks>
/// Regel des Windows-Dialogs: Eigene Schaltflächen und Befehlslinks dürfen nicht auf derselben Seite stehen
/// (Standard-Schaltflächen wie OK/Schließen dürfen zu beiden). Die Seiten werden in <c>UpdatePromptTests</c> angezeigt.
/// </remarks>
internal static class UpdatePrompt
{
    private static bool _open;

    /// <summary>Manuelle Prüfung ("Nach Updates suchen …") mit Ergebnis-Dialog.</summary>
    public static async Task CheckAndShowAsync(IWin32Window? owner, Action exitApp)
    {
        if (_open) return;
        ReleaseInfo? release;
        try
        {
            release = await UpdateService.CheckAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.Warn($"Update-Prüfung fehlgeschlagen: {ex.Message}");
            ShowMessage(owner, TaskDialogIcon.Warning, L.T("Update-Prüfung fehlgeschlagen"),
                L.F("GitHub ist nicht erreichbar: {0}", ex.Message));
            return;
        }

        if (release == null)
        {
            ShowMessage(owner, TaskDialogIcon.Information, L.T("Die App ist aktuell"),
                L.F("Version {0} ist die neueste Version.", AppInfo.Version));
            return;
        }
        Show(owner, release, exitApp);
    }

    /// <summary>Zeigt den Hinweis auf eine neue Version und führt ggf. das Update aus.</summary>
    public static void Show(IWin32Window? owner, ReleaseInfo release, Action exitApp)
    {
        if (_open) return;
        _open = true;
        try
        {
            ShowCore(owner, release, exitApp);
        }
        catch (Exception ex)
        {
            Log.Error("Update-Dialog fehlgeschlagen", ex);
        }
        finally
        {
            _open = false;
        }
    }

    internal sealed record MainPage(TaskDialogPage Page, TaskDialogButton Update, TaskDialogButton ReleasePage, TaskDialogButton Later);

    /// <summary>Hinweisseite. "Jetzt aktualisieren" nur bei einer Installation pro Benutzer.</summary>
    internal static MainPage BuildMainPage(ReleaseInfo release, InstallKind kind)
    {
        // Alle als Befehlslinks (keine Mischung mit eigenen Schaltflächen, siehe oben).
        var update = new TaskDialogCommandLinkButton(L.T("Jetzt aktualisieren"),
            L.T("Herunterladen, Prüfsumme prüfen, installieren und neu starten (dauert etwa eine Minute)."))
        {
            AllowCloseDialog = false,
        };
        var page = new TaskDialogCommandLinkButton(L.T("Release-Seite öffnen"), L.T("Änderungen ansehen und Dateien selbst herunterladen."));
        var later = new TaskDialogCommandLinkButton(L.T("Später"), L.T("Erinnerung bei der nächsten täglichen Prüfung."));

        string text = L.F("Installiert ist Version {0}.", AppInfo.Version) + " " + kind switch
        {
            InstallKind.PerMachine => L.T("Diese Installation gilt für alle Benutzer des Computers und wird von der IT bzw. einem Administrator aktualisiert."),
            InstallKind.Portable => L.T("Diese EXE wurde nicht mit dem Installer installiert – die neue Version gibt es auf der Release-Seite."),
            _ => L.T("Die Einstellungen bleiben erhalten."),
        };

        var main = new TaskDialogPage
        {
            Caption = AppInfo.Name,
            Heading = L.F("Version {0} ist verfügbar", release.Version),
            Text = text,
            Icon = TaskDialogIcon.Information,
            AllowCancel = true,
        };
        if (kind == InstallKind.PerUser) main.Buttons.Add(update);
        main.Buttons.Add(page);
        main.Buttons.Add(later);
        return new MainPage(main, update, page, later);
    }

    /// <summary>Fortschrittsseite während des Downloads.</summary>
    internal static TaskDialogPage BuildProgressPage(ReleaseInfo release, out TaskDialogButton cancel)
    {
        cancel = new TaskDialogButton(L.T("Abbrechen"));
        var page = new TaskDialogPage
        {
            Caption = AppInfo.Name,
            Heading = L.F("Version {0} wird heruntergeladen …", release.Version),
            Text = L.T("Danach wird die Datei geprüft und die Installation gestartet. Die App startet anschließend neu."),
            Icon = TaskDialogIcon.Information,
            ProgressBar = new TaskDialogProgressBar { Minimum = 0, Maximum = 100, Value = 0 },
            AllowCancel = true,
        };
        page.Buttons.Add(cancel);
        return page;
    }

    /// <summary>Fehlerseite mit Link zur Release-Seite.</summary>
    internal static TaskDialogPage BuildErrorPage(ReleaseInfo release, string message)
    {
        var openPage = new TaskDialogButton(L.T("Release-Seite öffnen"));
        openPage.Click += (_, _) => UpdateService.OpenReleasePage(release);
        var error = new TaskDialogPage
        {
            Caption = AppInfo.Name,
            Heading = L.T("Update fehlgeschlagen"),
            Text = message,
            Icon = TaskDialogIcon.Error,
            AllowCancel = true,
        };
        error.Buttons.Add(openPage);
        error.Buttons.Add(TaskDialogButton.Close);
        return error;
    }

    private static void ShowCore(IWin32Window? owner, ReleaseInfo release, Action exitApp)
    {
        var kind = UpdateService.InstallKind;
        var main = BuildMainPage(release, kind);

        bool exit = false;
        main.Update.Click += (_, _) =>
        {
            Log.Info($"Update auf {release.Version}: Download gestartet");
            StartDownload(main.Page, release, () => exit = true);
        };

        Log.Info($"Update-Hinweis für {release.Version} angezeigt ({kind})");
        var result = owner != null ? TaskDialog.ShowDialog(owner, main.Page) : TaskDialog.ShowDialog(main.Page);
        if (!exit && result == main.Later) Log.Info("Update-Hinweis: später");
        if (result == main.ReleasePage) UpdateService.OpenReleasePage(release);
        if (exit) exitApp();
    }

    private static void StartDownload(TaskDialogPage main, ReleaseInfo release, Action markExit)
    {
        var cts = new CancellationTokenSource();
        var progressPage = BuildProgressPage(release, out var cancel);
        cancel.Click += (_, _) => cts.Cancel();
        // Auch beim Schließen über X abbrechen – sonst liefe der Download im Hintergrund weiter und startete die Installation.
        progressPage.Destroyed += (_, _) => cts.Cancel();

        progressPage.Created += async (_, _) =>
        {
            try
            {
                var progress = new Progress<int>(p =>
                {
                    if (progressPage.BoundDialog != null) progressPage.ProgressBar!.Value = Math.Clamp(p, 0, 100);
                });
                string msi = await UpdateService.DownloadAsync(release, progress, cts.Token);
                cts.Token.ThrowIfCancellationRequested();
                UpdateService.LaunchInstaller(msi);
                markExit();
                progressPage.BoundDialog?.Close();
            }
            catch (OperationCanceledException)
            {
                Log.Info("Update abgebrochen");
            }
            catch (Exception ex)
            {
                Log.Error("Update fehlgeschlagen", ex);
                if (progressPage.BoundDialog == null) return;
                progressPage.Navigate(BuildErrorPage(release,
                    ex is InvalidOperationException ? ex.Message : L.F("Fehler: {0}", ex.Message)));
            }
        };

        main.Navigate(progressPage);
    }

    private static void ShowMessage(IWin32Window? owner, TaskDialogIcon icon, string heading, string text)
    {
        var page = new TaskDialogPage
        {
            Caption = AppInfo.Name,
            Heading = heading,
            Text = text,
            Icon = icon,
            AllowCancel = true,
            Buttons = { TaskDialogButton.OK },
        };
        if (owner != null) TaskDialog.ShowDialog(owner, page);
        else TaskDialog.ShowDialog(page);
    }
}
