namespace WinPerf.Tests;

public sealed class UiDensityTogglePolishTests
{
    private static readonly string MainWindowXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml"));

    private static readonly string MainWindowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml.cs"));

    [Fact]
    public void StatusBarDensityToggle_UsesCompactPillDropdown()
    {
        Assert.Contains("x:Key=\"StatusPillButton\"", MainWindowXaml);
        Assert.Contains("x:Name=\"UiDensityButton\"", MainWindowXaml);
        Assert.Contains("Style=\"{StaticResource StatusPillButton}\"", MainWindowXaml);
        Assert.Contains("Property=\"Height\" Value=\"22\"", MainWindowXaml);
        Assert.Contains("Property=\"MinWidth\" Value=\"132\"", MainWindowXaml);
        Assert.Contains("Property=\"VerticalAlignment\" Value=\"Center\"", MainWindowXaml);
        Assert.Contains("<Button.ContextMenu>", MainWindowXaml);
    }

    [Fact]
    public void DensitySelection_CapturesLayoutBeforeSwitching()
    {
        Assert.Contains("private void SetUiDensity(string density)", MainWindowSource);
        Assert.Contains("CaptureDashboardLayout();", MainWindowSource);
        Assert.Contains("ApplyUiDensity(resizeWindow: false);", MainWindowSource);
    }

    [Fact]
    public void SaveDashboardLayout_ReusesCaptureHelper()
    {
        Assert.Contains("private void SaveDashboardLayout()", MainWindowSource);
        Assert.Contains("CaptureDashboardLayout();", MainWindowSource);
        Assert.Contains("_settingsStore.Save(_settings);", MainWindowSource);
    }
}
