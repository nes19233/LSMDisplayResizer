using LSMDisplayResizer.Overlay;
using LSMDisplayResizer.Settings;

namespace LSMDisplayResizer.Tests;

public class SeparatorTests
{
    [Fact]
    public void SideBySide_OneVerticalLineOnLeftMonitorEdge()
    {
        var left = new Rectangle(0, 0, 1720, 1440);
        var right = new Rectangle(1720, 0, 1720, 1440);

        var lines = MonitorSeparatorOverlay.ComputeSegments([left, right], thickness: 1);

        Assert.Equal([new Rectangle(1719, 0, 1, 1440)], lines);
    }

    [Fact]
    public void DifferentHeights_LineCoversOnlySharedPart()
    {
        var laptop = new Rectangle(0, 360, 1920, 1080);
        var big = new Rectangle(1920, 0, 2560, 1440);

        var lines = MonitorSeparatorOverlay.ComputeSegments([laptop, big], thickness: 2);

        Assert.Equal([new Rectangle(1918, 360, 2, 1080)], lines);
    }

    [Fact]
    public void Stacked_HorizontalLine()
    {
        var top = new Rectangle(0, -1080, 1920, 1080);
        var bottom = new Rectangle(0, 0, 1920, 1080);

        var lines = MonitorSeparatorOverlay.ComputeSegments([top, bottom], thickness: 1);

        Assert.Equal([new Rectangle(0, -1, 1920, 1)], lines);
    }

    [Fact]
    public void SingleMonitor_OrNotTouching_NoLines()
    {
        Assert.Empty(MonitorSeparatorOverlay.ComputeSegments([new Rectangle(0, 0, 1920, 1080)], 1));
        Assert.Empty(MonitorSeparatorOverlay.ComputeSegments([new Rectangle(0, 0, 1920, 1080), new Rectangle(2000, 0, 1920, 1080)], 1));
    }

    [Fact]
    public void ThreeMonitors_TwoLines()
    {
        var lines = MonitorSeparatorOverlay.ComputeSegments(
            [new Rectangle(0, 0, 1920, 1080), new Rectangle(1920, 0, 1920, 1080), new Rectangle(3840, 0, 1920, 1080)], 1);
        Assert.Equal(2, lines.Count);
    }

    [Theory]
    [InlineData("#FF8000", 255, 128, 0)]
    [InlineData("00ff00", 0, 255, 0)]
    [InlineData("kaputt", 128, 128, 128)]
    public void ColorParsing(string hex, int r, int g, int b)
    {
        var c = SeparatorSettings.ParseColor(hex);
        Assert.Equal((r, g, b), (c.R, c.G, c.B));
    }
}
