namespace WinPerf.Tests;

public sealed class UiDensityTogglePolishTests
{
    private static readonly string MainWindowXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml"));

    private static readonly string MainWindowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml.cs"));

    [Fact]
    public void AppMenuDensityToggle_UsesSidebarDropdown()
    {
        Assert.Contains("x:Name=\"AppMenuSection\"", MainWindowXaml);
        Assert.Contains("x:Key=\"SidebarAppButton\"", MainWindowXaml);
        Assert.Contains("x:Name=\"UiDensityButton\"", MainWindowXaml);
        Assert.Contains("Style=\"{StaticResource SidebarAppButton}\"", MainWindowXaml);
        Assert.Contains("Property=\"MinHeight\" Value=\"30\"", MainWindowXaml);
        Assert.Contains("Property=\"HorizontalContentAlignment\" Value=\"Left\"", MainWindowXaml);
        Assert.Contains("<Button.ContextMenu>", MainWindowXaml);
        Assert.DoesNotMatch("x:Name=\\\"UiDensityButton\\\"[\\s\\S]{0,220}Style=\\\"\\{StaticResource StatusPillButton\\}\\\"", MainWindowXaml);
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
