namespace WinPerf.Tests;

public sealed class ThemeShellColorResourceTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    [Fact]
    public void Theme_DefinesSharedShellColorResources()
    {
        var theme = File.ReadAllText(Path.Combine(AppDirectory, "ResourceDictionaries", "Theme.xaml"));

        Assert.Contains("x:Key=\"Bg\" Color=\"#08111F\"", theme);
        Assert.Contains("x:Key=\"PanelDark\" Color=\"#07101D\"", theme);
        Assert.Contains("x:Key=\"Accent\" Color=\"#38BDF8\"", theme);
        Assert.Contains("Value=\"{StaticResource PanelDark}\"", theme);
        Assert.Contains("Value=\"{StaticResource Accent}\"", theme);
        Assert.Contains("x:Key=\"ShellWindowBorder\"", theme);
        Assert.Contains("x:Key=\"ShellTitleBar\"", theme);
        Assert.Contains("Property=\"Background\" Value=\"{StaticResource PanelDark}\"", theme);
        Assert.Contains("Property=\"BorderBrush\" Value=\"{StaticResource Accent}\"", theme);
        Assert.DoesNotContain("Value=\"#07101D\"", theme);
        Assert.DoesNotContain("Value=\"#38BDF8\"", theme);
    }

    [Fact]
    public void Windows_UseThemeResourcesForShellColors()
    {
        var windowFiles = new[]
        {
            "MainWindow.xaml",
            "AdvancedCommandWindow.xaml",
            "CustomCommandWindow.xaml",
            "SettingsWindow.xaml",
            "AboutWindow.xaml",
            "SponsorProUpdatesWindow.xaml"
        };

        foreach (var fileName in windowFiles)
        {
            var xaml = File.ReadAllText(Path.Combine(AppDirectory, fileName));

            Assert.DoesNotContain("Background=\"#08111F\"", xaml);
            Assert.DoesNotContain("Background=\"#07101D\"", xaml);
            Assert.DoesNotContain("BorderBrush=\"#38BDF8\"", xaml);
            Assert.Contains("<app:AppWindowChrome />", xaml);
            Assert.Contains("Style=\"{StaticResource ShellWindowBorder}\"", xaml);
            Assert.Contains("Style=\"{StaticResource ShellTitleBar}\"", xaml);
        }
    }

    [Fact]
    public void Theme_DefinesSharedBaseStylesBeforeTheyAreReferenced()
    {
        var theme = File.ReadAllText(Path.Combine(AppDirectory, "ResourceDictionaries", "Theme.xaml"));

        AssertDefinedBeforeUse(theme, "x:Key=\"TextBlockBase\"", "BasedOn=\"{StaticResource TextBlockBase}\"");
        AssertDefinedBeforeUse(theme, "x:Key=\"ShellTitleButton\"", "BasedOn=\"{StaticResource ShellTitleButton}\"");
        AssertDefinedBeforeUse(theme, "x:Key=\"CardBase\"", "BasedOn=\"{StaticResource CardBase}\"");
        AssertDefinedBeforeUse(theme, "x:Key=\"PrimaryButtonBase\"", "BasedOn=\"{StaticResource PrimaryButtonBase}\"");
        AssertDefinedBeforeUse(theme, "x:Key=\"SecondaryButtonBase\"", "BasedOn=\"{StaticResource SecondaryButtonBase}\"");
    }

    private static void AssertDefinedBeforeUse(string text, string definition, string usage)
    {
        var definitionIndex = text.IndexOf(definition, StringComparison.Ordinal);
        var usageIndex = text.IndexOf(usage, StringComparison.Ordinal);

        Assert.True(definitionIndex >= 0, $"Missing definition: {definition}");
        Assert.True(usageIndex >= 0, $"Missing usage: {usage}");
        Assert.True(definitionIndex < usageIndex, $"{definition} must be defined before {usage}.");
    }
}
