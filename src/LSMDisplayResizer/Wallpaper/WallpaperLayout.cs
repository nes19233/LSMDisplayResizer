using System.Drawing;
using LSMDisplayResizer.Settings;

namespace LSMDisplayResizer.Wallpaper;

/// <summary>Ein Monitor als Eingabe für die Aufteilung.</summary>
/// <param name="PixelBounds">Monitorbereich in echten Pixeln (virtueller Desktop).</param>
/// <param name="WidthMm">Breite der sichtbaren Bildfläche in mm (in der aktuellen Ausrichtung).</param>
/// <param name="HeightMm">Höhe der sichtbaren Bildfläche in mm.</param>
/// <param name="OffsetYMm">Vertikaler Versatz in mm (positiv = tiefer).</param>
internal sealed record MonitorLayoutInput(
    string Id,
    string Name,
    Rectangle PixelBounds,
    double WidthMm,
    double HeightMm,
    double OffsetYMm);

/// <summary>Ergebnis der Aufteilung: welcher Bildausschnitt auf welchem Monitor landet.</summary>
internal sealed record MonitorSlice(MonitorLayoutInput Monitor, RectangleF SourceRect)
{
    /// <summary>Quellpixel pro Bildschirmpixel. Unter 1 wird das Bild hochskaliert (unscharf).</summary>
    public double SourcePixelsPerScreenPixel => SourceRect.Width / Monitor.PixelBounds.Width;
}

internal sealed record WallpaperLayoutResult(
    IReadOnlyList<MonitorSlice> Slices,
    Rectangle VirtualBounds,
    WallpaperMode EffectiveMode,
    string? Warning)
{
    /// <summary>Stärkste Hochskalierung über alle Monitore (1 = keine).</summary>
    public double MaxUpscale => Slices.Count == 0 ? 1 : Math.Max(1, Slices.Max(s => 1 / s.SourcePixelsPerScreenPixel));

    /// <summary>Bildgröße, ab der nirgends hochskaliert werden muss.</summary>
    public Size RecommendedSourceSize(Size imageSize) => new(
        (int)Math.Ceiling(imageSize.Width * MaxUpscale),
        (int)Math.Ceiling(imageSize.Height * MaxUpscale));
}

/// <summary>
/// Reine Berechnung (ohne GDI+): Monitore werden auf eine gemeinsame Fläche gelegt – nach Pixeln oder nach
/// physischer Größe – und das Bild wird so zugeschnitten, dass es diese Fläche füllt.
/// </summary>
internal static class WallpaperLayout
{
    public static WallpaperLayoutResult Compute(IReadOnlyList<MonitorLayoutInput> monitors, Size imageSize,
        WallpaperMode mode, double gapMm, WallpaperAlignment alignment)
    {
        if (monitors.Count == 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
            return new WallpaperLayoutResult([], Rectangle.Empty, mode, "Keine Monitore oder kein gültiges Bild.");

        Rectangle virtualBounds = monitors.Select(m => m.PixelBounds).Aggregate(Rectangle.Union);
        string? warning = null;

        if (mode == WallpaperMode.Physical && !IsSideBySide(monitors))
        {
            mode = WallpaperMode.Pixels;
            warning = "Die Aufteilung nach physischer Größe unterstützt nur nebeneinander stehende Monitore. " +
                      "Es wird nach Pixeln aufgeteilt.";
        }

        List<RectangleF> canvas = mode == WallpaperMode.Physical
            ? PhysicalRects(monitors, gapMm, alignment)
            : monitors.Select(m => (RectangleF)m.PixelBounds).ToList();

        RectangleF canvasBounds = canvas.Aggregate(RectangleF.Union);
        RectangleF crop = CoverCrop(imageSize, canvasBounds.Size);
        double scale = crop.Width / canvasBounds.Width; // Quellpixel je Flächeneinheit

        var slices = new List<MonitorSlice>(monitors.Count);
        for (int i = 0; i < monitors.Count; i++)
        {
            RectangleF r = canvas[i];
            var src = new RectangleF(
                (float)(crop.X + (r.X - canvasBounds.X) * scale),
                (float)(crop.Y + (r.Y - canvasBounds.Y) * scale),
                (float)(r.Width * scale),
                (float)(r.Height * scale));
            slices.Add(new MonitorSlice(monitors[i], src));
        }

        return new WallpaperLayoutResult(slices, virtualBounds, mode, warning);
    }

    /// <summary>
    /// Physische Anordnung: Monitore von links nach rechts (Reihenfolge wie in Windows), dazwischen der Abstand,
    /// vertikal nach Ausrichtung plus individuellem Versatz. Einheit: mm.
    /// </summary>
    public static List<RectangleF> PhysicalRects(IReadOnlyList<MonitorLayoutInput> monitors, double gapMm, WallpaperAlignment alignment)
    {
        var result = new RectangleF[monitors.Count];
        double x = 0;
        foreach (int i in Enumerable.Range(0, monitors.Count).OrderBy(i => monitors[i].PixelBounds.Left))
        {
            var m = monitors[i];
            double y = alignment switch
            {
                WallpaperAlignment.Top => 0,
                WallpaperAlignment.Bottom => -m.HeightMm,
                _ => -m.HeightMm / 2,
            } + m.OffsetYMm;
            result[i] = new RectangleF((float)x, (float)y, (float)m.WidthMm, (float)m.HeightMm);
            x += m.WidthMm + gapMm;
        }
        return [.. result];
    }

    /// <summary>
    /// Nebeneinander: In der Windows-Anordnung (von links nach rechts sortiert) überlappen sich
    /// benachbarte Monitore horizontal höchstens geringfügig.
    /// </summary>
    public static bool IsSideBySide(IReadOnlyList<MonitorLayoutInput> monitors)
    {
        var sorted = monitors.Select(m => m.PixelBounds).OrderBy(b => b.Left).ToList();
        for (int i = 1; i < sorted.Count; i++)
        {
            int overlap = sorted[i - 1].Right - sorted[i].Left;
            int narrower = Math.Min(sorted[i - 1].Width, sorted[i].Width);
            if (overlap > narrower / 4) return false;
        }
        return true;
    }

    /// <summary>Größter mittiger Bildausschnitt mit dem Seitenverhältnis der Zielfläche („Füllen“).</summary>
    public static RectangleF CoverCrop(Size image, SizeF target)
    {
        double imageAspect = (double)image.Width / image.Height;
        double targetAspect = target.Width / target.Height;
        if (Math.Abs(imageAspect / targetAspect - 1) < 1e-5) return new RectangleF(0, 0, image.Width, image.Height);
        if (imageAspect > targetAspect)
        {
            double w = image.Height * targetAspect;
            return new RectangleF((float)((image.Width - w) / 2), 0, (float)w, image.Height);
        }
        double h = image.Width / targetAspect;
        return new RectangleF(0, (float)((image.Height - h) / 2), image.Width, (float)h);
    }
}
