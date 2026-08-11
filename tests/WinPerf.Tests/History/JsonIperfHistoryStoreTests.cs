using WinPerf.Core.History;
using WinPerf.Core.Iperf;

namespace WinPerf.Tests.History;

public sealed class JsonIperfHistoryStoreTests
{
    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsEmptyDocument()
    {
        var directory = CreateTempDirectory();
        var store = new JsonIperfHistoryStore(Path.Combine(directory, "data", "history.json"));

        var document = await store.LoadAsync();

        Assert.Empty(document.Entries);
    }

    [Fact]
    public async Task AddAsync_SavesNewestEntriesFirstAndTrims()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "data", "history.json");
        var store = new JsonIperfHistoryStore(path);

        var older = CreateEntry(DateTimeOffset.Parse("2026-08-11T10:00:00Z"));
        var newer = CreateEntry(DateTimeOffset.Parse("2026-08-11T11:00:00Z"));

        await store.AddAsync(older, maxEntries: 2);
        await store.AddAsync(newer, maxEntries: 1);

        var document = await store.LoadAsync();

        var entry = Assert.Single(document.Entries);
        Assert.Equal(newer.Id, entry.Id);
        Assert.Equal(IperfEngine.Iperf3, entry.Engine);
        Assert.Equal(IperfMode.TcpUpload, entry.Mode);
        Assert.Equal("test.local", entry.Server);
    }

    [Fact]
    public void GetDefaultFilePath_UsesPortableDataFolderNextToRuntime()
    {
        var path = JsonIperfHistoryStore.GetDefaultFilePath();

        Assert.Equal(
            Path.Combine(AppContext.BaseDirectory, "data", JsonIperfHistoryStore.DefaultFileName),
            path);
    }

    private static IperfHistoryEntry CreateEntry(DateTimeOffset finishedAtUtc)
    {
        return new IperfHistoryEntry
        {
            StartedAtUtc = finishedAtUtc.AddSeconds(-10),
            FinishedAtUtc = finishedAtUtc,
            Engine = IperfEngine.Iperf3,
            Mode = IperfMode.TcpUpload,
            Server = "test.local",
            Port = 5201,
            Streams = 10,
            DurationSeconds = 10,
            ExitCode = 0,
            Succeeded = true,
            AverageMbps = 923,
            CommandPreview = "-c test.local -p 5201 -t 10 -P 10",
            Summary = "iperf3 · TCP Upload · test.local:5201"
        };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "winperf-history-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
