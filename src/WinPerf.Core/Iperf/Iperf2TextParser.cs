using System.Globalization;
using System.Text.RegularExpressions;

namespace WinPerf.Core.Iperf;

public static partial class Iperf2TextParser
{
    public static bool TryParseIntervalSample(string line, out IperfIntervalSample sample)
    {
        sample = new IperfIntervalSample(null, null, null, null);

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = IntervalLineRegex().Match(line);

        if (!match.Success)
        {
            return false;
        }

        if (!double.TryParse(match.Groups["start"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var start) ||
            !double.TryParse(match.Groups["end"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var end) ||
            !double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        if (IsAggregateSummaryLine(start, end))
        {
            return false;
        }

        var bitsPerSecond = ToBitsPerSecond(value, match.Groups["unit"].Value);

        if (bitsPerSecond is null)
        {
            return false;
        }

        var seconds = Math.Max(0, end - start);

        sample = new IperfIntervalSample(
            seconds,
            bitsPerSecond,
            null,
            null);

        return true;
    }

    private static bool IsAggregateSummaryLine(double start, double end)
    {
        return start == 0d && end > 1.5d;
    }

    private static double? ToBitsPerSecond(double value, string unit)
    {
        return unit.ToLowerInvariant() switch
        {
            "bits/sec" => value,
            "kbits/sec" => value * 1_000d,
            "mbits/sec" => value * 1_000_000d,
            "gbits/sec" => value * 1_000_000_000d,
            _ => null
        };
    }

    [GeneratedRegex(@"\]\s+(?<start>\d+(?:\.\d+)?)\s*-\s*(?<end>\d+(?:\.\d+)?)\s+sec\s+.+?\s+(?<value>\d+(?:\.\d+)?)\s+(?<unit>[KMG]?bits/sec)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IntervalLineRegex();
}
