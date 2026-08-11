namespace WinPerf.Core.History;

public sealed record IperfHistoryDocument
{
    public List<IperfHistoryEntry> Entries { get; init; } = [];
}
