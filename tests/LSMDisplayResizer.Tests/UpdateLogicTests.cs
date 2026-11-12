using LSMDisplayResizer.Updates;

namespace LSMDisplayResizer.Tests;

public class UpdateLogicTests
{
    [Theory]
    [InlineData("v1.0.9", "1.0.8", true)]
    [InlineData("1.0.10", "1.0.9", true)]   // numerisch, nicht als Text vergleichen
    [InlineData("v1.1.0", "1.0.99", true)]
    [InlineData("v1.0.8", "1.0.8", false)]
    [InlineData("v1.0.7", "1.0.8", false)]
    [InlineData("v1.0", "1.0.0", false)]
    [InlineData("v2.0.0-beta", "1.9.0", true)]
    [InlineData("kaputt", "1.0.8", false)]
    [InlineData("v1.0.9", "", false)]
    public void IsNewer_ComparesVersionsNumerically(string candidate, string current, bool expected) =>
        Assert.Equal(expected, UpdateLogic.IsNewer(candidate, current));

    private const string ReleaseJson = """
        {
          "tag_name": "v1.0.9",
          "html_url": "https://github.com/nes19233/LSMDisplayResizer/releases/tag/v1.0.9",
          "draft": false,
          "prerelease": false,
          "assets": [
            { "name": "LSMDisplayResizer-1.0.9-win-x64-de.msi", "browser_download_url": "https://example.test/de.msi" },
            { "name": "LSMDisplayResizer-1.0.9-win-x64-en.msi", "browser_download_url": "https://example.test/en.msi" },
            { "name": "SHA256SUMS.txt", "browser_download_url": "https://example.test/sums" }
          ]
        }
        """;

    [Fact]
    public void ParseRelease_ReadsVersionPageAndAssets()
    {
        var release = UpdateLogic.ParseRelease(ReleaseJson);
        Assert.NotNull(release);
        Assert.Equal("1.0.9", release.Version);
        Assert.EndsWith("/v1.0.9", release.PageUrl);
        Assert.Equal("https://example.test/en.msi", release.Assets[UpdateLogic.MsiAssetName("1.0.9", english: true)]);
        Assert.Equal("https://example.test/de.msi", release.Assets[UpdateLogic.MsiAssetName("1.0.9", english: false)]);
        Assert.True(release.Assets.ContainsKey(UpdateLogic.ChecksumAssetName));
    }

    [Fact]
    public void ParseRelease_IgnoresPrereleases()
    {
        Assert.Null(UpdateLogic.ParseRelease(ReleaseJson.Replace("\"prerelease\": false", "\"prerelease\": true")));
    }

    [Fact]
    public void FindChecksum_MatchesExactFileName()
    {
        string a = new('a', 64), b = new('B', 64), c = new('c', 64);
        string sums = $"{a}  LSMDisplayResizer-1.0.9-win-x64-de.msi\n{b} *LSMDisplayResizer-1.0.9-win-x64-en.msi\r\n{c}  LSMDisplayResizer-1.0.9-win-x64.exe\n";

        Assert.Equal(a, UpdateLogic.FindChecksum(sums, "LSMDisplayResizer-1.0.9-win-x64-de.msi"));
        Assert.Equal(b.ToLowerInvariant(), UpdateLogic.FindChecksum(sums, "LSMDisplayResizer-1.0.9-win-x64-en.msi"));
        Assert.Null(UpdateLogic.FindChecksum(sums, "LSMDisplayResizer-1.0.9-win-x64.msi"));
    }

    [Fact]
    public void DetectInstallKind_FromExePath()
    {
        const string local = @"C:\Users\Test\AppData\Local";
        string[] pf = [@"C:\Program Files", @"C:\Program Files (x86)"];

        Assert.Equal(InstallKind.PerUser,
            UpdateLogic.DetectInstallKind(@"C:\Users\Test\AppData\Local\Programs\LSM Display Resizer\LSMDisplayResizer.exe", local, pf));
        Assert.Equal(InstallKind.PerMachine,
            UpdateLogic.DetectInstallKind(@"C:\Program Files\LSM Display Resizer\LSMDisplayResizer.exe", local, pf));
        Assert.Equal(InstallKind.Portable,
            UpdateLogic.DetectInstallKind(@"C:\Users\Test\Downloads\LSMDisplayResizer-1.0.9-win-x64.exe", local, pf));
    }
}
