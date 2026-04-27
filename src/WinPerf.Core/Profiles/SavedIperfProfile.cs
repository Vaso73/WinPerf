using WinPerf.Core.Iperf;

namespace WinPerf.Core.Profiles;

public enum SavedIperfRunMode
{
    Client,
    Server
}

public enum SavedIperfProtocol
{
    Tcp,
    Udp
}

public sealed record SavedIperfProfile
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }

    public SavedIperfRunMode RunMode { get; init; } = SavedIperfRunMode.Client;
    public SavedIperfProtocol Protocol { get; init; } = SavedIperfProtocol.Tcp;
    public IperfAddressFamily AddressFamily { get; init; } = IperfAddressFamily.IPv4;

    public string? Server { get; init; }
    public string? BindAddress { get; init; }
    public int Port { get; init; } = 5201;

    public int Streams { get; init; } = 1;
    public int DurationSeconds { get; init; } = 10;
    public int? ReportIntervalSeconds { get; init; } = 1;
    public int? OmitSeconds { get; init; }
    public int? ClientPort { get; init; }
    public string? Dscp { get; init; }

    public bool Reverse { get; init; }
    public bool Bidirectional { get; init; }

    public string UdpBandwidth { get; init; } = "0";
    public string? BufferLength { get; init; }
    public string? TcpWindow { get; init; }
    public string? TcpMss { get; init; }
    public bool TcpNoDelay { get; init; }
    public bool ZeroCopy { get; init; }

    public string ReportFormat { get; init; } = "M";
    public bool UseJsonStream { get; init; } = true;
    public bool Verbose { get; init; }
    public bool ServerOneOff { get; init; }
    public bool GetServerOutput { get; init; }

    public string? ExtraArguments { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public IperfTestOptions ToClientTestOptions()
    {
        if (RunMode != SavedIperfRunMode.Client)
        {
            throw new InvalidOperationException("Only client-mode profiles can be converted to client test options.");
        }

        SavedIperfProfileValidation.ThrowIfInvalid(this);

        return new IperfTestOptions
        {
            Server = Server?.Trim() ?? string.Empty,
            Port = Port,
            Mode = ToIperfMode(),
            Streams = Streams,
            DurationSeconds = DurationSeconds,
            AddressFamily = AddressFamily,
            UdpBandwidth = UdpBandwidth.Trim()
        };
    }

    public IperfMode ToIperfMode()
    {
        return Protocol switch
        {
            SavedIperfProtocol.Tcp when Bidirectional => IperfMode.TcpBidirectional,
            SavedIperfProtocol.Tcp when Reverse => IperfMode.TcpDownload,
            SavedIperfProtocol.Tcp => IperfMode.TcpUpload,
            SavedIperfProtocol.Udp when Reverse => IperfMode.UdpDownload,
            SavedIperfProtocol.Udp => IperfMode.UdpUpload,
            _ => throw new ArgumentOutOfRangeException(nameof(Protocol), Protocol, "Unsupported iperf protocol.")
        };
    }

    public static SavedIperfProfile FromClientTestOptions(
        Guid id,
        string name,
        IperfTestOptions options,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(options);

        var protocol = options.Mode is IperfMode.UdpUpload or IperfMode.UdpDownload
            ? SavedIperfProtocol.Udp
            : SavedIperfProtocol.Tcp;

        return new SavedIperfProfile
        {
            Id = id,
            Name = name,
            RunMode = SavedIperfRunMode.Client,
            Protocol = protocol,
            AddressFamily = options.AddressFamily,
            Server = options.Server,
            Port = options.Port,
            Streams = options.Streams,
            DurationSeconds = options.DurationSeconds,
            Reverse = options.Mode is IperfMode.TcpDownload or IperfMode.UdpDownload,
            Bidirectional = options.Mode == IperfMode.TcpBidirectional,
            UdpBandwidth = options.UdpBandwidth,
            CreatedAtUtc = nowUtc.ToUniversalTime(),
            UpdatedAtUtc = nowUtc.ToUniversalTime()
        };
    }
}
