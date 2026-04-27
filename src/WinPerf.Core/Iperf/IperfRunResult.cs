namespace WinPerf.Core.Iperf;

public sealed record IperfRunResult(
    int ExitCode,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    IReadOnlyList<IperfProcessOutputLine> Output);
