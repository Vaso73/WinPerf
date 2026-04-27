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

            var sum = TryGetObject(data, "sum") ?? TryGetObject(data, "sum_sent") ?? TryGetObject(data, "sum_received");

            if (sum is null)
            {
                return false;
            }

            var seconds = TryGetDouble(sum.Value, "seconds");
            var bitsPerSecond = TryGetDouble(sum.Value, "bits_per_second");
            var jitterMs = TryGetDouble(sum.Value, "jitter_ms");
            var lostPercent = TryGetDouble(sum.Value, "lost_percent");

            if (!jitterMs.HasValue || !lostPercent.HasValue)
            {
                var udp = TryGetFirstUdpStream(data);

                if (udp is not null)
                {
                    jitterMs ??= TryGetDouble(udp.Value, "jitter_ms");
                    lostPercent ??= TryGetDouble(udp.Value, "lost_percent");
                }
            }

            sample = new IperfIntervalSample(seconds, bitsPerSecond, jitterMs, lostPercent);
            return bitsPerSecond.HasValue || jitterMs.HasValue || lostPercent.HasValue;
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

            var summary =
                TryGetObject(data, "sum_received") ??
                TryGetObject(data, "sum") ??
                TryGetObject(data, "sum_sent") ??
                TryGetFirstUdpStream(data);

            if (summary is null)
            {
                return false;
            }

            var seconds = TryGetDouble(summary.Value, "seconds");
            var bitsPerSecond = TryGetDouble(summary.Value, "bits_per_second");
            var jitterMs = TryGetDouble(summary.Value, "jitter_ms");
            var lostPercent = TryGetDouble(summary.Value, "lost_percent");

            sample = new IperfIntervalSample(seconds, bitsPerSecond, jitterMs, lostPercent);
            return bitsPerSecond.HasValue || jitterMs.HasValue || lostPercent.HasValue;
        }
        catch (JsonException)
        {
            return false;
        }
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
}
