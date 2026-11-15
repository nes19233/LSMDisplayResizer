using LSMDisplayResizer.Resizing;
using LSMDisplayResizer.Settings;

namespace LSMDisplayResizer.Tests;

public class SizeCalculatorTests
{
    // Hälfte eines Dell U3425WE (Arbeitsbereich 1720×1392, 100 %) und Notebook-OLED (2880×1716, 175 %)
    private static readonly Size DellHalf = new(1720, 1392);
    private static readonly Size Oled = new(2880, 1716);

    [Fact]
    public void ScaleByWidth_KeepsWidthShareAndAspectRatio_WinBoxExample()
    {
        var result = SizeCalculator.ScaleByWidth(new Size(1535, 842), DellHalf, Oled);

        Assert.Equal(new Size(2570, 1410), result);   // 89,2 % der Breite, Höhe folgt der Form 1535 : 842
    }

    [Fact]
    public void ScaleByWidth_ChangeOnOtherMonitorIsKept()
    {
        // WinBox auf dem Notebook von Hand auf 2383×1334 gebracht, zurück auf die Dell-Hälfte: von dieser Größe aus.
        var result = SizeCalculator.ScaleByWidth(new Size(2383, 1334), Oled, DellHalf);

        Assert.Equal(1423, result.Width);
        Assert.InRange(result.Height, 796, 798);
    }

    [Theory]
    [InlineData(1535, 842)]
    [InlineData(1200, 600)]
    [InlineData(400, 300)]
    public void ScaleByWidth_RoundTripReturnsOriginal(int w, int h)
    {
        var there = SizeCalculator.ScaleByWidth(new Size(w, h), DellHalf, Oled);
        var back = SizeCalculator.ScaleByWidth(there, Oled, DellHalf, (double)w / h);

        Assert.InRange(back.Width, w - 1, w + 1);
        Assert.InRange(back.Height, h - 1, h + 1);
    }

    [Fact]
    public void ScaleByWidth_TooTall_OnlyHeightIsCut()
    {
        // Browser 1200×1300 auf der Dell-Hälfte: auf dem Notebook wären es 2009×2177 – höher als der Bildschirm.
        var result = SizeCalculator.ScaleByWidth(new Size(1200, 1300), DellHalf, Oled);

        Assert.Equal(new Size(2009, 1716), result);
        Assert.True(SizeCalculator.AspectDiffers(result, 1200.0 / 1300));
    }

    [Fact]
    public void ScaleByWidth_AfterCut_RememberedAspectRestoresOriginalShape()
    {
        double original = 1200.0 / 1300;
        var cut = SizeCalculator.ScaleByWidth(new Size(1200, 1300), DellHalf, Oled);

        var withoutMemory = SizeCalculator.ScaleByWidth(cut, Oled, DellHalf);
        var withMemory = SizeCalculator.ScaleByWidth(cut, Oled, DellHalf, original);

        Assert.InRange(withoutMemory.Height, 1020, 1030);   // gekürzte Form
        Assert.Equal(1200, withMemory.Width);
        Assert.InRange(withMemory.Height, 1299, 1301);      // ursprüngliche Form
    }

    [Fact]
    public void ScaleByWidth_IgnoresDisplayScaling()
    {
        // Gleiche Anteile, egal wie die Monitore skaliert sind: 50 % Breite bleibt 50 % Breite.
        var result = SizeCalculator.ScaleByWidth(new Size(960, 540), new Size(1920, 1040), new Size(2880, 1716));
        Assert.Equal(new Size(1440, 810), result);
    }

    [Fact]
    public void Percent_KeepAspectRatio_UsesWidthShare()
    {
        var oldFrame = new Rectangle(192, 104, 1152, 520);   // 60 % der Breite von A
        var result = SizeCalculator.Percent(WorkA, oldFrame, WorkB, oldFrame, keepCurrentPosition: false,
            heightScaling: 2.0 / 3, keepAspectRatio: true);

        Assert.Equal(new Size(2304, 1040), result.Size);     // 60 % von 3840, Höhe aus 1152 : 520
    }

    // Monitor A: Full HD mit Taskleiste unten (Arbeitsbereich 1920×1040)
    private static readonly Rectangle WorkA = new(0, 0, 1920, 1040);

    // Monitor B: 4K rechts daneben (Arbeitsbereich 3840×2100)
    private static readonly Rectangle WorkB = new(1920, 0, 3840, 2100);

