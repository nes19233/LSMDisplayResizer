using LSMDisplayResizer.Localization;
using LSMDisplayResizer.Logging;

namespace LSMDisplayResizer.Updates;

/// <summary>Dialoge zur Update-Prüfung: Hinweis, Download mit Fortschritt, Fehler.</summary>
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

    private static void ShowCore(IWin32Window? owner, ReleaseInfo release, Action exitApp)
    {
        var kind = UpdateService.InstallKind;
        var update = new TaskDialogCommandLinkButton(L.T("Jetzt aktualisieren"),
            L.T("Herunterladen, Prüfsumme prüfen, installieren und neu starten (dauert etwa eine Minute)."))
        {
            AllowCloseDialog = false,
        };
        var page = new TaskDialogButton(L.T("Release-Seite öffnen"));
        var later = new TaskDialogButton(L.T("Später"));

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

        bool exit = false;
        update.Click += (_, _) =>
        {
            Log.Info($"Update auf {release.Version}: Download gestartet");
            StartDownload(main, release, () => exit = true);
        };

        Log.Info($"Update-Hinweis für {release.Version} angezeigt ({kind})");
        var result = owner != null ? TaskDialog.ShowDialog(owner, main) : TaskDialog.ShowDialog(main);
        if (!exit && result == later) Log.Info("Update-Hinweis: später");
        if (result == page) UpdateService.OpenReleasePage(release);
        if (exit) exitApp();
    }

    private static void StartDownload(TaskDialogPage main, ReleaseInfo release, Action markExit)
    {
        var cts = new CancellationTokenSource();
        var cancel = new TaskDialogButton(L.T("Abbrechen"));
        var progressPage = new TaskDialogPage
        {
            Caption = AppInfo.Name,
            Heading = L.F("Version {0} wird heruntergeladen …", release.Version),
            Text = L.T("Danach wird die Datei geprüft und die Installation gestartet. Die App startet anschließend neu."),
            Icon = TaskDialogIcon.Information,
            ProgressBar = new TaskDialogProgressBar { Minimum = 0, Maximum = 100, Value = 0 },
            AllowCancel = true,
        };
        progressPage.Buttons.Add(cancel);
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
                var close = TaskDialogButton.Close;
                var openPage = new TaskDialogButton(L.T("Release-Seite öffnen"));
                openPage.Click += (_, _) => UpdateService.OpenReleasePage(release);
                var error = new TaskDialogPage
                {
                    Caption = AppInfo.Name,
                    Heading = L.T("Update fehlgeschlagen"),
                    Text = ex is InvalidOperationException ? ex.Message : L.F("Fehler: {0}", ex.Message),
                    Icon = TaskDialogIcon.Error,
                    AllowCancel = true,
                };
                error.Buttons.Add(openPage);
                error.Buttons.Add(close);
                progressPage.Navigate(error);
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
