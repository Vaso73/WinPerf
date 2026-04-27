using System.Text.RegularExpressions;

namespace WinPerf.Tests;

public sealed class CommandOverrideUxTests
{
    private static readonly string MainWindowSource = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml.cs"));

    private static readonly string MainWindowXaml = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "MainWindow.xaml"));

    [Fact]
    public void DashboardCommandOverrideUx_HasVisibleBadgeAndClearAction()
    {
        Assert.Contains("CommandOverridePanel", MainWindowXaml);
        Assert.Contains("CommandOverrideBadgeText", MainWindowXaml);
        Assert.Contains("ClearCommandOverrideButton", MainWindowXaml);
        Assert.Contains("Command override active", MainWindowXaml);
        Assert.Contains("ClearCommandOverrideButton_Click", MainWindowXaml);
    }

    [Fact]
    public void AdvancedAndCustomCommands_SetTypedCommandOverride()
    {
        Assert.Contains("AdvancedCommandOverrideSource", MainWindowSource);
        Assert.Contains("CustomCommandOverrideSource", MainWindowSource);
        Assert.Matches(
            new Regex(@"SetCommandOverride\(AdvancedCommandOverrideSource,\s*NormalizeCustomCommandText\(dialog\.CommandText\)\)", RegexOptions.Singleline),
            MainWindowSource);
        Assert.Matches(
            new Regex(@"SetCommandOverride\(CustomCommandOverrideSource,\s*NormalizeCustomCommandText\(dialog\.CommandText\)\)", RegexOptions.Singleline),
            MainWindowSource);
    }

    [Fact]
    public void DashboardInputChanged_ClearsCommandOverride()
    {
        Assert.Contains("ClearCommandOverride(updatePreview: false)", MainWindowSource);
        Assert.Contains("ClearCommandOverride(updatePreview: true)", MainWindowSource);
        Assert.Contains("UpdateCommandOverrideUx();", MainWindowSource);
    }

    [Fact]
    public void StartButton_UsesCommandOverrideWhenPresent()
    {
        Assert.Contains("var hasCommandOverride = !string.IsNullOrWhiteSpace(commandOverrideArguments);", MainWindowSource);
        Assert.Contains("new IperfCommand(_engineResolution.ExecutablePath, SplitCommandLine(commandOverrideArguments!))", MainWindowSource);
        Assert.Contains("if (!hasCommandOverride)", MainWindowSource);
    }
}
