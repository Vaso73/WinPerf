namespace WinPerf.Tests;

public sealed class VersionMetadataTests
{
    private static readonly string DirectoryBuildPropsPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Directory.Build.props"));

    [Fact]
    public void DirectoryBuildProps_DefinesInitialReleaseVersion()
    {
        var props = File.ReadAllText(DirectoryBuildPropsPath);

        Assert.Contains("<Product>WinPerf</Product>", props);
        Assert.Contains("<Authors>Vaso73</Authors>", props);
        Assert.Contains("<VersionPrefix>0.1.2</VersionPrefix>", props);
        Assert.Contains("<Version>$(VersionPrefix)</Version>", props);
        Assert.Contains("<AssemblyVersion>0.1.2.0</AssemblyVersion>", props);
        Assert.Contains("<FileVersion>0.1.2.0</FileVersion>", props);
        Assert.Contains("<InformationalVersion>$(Version)</InformationalVersion>", props);
    }

    [Fact]
    public void DirectoryBuildProps_KeepsReleaseSymbolsDisabled()
    {
        var props = File.ReadAllText(DirectoryBuildPropsPath);

        Assert.Contains("Condition=\"'$(Configuration)' == 'Release'\"", props);
        Assert.Contains("<DebugType>none</DebugType>", props);
        Assert.Contains("<DebugSymbols>false</DebugSymbols>", props);
    }
}
