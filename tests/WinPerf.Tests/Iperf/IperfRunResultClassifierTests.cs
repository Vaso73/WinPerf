using WinPerf.Core.Iperf;

namespace WinPerf.Tests.Iperf;

public sealed class IperfRunResultClassifierTests
{
    [Fact]
    public void Classify_CleanIperf2Run_IsCompleted()
    {
        var result = CreateResult(
            0,
            Stdout("[  1] 0.00-1.00 sec 111 MBytes 929 Mbits/sec"));

        var outcome = IperfRunResultClassifier.Classify(
            IperfEngine.Iperf2,
            result);

        Assert.Equal(IperfRunOutcomeKind.Completed, outcome.Kind);
        Assert.Equal("Test completed.", outcome.Message);
    }

    [Fact]
    public void Classify_Iperf2ConnectionRefusedWithExitCodeZero_IsFailed()
    {
        var result = CreateResult(
            0,
            Stderr(
                "[  1] tcp connect to 10.100.100.221 port 59999 failed " +
                "(Connection refused)"));

        var outcome = IperfRunResultClassifier.Classify(
            IperfEngine.Iperf2,
            result);

        Assert.Equal(IperfRunOutcomeKind.Failed, outcome.Kind);
        Assert.Contains("Connection refused", outcome.Message);
    }

    [Fact]
    public void Classify_Iperf2UdpAckWarningWithSamples_IsCompletedWithWarning()
    {
        var result = CreateResult(
            0,
            Stdout("[  1] 0.00-1.00 sec 1.25 MBytes 10.5 Mbits/sec"),
            Stderr("[  1] Read UDP fin failed: Connection reset by peer"),
            Stderr(
                "[336] WARNING: did not receive ack of last datagram " +
                "after 10 tries."));

        var outcome = IperfRunResultClassifier.Classify(
            IperfEngine.Iperf2,
            result);

        Assert.Equal(
            IperfRunOutcomeKind.CompletedWithWarning,
            outcome.Kind);
        Assert.Contains("did not receive ack", outcome.Message);
    }

    [Fact]
    public void Classify_Iperf2StderrWithoutSamples_IsFailed()
    {
        var result = CreateResult(
            0,
            Stderr("Unexpected iperf2 runtime warning."));

        var outcome = IperfRunResultClassifier.Classify(
            IperfEngine.Iperf2,
            result);

        Assert.Equal(IperfRunOutcomeKind.Failed, outcome.Kind);
    }

    [Fact]
    public void Classify_NonZeroExitCode_IsFailed()
    {
        var result = CreateResult(2);

        var outcome = IperfRunResultClassifier.Classify(
            IperfEngine.Iperf2,
            result);

        Assert.Equal(IperfRunOutcomeKind.Failed, outcome.Kind);
        Assert.Contains("code 2", outcome.Message);
    }

    [Fact]
    public void Classify_Iperf3ExitCodeZero_PreservesExistingBehaviour()
    {
        var result = CreateResult(
            0,
            Stderr("Non-fatal iperf3 diagnostic output."));

        var outcome = IperfRunResultClassifier.Classify(
            IperfEngine.Iperf3,
            result);

        Assert.Equal(IperfRunOutcomeKind.Completed, outcome.Kind);
    }

    private static IperfRunResult CreateResult(
        int exitCode,
        params IperfProcessOutputLine[] output)
    {
        return new IperfRunResult(
            exitCode,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(1),
            output);
    }

    private static IperfProcessOutputLine Stdout(string text)
    {
        return new IperfProcessOutputLine(
            IperfOutputStream.StandardOutput,
            text,
            DateTimeOffset.UnixEpoch);
    }

    private static IperfProcessOutputLine Stderr(string text)
    {
        return new IperfProcessOutputLine(
            IperfOutputStream.StandardError,
            text,
            DateTimeOffset.UnixEpoch);
    }
}
