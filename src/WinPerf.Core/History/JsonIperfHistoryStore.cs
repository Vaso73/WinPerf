using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinPerf.Core.History;

public sealed class JsonIperfHistoryStore
{
    public const string DefaultFileName = "history.json";
    public const int DefaultMaxEntries = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly string _filePath;

    public JsonIperfHistoryStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public static string GetDefaultFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "data", DefaultFileName);
    }

    public async Task<IperfHistoryDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new IperfHistoryDocument();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var document = await JsonSerializer.DeserializeAsync<IperfHistoryDocument>(
                stream,
                JsonOptions,
                cancellationToken);

            return SortNewestFirst(document ?? new IperfHistoryDocument());
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to parse iperf history from '{_filePath}'.", ex);
        }
    }

    public async Task AddAsync(
        IperfHistoryEntry entry,
        int maxEntries = DefaultMaxEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (maxEntries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "History must keep at least one entry.");
        }

        var document = await LoadAsync(cancellationToken);
        document.Entries.Insert(0, entry);
        document = new IperfHistoryDocument
        {
            Entries = document.Entries
                .OrderByDescending(item => item.FinishedAtUtc)
                .ThenByDescending(item => item.StartedAtUtc)
                .Take(maxEntries)
                .ToList()
        };

        await SaveAsync(document, cancellationToken);
    }

    public async Task SaveAsync(
        IperfHistoryDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var fullPath = Path.GetFullPath(_filePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static IperfHistoryDocument SortNewestFirst(IperfHistoryDocument document)
    {
        return new IperfHistoryDocument
        {
            Entries = document.Entries
                .OrderByDescending(item => item.FinishedAtUtc)
                .ThenByDescending(item => item.StartedAtUtc)
                .ToList()
        };
    }
}
