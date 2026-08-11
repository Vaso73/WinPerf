namespace WinPerf.Tests;

public sealed class ServerModeUiTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    private static readonly string MainWindowXaml = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml"));
    private static readonly string MainWindowCode = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml.cs"));
    private static readonly string ThemeXaml = File.ReadAllText(Path.Combine(AppDirectory, "ResourceDictionaries", "Theme.xaml"));

    [Fact]
    public void MainWindow_HasClickableDashboardAndServerModeNavigation()
    {
        Assert.Contains("x:Name=\"DashboardNavButton\"", MainWindowXaml);
        Assert.Contains("x:Name=\"ServerModeNavButton\"", MainWindowXaml);
        Assert.Contains("Click=\"DashboardNavButton_Click\"", MainWindowXaml);
        Assert.Contains("Click=\"ServerModeNavButton_Click\"", MainWindowXaml);
        Assert.Contains("x:Key=\"SidebarNavButton\"", ThemeXaml);
        Assert.Contains("x:Key=\"SidebarNavSelectedButton\"", ThemeXaml);
    }

    [Fact]
    public void MainWindow_ServerModePageHasRuntimeControlsAndOutput()
    {
        Assert.Contains("x:Name=\"ServerModeContentPanel\"", MainWindowXaml);
        Assert.Contains("Text=\"Server Mode\"", MainWindowXaml);
        Assert.Contains("x:Name=\"ServerEngineBox\"", MainWindowXaml);
        Assert.Contains("x:Name=\"ServerProtocolBox\"", MainWindowXaml);
        Assert.Contains("x:Name=\"ServerPortBox\"", MainWindowXaml);
        Assert.Contains("x:Name=\"ServerOneOffBox\"", MainWindowXaml);
        Assert.Contains("x:Name=\"ServerOneOffUnavailableText\"", MainWindowXaml);
        Assert.Contains("x:Name=\"StartServerButton\"", MainWindowXaml);
        Assert.Contains("x:Name=\"StopServerButton\"", MainWindowXaml);
        Assert.Contains("x:Name=\"ServerOutputText\"", MainWindowXaml);
        Assert.Contains("VerticalContentAlignment=\"Top\"", MainWindowXaml);
        Assert.Contains("One-off is iperf3 only. iperf2 runs until stopped.", MainWindowXaml);
    }

    [Fact]
    public void MainWindow_ServerModeUsesCoreBuilderAndProcessRunner()
    {
        Assert.Contains("IperfCommandBuilder.BuildServerCommand", MainWindowCode);
        Assert.Contains("BuildServerModeOptions()", MainWindowCode);
        Assert.Contains("UpdateServerOneOffAvailability()", MainWindowCode);
        Assert.Contains("_processRunner.RunAsync(", MainWindowCode);
        Assert.Contains("StopServerButton_Click", MainWindowCode);
        Assert.Contains("_serverRunCancellation?.Cancel();", MainWindowCode);
        Assert.Contains("One-off server mode is not supported for iperf2", File.ReadAllText(Path.Combine(AppDirectory, "..", "WinPerf.Core", "Iperf", "IperfCommandBuilder.cs")));
    }
}
