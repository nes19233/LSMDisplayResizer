using System.Drawing;
using LSMDisplayResizer.Logging;
using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Resizing;
using LSMDisplayResizer.Settings;
using Microsoft.Win32;
using static LSMDisplayResizer.Interop.NativeMethods;

namespace LSMDisplayResizer.Tracking;

/// <summary>
/// Überwacht Fensterbewegungen per SetWinEventHook, merkt sich pro Fenster den letzten Monitor
/// und passt die Größe an, wenn das Fenster auf einem anderen Monitor landet. Läuft komplett auf dem UI-Thread.
/// </summary>
internal sealed class WindowTracker : IDisposable
{
    private enum Trigger
    {
        /// <summary>Bestandsaufnahme beim Start oder nach Anzeigeänderungen – nur merken.</summary>
        Snapshot,

        /// <summary>Fenster wurde eingeblendet – nur merken.</summary>
        Shown,

        /// <summary>Fenster wurde aktiviert – nur merken.</summary>
        Foreground,

        /// <summary>Position per Tastatur (Win+Umschalt+Pfeil) oder Programm geändert (entprellt).</summary>
        Location,

        /// <summary>Verschieben/Größenänderung mit der Maus beendet.</summary>
        Drag,
    }

    private sealed class TrackedWindow
    {
        public string MonitorId = string.Empty;
        public string MonitorName = string.Empty;

        /// <summary>Sichtbarer Rahmen im letzten normalen Zustand (nicht maximiert/angeordnet).</summary>
        public Rectangle Frame;

        /// <summary>Arbeitsbereich des Monitors, auf dem <see cref="Frame"/> aufgenommen wurde.</summary>
        public Rectangle WorkArea;

        /// <summary>false, solange das Fenster nie im normalen Zustand gesehen wurde.</summary>
        public bool HasNormalFrame;

        public DateTime FirstSeen;
        public DateTime SuppressUntil;
        public bool InMoveSize;

        /// <summary>
        /// Merkt sich den aktuellen Monitor. Ohne <paramref name="frame"/> (maximiert/angeordnet) bleibt der
        /// letzte normale Rahmen samt Arbeitsbereich als Ausgangsbasis für die nächste Anpassung erhalten.
        /// </summary>
        public void Record(MonitorDescriptor monitor, Rectangle? frame)
        {
            MonitorId = monitor.DeviceId;
            MonitorName = monitor.DisplayName;
            if (frame is { } f)
            {
                Frame = f;
                WorkArea = monitor.WorkArea;
                HasNormalFrame = true;
            }
        }
    }

    /// <summary>Neue Fenster positionieren sich oft selbst – in dieser Zeit nicht eingreifen.</summary>
    private static readonly TimeSpan NewWindowGrace = TimeSpan.FromMilliseconds(1500);

    /// <summary>Nach einer eigenen Änderung werden Ereignisse dieses Fensters kurz nur protokolliert.</summary>
    private static readonly TimeSpan OwnChangeSuppression = TimeSpan.FromMilliseconds(800);

    /// <summary>Nach Anzeigeänderungen (Monitor ab-/angesteckt, Sperren, Standby) nichts anpassen.</summary>
    private static readonly TimeSpan DisplayChangeQuietTime = TimeSpan.FromSeconds(3);

    private readonly MonitorManager _monitors;
    private readonly Dictionary<IntPtr, TrackedWindow> _windows = [];
    private readonly Dictionary<IntPtr, (DateTime Due, Trigger Trigger)> _pending = [];
    private readonly WinEventDelegate _winEventCallback; // Feld hält den Delegaten vor dem GC fest
    private readonly List<IntPtr> _hooks = [];
    private readonly System.Windows.Forms.Timer _debounceTimer;
    private readonly System.Windows.Forms.Timer _cleanupTimer;

    private SynchronizationContext? _uiContext;
    private AppSettings _settings;
    private HashSet<string> _excluded;
    private DateTime _quietUntil = DateTime.MinValue;
    private bool _snapshotAfterQuiet;
    private bool _disposed;

