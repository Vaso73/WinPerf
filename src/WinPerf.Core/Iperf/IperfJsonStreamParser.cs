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

            sample = new IperfIntervalSample(seconds, bitsPerSecond, jitterMs, lostPercent);
            return bitsPerSecond.HasValue || jitterMs.HasValue || lostPercent.HasValue;
        }
        catch (JsonException)
        {
            return false;
        }
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
