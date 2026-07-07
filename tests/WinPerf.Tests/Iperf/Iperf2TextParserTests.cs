using WinPerf.Core.Iperf;

namespace WinPerf.Tests.Iperf;

public sealed class Iperf2TextParserTests
{
    [Fact]
    public void TryParseIntervalSample_ParsesMbitsInterval()
    {
        const string line = "[  3]  0.0- 1.0 sec   112 MBytes   941 Mbits/sec";

        var ok = Iperf2TextParser.TryParseIntervalSample(line, out var sample);

        Assert.True(ok);
        Assert.Equal(1, sample.Seconds);
        Assert.Equal(941_000_000, sample.BitsPerSecond);
        Assert.Equal(941, sample.MegabitsPerSecond);
        Assert.Null(sample.JitterMs);
        Assert.Null(sample.LostPercent);
    }

    [Fact]
    public void TryParseIntervalSample_ParsesGbitsInterval()
    {
        const string line = "[  5]   4.00-5.01   sec  3.87 GBytes  33.0 Gbits/sec";

        var ok = Iperf2TextParser.TryParseIntervalSample(line, out var sample);

        Assert.True(ok);
        Assert.Equal(1.01, sample.Seconds!.Value, precision: 2);
        Assert.Equal(33_000_000_000, sample.BitsPerSecond);
        Assert.Equal(33000, sample.MegabitsPerSecond);
    }

    [Fact]
    public void TryParseIntervalSample_IgnoresAggregateSummaryLine()
    {
        const string line = "[  4]  0.0- 5.0 sec  5448 MBytes  9140 Mbits/sec";

        var ok = Iperf2TextParser.TryParseIntervalSample(line, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseIntervalSample_IgnoresNonIntervalText()
    {
        var ok = Iperf2TextParser.TryParseIntervalSample("Server listening on 5001", out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseIntervalSample_WhenPreferSumIgnoresPerStreamLine()
    {
        const string line = "[400]  1.0- 2.0 sec  1.23 MBytes  10.3 Mbits/sec";

        var ok = Iperf2TextParser.TryParseIntervalSample(line, out _, preferSum: true);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseIntervalSample_WhenPreferSumParsesCumulativeSumLine()
    {
        const string line = "[SUM]  0.0- 3.0 sec  19.2 MBytes  80.7 Mbits/sec";

        var ok = Iperf2TextParser.TryParseIntervalSample(line, out var sample, preferSum: true, maxEndSeconds: 10);

        Assert.True(ok);
        Assert.Equal(3.0, sample.Seconds);
        Assert.Equal(80.7, sample.MegabitsPerSecond!.Value, precision: 1);
    }

    [Fact]
    public void TryParseIntervalSample_WhenPreferSumIgnoresFinalAggregateBeyondExpectedDuration()
    {
        const string line = "[SUM]  0.0-12.4 sec   118 MBytes  79.2 Mbits/sec";

        var ok = Iperf2TextParser.TryParseIntervalSample(line, out _, preferSum: true, maxEndSeconds: 10);

        Assert.False(ok);
    }


    [Fact]
    public void TryParseUdpServerReport_ParsesZeroLossReport()
    {
        const string line =
            "[  1] 0.00-10.01 sec  12.5 MBytes  10.5 Mbits/sec   0.000 ms 0/8910 (0%)";

        var ok = Iperf2TextParser.TryParseUdpServerReport(
            line,
            out var sample);

        Assert.True(ok);
        Assert.Null(sample.Seconds);
        Assert.Equal(10_500_000, sample.BitsPerSecond);
        Assert.Equal(10.5, sample.MegabitsPerSecond);
        Assert.Equal(0.000, sample.JitterMs);
        Assert.Equal(0, sample.LostPercent);
        Assert.Equal(
            0d,
            sample.EffectiveLostPercent!.Value);
        Assert.Equal(
            0L,
            sample.LostDatagrams!.Value);
        Assert.Equal(
            8910L,
            sample.TotalDatagrams!.Value);
    }

    [Fact]
    public void TryParseUdpServerReport_ParsesNonZeroLossReport()
    {
        const string line =
            "[  1] 0.00-10.00 sec  12.1 MBytes  10.1 Mbits/sec   0.125 ms 12/8910 (0.13%)";

        var ok = Iperf2TextParser.TryParseUdpServerReport(
            line,
            out var sample);

        Assert.True(ok);
        Assert.Equal(10_100_000, sample.BitsPerSecond);
        Assert.Equal(10.1, sample.MegabitsPerSecond);
        Assert.Equal(0.125, sample.JitterMs);
        Assert.Equal(0.13, sample.LostPercent);
        Assert.Equal(
            12L,
            sample.LostDatagrams!.Value);
        Assert.Equal(
            8910L,
            sample.TotalDatagrams!.Value);
        Assert.Equal(
            12d * 100d / 8910d,
            sample.EffectiveLostPercent!.Value,
            precision: 10);
    }

    [Fact]
    public void TryParseUdpServerReport_IgnoresNormalInterval()
    {
        const string line =
            "[  1] 1.00-2.00 sec  1.25 MBytes  10.5 Mbits/sec";

        var ok = Iperf2TextParser.TryParseUdpServerReport(
            line,
            out _);

        Assert.False(ok);
    }


    [Fact]
    public void TryParseUdpServerReport_ParsesSaturatedWifiResult()
    {
        const string line =
            "[  1] 0.00-10.21 sec   365 MBytes   300 Mbits/sec   0.000 ms 630768/891282 (70%)";

        var ok = Iperf2TextParser.TryParseUdpServerReport(
            line,
            out var sample);

        Assert.True(ok);
        Assert.Equal(300, sample.MegabitsPerSecond);
        Assert.Equal(0, sample.JitterMs);
        Assert.Equal(70, sample.LostPercent);
        Assert.Equal(
            630768L,
            sample.LostDatagrams!.Value);
        Assert.Equal(
            891282L,
            sample.TotalDatagrams!.Value);
        Assert.Equal(
            630768d * 100d / 891282d,
            sample.EffectiveLostPercent!.Value,
            precision: 10);
    }


    [Fact]
    public void TryAggregateUdpServerReports_SumsParallelStreams()
    {
        var reports = new[]
        {
            new IperfIntervalSample(
                null,
                105_000_000,
                0.001,
                0,
                LostDatagrams: 1,
                TotalDatagrams: 26752),
            new IperfIntervalSample(
                null,
                105_000_000,
                0.000,
                0,
                LostDatagrams: 0,
                TotalDatagrams: 26752)
        };

        var ok =
            Iperf2TextParser.TryAggregateUdpServerReports(
                reports,
                expectedStreamCount: 2,
                out var aggregate);

        Assert.True(ok);
        Assert.Equal(
            210,
            aggregate.MegabitsPerSecond);
        Assert.Equal(
            0.001,
            aggregate.JitterMs);
        Assert.Equal(
            1L,
            aggregate.LostDatagrams);
        Assert.Equal(
            53504L,
            aggregate.TotalDatagrams);
        Assert.Equal(
            1d * 100d / 53504d,
            aggregate.EffectiveLostPercent!.Value,
            precision: 10);
    }

    [Fact]
    public void TryAggregateUdpServerReports_RejectsIncompleteStreamSet()
    {
        var reports = new[]
        {
            new IperfIntervalSample(
                null,
                105_000_000,
                0,
                0,
                LostDatagrams: 0,
                TotalDatagrams: 26752)
        };

        var ok =
            Iperf2TextParser.TryAggregateUdpServerReports(
                reports,
                expectedStreamCount: 2,
                out _);

        Assert.False(ok);
    }

}
