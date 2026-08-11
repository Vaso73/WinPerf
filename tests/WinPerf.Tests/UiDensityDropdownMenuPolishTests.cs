namespace WinPerf.Tests;

public sealed class UiDensityDropdownMenuPolishTests
{
    private static readonly string MainWindowXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml"));

    private static readonly string MainWindowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml.cs"));

    [Fact]
    public void DensityDropdownMenu_UsesCompactMenuSizing()
    {
        Assert.Contains("x:Name=\"UiDensityContextMenu\"", MainWindowXaml);
        Assert.Contains("Width=\"112\"", MainWindowXaml);
        Assert.Contains("Padding=\"2\"", MainWindowXaml);
        Assert.Contains("Property=\"FontSize\" Value=\"11\"", MainWindowXaml);
        Assert.Contains("Property=\"MinHeight\" Value=\"22\"", MainWindowXaml);
        Assert.Contains("Property=\"Padding\" Value=\"4,2\"", MainWindowXaml);
        Assert.Contains("ControlTemplate TargetType=\"{x:Type MenuItem}\"", MainWindowXaml);
        Assert.Contains("Text=\"{TemplateBinding Header}\"", MainWindowXaml);
        Assert.Contains("Foreground=\"{TemplateBinding Foreground}\"", MainWindowXaml);
        Assert.Contains("TextTrimming=\"None\"", MainWindowXaml);
    }

    [Fact]
    public void DensityDropdownMenu_ShowsGreenCheckOnActiveItem()
    {
        Assert.Contains("private void ApplyUiDensityMenuItemState(MenuItem menuItem, bool isActive, string label)", MainWindowSource);
        Assert.Contains("menuItem.Header = isActive", MainWindowSource);
        Assert.Contains("$\"✓ {label}\"", MainWindowSource);
        Assert.Contains("menuItem.IsCheckable = false;", MainWindowSource);
        Assert.Contains("menuItem.IsChecked = false;", MainWindowSource);
        Assert.Contains("FindResource(isActive ? \"AccentGreen\" : \"TextMain\")", MainWindowSource);
        Assert.Contains("menuItem.FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold;", MainWindowSource);
        Assert.Contains("menuItem.Opacity = isActive ? 1.0 : 0.78;", MainWindowSource);
    }

    [Fact]
    public void DensityDropdownMenu_AppliesStateToBothItems()
    {
        Assert.Contains("ApplyUiDensityMenuItemState(CompactUiDensityMenuItem, isCompact, \"Compact\");", MainWindowSource);
        Assert.Contains("ApplyUiDensityMenuItemState(ComfortableUiDensityMenuItem, !isCompact, \"Comfortable\");", MainWindowSource);
    }
}
