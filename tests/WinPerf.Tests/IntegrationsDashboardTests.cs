namespace WinPerf.Tests;

public sealed class IntegrationsDashboardTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    [Fact]
    public void Dashboard_ShowsLoadedIntegrationsCard()
    {
        var xaml = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml"));

        Assert.Contains("x:Name=\"IntegrationsCard\"", xaml);
        Assert.Contains("Text=\"Loaded integrations\"", xaml);
        Assert.Contains("x:Name=\"Iperf3IntegrationStatusText\"", xaml);
        Assert.Contains("x:Name=\"Iperf2IntegrationStatusText\"", xaml);
        Assert.Contains("x:Name=\"UpdaterIntegrationStatusText\"", xaml);
        Assert.Contains("Text=\"winperf / WinPerf.zip\"", xaml);

        var header = xaml.IndexOf("Text=\"Dashboard\"", StringComparison.Ordinal);
        var integrations = xaml.IndexOf("x:Name=\"IntegrationsCard\"", StringComparison.Ordinal);
        var results = xaml.IndexOf("x:Name=\"ResultsGrid\"", StringComparison.Ordinal);

        Assert.True(header >= 0);
        Assert.True(integrations > header);
        Assert.True(results > integrations);
    }

    [Fact]
    public void MainWindow_RefreshesBothIperfIntegrationsAndUpdaterStatus()
    {
        var code = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml.cs"));

        Assert.Contains("RefreshIntegrationStatus();", code);
        Assert.Contains("private void RefreshIntegrationStatus()", code);
        Assert.Contains("ResolveIntegration(IperfEngine.Iperf3)", code);
        Assert.Contains("ResolveIntegration(IperfEngine.Iperf2)", code);
        Assert.Contains("WinPerfUpdateService.ProductId", code);
        Assert.Contains("WinPerfUpdateService.AssetName", code);
        Assert.Contains("Sponsor Pro update client and installer contracts loaded", code);
    }
}
