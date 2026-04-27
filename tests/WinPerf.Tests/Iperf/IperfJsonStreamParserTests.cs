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
    public void TryParseIntervalSample_ParsesPerStreamThroughput()
    {
        const string json = """
        {
          "event": "interval",
          "data": {
            "streams": [
              { "socket": 5, "seconds": 1, "bits_per_second": 100000000 },
              { "socket": 7, "seconds": 1, "bits_per_second": 200000000 }
            ],
            "sum": {
              "seconds": 1,
              "bits_per_second": 300000000
            }
          }
        }
        """;

        var ok = IperfJsonStreamParser.TryParseIntervalSample(json, out var sample);

        Assert.True(ok);
        Assert.Equal(300, sample.MegabitsPerSecond);
        Assert.NotNull(sample.StreamBitsPerSecond);
        Assert.Equal(2, sample.StreamBitsPerSecond!.Count);
        Assert.Equal(100000000, sample.StreamBitsPerSecond[0]);
        Assert.Equal(200000000, sample.StreamBitsPerSecond[1]);
        Assert.Equal(100, sample.StreamMegabitsPerSecond[0]);
        Assert.Equal(200, sample.StreamMegabitsPerSecond[1]);
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
    public void TryParseIntervalSample_ParsesUdpMetricsFromStreams()
    {
        const string json = """
        {
          "event": "interval",
          "data": {
            "streams": [
              {
                "socket": 4,
                "udp": {
                  "seconds": 1,
                  "bits_per_second": 956000000,
                  "jitter_ms": 0.037,
                  "lost_percent": 0.12
                }
              }
            ],
            "sum": {
              "seconds": 1,
              "bits_per_second": 956000000
            }
          }
        }
        """;

        var ok = IperfJsonStreamParser.TryParseIntervalSample(json, out var sample);

        Assert.True(ok);
        Assert.Equal(956, sample.MegabitsPerSecond);
        Assert.Equal(0.037, sample.JitterMs);
        Assert.Equal(0.12, sample.LostPercent);
    }

    [Fact]
    public void TryParseEndSummarySample_ParsesUdpEndSummary()
    {
        const string json = """
        {
          "event": "end",
          "data": {
            "sum_received": {
              "seconds": 10.015306,
              "bits_per_second": 922711633.573652,
              "jitter_ms": 0.014175647615401027,
              "lost_packets": 0,
              "packets": 819348,
              "lost_percent": 0,
              "sender": false
            }
          }
        }
        """;

        var ok = IperfJsonStreamParser.TryParseEndSummarySample(json, out var sample);

        Assert.True(ok);
        Assert.Equal(922.711633573652, sample.MegabitsPerSecond!.Value, precision: 9);
        Assert.Equal(0.014175647615401027, sample.JitterMs!.Value, precision: 12);
        Assert.Equal(0, sample.LostPercent);
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

    [Fact]
    public void ParsesBidirectionalIntervalSummaryAndStreams()
    {
        const string json = """
        {
          "event": "interval",
          "data": {
            "streams": [
              {
                "socket": 5,
                "seconds": 1.0,
                "bits_per_second": 900400000,
                "sender": true
              },
              {
                "socket": 7,
                "seconds": 1.0,
                "bits_per_second": 486700000,
                "sender": false
              }
            ],
            "sum": {
              "seconds": 1.0,
              "bits_per_second": 900400000,
              "sender": true
            },
            "sum_bidir_reverse": {
              "seconds": 1.0,
              "bits_per_second": 486700000,
              "sender": false
            }
          }
        }
        """;

        Assert.True(IperfJsonStreamParser.TryParseIntervalSample(json, out var sample));

        Assert.Equal(900400000, sample.BitsPerSecond);
        Assert.Equal(486700000, sample.ReverseBitsPerSecond);

        Assert.Single(sample.StreamBitsPerSecond!);
        Assert.Single(sample.ReverseStreamBitsPerSecond!);

        Assert.Equal(900400000, sample.StreamBitsPerSecond![0]);
        Assert.Equal(486700000, sample.ReverseStreamBitsPerSecond![0]);
    }

}
