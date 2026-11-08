using System.Drawing;
using System.Runtime.InteropServices;
using LSMDisplayResizer.Tracking;
using static LSMDisplayResizer.Interop.NativeMethods;

namespace LSMDisplayResizer.Resizing;

internal enum MoveOutcome
{
    /// <summary>Fenster hat die Zielgröße angenommen.</summary>
    Ok,

    /// <summary>Fenster wurde verändert, hat aber eigene Grenzen (Mindest-/Höchstgröße) durchgesetzt.</summary>
    Partial,

    /// <summary>Zugriff verweigert – typischerweise ein Programm mit Administratorrechten.</summary>
    AccessDenied,

    /// <summary>Sonstiger Fehler.</summary>
    Failed,
}

internal readonly record struct MoveResult(MoveOutcome Outcome, Rectangle ActualFrame, int Win32Error);

/// <summary>Setzt den sichtbaren Rahmen eines Fensters per SetWindowPos.</summary>
internal static class WindowMover
{
    /// <summary>
    /// Verschiebt das Fenster so, dass sein <b>sichtbarer</b> Rahmen <paramref name="targetFrame"/> entspricht.
    /// Die unsichtbaren Ränder (Differenz GetWindowRect ↔ DWMWA_EXTENDED_FRAME_BOUNDS) werden aufgeschlagen.
    /// </summary>
    public static MoveResult Move(IntPtr hwnd, Rectangle targetFrame)
    {
        Rectangle window = WindowInspector.GetWindowRectangle(hwnd);
        Rectangle frame = WindowInspector.GetFrameBounds(hwnd);

        // Unsichtbare Ränder (unter Windows 10/11 meist ~7 px links/rechts/unten, skaliert mit DPI).
        int left = frame.Left - window.Left;
        int top = frame.Top - window.Top;
        int right = window.Right - frame.Right;
        int bottom = window.Bottom - frame.Bottom;
        if (left < 0 || top < 0 || right < 0 || bottom < 0 || left + right > 100 || top + bottom > 100)
        {
            left = top = right = bottom = 0; // unplausibel → ohne Korrektur
        }

        int x = targetFrame.Left - left;
        int y = targetFrame.Top - top;
        int cx = targetFrame.Width + left + right;
        int cy = targetFrame.Height + top + bottom;

        bool ok = SetWindowPos(hwnd, IntPtr.Zero, x, y, cx, cy, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        int error = ok ? 0 : Marshal.GetLastWin32Error();
        Rectangle actual = WindowInspector.GetFrameBounds(hwnd);

        if (!ok)
        {
            return new MoveResult(error == ERROR_ACCESS_DENIED ? MoveOutcome.AccessDenied : MoveOutcome.Failed, actual, error);
        }

        // Kein Effekt, obwohl eine Änderung nötig war → vermutlich blockiert (UIPI/Admin-Fenster).
        if (actual == frame && frame != targetFrame && IsClearlyDifferent(frame, targetFrame))
        {
            return new MoveResult(MoveOutcome.AccessDenied, actual, 0);
        }

        return new MoveResult(IsClearlyDifferent(actual, targetFrame) ? MoveOutcome.Partial : MoveOutcome.Ok, actual, 0);
    }

    private static bool IsClearlyDifferent(Rectangle a, Rectangle b, int tolerance = 2) =>
        Math.Abs(a.Left - b.Left) > tolerance || Math.Abs(a.Top - b.Top) > tolerance ||
        Math.Abs(a.Width - b.Width) > tolerance || Math.Abs(a.Height - b.Height) > tolerance;
}
