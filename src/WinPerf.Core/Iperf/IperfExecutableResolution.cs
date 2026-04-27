namespace WinPerf.Core.Iperf;

public sealed record IperfExecutableResolution(
    bool IsConfigured,
    string? ExecutablePath,
    string Source,
    string Message);
