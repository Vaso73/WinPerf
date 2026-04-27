namespace WinPerf.Core.Iperf;

public sealed record IperfProcessOutputLine(
    IperfOutputStream Stream,
    string Text,
    DateTimeOffset TimestampUtc);
