using System.Drawing;
using LSMDisplayResizer.Settings;

namespace LSMDisplayResizer.Resizing;

/// <summary>
/// Reine Berechnungen (ohne Win32), damit sie testbar sind. Alle Rechtecke sind sichtbare
/// Fensterrahmen bzw. Arbeitsbereiche in echten Pixeln.
/// </summary>
internal static class SizeCalculator
{
    /// <summary>Kleinste Zielgröße, damit ein Fenster nie unbedienbar klein wird.</summary>
    public const int MinimumSize = 120;

    /// <summary>
    /// Prozentual: Größe relativ zum alten Arbeitsbereich auf den neuen Arbeitsbereich übertragen.
    /// </summary>
    /// <param name="oldWork">Arbeitsbereich des alten Monitors.</param>
    /// <param name="oldFrame">Fensterrahmen auf dem alten Monitor.</param>
    /// <param name="newWork">Arbeitsbereich des neuen Monitors.</param>
    /// <param name="currentFrame">Aktueller Fensterrahmen (bereits auf dem neuen Monitor).</param>
    /// <param name="keepCurrentPosition">true: linke obere Ecke von <paramref name="currentFrame"/> beibehalten
    /// (z. B. nach dem Ziehen mit der Maus), false: Position ebenfalls relativ übertragen.</param>
    /// <param name="heightScaling">Wie stark die Höhe mitwächst bzw. -schrumpft: 1 = voll proportional zum Arbeitsbereich,
    /// 0 = sichtbare Höhe bleibt gleich, dazwischen gedämpft (siehe <see cref="ScaleHeight"/>).</param>
    /// <param name="oldScale">Anzeigeskalierung des alten Monitors (1,0 = 100 %).</param>
    /// <param name="newScale">Anzeigeskalierung des neuen Monitors.</param>
    public static Rectangle Percent(Rectangle oldWork, Rectangle oldFrame, Rectangle newWork, Rectangle currentFrame,
        bool keepCurrentPosition, double heightScaling = 1.0, double oldScale = 1.0, double newScale = 1.0)
    {
        if (oldWork.Width <= 0 || oldWork.Height <= 0) return ClampToWorkArea(currentFrame, newWork);

        double relW = (double)oldFrame.Width / oldWork.Width;
        var size = new Size(Round(relW * newWork.Width),
            ScaleHeight(oldFrame.Height, oldWork.Height, newWork.Height, heightScaling, oldScale, newScale));

        Point location = keepCurrentPosition
            ? currentFrame.Location
            : TransferPosition(oldWork, oldFrame, newWork);

        return ClampToWorkArea(new Rectangle(location, size), newWork);
    }

    /// <summary>
    /// Gedämpfte Höhenskalierung, gerechnet in <b>sichtbarer</b> Größe (Pixel ÷ Skalierung): Ein Laptop-Display mit
    /// 2880 × 1800 bei 200 % ist sichtbar kleiner als ein Monitor mit 1440 Pixeln Höhe bei 100 % – obwohl es mehr Pixel hat.
    /// <para>sichtbare Höhe neu = sichtbare Höhe alt × (sichtbare Arbeitshöhe neu / alt) ^ Faktor</para>
    /// Faktor 1 = voll proportional zum Arbeitsbereich, 0 = sichtbare Höhe bleibt gleich, 2/3 = etwa zwei Drittel der
    /// Änderung. Durch den Exponenten landet ein Fenster nach Monitor A → B → A exakt wieder bei seiner Ausgangshöhe.
    /// </summary>
    public static int ScaleHeight(int height, int oldWorkHeight, int newWorkHeight, double factor,
        double oldScale = 1.0, double newScale = 1.0)
    {
        if (oldWorkHeight <= 0 || newWorkHeight <= 0) return height;
        if (oldScale <= 0) oldScale = 1.0;
        if (newScale <= 0) newScale = 1.0;
        factor = Math.Clamp(factor, 0, 1);

        double ratio = (newWorkHeight / newScale) / (oldWorkHeight / oldScale); // Verhältnis der sichtbaren Arbeitshöhen
        return Round(height * (newScale / oldScale) * Math.Pow(ratio, factor));
    }

    /// <summary>Feste Regel: Zielgröße aus der Regel, Position wie im prozentualen Modus.</summary>
    /// <param name="oldWork">Arbeitsbereich des alten Monitors oder null (z. B. bei manueller Auslösung).</param>
    /// <param name="oldFrame">Fensterrahmen auf dem alten Monitor oder null.</param>
    public static Rectangle FromRule(FixedRule rule, Rectangle? oldWork, Rectangle? oldFrame, Rectangle newWork,
        Rectangle currentFrame, bool keepCurrentPosition)
    {
        var size = RuleSize(rule, newWork);

        Point location = !keepCurrentPosition && oldWork is { Width: > 0, Height: > 0 } ow && oldFrame is { } of
            ? TransferPosition(ow, of, newWork)
            : currentFrame.Location;

        return ClampToWorkArea(new Rectangle(location, size), newWork);
    }

    /// <summary>Größe in Pixeln, die eine Regel auf einem Arbeitsbereich ergibt.</summary>
    public static Size RuleSize(FixedRule rule, Rectangle work) => rule.Unit == SizeUnit.Percent
        ? new Size(Round(rule.Width / 100.0 * work.Width), Round(rule.Height / 100.0 * work.Height))
        : new Size(Round(rule.Width), Round(rule.Height));

    /// <summary>Linke obere Ecke relativ zum alten Arbeitsbereich → gleiche relative Lage im neuen.</summary>
    public static Point TransferPosition(Rectangle oldWork, Rectangle oldFrame, Rectangle newWork)
    {
        double relX = (double)(oldFrame.Left - oldWork.Left) / oldWork.Width;
        double relY = (double)(oldFrame.Top - oldWork.Top) / oldWork.Height;
        return new Point(newWork.Left + Round(relX * newWork.Width), newWork.Top + Round(relY * newWork.Height));
    }

    /// <summary>Hält das Rechteck vollständig im Arbeitsbereich (verkleinert es notfalls).</summary>
    public static Rectangle ClampToWorkArea(Rectangle r, Rectangle work)
    {
        int minW = Math.Min(MinimumSize, work.Width);
        int minH = Math.Min(MinimumSize, work.Height);
        int w = Math.Clamp(r.Width, minW, work.Width);
        int h = Math.Clamp(r.Height, minH, work.Height);
        int x = Math.Clamp(r.Left, work.Left, work.Right - w);
        int y = Math.Clamp(r.Top, work.Top, work.Bottom - h);
        return new Rectangle(x, y, w, h);
    }

    private static int Round(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
}
