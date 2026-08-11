namespace WinPerf.Tests;

public sealed class SettingsDiscoverabilityTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    private static readonly string MainWindowXamlPath = Path.Combine(AppDirectory, "MainWindow.xaml");
    private static readonly string MainWindowCodePath = Path.Combine(AppDirectory, "MainWindow.xaml.cs");
    private static readonly string SettingsWindowXamlPath = Path.Combine(AppDirectory, "SettingsWindow.xaml");

    [Fact]
    public void MainWindow_AppSectionShowsSettingsAndAppActions()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);

        Assert.Contains("x:Name=\"AppMenuSection\"", xaml);
        Assert.Contains("x:Name=\"AppMenuButton\"", xaml);
        Assert.Contains("Content=\"App ▾\"", xaml);
        Assert.Contains("x:Name=\"AppContextMenu\"", xaml);
        Assert.Contains("x:Name=\"SettingsMenuItem\"", xaml);
        Assert.Contains("Header=\"Settings\"", xaml);
        Assert.Contains("Header=\"Sponsor Pro / Updates\"", xaml);
        Assert.Contains("Header=\"About WinPerf\"", xaml);
        Assert.Contains("Style=\"{StaticResource SidebarAppButton}\"", xaml);
        Assert.Contains("ToolTip=\"Open app settings, updates and information\"", xaml);
        Assert.Contains("Click=\"AppMenuButton_Click\"", xaml);
        Assert.Contains("x:Name=\"CompactIntegrationsCard\"", xaml);
        Assert.DoesNotContain("UiDensityButton", xaml);
        Assert.DoesNotContain("UI: Compact", xaml);

        var appSection = xaml.IndexOf("x:Name=\"AppMenuSection\"", StringComparison.Ordinal);
        var appMenuButton = xaml.IndexOf("x:Name=\"AppMenuButton\"", StringComparison.Ordinal);
        var integrationsCard = xaml.IndexOf("x:Name=\"CompactIntegrationsCard\"", StringComparison.Ordinal);
        var configColumn = xaml.IndexOf("x:Name=\"ConfigColumn\"", StringComparison.Ordinal);

        Assert.True(appSection >= 0);
        Assert.True(appMenuButton >= 0);
        Assert.True(appMenuButton > appSection);
        Assert.True(integrationsCard > appMenuButton);
        Assert.True(configColumn > integrationsCard);
        Assert.DoesNotMatch("x:Name=\\\"AppMenuButton\\\"[\\s\\S]{0,220}DockPanel.Dock=\\\"Right\\\"", xaml);
        Assert.DoesNotMatch("x:Name=\\\"AppMenuButton\\\"[\\s\\S]{0,220}Style=\\\"\\{StaticResource StatusPillButton\\}\\\"", xaml);
    }

    [Fact]
    public void MainWindow_AppMenuActionsOpenExpectedDialogs()
    {
        var code = File.ReadAllText(MainWindowCodePath);

        var appMenuHandler = code.IndexOf("private void AppMenuButton_Click", StringComparison.Ordinal);
        var settingsItemHandler = code.IndexOf("private void SettingsMenuItem_Click", StringComparison.Ordinal);
        var updatesItemHandler = code.IndexOf("private void UpdatesMenuItem_Click", StringComparison.Ordinal);
        var aboutItemHandler = code.IndexOf("private void AboutMenuItem_Click", StringComparison.Ordinal);
        var sharedHandler = code.IndexOf("private void OpenSettingsWindow", StringComparison.Ordinal);
        var aboutHandler = code.IndexOf("private void OpenAboutWindow", StringComparison.Ordinal);
        var updatesHandler = code.IndexOf("private void OpenSponsorProUpdatesWindow", StringComparison.Ordinal);

        Assert.True(appMenuHandler >= 0);
        Assert.True(settingsItemHandler > appMenuHandler);
        Assert.True(updatesItemHandler > settingsItemHandler);
        Assert.True(aboutItemHandler > updatesItemHandler);
        Assert.True(sharedHandler > aboutItemHandler);
        Assert.True(aboutHandler > sharedHandler);
        Assert.True(updatesHandler > aboutHandler);

        Assert.True(code.IndexOf("OpenSettingsWindow();", settingsItemHandler, StringComparison.Ordinal) > settingsItemHandler);
        Assert.True(code.IndexOf("OpenSponsorProUpdatesWindow();", updatesItemHandler, StringComparison.Ordinal) > updatesItemHandler);
        Assert.True(code.IndexOf("OpenAboutWindow();", aboutItemHandler, StringComparison.Ordinal) > aboutItemHandler);
        Assert.Contains("new SponsorProUpdatesWindow(ResolveAppVersionText())", code);
        Assert.Contains("new AboutWindow(ResolveAppVersionText())", code);
        Assert.DoesNotContain("EngineStatusText_MouseLeftButtonUp", code);
    }

    [Fact]
    public void SettingsWindow_PortableFolderButtonsHaveEnoughWidth()
    {
        var xaml = File.ReadAllText(SettingsWindowXamlPath);

        Assert.Contains("Text=\"Portable folders\"", xaml);
        Assert.Contains("<ColumnDefinition Width=\"104\" />", SliceAround(xaml, "Portable folders"));
        Assert.Contains("Content=\"Open data\"", xaml);
        Assert.Contains("Content=\"Open iperf3\"", xaml);
        Assert.Contains("Content=\"Open iperf2\"", xaml);
    }

    [Fact]
    public void SettingsWindow_UsesCompactEngineRowsInsteadOfLargeFallbackCards()
    {
        var xaml = File.ReadAllText(SettingsWindowXamlPath);

        Assert.Contains("Text=\"Engines\"", xaml);
        Assert.Contains("iperf3 executable · fallback tools\\iperf3\\iperf3.exe", xaml);
        Assert.Contains("iperf2 executable · fallback tools\\iperf2\\iperf.exe or iperf2.exe", xaml);
        Assert.DoesNotContain("Text=\"iperf3 fallback\"", xaml);
        Assert.DoesNotContain("Text=\"iperf2 fallback\"", xaml);
    }

    private static string SliceAround(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0);

        var start = Math.Max(0, index - 700);
        var length = Math.Min(text.Length - start, 2200);

        return text.Substring(start, length);
    }
}
