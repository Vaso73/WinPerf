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
        Assert.Contains("x:Key=\"SidebarAppButton\"", ThemeXaml);
        Assert.Contains("BasedOn=\"{StaticResource SecondaryButton}\"", ThemeXaml);
        Assert.Contains("Property=\"MinHeight\" Value=\"30\"", ThemeXaml);
        Assert.Contains("x:Name=\"AppMenuButton\"", MainWindowXaml);
        Assert.DoesNotMatch("x:Name=\\\"AppMenuButton\\\"[\\s\\S]{0,220}Background=\\\"#172A44\\\"", MainWindowXaml);
    }

    [Fact]
    public void InputsAndComboBoxesUseDarkCustomChromeInsteadOfWhiteWindowsDefault()
    {
        Assert.Contains("x:Key=\"TextBoxBase\"", ThemeXaml);
        Assert.Contains("ControlTemplate TargetType=\"{x:Type TextBox}\"", ThemeXaml);
        Assert.Contains("x:Name=\"PART_ContentHost\"", ThemeXaml);
        Assert.Contains("x:Key=\"ComboBoxBase\"", ThemeXaml);
        Assert.Contains("ControlTemplate TargetType=\"{x:Type ComboBox}\"", ThemeXaml);
        Assert.Contains("x:Name=\"PART_EditableTextBox\"", ThemeXaml);
        Assert.Contains("x:Name=\"PART_Popup\"", ThemeXaml);
        Assert.Contains("Background\" Value=\"#0B1628\"", ThemeXaml);
        Assert.Contains("Foreground\" Value=\"{StaticResource TextMain}\"", ThemeXaml);
        Assert.Contains("Style TargetType=\"{x:Type ComboBoxItem}\"", ThemeXaml);
        Assert.Contains("Background=\"{StaticResource PanelSoft}\"", ThemeXaml);
        Assert.DoesNotContain("Foreground\" Value=\"#0F172A\"", ThemeXaml);
        Assert.DoesNotContain("Background=\"White\"", ThemeXaml);
    }
}
