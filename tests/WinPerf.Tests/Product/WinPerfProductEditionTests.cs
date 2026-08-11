using WinPerf.Core.Product;

namespace WinPerf.Tests.Product;

public sealed class WinPerfProductEditionTests
{
    [Fact]
    public void CurrentBuild_UsesSelectedEditionCapabilities()
    {
        if (WinPerfProductEdition.IsPublicFree)
        {
            Assert.False(WinPerfProductEdition.IsSponsorPro);
            Assert.False(WinPerfProductEdition.SupportsSponsorProUpdates);
            Assert.False(WinPerfProductEdition.SupportsIperf2);
            Assert.False(WinPerfProductEdition.SupportsUdp);
            Assert.False(WinPerfProductEdition.SupportsBidirectional);
            Assert.False(WinPerfProductEdition.SupportsServerMode);
            Assert.False(WinPerfProductEdition.SupportsAdvancedCommands);
            Assert.False(WinPerfProductEdition.SupportsCustomCommands);
            Assert.False(WinPerfProductEdition.SupportsHistoryExportImport);
            Assert.Equal(1, WinPerfProductEdition.MaxStreams);
            Assert.Equal(10, WinPerfProductEdition.MaxDurationSeconds);
            Assert.Equal(5, WinPerfProductEdition.MaxSavedHistoryResults);
            Assert.Equal("WinPerf Free", WinPerfProductEdition.EditionName);
            Assert.Equal("free-data", WinPerfProductEdition.DataDirectoryName);
            return;
        }

        Assert.True(WinPerfProductEdition.IsSponsorPro);
        Assert.True(WinPerfProductEdition.SupportsSponsorProUpdates);
        Assert.True(WinPerfProductEdition.SupportsIperf2);
        Assert.True(WinPerfProductEdition.SupportsUdp);
        Assert.True(WinPerfProductEdition.SupportsBidirectional);
        Assert.True(WinPerfProductEdition.SupportsServerMode);
        Assert.True(WinPerfProductEdition.SupportsAdvancedCommands);
        Assert.True(WinPerfProductEdition.SupportsCustomCommands);
        Assert.True(WinPerfProductEdition.SupportsHistoryExportImport);
        Assert.Equal(int.MaxValue, WinPerfProductEdition.MaxStreams);
        Assert.Equal(int.MaxValue, WinPerfProductEdition.MaxDurationSeconds);
        Assert.Equal(int.MaxValue, WinPerfProductEdition.MaxSavedHistoryResults);
        Assert.Equal("WinPerf Sponsor Pro", WinPerfProductEdition.EditionName);
        Assert.Equal("data", WinPerfProductEdition.DataDirectoryName);
    }

    [Fact]
    public void SourceDefinesFreeEditionBoundary()
    {
        var coreDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.Core"));
        var source = File.ReadAllText(Path.Combine(coreDirectory, "Product", "WinPerfProductEdition.cs"));
        var project = File.ReadAllText(Path.Combine(coreDirectory, "WinPerf.Core.csproj"));

        Assert.Contains("#if WINPERF_FREE", source);
        Assert.Contains("SupportsSponsorProUpdates { get; } = false", source);
        Assert.Contains("SupportsIperf2 { get; } = false", source);
        Assert.Contains("SupportsUdp { get; } = false", source);
        Assert.Contains("SupportsBidirectional { get; } = false", source);
        Assert.Contains("SupportsServerMode { get; } = false", source);
        Assert.Contains("SupportsAdvancedCommands { get; } = false", source);
        Assert.Contains("SupportsCustomCommands { get; } = false", source);
        Assert.Contains("SupportsHistoryExportImport { get; } = false", source);
        Assert.Contains("MaxStreams { get; } = 1", source);
        Assert.Contains("MaxDurationSeconds { get; } = 10", source);
        Assert.Contains("MaxSavedHistoryResults { get; } = 5", source);
        Assert.Contains("<DefineConstants>$(DefineConstants);WINPERF_FREE</DefineConstants>", project);
    }
}
