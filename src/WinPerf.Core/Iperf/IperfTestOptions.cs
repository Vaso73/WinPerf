namespace WinPerf.Core.Iperf;

public sealed record IperfTestOptions
{
    public required string Server { get; init; }
    public int Port { get; init; } = 5201;
    public IperfEngine Engine { get; init; } = IperfEngine.Iperf3;
    public IperfMode Mode { get; init; } = IperfMode.TcpUpload;
    public int Streams { get; init; } = 1;
    public int DurationSeconds { get; init; } = 10;
    public int OmitSeconds { get; init; }
    public IperfAddressFamily AddressFamily { get; init; } = IperfAddressFamily.IPv4;
    public string UdpBandwidth { get; init; } = "10M";
}
