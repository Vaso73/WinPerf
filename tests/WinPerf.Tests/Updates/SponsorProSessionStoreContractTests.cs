namespace WinPerf.Tests.Updates;

public sealed class SponsorProSessionStoreContractTests
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void SessionStore_UsesCurrentUserDpapiOutsideSettingsJson()
    {
        var code = File.ReadAllText(Path.Combine(
            Root,
            "src",
            "WinPerf.App",
            "Updates",
            "SponsorProSessionStore.cs"));
        var project = File.ReadAllText(Path.Combine(
            Root,
            "src",
            "WinPerf.App",
            "WinPerf.App.csproj"));
        var settings = File.ReadAllText(Path.Combine(
            Root,
            "src",
            "WinPerf.App",
            "Settings",
            "WinPerfSettings.cs"));

        Assert.Contains("ProtectedData.Protect", code);
        Assert.Contains("ProtectedData.Unprotect", code);
        Assert.Contains("DataProtectionScope.CurrentUser", code);
        Assert.Contains("sponsor-pro-session.dat", code);
        Assert.Contains("Path.Combine(AppContext.BaseDirectory, \"data\")", code);
        Assert.Contains("System.Security.Cryptography.ProtectedData", project);
        Assert.DoesNotContain("SessionToken", settings, StringComparison.Ordinal);
    }
}
