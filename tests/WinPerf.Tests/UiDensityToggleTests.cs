namespace WinPerf.Tests;

public sealed class UiDensityToggleTests
{
    private static readonly string SettingsSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "Settings", "WinPerfSettings.cs"));

    private static readonly string MainWindowXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml"));

    private static readonly string MainWindowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml.cs"));

    [Fact]
    public void Settings_PersistUiDensity()
    {
        Assert.Contains("public string? UiDensity { get; set; }", SettingsSource);
    }

    [Fact]
    public void MainWindow_HasUiDensityDropdownMenu()
    {
        Assert.Contains("x:Name=\"UiDensityButton\"", MainWindowXaml);
        Assert.Contains("UI: Compact ▾", MainWindowXaml);
        Assert.Contains("x:Name=\"CompactUiDensityMenuItem\"", MainWindowXaml);
        Assert.Contains("x:Name=\"ComfortableUiDensityMenuItem\"", MainWindowXaml);
        Assert.Contains("Header=\"Compact\"", MainWindowXaml);
        Assert.Contains("Header=\"Comfortable\"", MainWindowXaml);
        Assert.DoesNotContain("IsCheckable=\"True\"", MainWindowXaml);
    }

    [Fact]
    public void MainWindow_SetsUiDensityFromDropdownAtRuntime()
    {
        Assert.Contains("private void UiDensityButton_Click", MainWindowSource);
        Assert.Contains("ContextMenu.IsOpen = true;", MainWindowSource);
        Assert.Contains("private void CompactUiDensityMenuItem_Click", MainWindowSource);
        Assert.Contains("private void ComfortableUiDensityMenuItem_Click", MainWindowSource);
        Assert.Contains("private void SetUiDensity(string density)", MainWindowSource);
        Assert.Contains("SetUiDensity(UiDensityCompact);", MainWindowSource);
        Assert.Contains("SetUiDensity(UiDensityComfortable);", MainWindowSource);
        Assert.Contains("ApplyUiDensity(resizeWindow: false);", MainWindowSource);
        Assert.Contains("_settingsStore.Save(_settings);", MainWindowSource);
    }

    [Fact]
    public void MainWindow_CompactsDashboardLayoutAtRuntime()
    {
        Assert.Contains("DashboardBodyGrid.LayoutTransform", MainWindowSource);
        Assert.Contains("new ScaleTransform(scale, scale)", MainWindowSource);
        Assert.Contains("LeftRailColumn.MinWidth = isCompact ? 280 : 320;", MainWindowSource);
        Assert.Contains("DashboardContentPanel.Margin = isCompact", MainWindowSource);
        Assert.Contains("MetricsRow.Height = new GridLength(isCompact ? 150 : 170);", MainWindowSource);
        Assert.Contains("LiveThroughputRow.MinHeight = isCompact ? 180 : 220;", MainWindowSource);
        Assert.Contains("EngineOutputRow.MinHeight = isCompact ? 80 : 95;", MainWindowSource);
    }
}
