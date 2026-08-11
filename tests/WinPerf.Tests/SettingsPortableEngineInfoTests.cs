namespace WinPerf.Tests;

public sealed class SettingsPortableEngineInfoTests
{
    private static readonly string SettingsWindowXamlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "SettingsWindow.xaml"));

    private static readonly string SettingsWindowCodePath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "SettingsWindow.xaml.cs"));

    [Fact]
    public void SettingsWindow_ShowsPortableIperf3EngineFolder()
    {
        var xaml = File.ReadAllText(SettingsWindowXamlPath);

        Assert.Contains("Portable iperf3 engine folder", xaml);
        Assert.Contains("x:Name=\"PortableIperf3EngineDirectoryText\"", xaml);
        Assert.Contains("x:Name=\"OpenPortableEngineDirectoryButton\"", xaml);
        Assert.Contains("Content=\"Open iperf3\"", xaml);
        Assert.Contains("Click=\"OpenPortableEngineDirectoryButton_Click\"", xaml);
    }

    [Fact]
    public void SettingsWindow_ShowsPortableIperf2EngineFolder()
    {
        var xaml = File.ReadAllText(SettingsWindowXamlPath);

        Assert.Contains("Portable iperf2 engine folder", xaml);
        Assert.Contains("x:Name=\"PortableIperf2EngineDirectoryText\"", xaml);
        Assert.Contains("x:Name=\"OpenPortableIperf2EngineDirectoryButton\"", xaml);
        Assert.Contains("Content=\"Open iperf2\"", xaml);
        Assert.Contains("Click=\"OpenPortableIperf2EngineDirectoryButton_Click\"", xaml);
    }

    [Fact]
    public void SettingsWindow_Iperf2BrowseDefaultsToAllExecutableFiles()
    {
        var code = File.ReadAllText(SettingsWindowCodePath);

        Assert.Contains("Select iperf2 executable", code);
        Assert.Contains("Executable files (*.exe)|*.exe|Common iperf2 names (iperf.exe;iperf2.exe)|iperf.exe;iperf2.exe|All files (*.*)|*.*", code);
    }

    [Fact]
    public void SettingsWindow_OpenPortableEngineFolderButtons_UsePortableEngineDirectories()
    {
        var code = File.ReadAllText(SettingsWindowCodePath);

        Assert.Contains("PortableIperf3EngineDirectoryText.Text = PortableIperf3EngineDirectory;", code);
        Assert.Contains("PortableIperf2EngineDirectoryText.Text = PortableIperf2EngineDirectory;", code);

        Assert.Contains("private void OpenPortableEngineDirectoryButton_Click", code);
        Assert.Contains("OpenDirectory(PortableIperf3EngineDirectory, AppText.T(\"portable iperf3 engine folder\"));", code);

        Assert.Contains("private void OpenPortableIperf2EngineDirectoryButton_Click", code);
        Assert.Contains("OpenDirectory(PortableIperf2EngineDirectory, AppText.T(\"portable iperf2 engine folder\"));", code);

        Assert.Contains("Directory.CreateDirectory(directory);", code);
        Assert.Contains("FileName = directory", code);
        Assert.Contains("UseShellExecute = true", code);
    }
}
