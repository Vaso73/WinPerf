namespace WinPerf.Tests;

public sealed class ReleaseWorkflowTests
{
    private static readonly string ReleaseWorkflowPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".github", "workflows", "release.yml"));

    [Fact]
    public void ReleaseWorkflow_RunsForVersionTags()
    {
        var workflow = File.ReadAllText(ReleaseWorkflowPath);

        Assert.Contains("name: Release", workflow);
        Assert.Contains("push:", workflow);
        Assert.Contains("tags:", workflow);
        Assert.Contains("\"v*.*.*\"", workflow);
    }

    [Fact]
    public void ReleaseWorkflow_BuildsTestsPackagesAndCreatesRelease()
    {
        var workflow = File.ReadAllText(ReleaseWorkflowPath);

        Assert.Contains("permissions:", workflow);
        Assert.Contains("contents: write", workflow);
        Assert.Contains("actions/checkout@v4", workflow);
        Assert.Contains("actions/setup-dotnet@v4", workflow);
        Assert.Contains("dotnet-version: \"9.0.x\"", workflow);
        Assert.Contains("dotnet test tests/WinPerf.Tests/WinPerf.Tests.csproj --configuration Release", workflow);
        Assert.Contains("dotnet publish src/WinPerf.App/WinPerf.App.csproj", workflow);
        Assert.Contains("--runtime win-x64", workflow);
        Assert.Contains("--self-contained false", workflow);
        Assert.Contains("--output artifacts/publish/WinPerf-win-x64", workflow);
        Assert.Contains("WinPerf.exe", workflow);
        Assert.Contains("WinPerf.zip", workflow);
        Assert.Contains("gh release create", workflow);
        Assert.Contains("${GITHUB_REF_NAME}", workflow);
    }
}
