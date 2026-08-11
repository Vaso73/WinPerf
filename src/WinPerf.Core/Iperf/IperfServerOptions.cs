namespace WinPerf.Core.Iperf;

public sealed class IperfServerOptions
{
    public IperfEngine Engine { get; init; } = IperfEngine.Iperf3;

    public IperfServerProtocol Protocol { get; init; } = IperfServerProtocol.Tcp;

    public int Port { get; init; } = 5201;

    public IperfAddressFamily AddressFamily { get; init; } = IperfAddressFamily.IPv4;

    public bool OneOff { get; init; }
}
