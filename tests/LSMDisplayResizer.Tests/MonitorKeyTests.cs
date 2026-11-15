using LSMDisplayResizer.Monitors;

namespace LSMDisplayResizer.Tests;

public class ReopenSizeTests
{
    [Theory]
    [InlineData(1600, 1200, 1600, 1200, 1.0, true)]   // alte Pixelgröße wiederhergestellt
    [InlineData(1605, 1195, 1600, 1200, 1.0, true)]   // kleine Abweichung (Rahmen, Rundung)
    [InlineData(3200, 2400, 1600, 1200, 2.0, true)]   // logische Größe, auf 200 % umgerechnet
    [InlineData(1000, 700, 1600, 1200, 1.0, false)]   // Programm hat selbst eine passende Größe gewählt
    [InlineData(800, 600, 1600, 1200, 2.0, false)]
    public void SizeMatches(int w, int h, int rw, int rh, double ratio, bool expected) =>
        Assert.Equal(expected, LSMDisplayResizer.Tracking.WindowTracker.SizeMatches(
            new System.Drawing.Size(w, h), new System.Drawing.Size(rw, rh), ratio));

    [Theory]
    // WinBox: auf dem Notebook (175 %) 2383×1334 geschlossen, auf der Dell-Hälfte (1720×1392) mit der alten
    // Pixelgröße geöffnet – Breite auf den Bildschirm begrenzt (1722 inkl. Rahmen), Höhe fast gleich.
    [InlineData(1722, 1310, 2383, 1334, 1720, 1392, true)]
    [InlineData(1720, 1392, 2383, 1500, 1720, 1392, true)]   // beide Seiten begrenzt
    [InlineData(1722, 900, 2383, 1334, 1720, 1392, false)]   // Höhe passt nicht
    [InlineData(1722, 1310, 1500, 1310, 1720, 1392, false)]  // füllt die Breite, war aber schmaler
    public void SizeMatches_LimitedToWorkArea(int w, int h, int rw, int rh, int ww, int wh, bool expected) =>
        Assert.Equal(expected, LSMDisplayResizer.Tracking.WindowTracker.SizeMatches(
            new System.Drawing.Size(w, h), new System.Drawing.Size(rw, rh), 1.0 / 1.75,
            new System.Drawing.Size(ww, wh)));
}

public class MonitorKeyTests
{
    private static MonitorDescriptor Monitor(bool placeholder) =>
        new(IntPtr.Zero, @"\\.\DISPLAY1", @"\\.\DISPLAY1", "", "DELL U3824DW",
            new System.Drawing.Rectangle(0, 0, 1024, 768), new System.Drawing.Rectangle(0, 0, 1024, 728), true,
            IsPlaceholder: placeholder);

    [Fact]
    public void OnlyPlaceholders_OnlyWhenNoRealMonitor()
    {
        Assert.True(MonitorDescriptor.OnlyPlaceholders([Monitor(true)]));
        Assert.False(MonitorDescriptor.OnlyPlaceholders([Monitor(true), Monitor(false)]));
        Assert.False(MonitorDescriptor.OnlyPlaceholders([Monitor(false)]));
        Assert.False(MonitorDescriptor.OnlyPlaceholders([]));
    }

    [Fact]
    public void SameMonitor_DifferentDeviceIds_GetSameKey()
    {
        // Derselbe Dell nach erneutem Andocken: andere Produkt-/Anschluss-ID, gleiche Seriennummer
        var before = MonitorManager.ComputeStableKeys([
            (@"\\.\DISPLAY1", 0, 0, "DEL:BLS34Z3", "DELA244", @"\\?\DISPLAY#DELA244#4&1f2e3873&0&UID8261#{x}"),
            (@"\\.\DISPLAY2", 1720, 0, "DEL:BLS34Z3", "DELA242", @"\\?\DISPLAY#DELA242#4&1f2e3873&0&UID12613#{x}"),
        ]);
        var after = MonitorManager.ComputeStableKeys([
            (@"\\.\DISPLAY3", 0, 0, "DEL:BLS34Z3", "DELA20A", @"\\?\DISPLAY#DELA20A#4&1f2e3873&0&UID41031#{x}"),
            (@"\\.\DISPLAY4", 1720, 0, "DEL:BLS34Z3", "DELA20C", @"\\?\DISPLAY#DELA20C#4&1f2e3873&0&UID45383#{x}"),
        ]);

        Assert.Equal(before[@"\\.\DISPLAY1"], after[@"\\.\DISPLAY3"]); // linke Hälfte
        Assert.Equal(before[@"\\.\DISPLAY2"], after[@"\\.\DISPLAY4"]); // rechte Hälfte
        Assert.NotEqual(before[@"\\.\DISPLAY1"], before[@"\\.\DISPLAY2"]);
    }

    [Fact]
    public void TwoIdenticalMonitors_AreDistinguishedBySerial()
    {
        var keys = MonitorManager.ComputeStableKeys([
            ("A", 0, 0, "DEL:AAA111", "DEL4127", "id-a"),
            ("B", 1920, 0, "DEL:BBB222", "DEL4127", "id-b"),
        ]);
        Assert.Equal("S:DEL:AAA111", keys["A"]);
        Assert.Equal("S:DEL:BBB222", keys["B"]);
    }

    [Fact]
    public void WithoutSerial_FallsBackToModel_ThenDeviceId()
    {
        var keys = MonitorManager.ComputeStableKeys([
            ("A", 0, 0, null, "LEN414D", "id-a"),
            ("B", 1920, 0, null, "", "id-b"),
        ]);
        Assert.Equal("M:LEN414D", keys["A"]);
        Assert.Equal("D:id-b", keys["B"]);
    }
}