    [Fact]
    public void Percent_TransfersRelativeSize()
    {
        // 60 % × 50 % auf A
        var oldFrame = new Rectangle(192, 104, 1152, 520);
        var current = new Rectangle(2200, 100, 1152, 520);

        var result = SizeCalculator.Percent(WorkA, oldFrame, WorkB, current, keepCurrentPosition: true);

        Assert.Equal(new Size(2304, 1050), result.Size); // 60 % von 3840, 50 % von 2100
        Assert.Equal(new Point(2200, 100), result.Location);
    }

    [Fact]
    public void Percent_TransfersRelativePosition_WhenNotKeepingPosition()
    {
        var oldFrame = new Rectangle(192, 104, 1152, 520); // 10 % / 10 % vom Rand
        var current = new Rectangle(3000, 500, 1152, 520);

        var result = SizeCalculator.Percent(WorkA, oldFrame, WorkB, current, keepCurrentPosition: false);

        Assert.Equal(new Point(1920 + 384, 210), result.Location);
        Assert.Equal(new Size(2304, 1050), result.Size);
    }

    [Fact]
    public void Percent_FromLargeToSmallMonitor_ShrinksWindow()
    {
        var oldFrame = new Rectangle(1920 + 384, 210, 2304, 1050);
        var current = new Rectangle(100, 100, 2304, 1050); // ragt über Monitor A hinaus

        var result = SizeCalculator.Percent(WorkB, oldFrame, WorkA, current, keepCurrentPosition: true);

        Assert.Equal(new Size(1152, 520), result.Size);
        Assert.True(WorkA.Contains(result));
    }

    [Fact]
    public void HeightScaling_TwoThirds_GrowsLessThanProportional()
    {
        // A → B: Arbeitshöhe 1040 → 2100 (Faktor 2,02); 520 px hoch
        var oldFrame = new Rectangle(192, 104, 1152, 520);

        var full = SizeCalculator.Percent(WorkA, oldFrame, WorkB, oldFrame, keepCurrentPosition: false, heightScaling: 1.0);
        var damped = SizeCalculator.Percent(WorkA, oldFrame, WorkB, oldFrame, keepCurrentPosition: false, heightScaling: 2.0 / 3);

        Assert.Equal(1050, full.Height);
        Assert.InRange(damped.Height, 820, 840);   // 520 × 2,02^(2/3) ≈ 831
        Assert.Equal(full.Width, damped.Width);    // Breite immer voll proportional
    }

    [Fact]
    public void HeightScaling_Zero_KeepsPixelHeight()
    {
        var oldFrame = new Rectangle(192, 104, 1152, 520);
        var result = SizeCalculator.Percent(WorkA, oldFrame, WorkB, oldFrame, keepCurrentPosition: false, heightScaling: 0);
        Assert.Equal(520, result.Height);
    }

    [Theory]
    [InlineData(520)]
    [InlineData(333)]
    [InlineData(901)]
    public void HeightScaling_RoundTrip_ReturnsToOriginalHeight(int height)
    {
        // Hin und zurück darf ein Fenster nicht wachsen oder schrumpfen
        int there = SizeCalculator.ScaleHeight(height, 1040, 2100, 2.0 / 3);
        int back = SizeCalculator.ScaleHeight(there, 2100, 1040, 2.0 / 3);
        Assert.InRange(back, height - 1, height + 1);
    }

    // Dell-Hälfte 1720 × 1392 bei 100 %, Laptop 2880 × 1704 bei 200 % (sichtbar nur 1440 × 852)
    private static readonly Rectangle WorkDell = new(0, 0, 1720, 1392);
    private static readonly Rectangle WorkLaptop = new(0, 0, 2880, 1704);

    [Fact]
    public void HeightScaling_DifferentDpi_VisiblySmallerScreen_GetsLargerShare()
    {
        // Fenster 829 px hoch = 60 % der Dell-Höhe. Der Laptop hat zwar mehr Pixel, ist aber sichtbar kleiner:
        // Das Fenster muss dort einen GRÖSSEREN Anteil der Höhe bekommen (vorher fälschlich kleiner).
        var oldFrame = new Rectangle(100, 100, 1370, 829);

        var result = SizeCalculator.Percent(WorkDell, oldFrame, WorkLaptop, oldFrame, keepCurrentPosition: false,
            heightScaling: 2.0 / 3, oldScale: 1.0, newScale: 2.0);

        double oldShare = 829.0 / 1392;
        double newShare = (double)result.Height / 1704;
        Assert.True(newShare > oldShare, $"Anteil vorher {oldShare:P1}, nachher {newShare:P1}");
        Assert.InRange(result.Height, 1180, 1210);  // ≈ 1194 px
    }

