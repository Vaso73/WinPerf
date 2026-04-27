namespace WinPerf.Tests;

public sealed class SettingsPortableDataInfoTests
{
    private static readonly string SettingsWindowXamlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "SettingsWindow.xaml"));

    private static readonly string SettingsWindowCodePath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "SettingsWindow.xaml.cs"));

    [Fact]
    public void SettingsWindow_ShowsPortableDataFolder()
    {
        var xaml = File.ReadAllText(SettingsWindowXamlPath);

        Assert.Contains("Portable data folder", xaml);
        Assert.Contains("x:Name=\"DataDirectoryText\"", xaml);
        Assert.Contains("x:Name=\"OpenDataDirectoryButton\"", xaml);
        Assert.Contains("Content=\"Open data folder\"", xaml);
        Assert.Contains("Click=\"OpenDataDirectoryButton_Click\"", xaml);
    }

    [Fact]
    public void SettingsWindow_OpenDataDirectoryButton_UsesPortableDataDirectory()
    {
        var code = File.ReadAllText(SettingsWindowCodePath);

        Assert.Contains("DataDirectoryText.Text = DataDirectory;", code);
        Assert.Contains("Path.Combine(_appDirectory, \"data\")", code);
        Assert.Contains("private void OpenDataDirectoryButton_Click", code);
        Assert.Contains("Directory.CreateDirectory(DataDirectory);", code);
        Assert.Contains("UseShellExecute = true", code);
    }
}
