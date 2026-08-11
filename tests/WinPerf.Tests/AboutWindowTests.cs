namespace WinPerf.Tests;

public sealed class AboutWindowTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    [Fact]
    public void AboutWindow_ShowsSponsorProAndUpdateIntegration()
    {
        var xaml = File.ReadAllText(Path.Combine(AppDirectory, "AboutWindow.xaml"));

        Assert.Contains("Title=\"About WinPerf\"", xaml);
        Assert.Contains("x:Name=\"VersionText\"", xaml);
        Assert.Contains("Sponsor Pro / Updates", xaml);
        Assert.Contains("Update core", xaml);
        Assert.Contains("Client, manifest validation and installer contract are loaded", xaml);
        Assert.Contains("sponsor-pro / WinPerf.zip", xaml);
        Assert.Contains("x:Name=\"CheckUpdatesButton\"", xaml);
        Assert.Contains("x:Name=\"SponsorLoginButton\"", xaml);
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
