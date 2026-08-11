namespace WinPerf.Tests;

public sealed class ReleaseWorkflowTests
{
    private static readonly string ReleaseWorkflowPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".github", "workflows", "release.yml"));

    [Fact]
    public void ReleaseWorkflow_IsManualOnlyWithConfirmation()
    {
        var workflow = File.ReadAllText(ReleaseWorkflowPath);

        Assert.Contains("name: Release", workflow);
        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("confirm_source_release:", workflow);
        Assert.Contains("publish-source-release", workflow);
        Assert.DoesNotContain("push:", workflow);
        Assert.DoesNotContain("tags:", workflow);
    }

    [Fact]
    public void ReleaseWorkflow_BuildsTestsPackagesAndCreatesRelease()
    {
        var workflow = File.ReadAllText(ReleaseWorkflowPath);

        Assert.Contains("permissions:", workflow);
        Assert.Contains("contents: write", workflow);
        Assert.Contains("actions/checkout@v4", workflow);
        Assert.Contains("fetch-depth: 0", workflow);
        Assert.Contains("Resolve release version", workflow);
        Assert.Contains("VersionPrefix", workflow);
        Assert.Contains("actions/setup-dotnet@v4", workflow);
        Assert.Contains("dotnet-version: \"9.0.x\"", workflow);
        Assert.Contains("dotnet test tests/WinPerf.Tests/WinPerf.Tests.csproj --configuration Release", workflow);
        Assert.Contains("dotnet publish src/WinPerf.App/WinPerf.App.csproj", workflow);
        Assert.Contains("--runtime win-x64", workflow);
        Assert.Contains("--self-contained true", workflow);
        Assert.DoesNotContain("--self-contained false", workflow);
        Assert.Contains("-p:PublishSingleFile=true", workflow);
        Assert.Contains("-p:EnableCompressionInSingleFile=true", workflow);
        Assert.Contains("--output artifacts/publish/WinPerf-win-x64", workflow);
        Assert.Contains("WinPerf.exe", workflow);
        Assert.Contains("WinPerf.zip", workflow);
        Assert.Contains("Verify ZIP contract", workflow);
        Assert.Contains("test \"$(zipinfo -1 artifacts/release/WinPerf.zip | wc -l)\" -eq 1", workflow);
        Assert.Contains("grep -c '^WinPerf.exe$'", workflow);
        Assert.Contains("sha256sum artifacts/release/WinPerf.zip", workflow);
        Assert.Contains("Create release tag", workflow);
        Assert.Contains("git tag \"$TAG_NAME\" \"$GITHUB_SHA\"", workflow);
        Assert.Contains("gh release create", workflow);
        Assert.Contains("--verify-tag", workflow);
        Assert.Contains("$TAG_NAME", workflow);
    }
}
