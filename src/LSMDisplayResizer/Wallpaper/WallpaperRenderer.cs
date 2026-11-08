using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LSMDisplayResizer.Wallpaper;

/// <summary>Setzt das Gesamtbild (Größe des virtuellen Desktops) aus den Bildausschnitten zusammen.</summary>
internal static class WallpaperRenderer
{
    public static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".gif"];

    /// <summary>Lädt ein Bild vollständig in den Speicher (Datei bleibt nicht gesperrt) und richtet es nach EXIF aus.</summary>
    public static Bitmap LoadImage(string path)
    {
        using var stream = File.OpenRead(path);
        using var image = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
        var bitmap = new Bitmap(image);
        ApplyExifOrientation(image, bitmap);
        return bitmap;
    }

    /// <summary>Verkleinerte Kopie für die Vorschau (längste Seite höchstens <paramref name="maxDimension"/>).</summary>
    public static Bitmap CreateScaledCopy(Image source, int maxDimension)
    {
        double scale = Math.Min(1.0, (double)maxDimension / Math.Max(source.Width, source.Height));
        int w = Math.Max(1, (int)Math.Round(source.Width * scale));
        int h = Math.Max(1, (int)Math.Round(source.Height * scale));
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(source, new Rectangle(0, 0, w, h));
        return bmp;
    }

    /// <summary>
    /// Erstellt das Gesamtbild. <paramref name="layoutImageSize"/> ist die Bildgröße, mit der das Layout berechnet
    /// wurde – <paramref name="source"/> darf eine verkleinerte Kopie davon sein (Vorschau).
    /// </summary>
    /// <param name="outputScale">1 = echte Pixel des virtuellen Desktops, kleiner = Vorschau.</param>
    public static Bitmap Compose(Image source, Size layoutImageSize, WallpaperLayoutResult layout, double outputScale = 1.0)
    {
        Rectangle vb = layout.VirtualBounds;
        int width = Math.Max(1, (int)Math.Round(vb.Width * outputScale));
        int height = Math.Max(1, (int)Math.Round(vb.Height * outputScale));

        var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Black);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;

        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY); // keine dunklen Ränder an den Ausschnittkanten

        double fx = (double)source.Width / layoutImageSize.Width;
        double fy = (double)source.Height / layoutImageSize.Height;

        foreach (var slice in layout.Slices)
        {
            Rectangle pb = slice.Monitor.PixelBounds;
            var dest = Rectangle.FromLTRB(
                (int)Math.Round((pb.Left - vb.Left) * outputScale),
                (int)Math.Round((pb.Top - vb.Top) * outputScale),
                (int)Math.Round((pb.Right - vb.Left) * outputScale),
                (int)Math.Round((pb.Bottom - vb.Top) * outputScale));

            RectangleF s = slice.SourceRect;
            g.DrawImage(source, dest,
                (float)(s.X * fx), (float)(s.Y * fy), (float)(s.Width * fx), (float)(s.Height * fy),
                GraphicsUnit.Pixel, attributes);
        }
        return bmp;
    }

    private static void ApplyExifOrientation(Image original, Bitmap target)
    {
        const int OrientationId = 0x0112;
        if (!original.PropertyIdList.Contains(OrientationId)) return;
        var prop = original.GetPropertyItem(OrientationId);
        if (prop?.Value == null || prop.Value.Length < 2) return;

        RotateFlipType? flip = BitConverter.ToUInt16(prop.Value, 0) switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => null,
        };
        if (flip is { } f) target.RotateFlip(f);
    }
}
