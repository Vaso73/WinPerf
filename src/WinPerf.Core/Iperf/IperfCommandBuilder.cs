namespace WinPerf.Core.Iperf;

public static class IperfCommandBuilder
{
    public static IperfCommand BuildClientCommand(string executablePath, IperfTestOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Server);

        if (options.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options.Port), "Port must be between 1 and 65535.");

        if (options.Streams < 1)
            throw new ArgumentOutOfRangeException(nameof(options.Streams), "Streams must be at least 1.");

        if (options.DurationSeconds < 1)
            throw new ArgumentOutOfRangeException(nameof(options.DurationSeconds), "Duration must be at least 1 second.");

        var args = new List<string>
        {
            "-c", options.Server,
            "-p", options.Port.ToString(),
            "-t", options.DurationSeconds.ToString(),
            "-P", options.Streams.ToString(),
            "--json-stream"
        };

        switch (options.AddressFamily)
        {
            case IperfAddressFamily.IPv4:
                args.Add("-4");
                break;
            case IperfAddressFamily.IPv6:
                args.Add("-6");
                break;
        }

        switch (options.Mode)
        {
            case IperfMode.TcpUpload:
                break;

            case IperfMode.TcpDownload:
                args.Add("-R");
                break;

            case IperfMode.TcpBidirectional:
                args.Add("--bidir");
                break;

            case IperfMode.UdpUpload:
                args.Add("-u");
                args.Add("-b");
                args.Add(options.UdpBandwidth);
                break;

            case IperfMode.UdpDownload:
                args.Add("-u");
                args.Add("-b");
                args.Add(options.UdpBandwidth);
                args.Add("-R");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(options.Mode), options.Mode, "Unsupported iperf mode.");
        }

        return new IperfCommand(executablePath, args);
    }
}
