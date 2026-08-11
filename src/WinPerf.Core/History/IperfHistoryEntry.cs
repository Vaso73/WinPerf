using WinPerf.Core.Iperf;

namespace WinPerf.Core.History;

public sealed record IperfHistoryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset FinishedAtUtc { get; init; }
    public IperfEngine Engine { get; init; }
    public IperfMode Mode { get; init; }
    public string Server { get; init; } = string.Empty;
    public int Port { get; init; }
    public int Streams { get; init; }
    public int DurationSeconds { get; init; }
    public int OmitSeconds { get; init; }
    public string? UdpBandwidth { get; init; }
    public int ExitCode { get; init; }
    public bool Succeeded { get; init; }
    public double? AverageMbps { get; init; }
    public double? MinimumMbps { get; init; }
    public double? MaximumMbps { get; init; }
    public double? ReverseAverageMbps { get; init; }
    public string CommandPreview { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
}
