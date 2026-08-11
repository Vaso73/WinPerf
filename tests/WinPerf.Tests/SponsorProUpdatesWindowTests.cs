namespace WinPerf.Tests;

public sealed class SponsorProUpdatesWindowTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    [Fact]
    public void SponsorProUpdatesWindow_ShowsAccountAndPrivateUpdateChannel()
    {
        var xaml = File.ReadAllText(Path.Combine(AppDirectory, "SponsorProUpdatesWindow.xaml"));
        var theme = File.ReadAllText(Path.Combine(AppDirectory, "ResourceDictionaries", "Theme.xaml"));

        Assert.Contains("Title=\"Sponsor Pro / Updates\"", xaml);
        Assert.Contains("x:Name=\"SponsorAccountCard\"", xaml);
        Assert.Contains("x:Name=\"AccountStatusChip\"", xaml);
        Assert.Contains("Style=\"{StaticResource StatusChip}\"", xaml);
        Assert.Contains("x:Name=\"SponsorProAccountButton\"", xaml);
        Assert.Contains("Content=\"Sign in with GitHub\"", xaml);
        Assert.Contains("x:Name=\"UpdateChannelCard\"", xaml);
        Assert.Contains("x:Key=\"StatusChip\"", theme);
        Assert.Contains("Private Sponsor Pro updater", xaml);
        Assert.Contains("sponsor-pro / WinPerf.zip", xaml);
        Assert.Contains("x:Name=\"CheckUpdatesButton\"", xaml);
        Assert.Contains("x:Name=\"InstallUpdateButton\"", xaml);
        Assert.Contains("Click=\"InstallUpdateButton_Click\"", xaml);
    }

    [Fact]
    public void SponsorProUpdatesWindow_CodeUsesSessionStoreLoginLogoutAndUpdateService()
    {
        var code = File.ReadAllText(Path.Combine(AppDirectory, "SponsorProUpdatesWindow.xaml.cs"));

        Assert.Contains("SponsorProSessionStore _sessionStore = new();", code);
        Assert.Contains("_sessionStore.Load()", code);
        Assert.Contains("_sessionStore.Save(result.Session)", code);
        Assert.Contains("_sessionStore.Clear()", code);
        Assert.Contains("StartLoginAsync", code);
        Assert.Contains("PollLoginAsync", code);
        Assert.Contains("CheckAsync(ParseVersion(_versionText)", code);
        Assert.Contains("RequestDownloadTicketAsync", code);
        Assert.Contains("DownloadAndStageAsync", code);
        Assert.Contains("WinPerfUpdateHelper.Launch", code);
        Assert.Contains("WinPerfProductEdition.SupportsSponsorProUpdates", code);
        Assert.Contains("WinPerf is not enabled on the Sponsor Pro update server yet.", code);
        Assert.DoesNotContain("Installer launcher/startup wiring is the next updater slice.", code);
        Assert.Contains("WindowPlacementStore.Track(this, \"SponsorProUpdatesWindow\");", code);
        Assert.Contains("Signed out locally. Your GitHub browser session is unchanged.", code);
    }

    [Fact]
    public void AppStartup_RoutesUpdateHelperBeforeOpeningMainWindow()
    {
        var appXaml = File.ReadAllText(Path.Combine(AppDirectory, "App.xaml"));
        var appCode = File.ReadAllText(Path.Combine(AppDirectory, "App.xaml.cs"));
        var helperCode = File.ReadAllText(Path.Combine(AppDirectory, "Updates", "WinPerfUpdateHelper.cs"));

        Assert.Contains("Startup=\"Application_Startup\"", appXaml);
        Assert.DoesNotContain("StartupUri=\"MainWindow.xaml\"", appXaml);
        Assert.Contains("WinPerfUpdateHelper.IsApplyRequest(e.Args)", appCode);
        Assert.Contains("WinPerfUpdateHelper.RunApply(e.Args)", appCode);
        Assert.Contains("WinPerfUpdateHelper.ScheduleCleanup(e.Args)", appCode);
        Assert.Contains("new MainWindow().Show();", appCode);
        Assert.Contains("--winperf-cleanup-update-helper", helperCode);
        Assert.Contains("WinPerf.UpdateHelper.exe", helperCode);
        Assert.Contains("new WinPerfUpdateInstaller().Apply", helperCode);
    }
}
