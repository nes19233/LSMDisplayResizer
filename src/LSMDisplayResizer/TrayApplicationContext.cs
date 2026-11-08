using LSMDisplayResizer.Logging;
using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Settings;
using LSMDisplayResizer.Tracking;

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

    private AppSettings _settings;

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
        // Einstellungsfenster folgt im nächsten Schritt.
        MessageBox.Show($"Einstellungen: {_store.FilePath}", AppInfo.Name);
    }

    protected override void ExitThreadCore()
    {
        _activateWait.Unregister(null);
        _tracker.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
        base.ExitThreadCore();
    }
}
