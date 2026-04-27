namespace WinPerf.Tests;

public sealed class UiDensitySpecificLayoutTests
{
    private static readonly string SettingsSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "Settings", "WinPerfSettings.cs"));

    private static readonly string MainWindowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml.cs"));

    [Fact]
    public void Settings_PersistDashboardLayoutPerUiDensity()
    {
        Assert.Contains("public double? CompactDashboardEngineOutputHeight { get; set; }", SettingsSource);
        Assert.Contains("public double? CompactDashboardLeftRailWidth { get; set; }", SettingsSource);
        Assert.Contains("public double? ComfortableDashboardEngineOutputHeight { get; set; }", SettingsSource);
        Assert.Contains("public double? ComfortableDashboardLeftRailWidth { get; set; }", SettingsSource);
    }

    [Fact]
    public void MainWindow_ReadsDensitySpecificDashboardLayoutWithLegacyFallback()
    {
        Assert.Contains("private double? GetSavedDashboardEngineOutputHeight()", MainWindowSource);
        Assert.Contains("_settings.CompactDashboardEngineOutputHeight ?? _settings.DashboardEngineOutputHeight", MainWindowSource);
        Assert.Contains("_settings.ComfortableDashboardEngineOutputHeight ?? _settings.DashboardEngineOutputHeight", MainWindowSource);
        Assert.Contains("_settings.CompactDashboardLeftRailWidth ?? _settings.DashboardLeftRailWidth", MainWindowSource);
        Assert.Contains("_settings.ComfortableDashboardLeftRailWidth ?? _settings.DashboardLeftRailWidth", MainWindowSource);
    }

    [Fact]
    public void MainWindow_WritesDashboardLayoutToCurrentUiDensityOnly()
    {
        Assert.Contains("private void SetSavedDashboardEngineOutputHeight(double height)", MainWindowSource);
        Assert.Contains("_settings.CompactDashboardEngineOutputHeight = height;", MainWindowSource);
        Assert.Contains("_settings.ComfortableDashboardEngineOutputHeight = height;", MainWindowSource);
        Assert.Contains("_settings.CompactDashboardLeftRailWidth = width;", MainWindowSource);
        Assert.Contains("_settings.ComfortableDashboardLeftRailWidth = width;", MainWindowSource);
        Assert.Contains("SetSavedDashboardEngineOutputHeight(Math.Round(height, 0));", MainWindowSource);
        Assert.Contains("SetSavedDashboardLeftRailWidth(", MainWindowSource);
    }
}
