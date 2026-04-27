using WinPerf.Core.Iperf;

namespace WinPerf.Tests.Iperf;

public sealed class IperfProcessRunnerTests
{
    [Fact]
    public void CreateStartInfo_ConfiguresSafeRedirectedProcess()
    {
        var command = new IperfCommand(
            "iperf3.exe",
            ["-c", "10.100.100.1", "-p", "5201", "--json-stream"]);

        var startInfo = IperfProcessRunner.CreateStartInfo(command);

        Assert.Equal("iperf3.exe", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(["-c", "10.100.100.1", "-p", "5201", "--json-stream"], startInfo.ArgumentList);
    }

    [Fact]
    public void CreateStartInfo_PreservesArgumentsWithoutShellQuoting()
    {
        var command = new IperfCommand(
            @"C:\Tools\iperf3.exe",
            ["-c", "server with spaces", "-P", "4"]);

        var startInfo = IperfProcessRunner.CreateStartInfo(command);

        Assert.Equal(@"C:\Tools\iperf3.exe", startInfo.FileName);
        Assert.Equal(["-c", "server with spaces", "-P", "4"], startInfo.ArgumentList);
    }

    [Fact]
    public void CreateStartInfo_RejectsMissingExecutablePath()
    {
        var command = new IperfCommand("", []);

        Assert.Throws<ArgumentException>(() => IperfProcessRunner.CreateStartInfo(command));
    }
}
