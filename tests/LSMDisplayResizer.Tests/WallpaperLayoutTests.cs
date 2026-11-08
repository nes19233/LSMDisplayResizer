using LSMDisplayResizer.Settings;
using LSMDisplayResizer.Wallpaper;

namespace LSMDisplayResizer.Tests;

public class WallpaperLayoutTests
{
    // 27" 4K links (597 × 336 mm), 24" Full HD rechts (531 × 299 mm)
    private static readonly MonitorLayoutInput Big = new("A", "4K", new Rectangle(0, 0, 3840, 2160), 597, 336, 0);
    private static readonly MonitorLayoutInput Small = new("B", "FHD", new Rectangle(3840, 0, 1920, 1080), 531, 299, 0);

    [Fact]
    public void Pixels_TwoEqualMonitors_SplitImageInHalves()
    {
        var a = new MonitorLayoutInput("A", "A", new Rectangle(0, 0, 1920, 1080), 531, 299, 0);
        var b = new MonitorLayoutInput("B", "B", new Rectangle(1920, 0, 1920, 1080), 531, 299, 0);

        var r = WallpaperLayout.Compute([a, b], new Size(3840, 1080), WallpaperMode.Pixels, 0, WallpaperAlignment.Center);

        Assert.Equal(new RectangleF(0, 0, 1920, 1080), r.Slices[0].SourceRect);
        Assert.Equal(new RectangleF(1920, 0, 1920, 1080), r.Slices[1].SourceRect);
        Assert.Equal(new Rectangle(0, 0, 3840, 1080), r.VirtualBounds);
        Assert.Equal(1, r.MaxUpscale);
    }

    [Fact]
    public void CoverCrop_CutsWiderImageCentered()
    {
        var crop = WallpaperLayout.CoverCrop(new Size(4000, 1000), new SizeF(2000, 1000));
        Assert.Equal(new RectangleF(1000, 0, 2000, 1000), crop);
    }

    [Fact]
    public void CoverCrop_CutsTallerImageCentered()
    {
        var crop = WallpaperLayout.CoverCrop(new Size(2000, 2000), new SizeF(2000, 1000));
        Assert.Equal(new RectangleF(0, 500, 2000, 1000), crop);
    }

    [Fact]
    public void Physical_KeepsSameScaleInMillimetres()
    {
        var r = WallpaperLayout.Compute([Big, Small], new Size(11280, 3360), WallpaperMode.Physical, 0, WallpaperAlignment.Center);

        // Gleicher Maßstab: Quellpixel pro mm muss auf beiden Monitoren gleich sein
        double perMmBig = r.Slices[0].SourceRect.Width / Big.WidthMm;
        double perMmSmall = r.Slices[1].SourceRect.Width / Small.WidthMm;
        Assert.Equal(perMmBig, perMmSmall, 3);

        // Nahtlos: rechter Rand links = linker Rand rechts (ohne Abstand)
        Assert.Equal(r.Slices[0].SourceRect.Right, r.Slices[1].SourceRect.Left, 2);
    }

    [Fact]
    public void Physical_GapSkipsHiddenPartOfImage()
    {
        var r = WallpaperLayout.Compute([Big, Small], new Size(11280, 3360), WallpaperMode.Physical, 20, WallpaperAlignment.Center);
        double perMm = r.Slices[0].SourceRect.Width / Big.WidthMm;

        Assert.Equal(20 * perMm, r.Slices[1].SourceRect.Left - r.Slices[0].SourceRect.Right, 1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 18.5)]   // (336 - 299) / 2
    [InlineData(2, 37)]     // 336 - 299
    public void Physical_VerticalAlignment(int alignment, double expectedOffsetMm)
    {
        var rects = WallpaperLayout.PhysicalRects([Big, Small], 0, (WallpaperAlignment)alignment);
        Assert.Equal(expectedOffsetMm, rects[1].Top - rects[0].Top, 3);
    }

    [Fact]
    public void Physical_OffsetMovesMonitorDown()
    {
        var lowered = Small with { OffsetYMm = 10 };
        var rects = WallpaperLayout.PhysicalRects([Big, lowered], 0, WallpaperAlignment.Top);
        Assert.Equal(10, rects[1].Top - rects[0].Top, 3);
    }

    [Fact]
    public void Physical_UsesWindowsOrderLeftToRight()
    {
        // In der Liste zuerst der rechte Monitor
        var right = Small with { PixelBounds = new Rectangle(0, 0, 1920, 1080) };
        var left = Big with { PixelBounds = new Rectangle(-3840, 0, 3840, 2160) };

        var rects = WallpaperLayout.PhysicalRects([right, left], 0, WallpaperAlignment.Top);

        Assert.True(rects[1].Left < rects[0].Left);
    }

    [Fact]
    public void Physical_StackedMonitors_FallBackToPixels()
    {
        var top = new MonitorLayoutInput("A", "A", new Rectangle(0, -1080, 1920, 1080), 531, 299, 0);
        var bottom = new MonitorLayoutInput("B", "B", new Rectangle(0, 0, 1920, 1080), 531, 299, 0);

        var r = WallpaperLayout.Compute([top, bottom], new Size(1920, 2160), WallpaperMode.Physical, 0, WallpaperAlignment.Center);

        Assert.Equal(WallpaperMode.Pixels, r.EffectiveMode);
        Assert.NotNull(r.Warning);
    }

    [Fact]
    public void SmallImage_ReportsUpscaleAndRecommendedSize()
    {
        var a = new MonitorLayoutInput("A", "A", new Rectangle(0, 0, 1920, 1080), 531, 299, 0);
        var b = new MonitorLayoutInput("B", "B", new Rectangle(1920, 0, 1920, 1080), 531, 299, 0);

        var r = WallpaperLayout.Compute([a, b], new Size(1920, 540), WallpaperMode.Pixels, 0, WallpaperAlignment.Center);

        Assert.Equal(2, r.MaxUpscale, 3);
        Assert.Equal(new Size(3840, 1080), r.RecommendedSourceSize(new Size(1920, 540)));
    }

    [Fact]
    public void Edid_ParsesDetailedTimingSize()
    {
        var edid = new byte[128];
        edid[1] = edid[2] = edid[3] = edid[4] = edid[5] = edid[6] = 0xFF;
        edid[21] = 60; edid[22] = 34;           // cm (Basisblock)
        edid[54] = 0x01;                         // Pixeltakt != 0
        edid[66] = 0x55; edid[67] = 0x50;        // 597 = 0x255, 336 = 0x150
        edid[68] = 0x21;                         // obere Nibbles: 2 (Breite), 1 (Höhe)

        Assert.Equal((597d, 336d), EdidReader.Parse(edid));
    }

    [Fact]
    public void Edid_FallsBackToCentimetres_AndRejectsInvalid()
    {
        var edid = new byte[128];
        edid[1] = edid[2] = edid[3] = edid[4] = edid[5] = edid[6] = 0xFF;
        edid[21] = 53; edid[22] = 30;
        Assert.Equal((530d, 300d), EdidReader.Parse(edid));

        edid[21] = 0; edid[22] = 0;  // Projektor: keine Größe
        Assert.Null(EdidReader.Parse(edid));
        Assert.Null(EdidReader.Parse(new byte[10]));
    }
}
