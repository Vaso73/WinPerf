namespace WinPerf.Tests;

public sealed class ButtonChromeStyleTests
{
    private static readonly string ThemeXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "ResourceDictionaries", "Theme.xaml"));

    private static readonly string MainWindowXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml"));

    [Fact]
    public void SharedButtonStylesUseRoundedTemplateHoverAndDarkDisabledState()
    {
        Assert.Contains("x:Key=\"PrimaryButtonBase\"", ThemeXaml);
        Assert.Contains("x:Key=\"SecondaryButtonBase\"", ThemeXaml);
        Assert.Contains("ControlTemplate TargetType=\"{x:Type Button}\"", ThemeXaml);
        Assert.Contains("CornerRadius=\"8\"", ThemeXaml);
        Assert.Contains("Property=\"IsMouseOver\" Value=\"True\"", ThemeXaml);
        Assert.Contains("Property=\"IsPressed\" Value=\"True\"", ThemeXaml);
        Assert.Contains("Property=\"IsEnabled\" Value=\"False\"", ThemeXaml);
        Assert.Contains("Property=\"Background\" Value=\"#13233A\"", ThemeXaml);
        Assert.Contains("Property=\"Foreground\" Value=\"{StaticResource TextMuted}\"", ThemeXaml);
        Assert.Contains("Property=\"Opacity\" Value=\"0.55\"", ThemeXaml);
        Assert.DoesNotContain("SystemColors.ControlBrushKey", ThemeXaml);
    }

    [Fact]
    public void AppMenuButtonsInheritRoundedSecondaryChrome()
    {
        Assert.Contains("x:Key=\"SidebarAppButton\"", MainWindowXaml);
        Assert.Contains("BasedOn=\"{StaticResource SecondaryButton}\"", MainWindowXaml);
        Assert.Contains("Property=\"MinHeight\" Value=\"30\"", MainWindowXaml);
        Assert.Contains("x:Name=\"AppMenuButton\"", MainWindowXaml);
        Assert.DoesNotMatch("x:Name=\\\"AppMenuButton\\\"[\\s\\S]{0,220}Background=\\\"#172A44\\\"", MainWindowXaml);
    }
}
