namespace WinPerf.Tests;

public sealed class DashboardThroughputChartLabelTests
{
    private static string RepositoryRoot => FindRepositoryRoot();

    [Fact]
    public void Dashboard_LabelsTotalAndPerStreamThroughputClearly()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src",
                "WinPerf.App",
                "MainWindow.xaml"));
        var code = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src",
                "WinPerf.App",
                "MainWindow.xaml.cs"));

        Assert.Contains("Total bandwidth", xaml);
        Assert.Contains("Live Total Throughput", xaml);
        Assert.Contains("Live total average", xaml);
        Assert.Contains("AppText.T(\"Total bandwidth\")", code);
        Assert.Contains("BuildPerStreamScaleLabel(streamAxisMax, streamCount)", code);
        Assert.Contains("Per-stream: {0} streams · avg {1} · min {2} · max {3} · scale 0-{4}", code);
        Assert.Contains("Per-stream: {0} streams · scale 0-{1}", code);
        Assert.Contains("total {0}   min {1} · avg {2} · max {3}", code);
        Assert.Contains("Server received total", code);
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
