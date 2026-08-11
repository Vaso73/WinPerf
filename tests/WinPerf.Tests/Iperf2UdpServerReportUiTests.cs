namespace WinPerf.Tests;

public sealed class Iperf2UdpServerReportUiTests
{
    [Fact]
    public void Dashboard_DefersUdpResultUntilProcessCompletion()
    {
        var root = FindRepositoryRoot();
        var code = Read(
            root,
            "src",
            "WinPerf.App",
            "MainWindow.xaml.cs");

        Assert.Contains(
            "? \"pending\"",
            code);
        Assert.Contains(
            "\"Awaiting server result\"",
            code);
        Assert.Contains(
            "keepIperf2UdpResultPending",
            code);
        Assert.Contains(
            "ReconcileFinalIperf2UdpServerReport(",
            code);

        var handlerStart = code.IndexOf(
            "private bool TryHandleStructuredIperfOutput",
            StringComparison.Ordinal);

        var reconcileStart = code.IndexOf(
            "private int ReconcileFinalIperf2UdpServerReport",
            StringComparison.Ordinal);

        Assert.True(handlerStart >= 0);
        Assert.True(reconcileStart > handlerStart);

        var handler = code[
            handlerStart..reconcileStart];

        Assert.DoesNotContain(
            "ApplyIperf2UdpServerReport(",
            handler);
        Assert.DoesNotContain(
            "_iperf2UdpServerReport = udpServerReport",
            handler);
    }

    [Fact]
    public void Dashboard_AggregatesAllParallelUdpServerReports()
    {
        var root = FindRepositoryRoot();
        var code = Read(
            root,
            "src",
            "WinPerf.App",
            "MainWindow.xaml.cs");

        Assert.Contains(
            "TryAggregateUdpServerReports(",
            code);
        Assert.Contains(
            "reports.Count",
            code);
        Assert.Contains(
            "options.Streams",
            code);
        Assert.Contains(
            "incomplete iperf2 UDP server report",
            code);
        Assert.Contains(
            "hasAuthoritativeIperf2UdpServerResult",
            code);
        Assert.Contains(
            "receivedIperf2UdpReportCount == options.Streams",
            code);
        Assert.Contains(
            "Server result unavailable",
            code);
        Assert.Contains(
            "ThroughputCaptionText.Text = \"Server received total\";",
            code);
    }

    [Fact]
    public void Classifier_TreatsWriteFinFailureAsFatal()
    {
        var root = FindRepositoryRoot();
        var code = Read(
            root,
            "src",
            "WinPerf.Core",
            "Iperf",
            "IperfRunResultClassifier.cs");

        Assert.Contains(
            "Contains(text, \"write-fin failed\")",
            code);
    }

    private static string Read(
        string root,
        params string[] parts)
    {
        return File.ReadAllText(
            Path.Combine(
                new[] { root }.Concat(parts).ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        var directory =
            new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(
                Path.Combine(
                    directory.FullName,
                    "WinPerf.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository root.");
    }
}
