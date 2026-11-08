using LSMDisplayResizer.Logging;
using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Settings;
using LSMDisplayResizer.Tracking;
using LSMDisplayResizer.UI;
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
    private readonly Icon _iconActive;
    private readonly Icon _iconPaused;
    private readonly RegisteredWaitHandle _activateWait;
    private readonly SynchronizationContext _uiContext;
    private readonly HotkeyManager _hotkey;

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
        _menu.Items.Add(new ToolStripMenuItem("Einstellungen …", null, (_, _) => ShowSettings()) { Font = new Font(_menu.Font, FontStyle.Bold) });
        _menu.Items.Add(new ToolStripMenuItem("Log anzeigen …", null, (_, _) => ShowLog()));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Beenden", null, (_, _) => ExitThread()));

        _tray = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowSettings();

        _tracker = new WindowTracker(_monitors, _settings);
        _tracker.Start();

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
                "Läuft im Hintergrund. Rechtsklick auf das Symbol für Einstellungen.", ToolTipIcon.Info);
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

    private void UpdateTrayState()
    {
        _activeItem.Checked = _settings.Enabled;
        _pausedItem.Checked = !_settings.Enabled;
        _tray.Icon = _settings.Enabled ? _iconActive : _iconPaused;
        _tray.Text = $"{AppInfo.Name} – {(_settings.Enabled ? "aktiv" : "pausiert")}";
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

            _settingsForm = new SettingsForm(_settings.Clone(), _monitors, ApplySettings, ShowLog);
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
        _settings = settings;
        bool saved = _store.Save(settings);
        Log.VerboseEnabled = settings.VerboseLog;
        _tracker.UpdateSettings(settings);
        UpdateTrayState();
        string? hotkeyError = ApplyHotkey();

        Log.Info($"Einstellungen gespeichert (Modus: {(settings.Mode == ResizeMode.Percent ? "prozentual" : "feste Regeln")}, " +
                 $"{settings.Rules.Count} Regel(n), {settings.ExcludedProcesses.Count} ausgeschlossen)");

        if (!saved) return "Die Einstellungen konnten nicht gespeichert werden (Details im Log). Sie gelten bis zum Beenden.";
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

        string message = $"Das Tastenkürzel {text} ist bereits von einem anderen Programm belegt. Bitte ein anderes wählen.";
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
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
        base.ExitThreadCore();
    }
}
