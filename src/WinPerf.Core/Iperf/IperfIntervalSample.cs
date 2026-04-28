using System.Collections.Generic;
using System.Linq;

namespace WinPerf.Core.Iperf;

public sealed record IperfIntervalSample(
    double? Seconds,
    double? BitsPerSecond,
    double? JitterMs,
    double? LostPercent,
    IReadOnlyList<double>? StreamBitsPerSecond = null,
    double? ReverseBitsPerSecond = null,
    IReadOnlyList<double>? ReverseStreamBitsPerSecond = null,
    bool Omitted = false)
{
    public double? MegabitsPerSecond =>
        BitsPerSecond.HasValue ? BitsPerSecond.Value / 1_000_000d : null;

    public double? ReverseMegabitsPerSecond =>
        ReverseBitsPerSecond.HasValue ? ReverseBitsPerSecond.Value / 1_000_000d : null;

    public IReadOnlyList<double> StreamMegabitsPerSecond =>
        StreamBitsPerSecond?.Select(value => value / 1_000_000d).ToList() ?? [];

    public IReadOnlyList<double> ReverseStreamMegabitsPerSecond =>
        ReverseStreamBitsPerSecond?.Select(value => value / 1_000_000d).ToList() ?? [];
}
