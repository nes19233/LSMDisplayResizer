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

    /// <summary>
    /// Zustand eines Fensters. Entscheidend ist der Monitor, auf dem das Fenster zuletzt im normalen
    /// Zustand (nicht maximiert/angeordnet) war: Wird es z. B. maximiert, auf einen anderen Monitor
    /// verschoben und dort wiederhergestellt, wird die gemerkte normale Größe umgerechnet.
    /// </summary>
    private sealed class TrackedWindow
    {
        /// <summary>Monitor, auf dem <see cref="Frame"/> aufgenommen wurde.</summary>
        public string MonitorId = string.Empty;
        public string MonitorName = string.Empty;

        /// <summary>Sichtbarer Rahmen im letzten normalen Zustand (nicht maximiert/angeordnet).</summary>
        public Rectangle Frame;

        /// <summary>Arbeitsbereich des Monitors, auf dem <see cref="Frame"/> aufgenommen wurde.</summary>
        public Rectangle WorkArea;

        /// <summary>Anzeigeskalierung des Monitors, auf dem <see cref="Frame"/> aufgenommen wurde.</summary>
        public double Scale = 1.0;

        /// <summary>false, solange das Fenster nie im normalen Zustand gesehen wurde.</summary>
        public bool HasNormalFrame;

        /// <summary>
        /// Zuletzt im normalen Zustand gesehen (nicht maximiert, angeordnet oder in voller Höhe). Während einer
        /// Anzeigeänderung eingefroren: Hat erst Windows das Fenster auf volle Höhe gestaucht, wird es trotzdem angepasst.
        /// </summary>
        public bool WasNormal;

        /// <summary>Programm und Fensterklasse ("outlook.exe|rctrl_renwnd32") – zum Wiedererkennen nach dem Schließen.</summary>
        public string Identity = string.Empty;

        public DateTime FirstSeen;
        public DateTime SuppressUntil;
        public bool InMoveSize;

        /// <summary>Gemerkte Anordnung je Monitorkonstellation (Schlüssel: Geräte-IDs aller angeschlossenen Monitore).</summary>
        public readonly Dictionary<string, LayoutEntry> Layouts = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Merkt sich den normalen Rahmen samt Monitor. Ohne <paramref name="frame"/> (maximiert/angeordnet)
        /// bleibt alles unverändert – der letzte normale Zustand ist die Ausgangsbasis für die nächste Anpassung.
        /// </summary>
        public void Record(MonitorDescriptor monitor, Rectangle? frame)
        {
            WasNormal = frame != null;
            if (frame is not { } f) return;
            MonitorId = monitor.Key;
            MonitorName = monitor.DisplayName;
            Frame = f;
            WorkArea = monitor.WorkArea;
            Scale = monitor.Scale;
            HasNormalFrame = true;
        }
    }

    /// <summary>Anordnung eines Fensters in einer Monitorkonstellation.</summary>
    /// <param name="MonitorId">Monitor, auf dem das Fenster lag.</param>
    /// <param name="Frame">Sichtbarer Rahmen (bei maximierten Fenstern ohne Bedeutung).</param>
    /// <param name="WorkArea">Arbeitsbereich des Monitors beim Merken.</param>
    /// <param name="Maximized">Fenster war maximiert – dann wird nur der Monitor wiederhergestellt.</param>
    private readonly record struct LayoutEntry(string MonitorId, string MonitorName, Rectangle Frame, Rectangle WorkArea, bool Maximized);

    /// <summary>Letzter normaler Zustand eines geschlossenen Fensters (je Programm und Fensterklasse).</summary>
    private sealed record ClosedWindow(string MonitorId, string MonitorName, Rectangle Frame, Rectangle WorkArea, double Scale);

    /// <summary>Höchstzahl gemerkter geschlossener Fenster (älteste fallen heraus).</summary>
    private const int MaxClosedWindows = 200;

    /// <summary>Neue Fenster positionieren sich oft selbst – in dieser Zeit nicht eingreifen.</summary>
    private static readonly TimeSpan NewWindowGrace = TimeSpan.FromMilliseconds(1500);

    /// <summary>Nach einer eigenen Änderung werden Ereignisse dieses Fensters kurz nur protokolliert.</summary>
    private static readonly TimeSpan OwnChangeSuppression = TimeSpan.FromMilliseconds(800);

    // --- Anzeigeänderungen (An-/Abdocken) ---------------------------------------------------
    // Windows meldet nicht, wann es mit dem Umräumen fertig ist. Stattdessen wird "Ruhe" erkannt:

    /// <summary>Mindestzeit nach der (letzten) Anzeigeänderung – Docks melden oft mehrere Änderungen nacheinander.</summary>
    private static readonly TimeSpan MinSettleTime = TimeSpan.FromSeconds(2);

    /// <summary>Ein Fenster gilt als fertig umgeräumt, wenn es so lange nicht mehr bewegt wurde.</summary>
    private static readonly TimeSpan WindowCalmTime = TimeSpan.FromSeconds(1);

    /// <summary>Abschluss, wenn so lange gar kein Fenster mehr bewegt wurde (sonst nach der Höchstdauer aus den Einstellungen).</summary>
    private static readonly TimeSpan GlobalCalmTime = TimeSpan.FromSeconds(2);

    private readonly MonitorManager _monitors;
    private readonly Dictionary<IntPtr, TrackedWindow> _windows = [];
    /// <summary>
    /// Geschlossene Fenster je Programm und Fensterklasse. Öffnet das Programm später ein Fenster auf einem anderen
    /// Monitor und stellt dabei seine alte Größe wieder her, wird es wie ein verschobenes Fenster angepasst.
    /// </summary>
    private readonly Dictionary<string, ClosedWindow> _closed = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Neu geöffnete Fenster, die nach der Schonfrist mit ihrem geschlossenen Vorgänger verglichen werden.</summary>
    private readonly Dictionary<IntPtr, (DateTime Due, ClosedWindow Closed)> _reopenChecks = [];
    private readonly Dictionary<IntPtr, (DateTime Due, Trigger Trigger)> _pending = [];
    private readonly WinEventDelegate _winEventCallback; // Feld hält den Delegaten vor dem GC fest
    private readonly List<IntPtr> _hooks = [];
    private readonly System.Windows.Forms.Timer _debounceTimer;
    private readonly System.Windows.Forms.Timer _cleanupTimer;

    private SynchronizationContext? _uiContext;
    private AppSettings _settings;
    private HashSet<string> _excluded;
    /// <summary>Anzeigeänderung läuft: Ausgangszustände eingefroren, Fenster werden nach und nach angepasst.</summary>
    private bool _relayoutPending;
    private DateTime _displayChangeStarted;
    private DateTime _minSettleUntil;
    private DateTime _maxSettleUntil;
    private int _adjustedThisChange;
    /// <summary>Letzte fremde Fensterbewegung (eigene Anpassungen zählen nicht).</summary>
    private DateTime _lastLocationEvent = DateTime.MinValue;
    /// <summary>Während einer Anzeigeänderung bewegte Fenster und Zeitpunkt ihrer letzten Bewegung.</summary>
    private readonly Dictionary<IntPtr, DateTime> _movedDuringChange = [];
    /// <summary>
    /// Nur eine Ersatzanzeige aktiv (z. B. 1024×768 nach dem Abstecken bei zugeklapptem Notebook). Windows quetscht die
    /// Fenster hinein – diese Größen dürfen nicht zur Ausgangsbasis werden. Die Zustände von vorher bleiben eingefroren,
    /// bis wieder ein echter Monitor da ist; dann wird von dort aus umgerechnet.
    /// </summary>
    private bool _onPlaceholder;
    private bool _disposed;

    /// <summary>Zustände eingefroren: Anzeigeänderung läuft oder nur Ersatzanzeige aktiv.</summary>
    private bool Frozen => _relayoutPending || _onPlaceholder;

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
                    _windows.TryGetValue(hwnd, out var moving);
                    var moved = DateTime.UtcNow;
                    if (moving == null || moved >= moving.SuppressUntil) // eigene Anpassungen nicht mitzählen
                    {
                        _lastLocationEvent = moved;
                        if (_relayoutPending) _movedDuringChange[hwnd] = moved;
                    }
                    if (moving is { InMoveSize: true }) return;
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

        if (_relayoutPending && now >= _minSettleUntil) SettleAfterDisplayChange(now);

        // Während einer Anzeigeänderung/Ersatzanzeige warten – danach stimmen Monitor und Arbeitsbereich.
        if (_reopenChecks.Count > 0 && !Frozen)
        {
            foreach (var (hwnd, (_, closed)) in _reopenChecks.Where(p => p.Value.Due <= now).ToList())
            {
                _reopenChecks.Remove(hwnd);
                CheckReopened(hwnd, closed);
            }
        }

        if (_pending.Count == 0 && !_relayoutPending && _reopenChecks.Count == 0) _debounceTimer.Stop();
    }

    private void BeginMoveSize(IntPtr hwnd)
    {
        var reason = WindowInspector.Check(hwnd, _excluded, out var snap);
        if (snap == null || reason is not (SkipReason.None or SkipReason.Maximized or SkipReason.Arranged)) return;
        var monitor = _monitors.FromWindow(hwnd);
        if (monitor == null) return;

        // Ausgangszustand vor dem Ziehen. Bei angeordneten (Snap) Fenstern und solchen mit voller Höhe
        // bleibt der letzte normale Rahmen.
        bool normal = reason == SkipReason.None && !IsFullHeight(snap, monitor);
        var tw = GetOrAdd(hwnd, firstSeen: DateTime.MinValue, snap);
        tw.Record(monitor, normal ? snap.FrameRect : null);
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
            Forget(hwnd);
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

        if (IsFullHeight(snap, monitor))
        {
            HandleSkipped(hwnd, SkipReason.FullHeight, snap, trigger);
            return;
        }

        var now = DateTime.UtcNow;
        if (!_windows.TryGetValue(hwnd, out var tw))
        {
            tw = GetOrAdd(hwnd, trigger == Trigger.Snapshot ? DateTime.MinValue : now, snap);
            tw.Record(monitor, snap.FrameRect);
            RememberLayout(tw, monitor, snap.FrameRect, maximized: false);
            if (trigger != Trigger.Snapshot) ScheduleReopenCheck(hwnd, tw, now);
            return;
        }

        // Während einer Anzeigeänderung räumt Windows die Fenster um. Den Zustand von vorher behalten –
        // SettleAfterDisplayChange passt anschließend alle Fenster an. Ebenso, solange nur eine Ersatzanzeige aktiv ist.
        if (Frozen) return;

        // Vergleich mit dem Monitor des letzten normalen Zustands (siehe TrackedWindow).
        bool monitorChanged = tw.HasNormalFrame &&
                              !string.Equals(tw.MonitorId, monitor.Key, StringComparison.OrdinalIgnoreCase);
        if (!monitorChanged || trigger is Trigger.Snapshot or Trigger.Shown or Trigger.Foreground)
        {
            tw.Record(monitor, snap.FrameRect);
            RememberLayout(tw, monitor, snap.FrameRect, maximized: false);
            return;
        }

        // Monitor hat gewechselt – prüfen, ob angepasst werden darf.
        string? blocked =
            !_settings.Enabled ? "pausiert" :
            now < tw.SuppressUntil ? "eigene Änderung" :
            now - tw.FirstSeen < NewWindowGrace ? "Fenster ist neu" :
            null;

        var previous = new MonitorChange(tw.MonitorName, tw.WorkArea, tw.Frame, tw.Scale);
        tw.Record(monitor, snap.FrameRect);
        RememberLayout(tw, monitor, snap.FrameRect, maximized: false);

        if (blocked != null)
        {
            Log.Debug($"Monitorwechsel ohne Anpassung ({blocked}): {snap.Describe()} → {monitor.DisplayName}");
            return;
        }

        OnMonitorChanged(snap, previous, monitor, trigger == Trigger.Drag, tw);
    }

    /// <summary>Zustand des Fensters auf dem vorherigen Monitor.</summary>
    private readonly record struct MonitorChange(string OldMonitorName, Rectangle OldWorkArea, Rectangle OldFrame, double OldScale);

    private void OnMonitorChanged(WindowSnapshot window, MonitorChange previous, MonitorDescriptor target,
        bool byDrag, TrackedWindow tw, string note = "")
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
                window.FrameRect, keepPosition, _settings.PercentHeightScaling / 100.0, previous.OldScale, target.Scale);
            how = _settings.Mode == ResizeMode.Percent ? "prozentual" : "prozentual, keine Regel";
        }
        else
        {
            Log.Info($"{window.Describe()}: keine Regel für {target.DisplayName} – unverändert");
            return;
        }

        Apply(window, targetFrame, $"{previous.OldMonitorName} → {target.DisplayName}{note}", how, target, tw);
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
            if (result.Outcome is MoveOutcome.Ok or MoveOutcome.Partial)
            {
                tw.Record(monitor, result.ActualFrame);
                RememberLayout(tw, monitor, result.ActualFrame, maximized: false);
            }
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
        if (snap != null && reason is SkipReason.Maximized or SkipReason.Arranged or SkipReason.FullHeight)
        {
            var monitor = _monitors.FromWindow(hwnd);
            if (monitor != null)
            {
                var tw = GetOrAdd(hwnd, trigger == Trigger.Snapshot ? DateTime.MinValue : DateTime.UtcNow, snap);
                // Eingefroren (Anzeigeänderung/Ersatzanzeige): Zustand von vorher behalten – Windows staucht beim
                // Umräumen Fenster auf volle Höhe, das ist kein Andocken durch den Benutzer.
                if (!Frozen) tw.Record(monitor, frame: null);
                // Maximierte Fenster: Monitor je Konstellation merken (angedockte/aufgezogene nicht – die ordnet Windows).
                if (reason == SkipReason.Maximized) RememberLayout(tw, monitor, tw.Frame, maximized: true);
            }
        }

        if (trigger == Trigger.Drag && Log.VerboseEnabled)
        {
            string what = snap?.Describe() ?? $"0x{hwnd.ToInt64():X}";
            Log.Debug($"Ignoriert ({WindowInspector.SkipReasonText(reason)}): {what}");
        }
    }

    /// <summary>Volle Höhe des Arbeitsbereichs (angedockt/aufgezogen) – nur wenn in den Einstellungen aktiviert.</summary>
    private bool IsFullHeight(WindowSnapshot snap, MonitorDescriptor monitor) =>
        _settings.TreatFullHeightAsSnapped && WindowInspector.SpansFullHeight(snap.FrameRect, monitor.WorkArea);

    private TrackedWindow GetOrAdd(IntPtr hwnd, DateTime firstSeen, WindowSnapshot? snap = null)
    {
        if (!_windows.TryGetValue(hwnd, out var tw))
        {
            tw = new TrackedWindow { FirstSeen = firstSeen };
            _windows[hwnd] = tw;
        }
        if (tw.Identity.Length == 0 && snap != null) tw.Identity = $"{snap.ProcessName}|{snap.ClassName}";
        return tw;
    }

    /// <summary>Fenster ist geschlossen: letzten normalen Zustand je Programm/Fensterklasse merken.</summary>
    private void Forget(IntPtr hwnd)
    {
        if (!_windows.Remove(hwnd, out var tw)) return;
        _reopenChecks.Remove(hwnd);
        if (tw.Identity.Length == 0 || !tw.HasNormalFrame) return;

        _closed.Remove(tw.Identity); // neu einfügen = ans Ende (jüngster Eintrag)
        _closed[tw.Identity] = new ClosedWindow(tw.MonitorId, tw.MonitorName, tw.Frame, tw.WorkArea, tw.Scale);
        if (_closed.Count > MaxClosedWindows) _closed.Remove(_closed.Keys.First());
    }

    /// <summary>
    /// Neues Fenster eines Programms, dessen Fenster zuvor geschlossen wurde: nach der Schonfrist prüfen, ob es mit der
    /// alten Größe auf einem anderen Monitor geöffnet wurde (siehe <see cref="CheckReopened"/>).
    /// </summary>
    private void ScheduleReopenCheck(IntPtr hwnd, TrackedWindow tw, DateTime now)
    {
        if (tw.Identity.Length == 0) return;
        RemoveDeadWindows(); // gerade geschlossene Fenster sofort übernehmen, nicht erst beim nächsten Aufräumen
        if (!_closed.Remove(tw.Identity, out var closed)) return;
        _reopenChecks[hwnd] = (now + NewWindowGrace, closed);
        if (!_debounceTimer.Enabled) _debounceTimer.Start();
    }

    /// <summary>
    /// Passt ein neu geöffnetes Fenster an, wenn das Programm die Größe seines zuletzt geschlossenen Fensters von einem
    /// anderen Monitor (oder anderer Auflösung) wiederhergestellt hat – ausgehend von dessen Zustand.
    /// </summary>
    private void CheckReopened(IntPtr hwnd, ClosedWindow closed)
    {
        try
        {
            if (!_settings.Enabled || !_windows.TryGetValue(hwnd, out var tw) || tw.InMoveSize) return;
            var reason = WindowInspector.Check(hwnd, _excluded, out var snap);
            if (reason != SkipReason.None || snap == null) return;
            var monitor = _monitors.FromWindow(hwnd);
            if (monitor == null || WindowInspector.IsFullScreen(snap, monitor.Bounds) || IsFullHeight(snap, monitor)) return;

            bool otherMonitor = !string.Equals(closed.MonitorId, monitor.Key, StringComparison.OrdinalIgnoreCase);
            if (!otherMonitor && closed.WorkArea == monitor.WorkArea)
            {
                // Gleicher Monitor wie beim Schließen – das Programm hat seine Größe selbst richtig wiederhergestellt.
                Log.Debug($"{snap.Describe()}: neu geöffnet auf {monitor.DisplayName}, dort zuletzt geschlossen – unverändert");
                return;
            }

            if (!SizeMatches(snap.FrameRect.Size, closed.Frame.Size, closed.Scale > 0 ? monitor.Scale / closed.Scale : 1.0))
            {
                Log.Debug($"{snap.Describe()}: neu geöffnet auf {monitor.DisplayName}, Größe {snap.FrameRect.Width}×{snap.FrameRect.Height} " +
                          $"passt nicht zum geschlossenen Fenster ({closed.Frame.Width}×{closed.Frame.Height}) – unverändert");
                return;
            }

            var previous = new MonitorChange(closed.MonitorName, closed.WorkArea, closed.Frame, closed.Scale);
            OnMonitorChanged(snap, previous, monitor, byDrag: false, tw, " (neu geöffnet, dort zuletzt geschlossen)");
        }
        catch (Exception ex)
        {
            Log.Error($"Fehler bei neu geöffnetem Fenster 0x{hwnd.ToInt64():X}", ex);
        }
    }

    /// <summary>
    /// Hat das Programm die alte Größe wiederhergestellt? Gleiche Pixelgröße oder – bei Programmen, die ihre Größe in
    /// logischen Einheiten speichern – um das Skalierungsverhältnis umgerechnet. Toleranz 2 % (mindestens 8 px).
    /// </summary>
    internal static bool SizeMatches(Size actual, Size remembered, double scaleRatio)
    {
        static bool Close(int a, double b) => Math.Abs(a - b) <= Math.Max(8, b * 0.02);
        return (Close(actual.Width, remembered.Width) && Close(actual.Height, remembered.Height)) ||
               (Close(actual.Width, remembered.Width * scaleRatio) && Close(actual.Height, remembered.Height * scaleRatio));
    }

    private void SnapshotAllWindows()
    {
        foreach (var hwnd in WindowInspector.EnumerateTopLevelWindows())
            Process(hwnd, Trigger.Snapshot);
    }

    /// <summary>
    /// Läuft nach einer Anzeigeänderung (An-/Abdocken, Monitor ab-/angesteckt, Standby) bei jedem Timer-Tick, sobald
    /// die Mindestzeit vorbei ist: Fenster, die Windows umgeräumt hat und die seit <see cref="WindowCalmTime"/> ruhen,
    /// werden sofort angepasst. Ruhen alle Fenster seit <see cref="GlobalCalmTime"/> (spätestens nach der Höchstdauer),
    /// prüft ein letzter Durchgang alle Fenster – auch solche ohne eigenes Ereignis (z. B. nur andere Auflösung).
    /// </summary>
    private void SettleAfterDisplayChange(DateTime now)
    {
        bool adjust = _settings.Enabled && _settings.AdjustAfterDisplayChange;

        var calmWindows = _movedDuringChange.Where(p => now - p.Value >= WindowCalmTime).Select(p => p.Key).ToList();
        if (calmWindows.Count > 0)
        {
            // Monitorkennungen frisch lesen: Mitten im Umbau zwischengespeicherte können schon veraltet sein.
            _monitors.Invalidate();
            bool placeholderNow = MonitorDescriptor.OnlyPlaceholders(_monitors.GetAll());
            foreach (var hwnd in calmWindows)
            {
                _movedDuringChange.Remove(hwnd);
                _pending.Remove(hwnd);
                // Auf der Ersatzanzeige nichts anpassen – der letzte Durchgang entscheidet mit dem endgültigen Stand.
                if (!placeholderNow && AdjustAfterDisplayChange(hwnd, adjust)) _adjustedThisChange++;
            }
        }

        bool calm = now - _lastLocationEvent >= GlobalCalmTime;
        if (!calm && now < _maxSettleUntil) return;

        _relayoutPending = false;
        _movedDuringChange.Clear();
        _pending.Clear();
        _monitors.Invalidate();

        var monitors = _monitors.GetAll();
        if (MonitorDescriptor.OnlyPlaceholders(monitors))
        {
            if (!_onPlaceholder)
                Log.Info($"Nur Ersatzanzeige ohne Monitor aktiv ({string.Join(", ", monitors.Select(m => $"{m.Bounds.Width}×{m.Bounds.Height}"))}, " +
                         "z. B. abgesteckt bei zugeklapptem Notebook) – Fenster werden erst angepasst, wenn wieder ein Monitor da ist");
            _onPlaceholder = true;
            return;
        }
        bool afterPlaceholder = _onPlaceholder;
        _onPlaceholder = false;

        // Zur Nachvollziehbarkeit: welche Konstellation erkannt wurde und für wie viele Fenster eine Anordnung bekannt ist.
        string configKey = CurrentConfigKey();
        int known = _windows.Values.Count(t => t.Layouts.ContainsKey(configKey));
        Log.Info($"Monitorkonstellation: {string.Join(", ", monitors.Select(m => $"{m.FriendlyName} [{m.Key}]"))}" +
                 $" – gemerkte Anordnung für {known} Fenster{(afterPlaceholder ? " (Ausgangsbasis: Stand vor der Ersatzanzeige)" : string.Empty)}");
        foreach (var hwnd in WindowInspector.EnumerateTopLevelWindows())
        {
            if (AdjustAfterDisplayChange(hwnd, adjust)) _adjustedThisChange++;
        }

        string how = calm ? "alle Fenster ruhen" : "Höchstdauer erreicht";
        Log.Info(adjust
            ? $"Anzeigeänderung abgeschlossen nach {(now - _displayChangeStarted).TotalSeconds:0.0} s ({how}): " +
              $"{_adjustedThisChange} Fenster angepasst, {_monitors.GetAll().Count} Monitor(e)"
            : $"Anzeigeänderung abgeschlossen ({how}), Fenster nicht angepasst " +
              $"({(_settings.Enabled ? "Anpassung nach Anzeigeänderungen ausgeschaltet" : "pausiert")})");
    }

    /// <summary>
    /// Passt ein Fenster nach einer Anzeigeänderung an, falls es jetzt auf einem anderen Monitor liegt oder sein Monitor
    /// einen anderen Arbeitsbereich hat – ausgehend vom gemerkten Zustand vor der Änderung. Rückgabe: angepasst.
    /// </summary>
    private bool AdjustAfterDisplayChange(IntPtr hwnd, bool adjust)
    {
        try
        {
            var reason = WindowInspector.Check(hwnd, _excluded, out var snap);
            if (reason == SkipReason.Invalid)
            {
                Forget(hwnd);
                return false;
            }

            // Zuerst: Ist diese Monitorkonstellation bekannt, kommt das Fenster dorthin zurück, wo es zuletzt war –
            // vor allem anderen, damit die von Windows umgeräumte Position nicht als neue Anordnung gemerkt wird.
            if (snap != null && reason is SkipReason.None or SkipReason.Maximized &&
                _settings.Enabled && _settings.RestoreLayoutPerConfiguration &&
                _windows.TryGetValue(hwnd, out var known) && _monitors.FromWindow(hwnd) is { } current &&
                TryRestoreLayout(hwnd, snap, reason == SkipReason.Maximized, current, known, out bool restored))
            {
                return restored;
            }

            if (reason != SkipReason.None || snap == null)
            {
                HandleSkipped(hwnd, reason, snap, Trigger.Snapshot);
                return false;
            }

            var monitor = _monitors.FromWindow(hwnd);
            if (monitor == null || WindowInspector.IsFullScreen(snap, monitor.Bounds)) return false;

            _windows.TryGetValue(hwnd, out var tw);
            // Volle Höhe: angedockt lassen – außer das Fenster war vorher normal und erst Windows hat es beim Umräumen
            // auf die Höhe des (kleineren) Bildschirms gestaucht.
            bool squeezed = IsFullHeight(snap, monitor) && tw is { WasNormal: true, HasNormalFrame: true };
            if (IsFullHeight(snap, monitor) && !squeezed)
            {
                HandleSkipped(hwnd, SkipReason.FullHeight, snap, Trigger.Snapshot);
                return false;
            }

            if (tw is not { HasNormalFrame: true })
            {
                GetOrAdd(hwnd, DateTime.MinValue, snap).Record(monitor, snap.FrameRect);
                return false;
            }

            bool otherMonitor = !string.Equals(tw.MonitorId, monitor.Key, StringComparison.OrdinalIgnoreCase);
            bool otherWorkArea = tw.WorkArea != monitor.WorkArea;
            if (!otherMonitor && !otherWorkArea)
            {
                if (squeezed && adjust && snap.FrameRect != tw.Frame)
                {
                    // Zurück auf demselben Monitor, aber von Windows gestaucht: alte Größe wiederherstellen.
                    Apply(snap, tw.Frame, $"zurück auf {monitor.DisplayName}", "Größe von vor der Anzeigeänderung", monitor, tw);
                    return true;
                }
                tw.Record(monitor, snap.FrameRect);
                RememberLayout(tw, monitor, snap.FrameRect, maximized: false);
                return false;
            }

            var previous = new MonitorChange(tw.MonitorName, tw.WorkArea, tw.Frame, tw.Scale);
            tw.Record(monitor, snap.FrameRect);
            RememberLayout(tw, monitor, snap.FrameRect, maximized: false);
            if (!adjust) return false;

            OnMonitorChanged(snap, previous, monitor, byDrag: false, tw);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Fehler bei Fenster 0x{hwnd.ToInt64():X} nach der Anzeigeänderung", ex);
            return false;
        }
    }

    /// <summary>
    /// Stellt die gemerkte Anordnung für die aktuelle Monitorkonstellation wieder her.
    /// Normale Fenster: Monitor, Position und Größe. Maximierte Fenster: nur der Monitor (bleiben maximiert).
    /// Rückgabe true = für diese Konstellation ist eine Anordnung bekannt (das Fenster ist damit erledigt);
    /// <paramref name="restored"/> = es wurde tatsächlich verschoben.
    /// </summary>
    private bool TryRestoreLayout(IntPtr hwnd, WindowSnapshot snap, bool isMaximized, MonitorDescriptor current,
        TrackedWindow tw, out bool restored)
    {
        restored = false;
        if (!tw.Layouts.TryGetValue(CurrentConfigKey(), out var entry)) return false;
        if (entry.Maximized != isMaximized) return false; // Zustand hat sich geändert → normale Logik

        var target = _monitors.GetAll().FirstOrDefault(m => string.Equals(m.Key, entry.MonitorId, StringComparison.OrdinalIgnoreCase));
        if (target == null) return false;
        bool sameMonitor = string.Equals(current.Key, target.Key, StringComparison.OrdinalIgnoreCase);

        if (isMaximized)
        {
            if (sameMonitor) return true;

            // Wiederherstellungsrechteck auf dem Zielmonitor, damit das Fenster dort maximiert wird.
            Rectangle normal = tw.HasNormalFrame
                ? SizeCalculator.Percent(tw.WorkArea, tw.Frame, target.WorkArea, tw.Frame, keepCurrentPosition: false)
                : Rectangle.Inflate(target.WorkArea, -target.WorkArea.Width / 8, -target.WorkArea.Height / 8);

            tw.SuppressUntil = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            restored = WindowMover.MoveMaximized(hwnd, normal);
            if (restored)
            {
                tw.Record(target, normal);
                RememberLayout(tw, target, normal, maximized: true);
                Log.Info($"{snap.Describe()}: Anordnung wiederhergestellt – maximiert auf {target.DisplayName} (bekannte Monitorkonstellation)");
            }
            else
            {
                Log.Warn($"{snap.Describe()}: maximiertes Fenster konnte nicht auf {target.DisplayName} verschoben werden");
            }
            return true;
        }

        // Normales Fenster: angedockte/Vollbild-Fenster nicht anfassen (volle Höhe nur, wenn nicht erst Windows gestaucht hat).
        if (WindowInspector.IsFullScreen(snap, current.Bounds) || (IsFullHeight(snap, current) && !tw.WasNormal)) return false;

        // Exakt die gemerkte Position – hat sich der Arbeitsbereich geändert (z. B. Auflösung), prozentual umgerechnet.
        var targetFrame = SizeCalculator.Percent(entry.WorkArea, entry.Frame, target.WorkArea, snap.FrameRect, keepCurrentPosition: false);
        if (sameMonitor && targetFrame == snap.FrameRect)
        {
            tw.Record(current, snap.FrameRect);
            RememberLayout(tw, current, snap.FrameRect, maximized: false);
            return true;
        }

        Apply(snap, targetFrame, $"Anordnung wiederhergestellt → {target.DisplayName}", "bekannte Monitorkonstellation", target, tw);
        restored = true;
        return true;
    }

    /// <summary>Schlüssel der aktuellen Monitorkonstellation: Geräte-IDs aller angeschlossenen Monitore.</summary>
    private string CurrentConfigKey() =>
        string.Join("|", _monitors.GetAll().Select(m => m.Key).OrderBy(id => id, StringComparer.OrdinalIgnoreCase));

    /// <summary>Merkt sich die Anordnung des Fensters für die aktuelle Monitorkonstellation (nicht während einer Anzeigeänderung).</summary>
    private void RememberLayout(TrackedWindow tw, MonitorDescriptor monitor, Rectangle frame, bool maximized)
    {
        if (!_settings.RestoreLayoutPerConfiguration || Frozen) return;
        string key = CurrentConfigKey();
        // Nur merken, wenn der Monitor zur ermittelten Konstellation gehört (Schutz vor Momentaufnahmen mitten im Umbau).
        if (!key.Split('|').Contains(monitor.Key, StringComparer.OrdinalIgnoreCase)) return;
        tw.Layouts[key] = new LayoutEntry(monitor.Key, monitor.DisplayName, frame, monitor.WorkArea, maximized);
    }

    private void RemoveDeadWindows()
    {
        foreach (var hwnd in _windows.Keys.Where(h => !IsWindow(h)).ToList())
            Forget(hwnd);
    }

    // --- Systemereignisse (kommen ggf. auf einem anderen Thread) ---------------------------

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => PostQuietPeriod("Anzeigeeinstellungen geändert");

