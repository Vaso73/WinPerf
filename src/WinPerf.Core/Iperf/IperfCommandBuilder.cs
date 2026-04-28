namespace WinPerf.Core.Iperf;

public static class IperfCommandBuilder
{
    public static IperfCommand BuildClientCommand(string executablePath, IperfTestOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Server);

        ValidateCommonOptions(options);

        return options.Engine switch
        {
            IperfEngine.Iperf3 => BuildIperf3ClientCommand(executablePath, options),
            IperfEngine.Iperf2 => BuildIperf2ClientCommand(executablePath, options),
            _ => throw new ArgumentOutOfRangeException(nameof(options.Engine), options.Engine, "Unsupported iperf engine.")
        };
    }

    private static IperfCommand BuildIperf3ClientCommand(string executablePath, IperfTestOptions options)
    {
        var args = new List<string>
        {
            "-c", options.Server,
            "-p", options.Port.ToString()
        };

        AddAddressFamily(args, options.AddressFamily);

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

        args.Add("-t");
        args.Add(options.DurationSeconds.ToString());
        args.Add("-P");
        args.Add(options.Streams.ToString());

        if (options.OmitSeconds > 0)
        {
            args.Add("-O");
            args.Add(options.OmitSeconds.ToString());
        }

        args.Add("--json-stream");

        return new IperfCommand(executablePath, args);
    }

    private static IperfCommand BuildIperf2ClientCommand(string executablePath, IperfTestOptions options)
    {
        var args = new List<string>
        {
            "-c", options.Server,
            "-p", options.Port.ToString()
        };

        ValidateIperf2AddressFamily(options.AddressFamily);

        switch (options.Mode)
        {
            case IperfMode.TcpUpload:
                break;

            case IperfMode.UdpUpload:
                args.Add("-u");
                args.Add("-b");
                args.Add(options.UdpBandwidth);
                break;

            case IperfMode.TcpDownload:
            case IperfMode.TcpBidirectional:
            case IperfMode.UdpDownload:
                throw new NotSupportedException($"{options.Mode} is not supported for iperf2 in this WinPerf version.");

            default:
                throw new ArgumentOutOfRangeException(nameof(options.Mode), options.Mode, "Unsupported iperf mode.");
        }

        args.Add("-t");
        args.Add(options.DurationSeconds.ToString());
        args.Add("-P");
        args.Add(options.Streams.ToString());

        if (options.OmitSeconds > 0)
        {
            args.Add("--omit");
            args.Add(options.OmitSeconds.ToString());
        }

        args.Add("-i");
        args.Add("1");
        args.Add("-f");
        args.Add("m");

        return new IperfCommand(executablePath, args);
    }

    private static void ValidateCommonOptions(IperfTestOptions options)
    {
        if (options.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options.Port), "Port must be between 1 and 65535.");

        if (options.Streams < 1)
            throw new ArgumentOutOfRangeException(nameof(options.Streams), "Streams must be at least 1.");

        if (options.DurationSeconds < 1)
            throw new ArgumentOutOfRangeException(nameof(options.DurationSeconds), "Duration must be at least 1 second.");

        if (options.OmitSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(options.OmitSeconds), "Omit seconds must be zero or greater.");

        if (options.OmitSeconds >= options.DurationSeconds)
            throw new ArgumentOutOfRangeException(nameof(options.OmitSeconds), "Omit seconds must be less than duration.");
    }

    private static void ValidateIperf2AddressFamily(IperfAddressFamily addressFamily)
    {
        if (addressFamily != IperfAddressFamily.IPv4)
        {
            throw new NotSupportedException("IPv6 is not supported for iperf2 in this WinPerf version.");
        }
    }

    private static void AddAddressFamily(List<string> args, IperfAddressFamily addressFamily)
    {
        switch (addressFamily)
        {
            case IperfAddressFamily.IPv4:
                args.Add("-4");
                break;
            case IperfAddressFamily.IPv6:
                args.Add("-6");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(addressFamily), addressFamily, "Unsupported address family.");
        }
    }
}
