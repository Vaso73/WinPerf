namespace WinPerf.Tests;

public sealed class SettingsPortableEngineInfoTests
{
    private static readonly string SettingsWindowXamlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "SettingsWindow.xaml"));

    private static readonly string SettingsWindowCodePath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "SettingsWindow.xaml.cs"));

    [Fact]
    public void SettingsWindow_ShowsPortableEngineFolder()
    {
        var xaml = File.ReadAllText(SettingsWindowXamlPath);

        Assert.Contains("Portable engine folder", xaml);
        Assert.Contains("x:Name=\"PortableEngineDirectoryText\"", xaml);
        Assert.Contains("x:Name=\"OpenPortableEngineDirectoryButton\"", xaml);
        Assert.Contains("Content=\"Open engine folder\"", xaml);
        Assert.Contains("Click=\"OpenPortableEngineDirectoryButton_Click\"", xaml);
    }

    [Fact]
    public void SettingsWindow_OpenPortableEngineFolderButton_UsesPortableEngineDirectory()
    {
        var code = File.ReadAllText(SettingsWindowCodePath);

        Assert.Contains("PortableEngineDirectoryText.Text = PortableEngineDirectory;", code);
        Assert.Contains("private void OpenPortableEngineDirectoryButton_Click", code);
        Assert.Contains("Directory.CreateDirectory(PortableEngineDirectory);", code);
        Assert.Contains("FileName = PortableEngineDirectory", code);
        Assert.Contains("UseShellExecute = true", code);
    }
}
