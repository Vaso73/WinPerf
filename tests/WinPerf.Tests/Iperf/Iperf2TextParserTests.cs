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

}
