namespace WinPerf.Core.Iperf;

public sealed record IperfEngineSettings
{
    public IperfEngine Engine { get; init; } = IperfEngine.Iperf3;

    // Legacy iperf3 path used by the current WinPerf settings file.
    public string? ExecutablePath { get; init; }

    public string? Iperf3ExecutablePath { get; init; }
    public string? Iperf2ExecutablePath { get; init; }
}
