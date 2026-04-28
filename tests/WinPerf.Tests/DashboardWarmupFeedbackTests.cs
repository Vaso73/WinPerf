namespace WinPerf.Tests;

public sealed class DashboardWarmupFeedbackTests
{
    [Fact]
    public void Dashboard_ShowsFeedbackDuringOmittedWarmup()
    {
        var code = ReadMainWindowCode();

        Assert.Contains("_activeOmitSeconds = Math.Max(0, options.OmitSeconds);", code);
        Assert.Contains("_omittedWarmupIntervalsReceived = 0;", code);
        Assert.Contains("Warm-up: omitting first", code);
        Assert.Contains("private void HandleOmittedWarmupSample(IperfIntervalSample sample)", code);
        Assert.Contains("Warm-up {elapsed}/{_activeOmitSeconds}s omitted", code);
        Assert.Contains("ShowWarmupChartPlaceholder", code);
        Assert.Contains("Ignoring warm-up samples. Live chart starts after warm-up.", code);
    }

    [Fact]
    public void Dashboard_DoesNotAddOmittedWarmupSamplesToLiveMetrics()
    {
        var code = ReadMainWindowCode();

        Assert.Contains("if (sample.Omitted)", code);
        Assert.Contains("HandleOmittedWarmupSample(sample);", code);
        Assert.Contains("return;", code);
        Assert.Contains("UpdateLiveMetrics(sample);", code);
    }

    private static string ReadMainWindowCode()
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, "src", "WinPerf.App", "MainWindow.xaml.cs"));
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
