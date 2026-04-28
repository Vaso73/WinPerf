namespace WinPerf.Tests;

public sealed class AppVersionFooterTests
{
    [Fact]
    public void MainWindowFooterShowsRuntimeAppVersion()
    {
        var xaml = ReadRepoFile("src", "WinPerf.App", "MainWindow.xaml");
        var code = ReadRepoFile("src", "WinPerf.App", "MainWindow.xaml.cs");

        Assert.Contains("x:Name=\"AppVersionText\"", xaml);
        Assert.Contains("Application version from the running executable.", xaml);
        Assert.Contains("AppVersionText.Text = ResolveAppVersionText();", code);
        Assert.Contains("AssemblyInformationalVersionAttribute", code);
        Assert.Contains("WinPerf v{version}", code);
    }

    [Fact]
    public void MainWindowFooterKeepsEngineStatusClickable()
    {
        var xaml = ReadRepoFile("src", "WinPerf.App", "MainWindow.xaml");

        Assert.Contains("DockPanel.Dock=\"Left\"", xaml);
        Assert.Contains("x:Name=\"EngineStatusText\"", xaml);
        Assert.Contains("ToolTip=\"Click to configure iperf3 engine\"", xaml);
        Assert.Contains("MouseLeftButtonUp=\"EngineStatusText_MouseLeftButtonUp\"", xaml);

        var versionIndex = xaml.IndexOf("x:Name=\"AppVersionText\"", StringComparison.Ordinal);
        var engineIndex = xaml.IndexOf("x:Name=\"EngineStatusText\"", StringComparison.Ordinal);

        Assert.True(versionIndex >= 0);
        Assert.True(engineIndex > versionIndex);
    }

    private static string ReadRepoFile(params string[] relativePath)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine([root, .. relativePath]));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
