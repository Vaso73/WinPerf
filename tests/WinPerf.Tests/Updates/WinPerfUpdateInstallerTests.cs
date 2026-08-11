using WinPerf.Core.Updates;

namespace WinPerf.Tests.Updates;

public sealed class WinPerfUpdateInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"winperf-installer-test-{Guid.NewGuid():N}");

    [Fact]
    public void Apply_ReplacesOnlyWinPerfExecutable()
    {
        var (staging, install, backups) = CreateLayout();
        File.WriteAllText(Path.Combine(install, "data", "settings.json"), "preserve-settings");
        File.WriteAllText(Path.Combine(install, "tools", "iperf3", "iperf3.exe"), "preserve-engine");

        new WinPerfUpdateInstaller().Apply(staging, install, backups);

        Assert.Equal("new-exe", File.ReadAllText(Path.Combine(install, "WinPerf.exe")));
        Assert.Equal("preserve-settings", File.ReadAllText(Path.Combine(install, "data", "settings.json")));
        Assert.Equal("preserve-engine", File.ReadAllText(Path.Combine(install, "tools", "iperf3", "iperf3.exe")));
        Assert.Empty(Directory.GetDirectories(backups));
    }

    [Fact]
    public void Apply_RollsBackExecutableWhenReplacementFails()
    {
        var (staging, install, backups) = CreateLayout();
        var installer = new WinPerfUpdateInstaller
        {
            ReplacementCompletedForTesting = count =>
            {
                if (count == 1)
                {
                    throw new IOException("simulated_failure");
                }
            }
        };

        var error = Assert.Throws<UpdateInstallationException>(
            () => installer.Apply(staging, install, backups));

        Assert.True(error.RollbackSucceeded);
        Assert.Equal("old-exe", File.ReadAllText(Path.Combine(install, "WinPerf.exe")));
        Assert.Empty(Directory.GetDirectories(backups));
    }

    [Fact]
    public void Apply_RejectsUnexpectedStagingFileBeforeChangingInstall()
    {
        var (staging, install, backups) = CreateLayout();
        Directory.CreateDirectory(Path.Combine(staging, "data"));
        File.WriteAllText(Path.Combine(staging, "data", "settings.json"), "bad");

        Assert.Throws<InvalidDataException>(() =>
            new WinPerfUpdateInstaller().Apply(staging, install, backups));

        Assert.Equal("old-exe", File.ReadAllText(Path.Combine(install, "WinPerf.exe")));
    }

    [Fact]
    public void Apply_RejectsOverlappingInstallAndBackupTrees()
    {
        var (staging, install, _) = CreateLayout();
        var nestedBackups = Path.Combine(install, "backups");

        Assert.Throws<InvalidDataException>(() =>
            new WinPerfUpdateInstaller().Apply(staging, install, nestedBackups));
    }

    private (string Staging, string Install, string Backups) CreateLayout()
    {
        var staging = Path.Combine(_root, "staging");
        var install = Path.Combine(_root, "install");
        var backups = Path.Combine(_root, "backups");

        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(Path.Combine(install, "data"));
        Directory.CreateDirectory(Path.Combine(install, "tools", "iperf3"));
        Directory.CreateDirectory(backups);

        File.WriteAllText(Path.Combine(staging, "WinPerf.exe"), "new-exe");
        File.WriteAllText(Path.Combine(install, "WinPerf.exe"), "old-exe");

        return (staging, install, backups);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
