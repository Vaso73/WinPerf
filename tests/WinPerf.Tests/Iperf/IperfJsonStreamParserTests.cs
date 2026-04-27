using WinPerf.Core.Iperf;

namespace WinPerf.Tests.Iperf;

public sealed class IperfJsonStreamParserTests
{
    [Fact]
    public void TryParseIntervalSample_ParsesTcpInterval()
    {
        const string json = """
        {
          "event": "interval",
          "data": {
            "sum": {
              "start": 0,
              "end": 1.0001,
              "seconds": 1.0001,
              "bytes": 117833728,
              "bits_per_second": 942000000
            }
          }
        }
        """;

        var ok = IperfJsonStreamParser.TryParseIntervalSample(json, out var sample);

        Assert.True(ok);
        Assert.Equal(1.0001, sample.Seconds);
        Assert.Equal(942000000, sample.BitsPerSecond);
        Assert.Equal(942, sample.MegabitsPerSecond);
        Assert.Null(sample.JitterMs);
        Assert.Null(sample.LostPercent);
    }

    [Fact]
    public void TryParseIntervalSample_ParsesUdpInterval()
    {
        const string json = """
        {
          "event": "interval",
          "data": {
            "sum": {
              "seconds": 1,
              "bits_per_second": 100000000,
              "jitter_ms": 0.42,
              "lost_percent": 0.5
            }
          }
        }
        """;

        var ok = IperfJsonStreamParser.TryParseIntervalSample(json, out var sample);

        Assert.True(ok);
        Assert.Equal(100, sample.MegabitsPerSecond);
        Assert.Equal(0.42, sample.JitterMs);
        Assert.Equal(0.5, sample.LostPercent);
    }

    [Fact]
    public void TryParseIntervalSample_IgnoresNonIntervalEvent()
    {
        const string json = """{"event":"start","data":{}}""";

        var ok = IperfJsonStreamParser.TryParseIntervalSample(json, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseIntervalSample_IgnoresInvalidJson()
    {
        var ok = IperfJsonStreamParser.TryParseIntervalSample("not-json", out _);

        Assert.False(ok);
    }
}
