using System.Drawing;
using LSMDisplayResizer.Logging;
using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Settings;
using LSMDisplayResizer.Tracking;
using Microsoft.Win32;
using static LSMDisplayResizer.Interop.NativeMethods;

namespace LSMDisplayResizer.Overlay;

/// <summary>
/// Zeigt optional eine feine Linie an jeder Grenze zwischen zwei Monitoren. Die Linien sind schmale, randlose Fenster,
/// die immer oben liegen, Mausklicks durchlassen und nicht in Taskleiste/Alt+Tab erscheinen. Ist ein Vollbildprogramm
/// aktiv, werden Linien an dessen Monitor ausgeblendet.
/// </summary>
internal sealed class MonitorSeparatorOverlay : IDisposable
{
    private readonly MonitorManager _monitors;
    private readonly Func<AppSettings> _getSettings;
    private readonly List<SeparatorLineForm> _lines = [];
    private readonly System.Windows.Forms.Timer _watchTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _rebuildTimer = new() { Interval = 2500 };
    private SynchronizationContext? _uiContext;
    private bool _disposed;

    public MonitorSeparatorOverlay(MonitorManager monitors, Func<AppSettings> getSettings)
    {
        _monitors = monitors;
        _getSettings = getSettings;
        _watchTimer.Tick += (_, _) => SafeRun(Watch);
        _rebuildTimer.Tick += (_, _) =>
        {
            _rebuildTimer.Stop();
            SafeRun(Rebuild);
        };
    }

    public void Start()
    {
        _uiContext = SynchronizationContext.Current;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Rebuild();
    }

    /// <summary>Nach dem Speichern der Einstellungen aufrufen.</summary>
    public void ApplySettings() => SafeRun(Rebuild);

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        _uiContext?.Post(_ =>
        {
            if (_disposed) return;
            CloseLines();            // während der Umstellung keine Linien an alten Positionen
            _rebuildTimer.Stop();
            _rebuildTimer.Start();   // nach kurzer Pause neu aufbauen
        }, null);

    private void Rebuild()
    {
        CloseLines();
        var s = _getSettings().Separator;
        if (!s.Enabled)
        {
            _watchTimer.Stop();
            return;
        }

        var segments = ComputeSegments(_monitors.GetAll().Select(m => m.Bounds).ToList(), s.Thickness);
        foreach (var segment in segments)
        {
            var line = new SeparatorLineForm(segment, s.GetColor(), s.OpacityPercent / 100.0);
            line.Show();
            _lines.Add(line);
        }

        if (_lines.Count > 0) _watchTimer.Start();
        else _watchTimer.Stop();
        Log.Debug($"Monitortrennlinien: {_lines.Count}");
    }

    /// <summary>
    /// Grenzen zwischen Monitoren: Wo zwei Monitore Kante an Kante liegen, entsteht eine Linie über die gemeinsame Länge.
    /// Die Linie liegt am Rand des linken bzw. oberen Monitors (überdeckt also nur dessen letzte Pixelspalte/-zeile).
    /// </summary>
    public static List<Rectangle> ComputeSegments(IReadOnlyList<Rectangle> monitors, int thickness)
    {
        var result = new List<Rectangle>();
        foreach (var a in monitors)
        {
            foreach (var b in monitors)
            {
                if (a == b) continue;

                if (a.Right == b.Left) // nebeneinander
                {
                    int top = Math.Max(a.Top, b.Top), bottom = Math.Min(a.Bottom, b.Bottom);
                    if (bottom > top) result.Add(new Rectangle(a.Right - thickness, top, thickness, bottom - top));
                }
                if (a.Bottom == b.Top) // übereinander
                {
                    int left = Math.Max(a.Left, b.Left), right = Math.Min(a.Right, b.Right);
                    if (right > left) result.Add(new Rectangle(left, a.Bottom - thickness, right - left, thickness));
                }
            }
        }
        return result.Distinct().ToList();
    }

