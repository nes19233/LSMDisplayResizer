using LSMDisplayResizer.Resizing;
using LSMDisplayResizer.Settings;

namespace LSMDisplayResizer.Tests;

public class SizeCalculatorTests
{
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
