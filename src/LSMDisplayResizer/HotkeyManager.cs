using static LSMDisplayResizer.Interop.NativeMethods;

namespace LSMDisplayResizer;

/// <summary>Globales Tastenkürzel über RegisterHotKey und ein unsichtbares Nachrichtenfenster.</summary>
internal sealed class HotkeyManager : NativeWindow, IDisposable
{
    private const int HotkeyId = 0x4C53; // "LS"
    private static readonly IntPtr HwndMessage = new(-3);

    private bool _registered;

    public event Action? Pressed;

    public HotkeyManager()
    {
        CreateHandle(new CreateParams { Caption = "LSMDisplayResizer.Hotkey", Parent = HwndMessage });
    }

    public bool Register(Keys keys)
    {
        Unregister();
        if (!IsValid(keys)) return false;

        uint modifiers = MOD_NOREPEAT;
        if ((keys & Keys.Control) != 0) modifiers |= MOD_CONTROL;
        if ((keys & Keys.Alt) != 0) modifiers |= MOD_ALT;
        if ((keys & Keys.Shift) != 0) modifiers |= MOD_SHIFT;

        _registered = RegisterHotKey(Handle, HotkeyId, modifiers, (uint)(keys & Keys.KeyCode));
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered) return;
        UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam == HotkeyId)
        {
            try
            {
                Pressed?.Invoke();
            }
            catch (Exception ex)
            {
                Logging.Log.Error("Fehler beim Ausführen des Tastenkürzels", ex);
            }
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>Gültig: eine "echte" Taste plus Strg/Alt/Umschalt, oder F1–F24 allein.</summary>
    public static bool IsValid(Keys keys)
    {
        Keys code = keys & Keys.KeyCode;
        if (code is Keys.None or Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin
            or Keys.LControlKey or Keys.RControlKey or Keys.LShiftKey or Keys.RShiftKey or Keys.LMenu or Keys.RMenu)
            return false;

        bool hasModifier = (keys & (Keys.Control | Keys.Alt | Keys.Shift)) != 0;
        bool isFunctionKey = code is >= Keys.F1 and <= Keys.F24;
        return hasModifier || isFunctionKey;
    }

    /// <summary>z. B. "Strg + Alt + R".</summary>
    public static string Format(Keys keys)
    {
        var parts = new List<string>();
        if ((keys & Keys.Control) != 0) parts.Add("Strg");
        if ((keys & Keys.Alt) != 0) parts.Add("Alt");
        if ((keys & Keys.Shift) != 0) parts.Add("Umschalt");

        Keys code = keys & Keys.KeyCode;
        if (code != Keys.None && IsValid(keys | Keys.Control))
        {
            parts.Add(code switch
            {
                >= Keys.D0 and <= Keys.D9 => ((char)('0' + (code - Keys.D0))).ToString(),
                >= Keys.NumPad0 and <= Keys.NumPad9 => "Num " + (code - Keys.NumPad0),
                Keys.Left => "Pfeil links",
                Keys.Right => "Pfeil rechts",
                Keys.Up => "Pfeil oben",
                Keys.Down => "Pfeil unten",
                Keys.Space => "Leertaste",
                Keys.Return => "Eingabe",
                Keys.Insert => "Einfg",
                Keys.Delete => "Entf",
                Keys.Home => "Pos1",
                Keys.End => "Ende",
                Keys.PageUp => "Bild auf",
                Keys.PageDown => "Bild ab",
                _ => code.ToString(),
            });
        }
        return parts.Count == 0 ? "(keins)" : string.Join(" + ", parts);
    }

    public void Dispose()
    {
        Unregister();
        DestroyHandle();
    }
}
