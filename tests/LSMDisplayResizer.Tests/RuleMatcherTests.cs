using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Resizing;
using LSMDisplayResizer.Settings;

namespace LSMDisplayResizer.Tests;

public class RuleMatcherTests
{
    private const string DellPort1 = @"\\?\DISPLAY#DEL4127#5&1a2b3c&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string DellPort2 = @"\\?\DISPLAY#DEL4127#5&9f8e7d&0&UID4354#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string Lg = @"\\?\DISPLAY#GSM5B7F#5&1a2b3c&0&UID4355#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    private static FixedRule Rule(string process, string monitor, double w = 50) =>
        new() { ProcessName = process, MonitorId = monitor, Width = w, Height = 50 };

    [Fact]
    public void ExtractModelKey_ReadsManufacturerAndProduct()
    {
        Assert.Equal("DEL4127", MonitorDescriptor.ExtractModelKey(DellPort1));
        Assert.Equal(string.Empty, MonitorDescriptor.ExtractModelKey(@"\\.\DISPLAY1"));
        Assert.Equal(string.Empty, MonitorDescriptor.ExtractModelKey(null));
    }

    [Fact]
    public void ExactMonitor_BeatsAnyMonitor()
    {
        var any = Rule("chrome.exe", FixedRule.Any, 10);
        var exact = Rule("chrome.exe", DellPort1, 20);

        var found = RuleMatcher.Find([any, exact], "chrome.exe", DellPort1, "DEL4127");

        Assert.Same(exact, found);
    }

    [Fact]
    public void SameModelOnOtherPort_MatchesAsFallback()
    {
        var rule = Rule("chrome.exe", DellPort1);

        Assert.Same(rule, RuleMatcher.Find([rule], "chrome.exe", DellPort2, "DEL4127"));
        Assert.Null(RuleMatcher.Find([rule], "chrome.exe", Lg, "GSM5B7F"));
    }

    [Fact]
    public void SpecificProcess_BeatsWildcardProcess()
    {
        var wildcard = Rule(FixedRule.Any, Lg, 10);
        var specific = Rule("code.exe", Lg, 20);

        Assert.Same(specific, RuleMatcher.Find([wildcard, specific], "code.exe", Lg, "GSM5B7F"));
        Assert.Same(wildcard, RuleMatcher.Find([wildcard, specific], "notepad.exe", Lg, "GSM5B7F"));
    }

    [Fact]
    public void ProcessName_IsCaseInsensitive()
    {
        var rule = Rule("chrome.exe", FixedRule.Any);
        Assert.Same(rule, RuleMatcher.Find([rule], "Chrome.EXE", Lg, "GSM5B7F"));
    }

    [Fact]
    public void NoMatch_ReturnsNull()
    {
        Assert.Null(RuleMatcher.Find([Rule("chrome.exe", Lg)], "code.exe", Lg, "GSM5B7F"));
    }

    [Theory]
    [InlineData("Game", "game.exe")]
    [InlineData("  GAME.EXE ", "game.exe")]
    [InlineData(@"C:\Spiele\Game.exe", "game.exe")]
    [InlineData("*", "*")]
    [InlineData("", "")]
    public void NormalizeProcessName(string input, string expected)
    {
        Assert.Equal(expected, AppSettings.NormalizeProcessName(input));
    }
}