    /// <summary>Linien oben halten und bei Vollbildprogrammen am betroffenen Monitor ausblenden.</summary>
    private void Watch()
    {
        Rectangle? fullScreenMonitor = null;
        IntPtr fg = GetForegroundWindow();
        if (fg != IntPtr.Zero && _lines.All(l => l.Handle != fg) && _monitors.FromWindow(fg) is { } mon)
        {
            var rect = WindowInspector.GetWindowRectangle(fg);
            string cls = GetClassName(fg);
            bool isShell = cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
            if (!isShell && rect.Contains(mon.Bounds)) fullScreenMonitor = mon.Bounds;
        }

        foreach (var line in _lines)
        {
            bool hide = fullScreenMonitor is { } fs && fs.IntersectsWith(line.Segment);
            if (hide && line.Visible) line.Hide();
            else if (!hide)
            {
                if (!line.Visible) line.Show();
                line.KeepOnTop();
            }
        }
    }

    private void CloseLines()
    {
        foreach (var line in _lines)
        {
            line.Close();
            line.Dispose();
        }
        _lines.Clear();
    }

    private static void SafeRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error("Fehler bei der Monitortrennlinie", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _watchTimer.Dispose();
        _rebuildTimer.Dispose();
        CloseLines();
    }
}

/// <summary>
/// Ein schmales, immer oben liegendes Popup-Fenster, das Mausklicks durchlässt. Bewusst kein WinForms-Formular:
/// WinForms erzwingt eine Mindestbreite von rund 20 px, ein natives Popup darf 1 px schmal sein.
/// </summary>
internal sealed class SeparatorLineForm : NativeWindow, IDisposable
{
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_ERASEBKGND = 0x0014;
    private const int WM_PAINT = 0x000F;
    private const int HTTRANSPARENT = -1;
    private const uint LWA_ALPHA = 0x2;
    private const int SW_HIDE = 0;
    private const int SW_SHOWNA = 8;
    private static readonly IntPtr HwndTopmost = new(-1);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private readonly Color _color;

    public Rectangle Segment { get; }

    public bool Visible { get; private set; }

    public SeparatorLineForm(Rectangle segment, Color color, double opacity)
    {
        Segment = segment;
        _color = color;
        CreateHandle(new CreateParams
        {
            Caption = "LSM Display Resizer – Monitortrennlinie",
            Style = WS_POPUP,
            ExStyle = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST,
            X = segment.X,
            Y = segment.Y,
            Width = segment.Width,
            Height = segment.Height,
        });
        SetLayeredWindowAttributes(Handle, 0, (byte)Math.Round(Math.Clamp(opacity, 0.1, 1.0) * 255), LWA_ALPHA);
    }

    /// <summary>Anzeigen ohne Aktivieren, exakt auf der Linie und ganz oben.</summary>
    public void Show()
    {
        ShowWindow(Handle, SW_SHOWNA);
        SetWindowPos(Handle, HwndTopmost, Segment.X, Segment.Y, Segment.Width, Segment.Height, SWP_NOACTIVATE);
        Visible = true;
    }

    public void Hide()
    {
        ShowWindow(Handle, SW_HIDE);
        Visible = false;
    }

    public void KeepOnTop() => SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

    public void Close() => Hide();

    public void Dispose()
    {
        if (Handle != IntPtr.Zero) DestroyHandle();
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_NCHITTEST:
                m.Result = new IntPtr(HTTRANSPARENT); // Klicks gehen durch
                return;
            case WM_ERASEBKGND:
                using (var g = Graphics.FromHdc(m.WParam)) g.Clear(_color);
                m.Result = new IntPtr(1);
                return;
            case WM_PAINT:
                using (var g = Graphics.FromHwnd(Handle)) g.Clear(_color);
                base.WndProc(ref m); // validiert den Bereich
                return;
        }
        base.WndProc(ref m);
    }
}
