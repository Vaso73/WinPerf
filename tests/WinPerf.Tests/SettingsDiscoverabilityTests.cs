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
        Assert.Contains("x:Name=\"SettingsButton\"", xaml);
        Assert.Contains("Content=\"Settings\"", xaml);
        Assert.Contains("Content=\"Sponsor Pro / Updates\"", xaml);
        Assert.Contains("Content=\"About WinPerf\"", xaml);
        Assert.Contains("Content=\"UI: Compact ▾\"", xaml);
        Assert.Contains("Style=\"{StaticResource SidebarAppButton}\"", xaml);
        Assert.Contains("ToolTip=\"Open Settings\"", xaml);
        Assert.Contains("Click=\"SettingsButton_Click\"", xaml);

        var appSection = xaml.IndexOf("x:Name=\"AppMenuSection\"", StringComparison.Ordinal);
        var settingsButton = xaml.IndexOf("x:Name=\"SettingsButton\"", StringComparison.Ordinal);
        var configColumn = xaml.IndexOf("x:Name=\"ConfigColumn\"", StringComparison.Ordinal);

        Assert.True(appSection >= 0);
        Assert.True(settingsButton >= 0);
        Assert.True(settingsButton > appSection);
        Assert.True(configColumn > settingsButton);
        Assert.DoesNotMatch("x:Name=\\\"SettingsButton\\\"[\\s\\S]{0,220}DockPanel.Dock=\\\"Right\\\"", xaml);
        Assert.DoesNotMatch("x:Name=\\\"SettingsButton\\\"[\\s\\S]{0,220}Style=\\\"\\{StaticResource StatusPillButton\\}\\\"", xaml);
    }

    [Fact]
    public void MainWindow_AppMenuActionsOpenExpectedDialogs()
    {
        var code = File.ReadAllText(MainWindowCodePath);

        var settingsButtonHandler = code.IndexOf("private void SettingsButton_Click", StringComparison.Ordinal);
        var updatesButtonHandler = code.IndexOf("private void UpdatesButton_Click", StringComparison.Ordinal);
        var aboutButtonHandler = code.IndexOf("private void AboutButton_Click", StringComparison.Ordinal);
        var sharedHandler = code.IndexOf("private void OpenSettingsWindow", StringComparison.Ordinal);
        var aboutHandler = code.IndexOf("private void OpenAboutWindow", StringComparison.Ordinal);

        Assert.True(settingsButtonHandler >= 0);
        Assert.True(updatesButtonHandler > settingsButtonHandler);
        Assert.True(aboutButtonHandler > updatesButtonHandler);
        Assert.True(sharedHandler > aboutButtonHandler);
        Assert.True(aboutHandler > sharedHandler);

        Assert.True(code.IndexOf("OpenSettingsWindow();", settingsButtonHandler, StringComparison.Ordinal) > settingsButtonHandler);
        Assert.True(code.IndexOf("OpenAboutWindow();", updatesButtonHandler, StringComparison.Ordinal) > updatesButtonHandler);
        Assert.True(code.IndexOf("OpenAboutWindow();", aboutButtonHandler, StringComparison.Ordinal) > aboutButtonHandler);
        Assert.DoesNotContain("EngineStatusText_MouseLeftButtonUp", code);
    }

    [Fact]
    public void SettingsWindow_PortableFolderButtonsHaveEnoughWidth()
    {
        var xaml = File.ReadAllText(SettingsWindowXamlPath);

        Assert.Contains("<ColumnDefinition Width=\"180\" />", SliceAround(xaml, "Portable data folder"));
        Assert.Contains("<ColumnDefinition Width=\"180\" />", SliceAround(xaml, "Portable iperf3 engine folder"));
        Assert.Contains("<ColumnDefinition Width=\"180\" />", SliceAround(xaml, "Portable iperf2 engine folder"));
        Assert.Contains("Content=\"Open data folder\"", xaml);
        Assert.Contains("Content=\"Open iperf3 folder\"", xaml);
        Assert.Contains("Content=\"Open iperf2 folder\"", xaml);
    }

    private static string SliceAround(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0);

        var start = Math.Max(0, index - 700);
        var length = Math.Min(text.Length - start, 1200);

        return text.Substring(start, length);
    }
}
