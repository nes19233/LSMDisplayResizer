using LSMDisplayResizer.Localization;
using LSMDisplayResizer.Logging;
using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Settings;
using LSMDisplayResizer.Tracking;
using LSMDisplayResizer.UI;
using LSMDisplayResizer.Wallpaper;
using LSMDisplayResizer.Overlay;
using LSMDisplayResizer.Updates;
using static LSMDisplayResizer.Interop.NativeMethods;

namespace LSMDisplayResizer;

/// <summary>Tray-App: Symbol im Infobereich mit Menü, kein Hauptfenster.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _store;
    private readonly MonitorManager _monitors = new();
    private readonly WindowTracker _tracker;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _activeItem;
    private readonly ToolStripMenuItem _pausedItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _logItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly ToolStripMenuItem _updateItem;
    private readonly Icon _iconActive;
    private readonly Icon _iconPaused;
    private readonly RegisteredWaitHandle _activateWait;
    private readonly SynchronizationContext _uiContext;
    private readonly HotkeyManager _hotkey;
    private readonly WallpaperService _wallpaper;
    private readonly MonitorSeparatorOverlay _separator;
    private readonly UpdateService _updates;
    private ReleaseInfo? _availableUpdate;
    private bool _balloonIsUpdate;

    private AppSettings _settings;
    private SettingsForm? _settingsForm;
    private LogForm? _logForm;

    public TrayApplicationContext(EventWaitHandle activateEvent)
    {
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _store = new SettingsStore(AppInfo.SettingsFile);
        bool firstStart = !_store.FileExists;
        _settings = _store.Load();
        Log.VerboseEnabled = _settings.VerboseLog;
        L.Configure(_settings.Language);
        Log.Info($"{AppInfo.Name} {AppInfo.Version} gestartet{(AppInfo.IsElevated ? " (als Administrator)" : string.Empty)}");

        _iconActive = AppIcons.LoadTray(paused: false);
        _iconPaused = AppIcons.LoadTray(paused: true);

        _activeItem = new ToolStripMenuItem("Aktiv", null, (_, _) => SetEnabled(true));
        _pausedItem = new ToolStripMenuItem("Pausiert", null, (_, _) => SetEnabled(false));

        _menu = new ContextMenuStrip();
        _menu.Items.Add(new ToolStripMenuItem($"{AppInfo.Name} {AppInfo.Version}") { Enabled = false });
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_activeItem);
        _menu.Items.Add(_pausedItem);
        _menu.Items.Add(new ToolStripSeparator());
        _settingsItem = new ToolStripMenuItem("Einstellungen …", null, (_, _) => ShowSettings()) { Font = new Font(_menu.Font, FontStyle.Bold) };
        _logItem = new ToolStripMenuItem("Log anzeigen …", null, (_, _) => ShowLog());
        _exitItem = new ToolStripMenuItem("Beenden", null, (_, _) => ExitThread());
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(_logItem);
        _updateItem = new ToolStripMenuItem("Nach Updates suchen …", null, (_, _) => OnUpdateItemClicked())
        {
            Visible = !UpdateService.DisabledByPolicy,
        };
        _menu.Items.Add(_updateItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_exitItem);
        ApplyMenuTexts();

        _tray = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowSettings();
        // Windows 11 legt die Meldung nach Ablauf in die Benachrichtigungszentrale – ein Klick dort kommt nach
        // "Closed" an. Deshalb merkt sich _balloonIsUpdate die zuletzt gezeigte Meldung, bis eine andere kommt.
        _tray.BalloonTipClicked += (_, _) =>
        {
            Log.Info($"Benachrichtigung angeklickt{(_balloonIsUpdate ? " (Update)" : string.Empty)}");
            if (_balloonIsUpdate && _availableUpdate != null) UpdatePrompt.Show(null, _availableUpdate, ExitThread);
        };

        _tracker = new WindowTracker(_monitors, _settings);
        _tracker.Start();

        _wallpaper = new WallpaperService(_monitors, () => _settings);
        _wallpaper.Start();

        _separator = new MonitorSeparatorOverlay(_monitors, () => _settings);
        _separator.Start();

        _updates = new UpdateService(() => _settings, _uiContext);
        _updates.UpdateAvailable += OnUpdateAvailable;
        _updates.Start();

#if DEBUG
        // Nur Debug-Build: Anzeigeänderung von außen simulieren (Tests ohne echtes An-/Abdocken).
        var simulate = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\LSMDisplayResizer.Debug.SimulateDisplayChange");
        ThreadPool.RegisterWaitForSingleObject(simulate, (_, _) => _tracker.SimulateDisplayChange(), null, Timeout.Infinite, false);
