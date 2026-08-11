namespace WinPerf.Tests;

public sealed class UiAutomationContractTests
{
    private static readonly string MainWindowXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml"));

    [Fact]
    public void MainWindow_ExposesStableAutomationIdsForSmokeTesting()
    {
        string[] requiredAutomationIds =
        [
            "AppMenuButton",
            "SettingsMenuItem",
            "UpdatesMenuItem",
            "AboutMenuItem",
            "DashboardNavButton",
            "ServerModeNavButton",
            "DashboardServerBox",
            "DashboardEngineBox",
            "DashboardModeBox",
            "DashboardPortBox",
            "DashboardStreamsBox",
            "DashboardDurationBox",
            "DashboardOmitSecondsBox",
            "DashboardStartButton",
            "DashboardStopButton",
            "DashboardCommandMenuButton",
            "DashboardEngineOutputText",
            "ServerModeEngineBox",
            "ServerModeProtocolBox",
            "ServerModePortBox",
            "ServerModeOneOffBox",
            "ServerModeOneOffUnavailableText",
            "ServerModeStartServerButton",
            "ServerModeStopServerButton",
            "ServerModeOutputText"
        ];

        foreach (var automationId in requiredAutomationIds)
        {
            Assert.Contains($"AutomationProperties.AutomationId=\"{automationId}\"", MainWindowXaml);
        }
    }
}
