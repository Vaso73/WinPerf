namespace WinPerf.Tests;

public sealed class AboutWindowTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    [Fact]
    public void AboutWindow_UsesTermHubStyleProductAccountAndFooterLayout()
    {
        var xaml = File.ReadAllText(Path.Combine(AppDirectory, "AboutWindow.xaml"));

        Assert.Contains("Title=\"About WinPerf\"", xaml);
        Assert.Contains("Width=\"620\"", xaml);
        Assert.Contains("Height=\"500\"", xaml);
        Assert.Contains("ResizeMode=\"NoResize\"", xaml);
        Assert.Contains("x:Name=\"VersionText\"", xaml);
        Assert.Contains("Sponsor Pro planned · Free edition will be reduced", xaml);
        Assert.Contains("Single portable WinPerf.exe", xaml);
        Assert.Contains("x:Name=\"AccountPanel\"", xaml);
        Assert.Contains("x:Name=\"AccountTitleText\"", xaml);
        Assert.Contains("x:Name=\"AccountStatusText\"", xaml);
        Assert.Contains("x:Name=\"AccountAvatar\"", xaml);
        Assert.Contains("x:Name=\"AccountGitHubIcon\"", xaml);
        Assert.Contains("WinPerfGitHubMarkGeometry", xaml);
        Assert.Contains("x:Name=\"SponsorProAccountButton\"", xaml);
        Assert.Contains("Content=\"Sign in with GitHub\"", xaml);
        Assert.Contains("x:Name=\"CheckForUpdatesButton\"", xaml);
        Assert.Contains("x:Name=\"InstallUpdateButton\"", xaml);
        Assert.Contains("DockPanel.Dock=\"Bottom\"", xaml);
        Assert.Contains("x:Name=\"StatusText\"", xaml);
    }

    [Fact]
    public void AboutWindow_TracksPlacementReceivesVersionAndHandlesSponsorProInline()
    {
        var code = File.ReadAllText(Path.Combine(AppDirectory, "AboutWindow.xaml.cs"));

        Assert.Contains("public AboutWindow(string versionText)", code);
        Assert.Contains("WindowPlacementStore.Track(this, \"AboutWindow\");", code);
        Assert.Contains("VersionText.Text = versionText;", code);
        Assert.Contains("EditionText.Text = WinPerfProductEdition.EditionName;", code);
        Assert.Contains("Sponsor Pro updates are available only in WinPerf Sponsor Pro.", code);
        Assert.Contains("SponsorProSessionStore _sessionStore = new();", code);
        Assert.Contains("RefreshSponsorProStatus();", code);
        Assert.DoesNotContain("new SponsorProUpdatesWindow", code);
        Assert.Contains("GitHubAvatarService", code);
        Assert.Contains("LoadAccountAvatarAsync", code);
        Assert.Contains("ResetAccountAvatar", code);
        Assert.Contains("RequestDownloadTicketAsync", code);
        Assert.Contains("DownloadAndStageAsync", code);
        Assert.Contains("WinPerfUpdateHelper.Launch", code);
        Assert.Contains("CheckForUpdatesButton_Click", code);
        Assert.Contains("InstallUpdateButton_Click", code);
        Assert.Contains("SponsorProAccountButton_Click", code);
        Assert.Contains("SignOutOfSponsorPro", code);
    }
}
