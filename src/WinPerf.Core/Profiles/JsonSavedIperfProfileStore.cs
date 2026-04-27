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
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        return Path.Combine(appData, "WinPerf", DefaultFileName);
    }

    public async Task<SavedIperfProfilesDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new SavedIperfProfilesDocument();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var document = await JsonSerializer.DeserializeAsync<SavedIperfProfilesDocument>(
                stream,
                JsonOptions,
                cancellationToken);

            if (document is null)
            {
                return new SavedIperfProfilesDocument();
            }

            SavedIperfProfileValidation.ThrowIfInvalid(document);
            return document;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to parse saved iperf profiles from '{_filePath}'.", ex);
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
}
