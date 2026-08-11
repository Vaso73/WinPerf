namespace WinPerf.Tests;

public sealed class IntegrationsDashboardTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    [Fact]
    public void LeftRail_ShowsCompactLoadedIntegrationsCard()
    {
        var xaml = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml"));
        var theme = File.ReadAllText(Path.Combine(AppDirectory, "ResourceDictionaries", "Theme.xaml"));

        Assert.Contains("x:Name=\"CompactIntegrationsCard\"", xaml);
        Assert.Contains("x:Key=\"CompactStatusChip\"", theme);
        Assert.Contains("Style=\"{StaticResource CompactStatusChip}\"", xaml);
        Assert.Contains("Text=\"Loaded\"", xaml);
        Assert.Contains("x:Name=\"Iperf3IntegrationStatusChip\"", xaml);
        Assert.Contains("x:Name=\"Iperf3IntegrationStatusText\"", xaml);
        Assert.Contains("x:Name=\"Iperf2IntegrationStatusChip\"", xaml);
        Assert.Contains("x:Name=\"Iperf2IntegrationStatusText\"", xaml);
        Assert.DoesNotContain("UpdaterIntegrationStatusChip", xaml);
        Assert.DoesNotContain("UpdaterIntegrationStatusText", xaml);
        Assert.DoesNotContain("Text=\"Sponsor Pro core\"", xaml);

        var appSection = xaml.IndexOf("x:Name=\"AppMenuSection\"", StringComparison.Ordinal);
        var integrations = xaml.IndexOf("x:Name=\"CompactIntegrationsCard\"", StringComparison.Ordinal);
        var config = xaml.IndexOf("x:Name=\"ConfigColumn\"", StringComparison.Ordinal);
        var results = xaml.IndexOf("x:Name=\"ResultsGrid\"", StringComparison.Ordinal);

        Assert.True(appSection >= 0);
        Assert.True(integrations > appSection);
        Assert.True(config > integrations);
        Assert.True(results > config);
        Assert.DoesNotContain("x:Name=\"IntegrationsCard\"", xaml);
    }

    [Fact]
    public void MainWindow_RefreshesOnlyIperfIntegrationsInLeftRail()
    {
        var code = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml.cs"));

        Assert.Contains("RefreshIntegrationStatus();", code);
        Assert.Contains("private void RefreshIntegrationStatus()", code);
        Assert.Contains("ResolveIntegration(IperfEngine.Iperf3)", code);
        Assert.Contains("ResolveIntegration(IperfEngine.Iperf2)", code);
        Assert.Contains("SetIntegrationChipState", code);
        Assert.DoesNotContain("UpdaterIntegrationStatusText", code);
        Assert.DoesNotContain("UpdaterIntegrationDetailText", code);
        Assert.DoesNotContain("UpdaterIntegrationPathText", code);
    }
}