#endif

        _hotkey = new HotkeyManager();
        _hotkey.Pressed += () => _tracker.AdjustNow(GetForegroundWindow());
        ApplyHotkey();

        UpdateTrayState();

        // Zweite Instanz gestartet → Einstellungen öffnen.
        _activateWait = ThreadPool.RegisterWaitForSingleObject(activateEvent,
            (_, _) => _uiContext.Post(_ => ShowSettings(), null), null, Timeout.Infinite, executeOnlyOnce: false);

        if (firstStart)
        {
            _store.Save(_settings);
            _tray.ShowBalloonTip(5000, AppInfo.Name,
                L.T("Läuft im Hintergrund. Rechtsklick auf das Symbol für Einstellungen."), ToolTipIcon.Info);
        }
    }

    private void SetEnabled(bool enabled)
    {
        if (_settings.Enabled == enabled) return;
        _settings.Enabled = enabled;
        _store.Save(_settings);
        _tracker.UpdateSettings(_settings);
        UpdateTrayState();
        Log.Info(enabled ? "Automatische Anpassung aktiviert" : "Automatische Anpassung pausiert");
    }

    /// <summary>Menütexte in der aktuellen Sprache setzen (auch nach einem Sprachwechsel).</summary>
    private void ApplyMenuTexts()
    {
        _activeItem.Text = L.T("Aktiv");
        _pausedItem.Text = L.T("Pausiert");
        _settingsItem.Text = L.T("Einstellungen …");
        _logItem.Text = L.T("Log anzeigen …");
        _exitItem.Text = L.T("Beenden");
        _updateItem.Text = _availableUpdate != null
            ? L.F("Update auf Version {0} installieren …", _availableUpdate.Version)
            : L.T("Nach Updates suchen …");
    }

    private void OnUpdateAvailable(ReleaseInfo release)
    {
        _availableUpdate = release;
        ApplyMenuTexts();
        _balloonIsUpdate = true;
        _tray.ShowBalloonTip(10000, AppInfo.Name,
            L.F("Version {0} ist verfügbar. Zum Aktualisieren hier klicken.", release.Version), ToolTipIcon.Info);
    }

    private void OnUpdateItemClicked()
    {
        if (_availableUpdate != null) UpdatePrompt.Show(null, _availableUpdate, ExitThread);
        else CheckForUpdates(null);
    }

    /// <summary>Manuelle Update-Prüfung (Tray-Menü bzw. Einstellungen).</summary>
    private async void CheckForUpdates(IWin32Window? owner)
    {
        try
        {
            await UpdatePrompt.CheckAndShowAsync(owner, ExitThread);
        }
        catch (Exception ex)
        {
            Log.Error("Update-Prüfung fehlgeschlagen", ex);
        }
    }

    /// <summary>Einstellungsfenster schließen und neu öffnen (z. B. nach einem Sprachwechsel).</summary>
    private void ReopenSettings() =>
        _uiContext.Post(_ =>
        {
            _settingsForm?.Close();
            _settingsForm = null;
            ShowSettings();
        }, null);

    private void UpdateTrayState()
    {
        _activeItem.Checked = _settings.Enabled;
        _pausedItem.Checked = !_settings.Enabled;
        _tray.Icon = _settings.Enabled ? _iconActive : _iconPaused;
        _tray.Text = L.F(_settings.Enabled ? "{0} – aktiv" : "{0} – pausiert", AppInfo.Name);
    }

    private void ShowSettings()
    {
        try
        {
            if (_settingsForm is { IsDisposed: false })
            {
                BringToFront(_settingsForm);
                return;
            }

            _settingsForm = new SettingsForm(_settings.Clone(), _monitors, _wallpaper, ApplySettings, ShowLog, ReopenSettings, CheckForUpdates);
            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
            _settingsForm.Show();
            BringToFront(_settingsForm);
        }
        catch (Exception ex)
        {
            Log.Error("Einstellungsfenster konnte nicht geöffnet werden", ex);
        }
    }

    private void ShowLog()
    {
        try
        {
            if (_logForm is { IsDisposed: false })
            {
                BringToFront(_logForm);
                return;
            }

            _logForm = new LogForm();
            _logForm.FormClosed += (_, _) => _logForm = null;
            _logForm.Show();
            BringToFront(_logForm);
        }
        catch (Exception ex)
        {
            Log.Error("Log-Fenster konnte nicht geöffnet werden", ex);
        }
    }

    private static void BringToFront(Form form)
    {
        if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
        form.Activate();
    }

    /// <summary>Übernimmt Einstellungen aus dem Einstellungsfenster. Rückgabe: Fehlermeldung oder null.</summary>
    private string? ApplySettings(AppSettings settings)
    {
        settings.Normalize();
        bool languageChanged = settings.Language != _settings.Language;
        _settings = settings;
        bool saved = _store.Save(settings);
        Log.VerboseEnabled = settings.VerboseLog;
        if (languageChanged)
        {
            L.Configure(settings.Language);
            ApplyMenuTexts();
            UpdateTrayState();
        }
        _tracker.UpdateSettings(settings);
        _wallpaper.NotifySettingsChanged();
        _separator.ApplySettings();
        UpdateTrayState();
        string? hotkeyError = ApplyHotkey();

        Log.Info($"Einstellungen gespeichert (Modus: {(settings.Mode == ResizeMode.Percent ? "prozentual" : "feste Regeln")}, " +
                 $"{settings.Rules.Count} Regel(n), {settings.ExcludedProcesses.Count} ausgeschlossen)");

        if (!saved) return L.T("Die Einstellungen konnten nicht gespeichert werden (Details im Log). Sie gelten bis zum Beenden.");
        return hotkeyError;
    }

    private string? ApplyHotkey()
    {
        _hotkey.Unregister();
        if (!_settings.HotkeyEnabled) return null;

        string text = HotkeyManager.Format(_settings.Hotkey);
        if (_hotkey.Register(_settings.Hotkey))
        {
            Log.Info($"Tastenkürzel {text} aktiv");
            return null;
        }

        string message = L.F("Das Tastenkürzel {0} ist bereits von einem anderen Programm belegt. Bitte ein anderes wählen.", text);
        Log.Warn(message);
        return message;
    }

    protected override void ExitThreadCore()
    {
        _settingsForm?.Close();
        _logForm?.Close();
        _activateWait.Unregister(null);
        _hotkey.Dispose();
        _tracker.Dispose();
        _wallpaper.Dispose();
        _separator.Dispose();
        _updates.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
        base.ExitThreadCore();
    }
}
