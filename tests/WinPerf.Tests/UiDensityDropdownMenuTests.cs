namespace WinPerf.Tests;

public sealed class UiDensityDropdownMenuTests
{
    private static readonly string MainWindowXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml"));

    private static readonly string MainWindowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml.cs"));

    [Fact]
    public void DropdownMenu_ShowsBothUiDensityChoices()
    {
        Assert.Contains("x:Name=\"CompactUiDensityMenuItem\"", MainWindowXaml);
        Assert.Contains("x:Name=\"ComfortableUiDensityMenuItem\"", MainWindowXaml);
        Assert.Contains("Header=\"Compact\"", MainWindowXaml);
        Assert.Contains("Header=\"Comfortable\"", MainWindowXaml);
    }

    [Fact]
    public void DropdownMenu_MarksTheActiveDensity()
    {
        Assert.Contains("private void UpdateUiDensityMenuUx(bool isCompact)", MainWindowSource);
        Assert.Contains("CompactUiDensityMenuItem.IsChecked = isCompact;", MainWindowSource);
        Assert.Contains("ComfortableUiDensityMenuItem.IsChecked = !isCompact;", MainWindowSource);
    }

    [Fact]
    public void DropdownButton_DisplaysCurrentDensity()
    {
        Assert.Contains("UI: Compact ▾", MainWindowSource);
        Assert.Contains("UI: Comfortable ▾", MainWindowSource);
        Assert.Contains("Active UI density: Compact", MainWindowSource);
        Assert.Contains("Active UI density: Comfortable", MainWindowSource);
    }
}
