namespace WinPerf.Tests;

public sealed class SettingsDiscoverabilityTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    private static readonly string MainWindowXamlPath = Path.Combine(AppDirectory, "MainWindow.xaml");
    private static readonly string MainWindowCodePath = Path.Combine(AppDirectory, "MainWindow.xaml.cs");
    private static readonly string SettingsWindowXamlPath = Path.Combine(AppDirectory, "SettingsWindow.xaml");

    [Fact]
    public void MainWindow_FooterShowsDedicatedSettingsButton()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);

        Assert.Contains("x:Name=\"SettingsButton\"", xaml);
        Assert.Contains("DockPanel.Dock=\"Right\"", xaml);
        Assert.Contains("Content=\"Settings\"", xaml);
        Assert.Contains("Style=\"{StaticResource StatusPillButton}\"", xaml);
        Assert.Contains("ToolTip=\"Open Settings\"", xaml);
        Assert.Contains("Click=\"SettingsButton_Click\"", xaml);

        var settingsButton = xaml.IndexOf("x:Name=\"SettingsButton\"", StringComparison.Ordinal);
        var engineStatus = xaml.IndexOf("x:Name=\"EngineStatusText\"", StringComparison.Ordinal);

        Assert.True(settingsButton >= 0);
        Assert.True(engineStatus > settingsButton);
    }

    [Fact]
    public void MainWindow_SettingsEntryPointsShareSettingsDialogHandler()
    {
        var code = File.ReadAllText(MainWindowCodePath);

        var settingsButtonHandler = code.IndexOf("private void SettingsButton_Click", StringComparison.Ordinal);
        var engineStatusHandler = code.IndexOf("private void EngineStatusText_MouseLeftButtonUp", StringComparison.Ordinal);
        var sharedHandler = code.IndexOf("private void OpenSettingsWindow", StringComparison.Ordinal);

        Assert.True(settingsButtonHandler >= 0);
        Assert.True(engineStatusHandler > settingsButtonHandler);
        Assert.True(sharedHandler > engineStatusHandler);

        Assert.True(code.IndexOf("OpenSettingsWindow();", settingsButtonHandler, StringComparison.Ordinal) > settingsButtonHandler);
        Assert.True(code.IndexOf("OpenSettingsWindow();", engineStatusHandler, StringComparison.Ordinal) > engineStatusHandler);
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
