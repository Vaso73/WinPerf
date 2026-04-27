namespace WinPerf.Tests;

public sealed class UiDensityToggleTests
{
    private static readonly string SettingsSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "Settings", "WinPerfSettings.cs"));

    private static readonly string MainWindowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml.cs"));

    private static readonly string MainWindowXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml"));

    [Fact]
    public void Settings_PersistUiDensity()
    {
        Assert.Contains("public string? UiDensity { get; set; }", SettingsSource);
    }

    [Fact]
    public void MainWindow_ExposesRuntimeUiDensityToggle()
    {
        Assert.Contains("UiDensityButton", MainWindowXaml);
        Assert.Contains("Compact UI", MainWindowXaml);
        Assert.Contains("UiDensityButton_Click", MainWindowXaml);
        Assert.Contains("DashboardBodyGrid", MainWindowXaml);
        Assert.Contains("DashboardContentPanel", MainWindowXaml);
        Assert.Contains("MetricsRow", MainWindowXaml);
    }

    [Fact]
    public void MainWindow_AppliesDensityWithoutRestart()
    {
        Assert.Contains("UiDensityComfortable", MainWindowSource);
        Assert.Contains("UiDensityCompact", MainWindowSource);
        Assert.Contains("private void ApplyUiDensity(bool resizeWindow)", MainWindowSource);
        Assert.Contains("DashboardBodyGrid.LayoutTransform", MainWindowSource);
        Assert.Contains("new ScaleTransform(scale, scale)", MainWindowSource);
        Assert.Contains("_settingsStore.Save(_settings);", MainWindowSource);
    }

    [Fact]
    public void MainWindow_CompactsDashboardLayoutAtRuntime()
    {
        Assert.Contains("LeftRailColumn.MinWidth = isCompact ? 280 : 320;", MainWindowSource);
        Assert.Contains("DashboardContentPanel.Margin = isCompact", MainWindowSource);
        Assert.Contains("MetricsRow.Height = new GridLength(isCompact ? 150 : 170);", MainWindowSource);
        Assert.Contains("EngineOutputRow.Height = new GridLength(isCompact ? 120 : 150);", MainWindowSource);
        Assert.Contains("Width = Math.Max(Width, MinWidth);", MainWindowSource);
    }
}
