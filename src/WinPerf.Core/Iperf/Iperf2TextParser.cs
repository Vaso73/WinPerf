using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace WinPerf.Core.Iperf;

public static partial class Iperf2TextParser
{
    public static bool TryParseIntervalSample(
        string line,
        out IperfIntervalSample sample,
        bool preferSum = false,
        double? maxEndSeconds = null)
    {
        sample = new IperfIntervalSample(null, null, null, null);

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var isSumLine = IsSumLine(line);

        if (preferSum && !isSumLine)
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

        if (preferSum && isSumLine && maxEndSeconds is double maxEnd && end > maxEnd + 0.5d)
        {
            return false;
        }

        if (!preferSum && IsAggregateSummaryLine(start, end))
        {
            return false;
        }

        var bitsPerSecond = ToBitsPerSecond(value, match.Groups["unit"].Value);

        if (bitsPerSecond is null)
        {
            return false;
        }

        var seconds = preferSum && isSumLine
            ? end
            : Math.Max(0, end - start);

        sample = new IperfIntervalSample(
            seconds,
            bitsPerSecond,
            null,
            null);

        return true;
    }

    public static bool TryParseUdpServerReport(
        string line,
        out IperfIntervalSample sample)
    {
        sample = new IperfIntervalSample(null, null, null, null);

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = UdpServerReportRegex().Match(line);

        if (!match.Success)
        {
            return false;
        }

        if (!double.TryParse(
                match.Groups["value"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var bandwidthValue) ||
            !double.TryParse(
                match.Groups["jitter"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var jitterMs) ||
            !long.TryParse(
                match.Groups["lost"].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var lostDatagrams) ||
            !long.TryParse(
                match.Groups["total"].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var totalDatagrams) ||
            !double.TryParse(
                match.Groups["loss"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var reportedLostPercent))
        {
            return false;
        }

        var bitsPerSecond = ToBitsPerSecond(
            bandwidthValue,
            match.Groups["unit"].Value);

        if (bitsPerSecond is null)
        {
            return false;
        }

        sample = new IperfIntervalSample(
            null,
            bitsPerSecond,
            jitterMs,
            reportedLostPercent,
            LostDatagrams: lostDatagrams,
            TotalDatagrams: totalDatagrams);

        return true;
    }

    public static bool TryAggregateUdpServerReports(
        IEnumerable<IperfIntervalSample> reports,
        int expectedStreamCount,
        out IperfIntervalSample aggregate)
    {
        aggregate =
            new IperfIntervalSample(null, null, null, null);

        if (reports is null || expectedStreamCount < 1)
        {
            return false;
        }

        var validReports = reports
            .Where(report =>
                report.BitsPerSecond.HasValue &&
                report.JitterMs.HasValue &&
                report.LostDatagrams.HasValue &&
                report.TotalDatagrams.HasValue)
            .ToList();

        if (validReports.Count != expectedStreamCount)
        {
            return false;
        }

        var bitsPerSecond = validReports.Sum(
            report => report.BitsPerSecond!.Value);

        var jitterMs = validReports.Max(
            report => report.JitterMs!.Value);

        var lostDatagrams = validReports.Sum(
            report => report.LostDatagrams!.Value);

        var totalDatagrams = validReports.Sum(
            report => report.TotalDatagrams!.Value);

        var lostPercent =
            totalDatagrams > 0
                ? lostDatagrams * 100d / totalDatagrams
                : 0d;

        aggregate = new IperfIntervalSample(
            null,
            bitsPerSecond,
            jitterMs,
            lostPercent,
            LostDatagrams: lostDatagrams,
            TotalDatagrams: totalDatagrams);

        return true;
    }

    private static bool IsAggregateSummaryLine(double start, double end)
    {
        return start == 0d && end > 1.5d;
    }

    private static bool IsSumLine(string line)
    {
        return line.TrimStart().StartsWith("[SUM]", StringComparison.Ordinal);
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

    [GeneratedRegex(
        @"\]\s+\d+(?:\.\d+)?\s*-\s*\d+(?:\.\d+)?\s+sec\s+.+?\s+(?<value>\d+(?:\.\d+)?)\s+(?<unit>[KMG]?bits/sec)\s+(?<jitter>\d+(?:\.\d+)?)\s+ms\s+(?<lost>\d+)\s*/\s*(?<total>\d+)\s+\((?<loss>\d+(?:\.\d+)?)%\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UdpServerReportRegex();


    [GeneratedRegex(@"\]\s+(?<start>\d+(?:\.\d+)?)\s*-\s*(?<end>\d+(?:\.\d+)?)\s+sec\s+.+?\s+(?<value>\d+(?:\.\d+)?)\s+(?<unit>[KMG]?bits/sec)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IntervalLineRegex();
}
