using LSMDisplayResizer.Tracking;

namespace LSMDisplayResizer.Tests;

public class WindowInspectorTests
{
    // Hälfte eines Ultrawide im Bild-neben-Bild-Modus, Taskleiste unten (48 px)
    private static readonly Rectangle Work = new(0, 0, 1720, 1392);

    [Fact]
    public void SnappedLeftHalf_IsFullHeight()
    {
        Assert.True(WindowInspector.SpansFullHeight(new Rectangle(0, 0, 860, 1392), Work));
    }

    [Fact]
    public void SmallDeviation_IsTolerated()
    {
        // DWM-Rahmen können je nach DPI um wenige Pixel abweichen
        Assert.True(WindowInspector.SpansFullHeight(new Rectangle(10, 1, 900, 1389), Work));
    }

    [Fact]
    public void NormalWindow_IsNotFullHeight()
    {
        Assert.False(WindowInspector.SpansFullHeight(new Rectangle(100, 100, 800, 500), Work));
    }

    [Fact]
    public void AlmostFullHeight_ButClearlyShorter_IsNotFullHeight()
    {
        Assert.False(WindowInspector.SpansFullHeight(new Rectangle(100, 0, 800, 1300), Work));
        Assert.False(WindowInspector.SpansFullHeight(new Rectangle(100, 60, 800, 1332), Work));
    }

    [Fact]
    public void WorkAreaOffset_IsRespected()
    {
        // Zweiter Monitor rechts, Taskleiste oben
        var work = new Rectangle(1720, 48, 1720, 1392);
        Assert.True(WindowInspector.SpansFullHeight(new Rectangle(1720, 48, 860, 1392), work));
        Assert.False(WindowInspector.SpansFullHeight(new Rectangle(1720, 0, 860, 1392), work));
    }
}
