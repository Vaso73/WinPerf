namespace WinPerf.Tests;

public sealed class DashboardControlLayoutPolishTests
{
    private static readonly string MainWindowXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml"));

    private static readonly string MainWindowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml.cs"));

    [Fact]
    public void Dashboard_StartStopButtonsShareAvailableWidth()
    {
        Assert.Contains("x:Name=\"RunButtonGrid\"", MainWindowXaml);
        Assert.Contains("<ColumnDefinition Width=\"8\" />", MainWindowXaml);
        Assert.Contains("x:Name=\"StartButton\"", MainWindowXaml);
        Assert.Contains("x:Name=\"StopButton\"", MainWindowXaml);
        Assert.Contains("MinHeight=\"36\"", MainWindowXaml);
        Assert.DoesNotMatch("x:Name=\\\"StartButton\\\"[\\s\\S]{0,500}Width=\\\"104\\\"", MainWindowXaml);
        Assert.DoesNotMatch("x:Name=\\\"StopButton\\\"[\\s\\S]{0,500}Width=\\\"84\\\"", MainWindowXaml);
    }

    [Fact]
    public void Dashboard_CommandAndRemoveButtonsUseConsistentControlSizing()
    {
        Assert.Contains("x:Name=\"CommandMenuButton\"", MainWindowXaml);
        Assert.Contains("Content=\"Command ▾\"", MainWindowXaml);
        Assert.Contains("x:Name=\"RemoveServerButton\"", MainWindowXaml);
        Assert.Contains("Style=\"{StaticResource CompactSecondaryButton}\"", SliceAround(MainWindowXaml, "x:Name=\"RemoveServerButton\""));
        Assert.Contains("MinHeight=\"30\"", SliceAround(MainWindowXaml, "x:Name=\"RemoveServerButton\""));
        Assert.Contains("FontSize=\"11\"", MainWindowXaml);
        Assert.Contains("<ColumnDefinition Width=\"76\" />", MainWindowXaml);
    }

    [Fact]
    public void AppMenu_UsesTitleBarDropdownForAppActions()
    {
        Assert.Contains("x:Name=\"AppMenuButton\"", MainWindowXaml);
        Assert.Contains("Content=\"⋯\"", MainWindowXaml);
        Assert.Contains("Style=\"{StaticResource ShellMenuButton}\"", MainWindowXaml);
        Assert.Contains("x:Name=\"AppContextMenu\"", MainWindowXaml);
        Assert.Contains("x:Name=\"SettingsMenuItem\"", MainWindowXaml);
        Assert.Contains("x:Name=\"UpdatesMenuItem\"", MainWindowXaml);
        Assert.Contains("x:Name=\"AboutMenuItem\"", MainWindowXaml);
        Assert.Contains("private void AppMenuButton_Click", MainWindowSource);
        Assert.DoesNotContain("x:Name=\"AppMenuSection\"", MainWindowXaml);
        Assert.DoesNotContain("SidebarAppButton", MainWindowXaml);
        Assert.DoesNotContain("UiDensityButton", MainWindowXaml);
    }

    [Fact]
    public void Dashboard_SplittersAreVisuallySubtle()
    {
        Assert.Contains("Width=\"4\"", MainWindowXaml);
        Assert.Contains("Height=\"4\"", MainWindowXaml);
        Assert.Contains("Background=\"{StaticResource SplitterBackground}\"", MainWindowXaml);
        Assert.Contains("Opacity=\"0.55\"", MainWindowXaml);
    }

    private static string SliceAround(string text, string marker)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0);

        var start = Math.Max(0, index - 300);
        var length = Math.Min(text.Length - start, 900);

        return text.Substring(start, length);
    }
}
