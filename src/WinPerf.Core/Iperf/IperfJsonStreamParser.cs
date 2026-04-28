using System.Collections.Generic;
using System.Text.Json;

namespace WinPerf.Core.Iperf;

public static class IperfJsonStreamParser
{
    public static bool TryParseIntervalSample(string jsonLine, out IperfIntervalSample sample)
    {
        sample = new IperfIntervalSample(null, null, null, null);

        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonLine);
            var root = doc.RootElement;

            if (!root.TryGetProperty("event", out var eventElement) ||
                !string.Equals(eventElement.GetString(), "interval", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!root.TryGetProperty("data", out var data))
            {
                return false;
            }

            var reverseSummary = TryGetObject(data, "sum_bidir_reverse");
            var sum = SelectPrimarySummary(data);

            if (sum is null)
            {
                return false;
            }

            var seconds = TryGetDouble(sum.Value, "seconds");
            var bitsPerSecond = TryGetDouble(sum.Value, "bits_per_second");
            var jitterMs = TryGetDouble(sum.Value, "jitter_ms");
            var lostPercent = TryGetDouble(sum.Value, "lost_percent");
            var omitted = TryGetBool(sum.Value, "omitted") ?? false;

            var streamBitsPerSecond = reverseSummary is null
                ? GetStreamBitsPerSecond(data)
                : GetStreamBitsPerSecond(data, sender: true);

            var reverseBitsPerSecond = reverseSummary is null
                ? null
                : TryGetDouble(reverseSummary.Value, "bits_per_second");

            var reverseStreamBitsPerSecond = reverseSummary is null
                ? null
                : GetStreamBitsPerSecond(data, sender: false);

            if (!jitterMs.HasValue || !lostPercent.HasValue)
            {
                var udp = TryGetFirstUdpStream(data);

                if (udp is not null)
                {
                    jitterMs ??= TryGetDouble(udp.Value, "jitter_ms");
                    lostPercent ??= TryGetDouble(udp.Value, "lost_percent");
                }
            }

            sample = new IperfIntervalSample(
                seconds,
                bitsPerSecond,
                jitterMs,
                lostPercent,
                streamBitsPerSecond,
                reverseBitsPerSecond,
                reverseStreamBitsPerSecond,
                omitted);

            return bitsPerSecond.HasValue ||
                   jitterMs.HasValue ||
                   lostPercent.HasValue ||
                   streamBitsPerSecond.Count > 0 ||
                   reverseBitsPerSecond.HasValue ||
                   (reverseStreamBitsPerSecond?.Count ?? 0) > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryParseEndSummarySample(string jsonLine, out IperfIntervalSample sample)
    {
        sample = new IperfIntervalSample(null, null, null, null);

        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonLine);
            var root = doc.RootElement;

            if (!root.TryGetProperty("event", out var eventElement) ||
                !string.Equals(eventElement.GetString(), "end", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!root.TryGetProperty("data", out var data))
            {
                return false;
            }

            var reverseSummary = TryGetObject(data, "sum_bidir_reverse");
            var summary = SelectPrimarySummary(data);

            if (summary is null)
            {
                return false;
            }

            var seconds = TryGetDouble(summary.Value, "seconds");
            var bitsPerSecond = TryGetDouble(summary.Value, "bits_per_second");
            var jitterMs = TryGetDouble(summary.Value, "jitter_ms");
            var lostPercent = TryGetDouble(summary.Value, "lost_percent");
            var omitted = TryGetBool(summary.Value, "omitted") ?? false;

            var streamBitsPerSecond = reverseSummary is null
                ? GetStreamBitsPerSecond(data)
                : GetStreamBitsPerSecond(data, sender: true);

            var reverseBitsPerSecond = reverseSummary is null
                ? null
                : TryGetDouble(reverseSummary.Value, "bits_per_second");

            var reverseStreamBitsPerSecond = reverseSummary is null
                ? null
                : GetStreamBitsPerSecond(data, sender: false);

            sample = new IperfIntervalSample(
                seconds,
                bitsPerSecond,
                jitterMs,
                lostPercent,
                streamBitsPerSecond,
                reverseBitsPerSecond,
                reverseStreamBitsPerSecond,
                omitted);

            return bitsPerSecond.HasValue ||
                   jitterMs.HasValue ||
                   lostPercent.HasValue ||
                   streamBitsPerSecond.Count > 0 ||
                   reverseBitsPerSecond.HasValue ||
                   (reverseStreamBitsPerSecond?.Count ?? 0) > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonElement? SelectPrimarySummary(JsonElement data)
    {
        if (TryGetObject(data, "sum_bidir_reverse") is not null)
        {
            return TryGetObject(data, "sum") ??
                   TryGetObject(data, "sum_sent") ??
                   TryGetObject(data, "sum_received") ??
                   TryGetFirstUdpStream(data);
        }

        return TryGetObject(data, "sum") ??
               TryGetObject(data, "sum_received") ??
               TryGetObject(data, "sum_sent") ??
               TryGetFirstUdpStream(data);
    }

    private static IReadOnlyList<double> GetStreamBitsPerSecond(JsonElement data, bool? sender = null)
    {
        var values = new List<double>();

        if (!data.TryGetProperty("streams", out var streams) ||
            streams.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (var stream in streams.EnumerateArray())
        {
            var udp = TryGetObject(stream, "udp");

            if (sender is bool expectedSender)
            {
                var streamSender = TryGetBool(stream, "sender") ??
                                   (udp is null ? null : TryGetBool(udp.Value, "sender"));

                if (streamSender != expectedSender)
                {
                    continue;
                }
            }

            var bitsPerSecond = TryGetDouble(stream, "bits_per_second");

            if (!bitsPerSecond.HasValue)
            {
                bitsPerSecond = udp is null
                    ? null
                    : TryGetDouble(udp.Value, "bits_per_second");
            }

            if (bitsPerSecond is double value)
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static JsonElement? TryGetFirstUdpStream(JsonElement data)
    {
        if (!data.TryGetProperty("streams", out var streams) ||
            streams.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var stream in streams.EnumerateArray())
        {
            var udp = TryGetObject(stream, "udp");

            if (udp is not null)
            {
                return udp;
            }
        }

        return null;
    }

    private static JsonElement? TryGetObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Object ? value : null;
    }

    private static double? TryGetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return null;
    }

    private static bool? TryGetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.True
            ? true
            : value.ValueKind == JsonValueKind.False
                ? false
                : null;
    }
}
