using System.Runtime.InteropServices;

namespace LSMDisplayResizer.Interop;

/// <summary>Position des Hintergrundbilds (DESKTOP_WALLPAPER_POSITION).</summary>
internal enum DesktopWallpaperPosition
{
    Center = 0,
    Tile = 1,
    Stretch = 2,
    Fit = 3,
    Fill = 4,
    Span = 5,
}

/// <summary>
/// IDesktopWallpaper (shobjidl_core.h, ab Windows 8) – dieselbe Schnittstelle, die die Windows-Einstellungen nutzen.
/// Anders als SystemParametersInfo schaltet sie über <see cref="Enable"/> auch von „Volltonfarbe“ auf „Bild“ um.
/// Die Reihenfolge der Methoden muss der vtable entsprechen.
/// </summary>
[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetMonitorDevicePathAt(uint monitorIndex);

    uint GetMonitorDevicePathCount();

    RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId);

    void SetBackgroundColor(uint color);

    uint GetBackgroundColor();

    void SetPosition(DesktopWallpaperPosition position);

    DesktopWallpaperPosition GetPosition();

    void SetSlideshow(IntPtr items);

    IntPtr GetSlideshow();

    void SetSlideshowOptions(uint options, uint slideshowTick);

    void GetSlideshowOptions(out uint options, out uint slideshowTick);

    void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, int direction);

    uint GetStatus();

    /// <summary>true = Hintergrundbild anzeigen, false = Volltonfarbe.</summary>
    void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
}

[ComImport]
[Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
internal class DesktopWallpaperClass
{
}
