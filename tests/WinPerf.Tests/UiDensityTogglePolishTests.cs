namespace WinPerf.Tests;

public sealed class UiDensityTogglePolishTests
{
    private static readonly string MainWindowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml.cs"));

    private static readonly string MainWindowXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml"));

    [Fact]
    public void UiDensityButton_FitsStatusBar()
    {
        Assert.Contains("x:Name=\"UiDensityButton\"", MainWindowXaml);
        Assert.Contains("Property=\"Height\" Value=\"22\"", MainWindowXaml);
        Assert.Contains("Property=\"MinWidth\" Value=\"132\"", MainWindowXaml);
        Assert.Contains("Property=\"VerticalAlignment\" Value=\"Center\"", MainWindowXaml);
        Assert.Contains("HorizontalContentAlignment=\"Center\"", MainWindowXaml);
    }

    [Fact]
    public void UiDensityToggle_CapturesCurrentSplitterLayoutBeforeApplyingDensity()
    {
        Assert.Contains("private void CaptureDashboardLayout()", MainWindowSource);
        Assert.Contains("CaptureDashboardLayout();", MainWindowSource);
        Assert.Contains("private void SaveDashboardLayout()", MainWindowSource);
        Assert.Contains("_settings.DashboardLeftRailWidth", MainWindowSource);
        Assert.Contains("_settings.DashboardEngineOutputHeight", MainWindowSource);
    }

    [Fact]
    public void SaveDashboardLayout_PersistsCapturedLayoutOnce()
    {
        Assert.Contains("CaptureDashboardLayout();\n        _settingsStore.Save(_settings);", MainWindowSource);
    }
}
