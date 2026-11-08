using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using LSMDisplayResizer.Interop;
using static LSMDisplayResizer.Interop.NativeMethods;

namespace LSMDisplayResizer.Tracking;

/// <summary>Grund, warum ein Fenster nicht angepasst wird.</summary>
internal enum SkipReason
{
    None,
    Invalid,
    NotTopLevel,
    Child,
    Invisible,
    Minimized,
    Cloaked,
    ToolWindow,
    ShellWindow,
    NoFrame,
    NotResizable,
    OwnProcess,
    Excluded,
    Maximized,
    Arranged,
    Hung,
    FullScreen,
}

/// <summary>Momentaufnahme eines Fensters.</summary>
internal sealed record WindowSnapshot(
    IntPtr Handle,
    uint ProcessId,
    string ProcessName,
    string Title,
    string ClassName,
    Rectangle WindowRect,
    Rectangle FrameRect)
{
    public string Describe() =>
        string.IsNullOrWhiteSpace(Title) ? ProcessName : $"{ProcessName} \"{Shorten(Title, 60)}\"";

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}

/// <summary>Prüft Fenster gegen die Ausschlussregeln und liest ihre Eigenschaften.</summary>
internal static class WindowInspector
{
    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    /// <summary>Fensterklassen der Shell, die nie angefasst werden.</summary>
    private static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
        "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow", "TaskListThumbnailWnd",
        "ForegroundStaging", "MultitaskingViewFrame", "TopLevelWindowForOverflowXamlIsland",
        "Shell_InputSwitchTopLevelWindow", "#32768", "tooltips_class32",
    };

    public static string SkipReasonText(SkipReason reason) => reason switch
    {
        SkipReason.Invalid => "Fenster existiert nicht mehr",
        SkipReason.NotTopLevel or SkipReason.Child => "Kind-Fenster",
        SkipReason.Invisible => "unsichtbar",
        SkipReason.Minimized => "minimiert",
        SkipReason.Cloaked => "verborgen (cloaked)",
        SkipReason.ToolWindow => "Tool-Fenster",
        SkipReason.ShellWindow => "Windows-Shell",
        SkipReason.NoFrame => "ohne Rahmen",
        SkipReason.NotResizable => "Größe nicht veränderbar",
        SkipReason.OwnProcess => "eigenes Fenster",
        SkipReason.Excluded => "auf der Ausschlussliste",
        SkipReason.Maximized => "maximiert",
        SkipReason.Arranged => "per Snap angeordnet",
        SkipReason.Hung => "Programm reagiert nicht",
        SkipReason.FullScreen => "Vollbild",
        _ => "-",
    };

    /// <summary>
    /// Prüft, ob das Fenster für eine Anpassung in Frage kommt. Vollbild wird hier nicht geprüft,
    /// weil dafür der Monitor nötig ist (siehe <see cref="IsFullScreen"/>).
    /// </summary>
    public static SkipReason Check(IntPtr hwnd, ISet<string>? excludedProcesses, out WindowSnapshot? snapshot)
    {
        snapshot = null;
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return SkipReason.Invalid;
        if (GetAncestor(hwnd, GA_ROOT) != hwnd) return SkipReason.NotTopLevel;

        long style = GetWindowLong(hwnd, GWL_STYLE);
        long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        if ((style & WS_CHILD) != 0) return SkipReason.Child;
        if (!IsWindowVisible(hwnd)) return SkipReason.Invisible;
        if (IsIconic(hwnd)) return SkipReason.Minimized;
        if (IsCloaked(hwnd)) return SkipReason.Cloaked;
        if ((exStyle & WS_EX_TOOLWINDOW) != 0) return SkipReason.ToolWindow;

        string className = NativeMethods.GetClassName(hwnd);
        if (ShellClasses.Contains(className)) return SkipReason.ShellWindow;

        bool hasCaption = (style & WS_CAPTION) == WS_CAPTION;
        bool hasThickFrame = (style & WS_THICKFRAME) != 0;
        if (!hasCaption && !hasThickFrame) return SkipReason.NoFrame;
        if (!hasThickFrame) return SkipReason.NotResizable;

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == OwnProcessId) return SkipReason.OwnProcess;

        string processName = GetProcessName(pid);
        if (excludedProcesses != null && excludedProcesses.Contains(processName)) return SkipReason.Excluded;

        snapshot = new WindowSnapshot(hwnd, pid, processName, NativeMethods.GetWindowText(hwnd), className,
            GetWindowRectangle(hwnd), GetFrameBounds(hwnd));

        if (IsZoomed(hwnd)) return SkipReason.Maximized;
        if (IsWindowArranged(hwnd)) return SkipReason.Arranged;
        if (IsHungAppWindow(hwnd)) return SkipReason.Hung;
        return SkipReason.None;
    }

    /// <summary>Vollbild: Das Fenster deckt den gesamten Monitor (nicht nur den Arbeitsbereich) ab.</summary>
    public static bool IsFullScreen(WindowSnapshot window, Rectangle monitorBounds) =>
        window.WindowRect.Contains(monitorBounds) || window.FrameRect.Contains(monitorBounds);

    public static bool IsCloaked(IntPtr hwnd) =>
        DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    public static Rectangle GetWindowRectangle(IntPtr hwnd) =>
        GetWindowRect(hwnd, out RECT r) ? r.ToRectangle() : Rectangle.Empty;

    /// <summary>
    /// Sichtbarer Fensterrahmen ohne die unsichtbaren Ränder (DWMWA_EXTENDED_FRAME_BOUNDS).
    /// Fällt auf GetWindowRect zurück, wenn DWM keine Angabe liefert.
    /// </summary>
    public static Rectangle GetFrameBounds(IntPtr hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT r, Marshal.SizeOf<RECT>()) == 0)
        {
            var rect = r.ToRectangle();
            if (rect.Width > 0 && rect.Height > 0) return rect;
        }
        return GetWindowRectangle(hwnd);
    }

    /// <summary>Dateiname der EXE in Kleinbuchstaben, z. B. "chrome.exe". Funktioniert auch für Admin-Prozesse.</summary>
    public static string GetProcessName(uint pid)
    {
        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h != IntPtr.Zero)
        {
            try
            {
                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                if (QueryFullProcessImageName(h, 0, sb, ref size))
                    return Path.GetFileName(sb.ToString()).ToLowerInvariant();
            }
            finally
            {
                CloseHandle(h);
            }
        }

        try
        {
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return (p.ProcessName + ".exe").ToLowerInvariant();
        }
        catch
        {
            return $"pid{pid}";
        }
    }

    /// <summary>Alle Fenster, die angepasst werden könnten (für Auswahllisten in der Oberfläche).</summary>
    public static List<WindowSnapshot> EnumerateCandidateWindows()
    {
        var result = new List<WindowSnapshot>();
        foreach (var h in EnumerateTopLevelWindows())
        {
            var reason = Check(h, null, out var snap);
            if (snap != null && reason is SkipReason.None or SkipReason.Maximized or SkipReason.Arranged)
                result.Add(snap);
        }
        return result;
    }

    /// <summary>Alle Top-Level-Fenster-Handles in Z-Reihenfolge.</summary>
    public static List<IntPtr> EnumerateTopLevelWindows()
    {
        var handles = new List<IntPtr>();
        EnumWindows((h, _) =>
        {
            handles.Add(h);
            return true;
        }, IntPtr.Zero);
        return handles;
    }
}