    [Fact]
    public void HeightScaling_FullFactor_IsProportional_RegardlessOfDpi()
    {
        var oldFrame = new Rectangle(100, 100, 1370, 829);
        var result = SizeCalculator.Percent(WorkDell, oldFrame, WorkLaptop, oldFrame, false, 1.0, 1.0, 2.0);
        Assert.Equal((int)Math.Round(829.0 * 1704 / 1392), result.Height);
    }

    [Fact]
    public void HeightScaling_Zero_KeepsVisibleHeight_AcrossDpi()
    {
        // 0 % = sichtbar gleich hoch: bei doppelter Skalierung doppelt so viele Pixel
        Assert.Equal(1000, SizeCalculator.ScaleHeight(500, 1392, 1704, 0, oldScale: 1.0, newScale: 2.0));
    }

    [Theory]
    [InlineData(829)]
    [InlineData(400)]
    public void HeightScaling_RoundTrip_AcrossDpi(int height)
    {
        int there = SizeCalculator.ScaleHeight(height, 1392, 1704, 2.0 / 3, 1.0, 2.0);
        int back = SizeCalculator.ScaleHeight(there, 1704, 1392, 2.0 / 3, 2.0, 1.0);
        Assert.InRange(back, height - 1, height + 1);
    }

    [Fact]
    public void Result_IsAlwaysInsideWorkArea()
    {
        var oldFrame = new Rectangle(1500, 900, 400, 300);  // unten rechts auf A
        var current = new Rectangle(5600, 2000, 400, 300);  // ragt unten rechts aus B heraus

        var result = SizeCalculator.Percent(WorkA, oldFrame, WorkB, current, keepCurrentPosition: true);

        Assert.True(WorkB.Contains(result), $"{result} liegt nicht in {WorkB}");
    }

    [Fact]
    public void ClampToWorkArea_ShrinksOversizedWindow()
    {
        var result = SizeCalculator.ClampToWorkArea(new Rectangle(-50, -50, 5000, 3000), WorkA);
        Assert.Equal(WorkA, result);
    }

    [Fact]
    public void ClampToWorkArea_EnforcesMinimumSize()
    {
        var result = SizeCalculator.ClampToWorkArea(new Rectangle(10, 10, 20, 20), WorkA);
        Assert.Equal(SizeCalculator.MinimumSize, result.Width);
        Assert.Equal(SizeCalculator.MinimumSize, result.Height);
    }

    [Fact]
    public void Rule_Percent_UsesWorkAreaOfTarget()
    {
        var rule = new FixedRule { ProcessName = "code.exe", Width = 50, Height = 80, Unit = SizeUnit.Percent };
        var current = new Rectangle(2000, 50, 800, 600);

        var result = SizeCalculator.FromRule(rule, null, null, WorkB, current, keepCurrentPosition: true);

        Assert.Equal(new Size(1920, 1680), result.Size);
        Assert.Equal(new Point(2000, 50), result.Location);
    }

    [Fact]
    public void Rule_Pixels_AreUsedAsIs_AndClamped()
    {
        var rule = new FixedRule { ProcessName = "code.exe", Width = 1400, Height = 2000, Unit = SizeUnit.Pixels };
        var current = new Rectangle(300, 200, 800, 600);

        var result = SizeCalculator.FromRule(rule, null, null, WorkA, current, keepCurrentPosition: true);

        Assert.Equal(1400, result.Width);
        Assert.Equal(WorkA.Height, result.Height); // 2000 passt nicht → auf 1040 begrenzt
        Assert.True(WorkA.Contains(result));
    }

    [Fact]
    public void Rule_TransfersPosition_WhenOldStateKnown()
    {
        var rule = new FixedRule { ProcessName = "x.exe", Width = 1000, Height = 500, Unit = SizeUnit.Pixels };
        var oldFrame = new Rectangle(960, 520, 500, 300); // Mitte von A

        var result = SizeCalculator.FromRule(rule, WorkA, oldFrame, WorkB, new Rectangle(2500, 10, 500, 300), false);

        Assert.Equal(new Point(1920 + 1920, 1050), result.Location);
    }
}
