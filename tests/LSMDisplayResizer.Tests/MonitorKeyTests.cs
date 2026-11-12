using LSMDisplayResizer.Monitors;

namespace LSMDisplayResizer.Tests;

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
