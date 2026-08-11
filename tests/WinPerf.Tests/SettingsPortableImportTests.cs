namespace WinPerf.Tests;

public sealed class SettingsPortableImportTests
{
    private static string RepositoryRoot => FindRepositoryRoot();

    [Fact]
    public void Settings_ImportsSelectedExecutableIntoCanonicalPaths()
    {
        var code = ReadSettingsCode();

        Assert.Contains(
            "PortableExecutableImporter.Import(",
            code);
        Assert.Contains(
            "PortableIperf3ExecutablePath",
            code);
        Assert.Contains(
            "PortableIperf2ExecutablePath",
            code);
        Assert.Contains(
            "Path.Combine(PortableIperf3EngineDirectory, \"iperf3.exe\")",
            code);
        Assert.Contains(
            "Path.Combine(PortableIperf2EngineDirectory, \"iperf.exe\")",
            code);
    }

    [Fact]
    public void Settings_DoesNotCopyEntireSourceDirectory()
    {
        var code = ReadSettingsCode();

        Assert.DoesNotContain("CopyDirectory(", code);
        Assert.DoesNotContain(
            "Directory.EnumerateDirectories",
            code);
        Assert.DoesNotContain(
            "Directory.EnumerateFiles",
            code);
    }

    [Fact]
    public void Settings_AcceptsIperf2ExecutableFromAnyFolderOrName()
    {
        var code = ReadSettingsCode();

        Assert.Contains(
            "Executable files (*.exe)|*.exe",
            code);
        Assert.Contains(
            "Select iperf2 executable",
            code);
        Assert.DoesNotContain(
            "PortableIperf2AlternateExecutablePath",
            code);
    }

    private static string ReadSettingsCode()
    {
        return File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src",
                "WinPerf.App",
                "SettingsWindow.xaml.cs"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(
                Path.Combine(directory.FullName, "WinPerf.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository root.");
    }
}