    public WindowTracker(MonitorManager monitors, AppSettings settings)
    {
        _monitors = monitors;
        _settings = settings;
        _excluded = BuildExcludedSet(settings);
        _winEventCallback = OnWinEvent;

        _debounceTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _debounceTimer.Tick += (_, _) => SafeRun(OnDebounceTick);

        _cleanupTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _cleanupTimer.Tick += (_, _) => SafeRun(RemoveDeadWindows);
    }

    /// <summary>Muss auf dem UI-Thread (mit Nachrichtenschleife) aufgerufen werden.</summary>
    public void Start()
    {
        _uiContext = SynchronizationContext.Current;

        const uint flags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;
        foreach (uint ev in new[]
                 {
                     EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZEEND, EVENT_OBJECT_LOCATIONCHANGE,
                     EVENT_OBJECT_SHOW, EVENT_SYSTEM_FOREGROUND,
                 })
        {
            IntPtr hook = SetWinEventHook(ev, ev, IntPtr.Zero, _winEventCallback, 0, 0, flags);
            if (hook == IntPtr.Zero)
                Log.Error($"SetWinEventHook für Ereignis 0x{ev:X4} fehlgeschlagen");
            else
                _hooks.Add(hook);
        }

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        SnapshotAllWindows();
        _cleanupTimer.Start();
        Log.Info($"Fensterüberwachung gestartet ({_windows.Count} Fenster erfasst, {_monitors.GetAll().Count} Monitor(e))");
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        _excluded = BuildExcludedSet(settings);
    }

    private static HashSet<string> BuildExcludedSet(AppSettings settings) =>
        new(settings.ExcludedProcesses.Select(AppSettings.NormalizeProcessName), StringComparer.OrdinalIgnoreCase);

    // --- Ereignisse ------------------------------------------------------------------------

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Callback kommt aus nativem Code: Exceptions dürfen hier niemals hinaus.
        try
        {
            if (_disposed || hwnd == IntPtr.Zero) return;

            switch (eventType)
            {
                case EVENT_OBJECT_LOCATIONCHANGE:
                    if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF) return;
                    if (GetAncestor(hwnd, GA_ROOT) != hwnd) return;
                    if (_windows.TryGetValue(hwnd, out var moving) && moving.InMoveSize) return;
                    Schedule(hwnd, Trigger.Location);
                    break;

                case EVENT_OBJECT_SHOW:
                    if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF) return;
                    if (GetAncestor(hwnd, GA_ROOT) != hwnd) return;
                    Schedule(hwnd, Trigger.Shown);
                    break;

                case EVENT_SYSTEM_FOREGROUND:
                    if (!_windows.ContainsKey(hwnd)) Schedule(hwnd, Trigger.Foreground);
                    break;

                case EVENT_SYSTEM_MOVESIZESTART:
                    _pending.Remove(hwnd);
                    BeginMoveSize(hwnd);
                    break;

