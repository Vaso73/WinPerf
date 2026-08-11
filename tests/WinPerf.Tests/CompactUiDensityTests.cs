namespace WinPerf.Tests;

public sealed class UnifiedCompactLayoutTests
{
    private static string ReadXaml(string fileName)
    {
        return File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", fileName));
    }

    [Fact]
    public void MainWindow_UsesCompactDefaultSize()
    {
        var xaml = ReadXaml("MainWindow.xaml");

        Assert.Contains("Width=\"1080\"", xaml);
        Assert.Contains("Height=\"720\"", xaml);
        Assert.Contains("MinWidth=\"760\"", xaml);
        Assert.Contains("MinHeight=\"520\"", xaml);
        Assert.Contains("x:Name=\"LeftRailColumn\" Width=\"360\" MinWidth=\"280\" MaxWidth=\"500\"", xaml);
        Assert.Contains("x:Name=\"DashboardContentPanel\" Grid.Column=\"2\" Margin=\"18\"", xaml);
        Assert.Contains("x:Name=\"MetricsRow\" Height=\"150\"", xaml);
        Assert.Contains("x:Name=\"LiveThroughputRow\" Height=\"*\" MinHeight=\"260\"", xaml);
        Assert.Contains("x:Name=\"EngineOutputRow\" Height=\"180\" MinHeight=\"110\" MaxHeight=\"260\"", xaml);
        Assert.Contains("<ScrollViewer VerticalScrollBarVisibility=\"Auto\"", xaml);
    }

    [Fact]
    public void Dialogs_UseCompactDefaultSizes()
    {
        var advanced = ReadXaml("AdvancedCommandWindow.xaml");
        var custom = ReadXaml("CustomCommandWindow.xaml");
        var settings = ReadXaml("SettingsWindow.xaml");
        var about = ReadXaml("AboutWindow.xaml");
        var updates = ReadXaml("SponsorProUpdatesWindow.xaml");

        Assert.Contains("Width=\"980\"", advanced);
        Assert.Contains("Height=\"680\"", advanced);
        Assert.Contains("MinWidth=\"900\"", advanced);
        Assert.Contains("MinHeight=\"600\"", advanced);
        Assert.Contains("Width=\"140\"", advanced);

        Assert.Contains("Width=\"760\"", custom);
        Assert.Contains("Height=\"500\"", custom);
        Assert.Contains("MinWidth=\"640\"", custom);
        Assert.Contains("MinHeight=\"440\"", custom);
        Assert.Contains("Width=\"140\"", custom);

        Assert.Contains("Width=\"720\"", settings);
        Assert.Contains("Height=\"460\"", settings);
        Assert.Contains("MinWidth=\"660\"", settings);
        Assert.Contains("MinHeight=\"420\"", settings);

        Assert.Contains("Width=\"620\"", about);
        Assert.Contains("Height=\"500\"", about);
        Assert.Contains("MinWidth=\"600\"", about);
        Assert.Contains("MinHeight=\"480\"", about);
        Assert.Contains("ResizeMode=\"NoResize\"", about);

        Assert.Contains("Width=\"760\"", updates);
        Assert.Contains("Height=\"560\"", updates);
        Assert.Contains("MinWidth=\"680\"", updates);
        Assert.Contains("MinHeight=\"500\"", updates);
    }

    [Fact]
    public void WindowChrome_UsesCompactTitleBars()
    {
        var combined =
            ReadXaml("MainWindow.xaml") +
            ReadXaml("AdvancedCommandWindow.xaml") +
            ReadXaml("CustomCommandWindow.xaml") +
            ReadXaml("SettingsWindow.xaml") +
            ReadXaml("AboutWindow.xaml") +
            ReadXaml("SponsorProUpdatesWindow.xaml");

        Assert.DoesNotContain("CaptionHeight=\"42\"", combined);
        Assert.DoesNotContain("<RowDefinition Height=\"42\" />", combined);
        Assert.DoesNotContain("Width=\"46\"", combined);
        Assert.Contains("CaptionHeight=\"38\"", combined);
        Assert.Contains("Width=\"40\"", combined);
    }

    [Fact]
    public void MainWindow_UsesOneCleanAppMenuWithoutUiDensityChoices()
    {
        var xaml = ReadXaml("MainWindow.xaml");

        Assert.Contains("x:Name=\"AppMenuButton\"", xaml);
        Assert.Contains("Content=\"App ▾\"", xaml);
        Assert.Contains("x:Name=\"AppContextMenu\"", xaml);
        Assert.Contains("x:Name=\"SettingsMenuItem\"", xaml);
        Assert.Contains("x:Name=\"UpdatesMenuItem\"", xaml);
        Assert.Contains("x:Name=\"AboutMenuItem\"", xaml);
        Assert.DoesNotContain("UiDensityButton", xaml);
        Assert.DoesNotContain("CompactUiDensityMenuItem", xaml);
        Assert.DoesNotContain("ComfortableUiDensityMenuItem", xaml);
        Assert.DoesNotContain("UI: Compact", xaml);
        Assert.DoesNotContain("UI: Comfortable", xaml);
    }
}
