using LSMDisplayResizer.Settings;
using LSMDisplayResizer.Wallpaper;

namespace LSMDisplayResizer.Tests;

public class WallpaperRendererTests
{
    /// <summary>Quellbild 400 × 100: Rotkanal = x * 255 / 399 (horizontaler Verlauf).</summary>
    private static Bitmap Gradient()
    {
        var bmp = new Bitmap(400, 100);
        for (int x = 0; x < 400; x++)
        {
            var c = Color.FromArgb((int)Math.Round(x * 255.0 / 399), 0, 0);
            for (int y = 0; y < 100; y++) bmp.SetPixel(x, y, c);
        }
        return bmp;
    }

    private static int SourceXFromRed(int red) => (int)Math.Round(red * 399.0 / 255);

    [Fact]
    public void Pixels_ComposedImageMatchesSource()
    {
        using var source = Gradient();
        var a = new MonitorLayoutInput("A", "A", new Rectangle(0, 0, 200, 100), 100, 50, 0);
        var b = new MonitorLayoutInput("B", "B", new Rectangle(200, 0, 200, 100), 100, 50, 0);
        var layout = WallpaperLayout.Compute([a, b], source.Size, WallpaperMode.Pixels, 0, WallpaperAlignment.Center);

        using var composed = WallpaperRenderer.Compose(source, source.Size, layout);

        Assert.Equal(new Size(400, 100), composed.Size);
        foreach (int x in new[] { 10, 199, 200, 390 })
            Assert.InRange(SourceXFromRed(composed.GetPixel(x, 50).R), x - 2, x + 2);
    }

    [Fact]
    public void Physical_DifferentPixelDensity_IsSeamless()
    {
        // A: 200 px auf 100 mm, B: 100 px auf 100 mm (halbe Pixeldichte), gleiche Höhe in mm
        using var source = Gradient();
        var a = new MonitorLayoutInput("A", "A", new Rectangle(0, 0, 200, 100), 100, 50, 0);
        var b = new MonitorLayoutInput("B", "B", new Rectangle(200, 0, 100, 50), 100, 50, 0);
        var layout = WallpaperLayout.Compute([a, b], source.Size, WallpaperMode.Physical, 0, WallpaperAlignment.Top);

        using var composed = WallpaperRenderer.Compose(source, source.Size, layout);

        // Rechter Rand von A und linker Rand von B zeigen benachbarte Bildstellen (Mitte des Quellbilds)
        int leftOfSeam = SourceXFromRed(composed.GetPixel(199, 25).R);
        int rightOfSeam = SourceXFromRed(composed.GetPixel(200, 25).R);
        Assert.InRange(leftOfSeam, 195, 202);
        Assert.InRange(rightOfSeam, 199, 206);

        // B zeigt die zweite Bildhälfte auf halb so vielen Pixeln: rechter Rand ≈ Quellbild-Ende
        Assert.InRange(SourceXFromRed(composed.GetPixel(299, 25).R), 392, 399);
    }
}
