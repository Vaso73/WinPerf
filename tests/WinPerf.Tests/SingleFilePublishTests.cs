namespace WinPerf.Tests;

public sealed class SingleFilePublishTests
{
    private static readonly string AppProjectPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "WinPerf.App.csproj"));

    [Fact]
    public void WinPerfAppProject_PublishesSingleFileWithNativeSelfExtraction()
    {
        var project = File.ReadAllText(AppProjectPath);

        Assert.Contains("<PublishSingleFile>true</PublishSingleFile>", project);
        Assert.Contains("<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>", project);
    }
}
