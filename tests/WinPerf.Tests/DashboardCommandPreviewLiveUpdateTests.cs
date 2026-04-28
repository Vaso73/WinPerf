using System.Text.RegularExpressions;

namespace WinPerf.Tests;

public sealed class DashboardCommandPreviewLiveUpdateTests
{
    private static string ReadMainWindowXaml()
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, "src", "WinPerf.App", "MainWindow.xaml"));
    }

    private static string ReadMainWindowCode()
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, "src", "WinPerf.App", "MainWindow.xaml.cs"));
    }

    [Fact]
    public void ServerInput_UpdatesCommandPreviewWhileTyping()
    {
        var xaml = ReadMainWindowXaml();

        var serverBox = ExtractControl(xaml, "ComboBox", "ServerBox");

        Assert.Contains("TextBoxBase.TextChanged=\"DashboardInputChanged\"", serverBox);
        Assert.Contains("SelectionChanged=\"DashboardInputChanged\"", serverBox);
        Assert.Contains("KeyUp=\"DashboardInputChanged\"", serverBox);
        Assert.Contains("LostFocus=\"DashboardInputChanged\"", serverBox);
    }

    [Fact]
    public void NumericInputs_UpdateCommandPreviewWhileTyping()
    {
        var xaml = ReadMainWindowXaml();

        foreach (var name in new[] { "PortBox", "StreamsBox", "DurationBox" })
        {
            var textBox = ExtractControl(xaml, "TextBox", name);

            Assert.Contains("TextChanged=\"DashboardInputChanged\"", textBox);
            Assert.Contains("KeyUp=\"DashboardInputChanged\"", textBox);
            Assert.Contains("LostFocus=\"DashboardInputChanged\"", textBox);
        }
    }

    [Fact]
    public void ModeSelection_UpdatesCommandPreviewImmediately()
    {
        var xaml = ReadMainWindowXaml();

        var modeBox = ExtractControl(xaml, "ComboBox", "ModeBox");

        Assert.Contains("SelectionChanged=\"DashboardInputChanged\"", modeBox);
    }

    [Fact]
    public void EngineSelectionChanged_GuardsDuringXamlInitialization()
    {
        var code = ReadMainWindowCode();

        Assert.Contains("if (EngineBox is null || PortBox is null)", code);
    }

    [Fact]
    public void EngineSelection_UpdatesCommandPreviewAndEngineStatusImmediately()
    {
        var xaml = ReadMainWindowXaml();

        var engineBox = ExtractControl(xaml, "ComboBox", "EngineBox");

        Assert.Contains("SelectionChanged=\"EngineSelectionChanged\"", engineBox);
        Assert.Contains("x:Name=\"EngineBox\"", xaml);
        Assert.Contains("<ComboBoxItem Content=\"iperf3\" />", xaml);
        Assert.Contains("<ComboBoxItem Content=\"iperf2\" />", xaml);
    }

    private static string ExtractControl(string xaml, string controlName, string xName)
    {
        var pattern = $@"<{controlName}\s+x:Name=""{Regex.Escape(xName)}""[\s\S]*?(/>|>)";
        var match = Regex.Match(xaml, pattern);

        Assert.True(match.Success, $"{controlName} {xName} was not found.");

        return match.Value;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WinPerf.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