                case EVENT_SYSTEM_MOVESIZEEND:
                    _pending.Remove(hwnd);
                    if (_windows.TryGetValue(hwnd, out var tw)) tw.InMoveSize = false;
                    Process(hwnd, Trigger.Drag);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Fehler bei der Verarbeitung eines Fensterereignisses", ex);
        }
    }

    private void Schedule(IntPtr hwnd, Trigger trigger)
    {
        // Ein Location-Ereignis hat Vorrang vor reinen "Merken"-Ereignissen.
        if (_pending.TryGetValue(hwnd, out var existing) && existing.Trigger == Trigger.Location)
            trigger = Trigger.Location;

        _pending[hwnd] = (DateTime.UtcNow.AddMilliseconds(_settings.DebounceMilliseconds), trigger);
        if (!_debounceTimer.Enabled) _debounceTimer.Start();
    }

    private void OnDebounceTick()
    {
        var now = DateTime.UtcNow;
        if (_pending.Count > 0)
        {
            var due = _pending.Where(p => p.Value.Due <= now).ToList();
            foreach (var (hwnd, (_, trigger)) in due)
            {
                _pending.Remove(hwnd);
                Process(hwnd, trigger);
            }
        }

        if (_snapshotAfterQuiet && now >= _quietUntil)
        {
            _snapshotAfterQuiet = false;
            SnapshotAllWindows();
            Log.Info("Anzeigekonfiguration neu eingelesen");
        }

        if (_pending.Count == 0 && !_snapshotAfterQuiet) _debounceTimer.Stop();
    }

    private void BeginMoveSize(IntPtr hwnd)
    {
        var reason = WindowInspector.Check(hwnd, _excluded, out var snap);
        if (snap == null || reason is not (SkipReason.None or SkipReason.Maximized or SkipReason.Arranged)) return;
        var monitor = _monitors.FromWindow(hwnd);
        if (monitor == null) return;

        // Ausgangszustand vor dem Ziehen. Bei angeordneten (Snap) Fenstern bleibt der letzte normale Rahmen.
        var tw = GetOrAdd(hwnd, firstSeen: DateTime.MinValue);
        tw.Record(monitor, reason == SkipReason.None ? snap.FrameRect : null);
        tw.InMoveSize = true;
    }

    // --- Kernlogik -------------------------------------------------------------------------

    private void Process(IntPtr hwnd, Trigger trigger)
    {
        try
        {
            ProcessCore(hwnd, trigger);
        }
        catch (Exception ex)
        {
            Log.Error($"Fehler bei Fenster 0x{hwnd.ToInt64():X}", ex);
        }
    }

    private void ProcessCore(IntPtr hwnd, Trigger trigger)
    {
        var reason = WindowInspector.Check(hwnd, _excluded, out var snap);
        if (reason == SkipReason.Invalid)
        {
            _windows.Remove(hwnd);
            return;
        }

        if (reason != SkipReason.None || snap == null)
        {
            HandleSkipped(hwnd, reason, snap, trigger);
            return;
        }

        var monitor = _monitors.FromWindow(hwnd);
        if (monitor == null) return;

        if (WindowInspector.IsFullScreen(snap, monitor.Bounds))
        {
            HandleSkipped(hwnd, SkipReason.FullScreen, snap, trigger);
            return;
        }

        var now = DateTime.UtcNow;
        if (!_windows.TryGetValue(hwnd, out var tw))
        {
            tw = GetOrAdd(hwnd, trigger == Trigger.Snapshot ? DateTime.MinValue : now);
            tw.Record(monitor, snap.FrameRect);
            return;
        }

        bool monitorChanged = !string.Equals(tw.MonitorId, monitor.DeviceId, StringComparison.OrdinalIgnoreCase);
        if (!monitorChanged || trigger is Trigger.Snapshot or Trigger.Shown or Trigger.Foreground)
        {
            tw.Record(monitor, snap.FrameRect);
            return;
        }

        // Monitor hat gewechselt – prüfen, ob angepasst werden darf.
        string? blocked =
            !_settings.Enabled ? "pausiert" :
            now < _quietUntil ? "Anzeigeänderung läuft" :
            now < tw.SuppressUntil ? "eigene Änderung" :
            now - tw.FirstSeen < NewWindowGrace ? "Fenster ist neu" :
            !tw.HasNormalFrame ? "kein Ausgangszustand (war maximiert)" :
            null;

        var previous = new MonitorChange(tw.MonitorName, tw.WorkArea, tw.Frame);
        tw.Record(monitor, snap.FrameRect);

        if (blocked != null)
        {
            Log.Debug($"Monitorwechsel ohne Anpassung ({blocked}): {snap.Describe()} → {monitor.DisplayName}");
            return;
        }

        OnMonitorChanged(snap, previous, monitor, trigger == Trigger.Drag, tw);
    }

    /// <summary>Zustand des Fensters auf dem vorherigen Monitor.</summary>
    private readonly record struct MonitorChange(string OldMonitorName, Rectangle OldWorkArea, Rectangle OldFrame);

    private void OnMonitorChanged(WindowSnapshot window, MonitorChange previous, MonitorDescriptor target,
        bool byDrag, TrackedWindow tw)
    {
        bool keepPosition = byDrag && _settings.KeepDropPositionOnDrag;
        Rectangle targetFrame;
        string how;

        FixedRule? rule = _settings.Mode == ResizeMode.FixedRules
            ? RuleMatcher.Find(_settings.Rules, window.ProcessName, target)
            : null;

        if (rule != null)
        {
            targetFrame = SizeCalculator.FromRule(rule, previous.OldWorkArea, previous.OldFrame, target.WorkArea,
                window.FrameRect, keepPosition);
            how = $"Regel {rule.Width:0.#} × {rule.Height:0.#} {rule.UnitText}";
        }
        else if (_settings.Mode == ResizeMode.Percent || _settings.PercentFallbackWithoutRule)
        {
            targetFrame = SizeCalculator.Percent(previous.OldWorkArea, previous.OldFrame, target.WorkArea,
                window.FrameRect, keepPosition);
            how = _settings.Mode == ResizeMode.Percent ? "prozentual" : "prozentual, keine Regel";
        }
        else
        {
            Log.Info($"{window.Describe()}: keine Regel für {target.DisplayName} – unverändert");
            return;
        }

        Apply(window, targetFrame, $"{previous.OldMonitorName} → {target.DisplayName}", how, target, tw);
    }

    /// <summary>
    /// Manuelle Anpassung (Tastenkürzel), unabhängig von Aktiv/Pausiert: passende feste Regel anwenden,
    /// sonst das Fenster vollständig in den Arbeitsbereich holen.
    /// </summary>
    public void AdjustNow(IntPtr hwnd)
    {
        try
        {
            var reason = WindowInspector.Check(hwnd, _excluded, out var snap);
            if (reason != SkipReason.None || snap == null)
            {
                Log.Info($"Tastenkürzel: aktives Fenster wird nicht angepasst ({WindowInspector.SkipReasonText(reason)})");
                return;
            }

            var monitor = _monitors.FromWindow(hwnd);
            if (monitor == null) return;
            if (WindowInspector.IsFullScreen(snap, monitor.Bounds))
            {
                Log.Info($"Tastenkürzel: {snap.Describe()} ist im Vollbild – unverändert");
                return;
            }

            var rule = RuleMatcher.Find(_settings.Rules, snap.ProcessName, monitor);
            Rectangle target;
            string how;
            if (rule != null)
            {
                target = SizeCalculator.FromRule(rule, null, null, monitor.WorkArea, snap.FrameRect, keepCurrentPosition: true);
                how = $"Regel {rule.Width:0.#} × {rule.Height:0.#} {rule.UnitText}";
            }
            else
            {
                target = SizeCalculator.ClampToWorkArea(snap.FrameRect, monitor.WorkArea);
                how = "keine Regel, nur in den Arbeitsbereich eingepasst";
            }

            _windows.TryGetValue(hwnd, out var tw);
            Apply(snap, target, $"Tastenkürzel auf {monitor.DisplayName}", how, monitor, tw);
        }
        catch (Exception ex)
        {
            Log.Error("Fehler bei der manuellen Anpassung", ex);
        }
    }

    private void Apply(WindowSnapshot window, Rectangle targetFrame, string context, string how,
        MonitorDescriptor monitor, TrackedWindow? tw)
    {
        string sizes = $"{window.FrameRect.Width}×{window.FrameRect.Height} → {targetFrame.Width}×{targetFrame.Height}";
        if (targetFrame == window.FrameRect)
        {
            Log.Info($"{window.Describe()}: {context}; Größe passt bereits ({how})");
            return;
        }

        MoveResult result = WindowMover.Move(window.Handle, targetFrame);

        if (tw != null)
        {
            tw.SuppressUntil = DateTime.UtcNow + OwnChangeSuppression;
            if (result.Outcome is MoveOutcome.Ok or MoveOutcome.Partial) tw.Record(monitor, result.ActualFrame);
        }

        switch (result.Outcome)
        {
            case MoveOutcome.Ok:
                Log.Info($"{window.Describe()}: {context}; {sizes} ({how})");
                break;
            case MoveOutcome.Partial:
                Log.Info($"{window.Describe()}: {context}; {sizes} ({how}) – Programm hat " +
                         $"{result.ActualFrame.Width}×{result.ActualFrame.Height} gewählt (eigene Mindest-/Höchstgröße)");
                break;
            case MoveOutcome.AccessDenied:
                Log.Warn($"{window.Describe()}: Zugriff verweigert. Das Programm läuft vermutlich mit " +
                         $"Administratorrechten – dafür muss {AppInfo.Name} ebenfalls als Administrator laufen.");
                break;
            default:
                Log.Warn($"{window.Describe()}: Anpassung fehlgeschlagen (Win32-Fehler {result.Win32Error})");
                break;
        }
    }

    private void HandleSkipped(IntPtr hwnd, SkipReason reason, WindowSnapshot? snap, Trigger trigger)
    {
        // Maximierte/angeordnete Fenster weiter verfolgen: Monitor merken, letzten normalen Rahmen behalten.
        if (snap != null && reason is SkipReason.Maximized or SkipReason.Arranged)
        {
            var monitor = _monitors.FromWindow(hwnd);
            if (monitor != null)
            {
                var tw = GetOrAdd(hwnd, trigger == Trigger.Snapshot ? DateTime.MinValue : DateTime.UtcNow);
                tw.Record(monitor, frame: null);
            }
        }

        if (trigger == Trigger.Drag && Log.VerboseEnabled)
        {
            string what = snap?.Describe() ?? $"0x{hwnd.ToInt64():X}";
            Log.Debug($"Ignoriert ({WindowInspector.SkipReasonText(reason)}): {what}");
        }
    }

    private TrackedWindow GetOrAdd(IntPtr hwnd, DateTime firstSeen)
    {
        if (!_windows.TryGetValue(hwnd, out var tw))
        {
            tw = new TrackedWindow { FirstSeen = firstSeen };
            _windows[hwnd] = tw;
        }
        return tw;
    }

    private void SnapshotAllWindows()
    {
        foreach (var hwnd in WindowInspector.EnumerateTopLevelWindows())
            Process(hwnd, Trigger.Snapshot);
    }

    private void RemoveDeadWindows()
    {
        foreach (var hwnd in _windows.Keys.Where(h => !IsWindow(h)).ToList())
            _windows.Remove(hwnd);
    }

    // --- Systemereignisse (kommen ggf. auf einem anderen Thread) ---------------------------

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => PostQuietPeriod("Anzeigeeinstellungen geändert");

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e) => PostQuietPeriod($"Sitzungswechsel ({e.Reason})");

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) PostQuietPeriod("Standby beendet");
    }

    private void PostQuietPeriod(string reason)
    {
        var ctx = _uiContext;
        if (ctx == null) return;
        ctx.Post(_ => SafeRun(() =>
        {
            if (_disposed) return;
            Log.Info($"{reason} – Anpassung für {DisplayChangeQuietTime.TotalSeconds:0} s ausgesetzt");
            _monitors.Invalidate();
            _quietUntil = DateTime.UtcNow + DisplayChangeQuietTime;
            _snapshotAfterQuiet = true;
            _debounceTimer.Start();
        }), null);
    }

    private static void SafeRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error("Unerwarteter Fehler in der Fensterüberwachung", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;

        foreach (var hook in _hooks) UnhookWinEvent(hook);
        _hooks.Clear();

        _debounceTimer.Dispose();
        _cleanupTimer.Dispose();
        _windows.Clear();
        _pending.Clear();
    }
}
