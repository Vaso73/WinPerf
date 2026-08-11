using WinPerf.Core.Product;

namespace WinPerf.Tests.Product;

public sealed class WinPerfProductEditionTests
{
    [Fact]
    public void DefaultBuild_IsSponsorProAndSupportsPrivateUpdater()
    {
        Assert.True(WinPerfProductEdition.IsSponsorPro);
        Assert.False(WinPerfProductEdition.IsPublicFree);
        Assert.True(WinPerfProductEdition.SupportsSponsorProUpdates);
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
        Assert.Contains("<DefineConstants>$(DefineConstants);WINPERF_FREE</DefineConstants>", project);
    }
}
