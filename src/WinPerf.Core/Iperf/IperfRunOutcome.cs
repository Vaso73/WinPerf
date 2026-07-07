namespace WinPerf.Core.Iperf;

public sealed record IperfRunOutcome(
    IperfRunOutcomeKind Kind,
    string Message);
