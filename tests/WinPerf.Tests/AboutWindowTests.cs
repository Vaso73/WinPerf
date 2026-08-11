namespace WinPerf.Tests;

public sealed class AboutWindowTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    [Fact]
    public void AboutWindow_ShowsCompactProductInformation()
    {
        var xaml = File.ReadAllText(Path.Combine(AppDirectory, "AboutWindow.xaml"));

        Assert.Contains("Title=\"About WinPerf\"", xaml);
        Assert.Contains("x:Name=\"VersionText\"", xaml);
        Assert.Contains("Sponsor Pro planned; free edition will be reduced", xaml);
        Assert.Contains("Single portable WinPerf.exe", xaml);
        Assert.DoesNotContain("x:Name=\"CheckUpdatesButton\"", xaml);
        Assert.DoesNotContain("x:Name=\"SponsorProAccountButton\"", xaml);
    }

    [Fact]
    public void AboutWindow_TracksPlacementAndReceivesRuntimeVersion()
    {
        var code = File.ReadAllText(Path.Combine(AppDirectory, "AboutWindow.xaml.cs"));

        Assert.Contains("public AboutWindow(string versionText)", code);
        Assert.Contains("WindowPlacementStore.Track(this, \"AboutWindow\");", code);
        Assert.Contains("VersionText.Text = versionText;", code);
    }
}
