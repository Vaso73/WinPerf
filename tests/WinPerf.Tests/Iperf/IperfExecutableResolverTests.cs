using WinPerf.Core.Iperf;

namespace WinPerf.Tests.Iperf;

public sealed class IperfExecutableResolverTests
{
    [Fact]
    public void Resolve_UsesConfiguredPathWhenItExists()
    {
        var resolver = new IperfExecutableResolver(path => path == @"C:\Tools\iperf3.exe");

        var result = resolver.Resolve(
            @"C:\Apps\WinPerf",
            new IperfEngineSettings { ExecutablePath = @"C:\Tools\iperf3.exe" });

        Assert.True(result.IsConfigured);
        Assert.Equal(@"C:\Tools\iperf3.exe", result.ExecutablePath);
        Assert.Equal("Configured", result.Source);
    }

    [Fact]
    public void Resolve_ReportsMissingConfiguredPathBeforeFallback()
    {
        var resolver = new IperfExecutableResolver(path => NormalizePath(path).EndsWith("tools/iperf3/iperf3.exe", StringComparison.OrdinalIgnoreCase));

        var result = resolver.Resolve(
            @"C:\Apps\WinPerf",
            new IperfEngineSettings { ExecutablePath = @"C:\Missing\iperf3.exe" });

        Assert.False(result.IsConfigured);
        Assert.Equal(@"C:\Missing\iperf3.exe", result.ExecutablePath);
        Assert.Equal("ConfiguredMissing", result.Source);
    }

    [Fact]
    public void Resolve_UsesBundledFallbackWhenNoConfiguredPathExists()
    {
        var resolver = new IperfExecutableResolver(path => NormalizePath(path).EndsWith("tools/iperf3/iperf3.exe", StringComparison.OrdinalIgnoreCase));

        var result = resolver.Resolve(
            @"C:\Apps\WinPerf",
            new IperfEngineSettings());

        Assert.True(result.IsConfigured);
        Assert.EndsWith("tools/iperf3/iperf3.exe", NormalizePath(result.ExecutablePath));
        Assert.Equal("Bundled", result.Source);
    }

    [Fact]
    public void Resolve_ReturnsNotConfiguredWhenNothingExists()
    {
        var resolver = new IperfExecutableResolver(_ => false);

        var result = resolver.Resolve(
            @"C:\Apps\WinPerf",
            new IperfEngineSettings());

        Assert.False(result.IsConfigured);
        Assert.Null(result.ExecutablePath);
        Assert.Equal("NotConfigured", result.Source);
    }
    private static string NormalizePath(string? path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }
}
