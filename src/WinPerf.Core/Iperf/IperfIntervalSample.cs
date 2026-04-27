namespace WinPerf.Core.Iperf;

public sealed record IperfIntervalSample(
    double? Seconds,
    double? BitsPerSecond,
    double? JitterMs,
    double? LostPercent)
{
    public double? MegabitsPerSecond =>
        BitsPerSecond.HasValue ? BitsPerSecond.Value / 1_000_000d : null;
}
