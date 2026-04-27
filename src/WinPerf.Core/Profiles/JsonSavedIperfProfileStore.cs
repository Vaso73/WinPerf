using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinPerf.Core.Profiles;

public sealed class JsonSavedIperfProfileStore : ISavedIperfProfileStore
{
    public const string DefaultFileName = "profiles.json";

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

    public JsonSavedIperfProfileStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public static string GetDefaultFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "data", DefaultFileName);
    }

    private static string GetLegacyDefaultFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinPerf",
            DefaultFileName);
    }

    public async Task<SavedIperfProfilesDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        var readPath = ResolveReadPath();

        if (readPath is null)
        {
            return new SavedIperfProfilesDocument();
        }

        try
        {
            await using var stream = File.OpenRead(readPath);
            var document = await JsonSerializer.DeserializeAsync<SavedIperfProfilesDocument>(
                stream,
                JsonOptions,
                cancellationToken);

            if (document is null)
            {
                return new SavedIperfProfilesDocument();
            }

            SavedIperfProfileValidation.ThrowIfInvalid(document);

            if (!SamePath(readPath, _filePath))
            {
                await SaveAsync(document, cancellationToken);
            }

            return document;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to parse saved iperf profiles from '{readPath}'.", ex);
        }
    }

    public async Task SaveAsync(
        SavedIperfProfilesDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        SavedIperfProfileValidation.ThrowIfInvalid(document);

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

    private string? ResolveReadPath()
    {
        if (File.Exists(_filePath))
        {
            return _filePath;
        }

        var defaultPath = GetDefaultFilePath();

        if (SamePath(_filePath, defaultPath))
        {
            var legacyPath = GetLegacyDefaultFilePath();

            if (!SamePath(_filePath, legacyPath) && File.Exists(legacyPath))
            {
                return legacyPath;
            }
        }

        return null;
    }

    private static bool SamePath(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
