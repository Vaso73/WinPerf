using WinPerf.Core.Iperf;

namespace WinPerf.Tests.Iperf;

public sealed class IperfCommandBuilderTests
{
    [Fact]
    public void BuildClientCommand_BuildsTcpUploadCommand()
    {
        var command = IperfCommandBuilder.BuildClientCommand(
            "iperf3.exe",
            new IperfTestOptions
            {
                Server = "10.100.100.1",
                Port = 5201,
                Mode = IperfMode.TcpUpload,
                Streams = 10,
                DurationSeconds = 15,
                AddressFamily = IperfAddressFamily.IPv4
            });

        Assert.Equal("iperf3.exe", command.ExecutablePath);
        Assert.Equal(
            ["-c", "10.100.100.1", "-p", "5201", "-4", "-t", "15", "-P", "10", "--json-stream"],
            command.Arguments);
    }

    [Fact]
    public void BuildClientCommand_BuildsCleanTcpDownloadCommandOrder()
    {
        var command = IperfCommandBuilder.BuildClientCommand(
            "iperf3.exe",
            new IperfTestOptions
            {
                Server = "10.100.100.221",
                Port = 5201,
                Mode = IperfMode.TcpDownload,
                Streams = 10,
                DurationSeconds = 45,
                OmitSeconds = 15,
                AddressFamily = IperfAddressFamily.IPv4
            });

        Assert.Equal(
            ["-c", "10.100.100.221", "-p", "5201", "-4", "-R", "-t", "45", "-P", "10", "-O", "15", "--json-stream"],
            command.Arguments);
    }

    [Fact]
    public void BuildClientCommand_BuildsTcpDownloadCommand()
    {
        var command = IperfCommandBuilder.BuildClientCommand(
            "iperf3.exe",
            new IperfTestOptions
            {
                Server = "speed.example.net",
                Mode = IperfMode.TcpDownload,
                Streams = 4
            });

        Assert.Contains("-R", command.Arguments);
    }

    [Fact]
    public void BuildClientCommand_BuildsUdpDownloadCommand()
    {
        var command = IperfCommandBuilder.BuildClientCommand(
            "iperf3.exe",
            new IperfTestOptions
            {
                Server = "10.100.100.1",
                Mode = IperfMode.UdpDownload,
                UdpBandwidth = "0"
            });

        Assert.Contains("-u", command.Arguments);
        Assert.Contains("-b", command.Arguments);
        Assert.Contains("0", command.Arguments);
        Assert.Contains("-R", command.Arguments);
    }


    [Fact]
    public void BuildClientCommand_AddsOmitSecondsWhenConfigured()
    {
        var command = IperfCommandBuilder.BuildClientCommand(
            "iperf3.exe",
            new IperfTestOptions
            {
                Server = "10.100.100.1",
                DurationSeconds = 45,
                OmitSeconds = 15
            });

        Assert.Equal(
            ["-c", "10.100.100.1", "-p", "5201", "-4", "-t", "45", "-P", "1", "-O", "15", "--json-stream"],
            command.Arguments);
    }

    [Fact]
    public void BuildClientCommand_RejectsOmitSecondsAtOrAboveDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IperfCommandBuilder.BuildClientCommand(
                "iperf3.exe",
                new IperfTestOptions
                {
                    Server = "10.0.0.1",
                    DurationSeconds = 10,
                    OmitSeconds = 10
                }));
    }

    [Fact]
    public void BuildClientCommand_AppendsJsonStreamLast()
    {
        var command = IperfCommandBuilder.BuildClientCommand(
            "iperf3.exe",
            new IperfTestOptions
            {
                Server = "10.100.100.1",
                Mode = IperfMode.TcpDownload,
                DurationSeconds = 45,
                OmitSeconds = 15
            });

        Assert.Equal("--json-stream", command.Arguments[^1]);
        Assert.Equal(1, command.Arguments.Count(argument => argument == "--json-stream"));
    }


    [Fact]
    public void BuildClientCommand_BuildsIperf2TcpUploadCommand()
    {
        var command = IperfCommandBuilder.BuildClientCommand(
            "iperf.exe",
            new IperfTestOptions
            {
                Engine = IperfEngine.Iperf2,
                Server = "10.100.100.1",
                Port = 5001,
                Mode = IperfMode.TcpUpload,
                Streams = 2,
                DurationSeconds = 15,
                OmitSeconds = 3,
                AddressFamily = IperfAddressFamily.IPv4
            });

        Assert.Equal("iperf.exe", command.ExecutablePath);
        Assert.Equal(
            ["-c", "10.100.100.1", "-p", "5001", "-t", "15", "-P", "2", "--omit", "3", "-i", "1", "-f", "m"],
            command.Arguments);
    }

    [Fact]
    public void BuildClientCommand_BuildsIperf2UdpUploadCommand()
    {
        var command = IperfCommandBuilder.BuildClientCommand(
            "iperf.exe",
            new IperfTestOptions
            {
                Engine = IperfEngine.Iperf2,
                Server = "10.100.100.1",
                Port = 5001,
                Mode = IperfMode.UdpUpload,
                UdpBandwidth = "100M"
            });

        Assert.Equal(
            ["-c", "10.100.100.1", "-p", "5001", "-u", "-b", "100M", "-t", "10", "-P", "1", "-i", "1", "-f", "m"],
            command.Arguments);
    }

    [Fact]
    public void BuildClientCommand_RejectsIperf2Ipv6ForNow()
    {
        Assert.Throws<NotSupportedException>(() =>
            IperfCommandBuilder.BuildClientCommand(
                "iperf.exe",
                new IperfTestOptions
                {
                    Engine = IperfEngine.Iperf2,
                    Server = "::1",
                    Port = 5001,
                    AddressFamily = IperfAddressFamily.IPv6
                }));
    }

    [Fact]
    public void BuildClientCommand_RejectsUnsupportedIperf2DownloadMode()
    {
        Assert.Throws<NotSupportedException>(() =>
            IperfCommandBuilder.BuildClientCommand(
                "iperf.exe",
                new IperfTestOptions
                {
                    Engine = IperfEngine.Iperf2,
                    Server = "10.0.0.1",
                    Mode = IperfMode.TcpDownload
                }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildClientCommand_RejectsMissingServer(string server)
    {
        Assert.Throws<ArgumentException>(() =>
            IperfCommandBuilder.BuildClientCommand(
                "iperf3.exe",
                new IperfTestOptions { Server = server }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void BuildClientCommand_RejectsInvalidPort(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IperfCommandBuilder.BuildClientCommand(
                "iperf3.exe",
                new IperfTestOptions { Server = "10.0.0.1", Port = port }));
    }
}