#if DEBUG
    /// <summary>Nur Debug-Build: Anzeigeänderung simulieren (für Tests ohne echtes An-/Abdocken).</summary>
    public void SimulateDisplayChange() => PostQuietPeriod("Simulierte Anzeigeänderung");
#endif

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
            var now = DateTime.UtcNow;
            int maxSeconds = _settings.DisplayChangeDelaySeconds;
            _monitors.Invalidate();

            if (!_relayoutPending)
            {
                _displayChangeStarted = now;
                _adjustedThisChange = 0;
                _movedDuringChange.Clear();
                _maxSettleUntil = DateTime.MinValue;
            }

            // Jede weitere Meldung (Docks melden oft mehrere Änderungen nacheinander) verlängert Mindest- und Höchstdauer.
            _minSettleUntil = now + (MinSettleTime < TimeSpan.FromSeconds(maxSeconds) ? MinSettleTime : TimeSpan.FromSeconds(maxSeconds));
            var max = now + TimeSpan.FromSeconds(maxSeconds);
            if (max > _maxSettleUntil) _maxSettleUntil = max;
            _relayoutPending = true;
            _debounceTimer.Start();
            Log.Info($"{reason} – passe umgeräumte Fenster an, sobald sie ruhen (höchstens {maxSeconds} s)");
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
