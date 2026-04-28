namespace WinPerf.Tests;

public sealed class DashboardChartDurationAxisTests
{
    [Fact]
    public void DashboardChart_UsesMeasuredRunDurationForTimeAxis()
    {
        var code = ReadMainWindowCode();

        Assert.Contains("_activeChartDurationSeconds = Math.Max(1, options.DurationSeconds);", code);
        Assert.Contains("var timeAxisMaxSeconds = Math.Max(1, _activeChartDurationSeconds);", code);
        Assert.Contains("DrawThroughputChartFrame(plotLeft, plotTop, plotWidth, plotHeight, axis.Min, axis.Max, axis.Step, timeAxisMaxSeconds)", code);
        Assert.Contains("BuildTimeAxisTicks(timeAxisMaxSeconds)", code);
        Assert.DoesNotContain("_activeChartOmitSeconds", code);
        Assert.DoesNotContain("const int verticalSteps = 10;", code);
    }

    [Fact]
    public void DashboardChart_MapsSamplesProgressivelyAcrossMeasuredDuration()
    {
        var code = ReadMainWindowCode();

        Assert.Contains("CalculateSampleX(plotLeft, plotWidth, i, _throughputSamples.Count, timeAxisMaxSeconds)", code);
        Assert.Contains("CalculateSampleX(plotLeft, plotWidth, i, _reverseThroughputSamples.Count, timeAxisMaxSeconds)", code);
        Assert.Contains("CalculateSampleX(plotLeft, plotWidth, sampleIndex, samples.Count, timeAxisMaxSeconds)", code);
        Assert.Contains("var elapsedSeconds = Math.Min(timeAxisMaxSeconds, sampleIndex + 1);", code);
        Assert.DoesNotContain("omittedSeconds", code);
    }

    [Fact]
    public void DashboardChart_IgnoresOmittedWarmupIntervals()
    {
        var code = ReadMainWindowCode();

        Assert.Contains("if (sample.Omitted)", code);
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
