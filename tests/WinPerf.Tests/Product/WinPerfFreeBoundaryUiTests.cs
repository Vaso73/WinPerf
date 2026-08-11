namespace WinPerf.Tests.Product;

public sealed class WinPerfFreeBoundaryUiTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    private static readonly string CoreDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.Core"));

    private static readonly string MainWindowXaml = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml"));
    private static readonly string MainWindowCode = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml.cs"));
    private static readonly string SettingsWindowXaml = File.ReadAllText(Path.Combine(AppDirectory, "SettingsWindow.xaml"));
    private static readonly string SettingsWindowCode = File.ReadAllText(Path.Combine(AppDirectory, "SettingsWindow.xaml.cs"));
    private static readonly string LanguagePackCode = File.ReadAllText(Path.Combine(CoreDirectory, "Localization", "LanguagePackService.cs"));

    [Fact]
    public void MainWindow_AppliesFreeBoundaryDuringStartupAndProfileLoading()
    {
        Assert.Contains("ApplyProductEditionBoundary();", MainWindowCode);
        Assert.Contains("NormalizeDashboardForProductEdition();", MainWindowCode);
        Assert.Contains("StreamsBox is null", MainWindowCode);
        Assert.Contains("DurationBox is null", MainWindowCode);
        Assert.Contains("UdpBandwidthPanel is null", MainWindowCode);
        Assert.Contains("HideComboBoxItemByContent(EngineBox, \"iperf2\")", MainWindowCode);
        Assert.Contains("HideComboBoxItemByTag(ModeBox, \"udp-upload\")", MainWindowCode);
        Assert.Contains("HideComboBoxItemByTag(ModeBox, \"udp-download\")", MainWindowCode);
        Assert.Contains("HideComboBoxItemByTag(ModeBox, \"tcp-bidirectional\")", MainWindowCode);
    }

    [Fact]
    public void MainWindow_HidesOrDisablesProOnlyControlsInFreeBuild()
    {
        Assert.Contains("x:Name=\"Iperf2IntegrationPanel\"", MainWindowXaml);
        Assert.Contains("x:Name=\"AdvancedCommandMenuItem\"", MainWindowXaml);
        Assert.Contains("x:Name=\"CustomCommandMenuItem\"", MainWindowXaml);
        Assert.Contains("Iperf2IntegrationPanel.Visibility = Visibility.Collapsed;", MainWindowCode);
        Assert.Contains("ServerModeNavButton.IsEnabled = WinPerfProductEdition.SupportsServerMode;", MainWindowCode);
        Assert.Contains("AdvancedCommandMenuItem.IsEnabled = WinPerfProductEdition.SupportsAdvancedCommands;", MainWindowCode);
        Assert.Contains("CustomCommandMenuItem.IsEnabled = WinPerfProductEdition.SupportsCustomCommands;", MainWindowCode);
        Assert.Contains("HistoryExportButton.IsEnabled = WinPerfProductEdition.SupportsHistoryExportImport;", MainWindowCode);
        Assert.Contains("HistoryImportButton.IsEnabled = WinPerfProductEdition.SupportsHistoryExportImport;", MainWindowCode);
    }

    [Fact]
    public void SettingsWindow_HidesIperf2ConfigurationInFreeBuild()
    {
        Assert.Contains("x:Name=\"Iperf2SettingsLabel\"", SettingsWindowXaml);
        Assert.Contains("x:Name=\"BrowseIperf2Button\"", SettingsWindowXaml);
        Assert.Contains("x:Name=\"ImportPortableIperf2Button\"", SettingsWindowXaml);
        Assert.Contains("x:Name=\"ClearIperf2Button\"", SettingsWindowXaml);
        Assert.Contains("x:Name=\"PortableIperf2EngineDirectoryLabel\"", SettingsWindowXaml);
        Assert.Contains("ApplyProductEditionBoundary();", SettingsWindowCode);
        Assert.Contains("if (!WinPerfProductEdition.SupportsIperf2)", SettingsWindowCode);
        Assert.Contains("Iperf2PathBox.Text = string.Empty;", SettingsWindowCode);
        Assert.Contains("element.Visibility = Visibility.Collapsed;", SettingsWindowCode);
    }

    [Fact]
    public void RuntimeDataPaths_UseEditionSpecificDataDirectory()
    {
        Assert.Contains("WinPerfProductEdition.DataDirectoryName", File.ReadAllText(Path.Combine(CoreDirectory, "History", "JsonIperfHistoryStore.cs")));
        Assert.Contains("WinPerfProductEdition.DataDirectoryName", File.ReadAllText(Path.Combine(CoreDirectory, "Profiles", "JsonSavedIperfProfileStore.cs")));
        Assert.Contains("WinPerfProductEdition.DataDirectoryName", File.ReadAllText(Path.Combine(AppDirectory, "Settings", "WinPerfSettingsStore.cs")));
        Assert.Contains("WinPerfProductEdition.DataDirectoryName", File.ReadAllText(Path.Combine(AppDirectory, "Settings", "WindowPlacementStore.cs")));
        Assert.Contains("WinPerfProductEdition.DataDirectoryName", File.ReadAllText(Path.Combine(AppDirectory, "Updates", "SponsorProSessionStore.cs")));
        Assert.Contains("Path.Combine(_appDirectory, WinPerfProductEdition.DataDirectoryName)", SettingsWindowCode);
    }

    [Fact]
    public void MainWindow_ValidatesFreeLimitsBeforeRunningCommands()
    {
        Assert.Contains("ValidateProductEditionTestOptions(options);", MainWindowCode);
        Assert.Contains("options.Engine == IperfEngine.Iperf2", MainWindowCode);
        Assert.Contains("!IsModeAllowedByProductEdition(options.Mode)", MainWindowCode);
        Assert.Contains("options.Streams > WinPerfProductEdition.MaxStreams", MainWindowCode);
        Assert.Contains("options.DurationSeconds > WinPerfProductEdition.MaxDurationSeconds", MainWindowCode);
    }

    [Fact]
    public void MainWindow_LimitsHistoryAndBlocksFreeImportExport()
    {
        Assert.Contains("AddAsync(entry, WinPerfProductEdition.MaxSavedHistoryResults)", MainWindowCode);
        Assert.Contains("MergeAsync(importedDocument, WinPerfProductEdition.MaxSavedHistoryResults)", MainWindowCode);
        Assert.Contains("if (!WinPerfProductEdition.SupportsHistoryExportImport)", MainWindowCode);
        Assert.Contains("HistoryStatusText.Text = AppText.T(\"Available in WinPerf Sponsor Pro.\");", MainWindowCode);
    }

    [Fact]
    public void FreeBoundaryMessages_AreLocalized()
    {
        foreach (var key in new[]
        {
            "Available in WinPerf Sponsor Pro.",
            "WinPerf Free includes iperf3 TCP upload/download, 1 stream and 10 second tests.",
            "iperf2 is available in WinPerf Sponsor Pro.",
            "This test mode is available in WinPerf Sponsor Pro.",
            "WinPerf Free allows up to {0} stream.",
            "WinPerf Free allows tests up to {0} seconds."
        })
        {
            Assert.Contains($"[\"{key}\"]", LanguagePackCode);
        }
    }
}
