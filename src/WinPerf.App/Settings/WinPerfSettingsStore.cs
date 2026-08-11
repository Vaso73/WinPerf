using System.IO;
using System.Text.Json;
using WinPerf.Core.Product;

namespace WinPerf.App.Settings;

public sealed class WinPerfSettingsStore
{
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsDirectory { get; }
    public string SettingsPath { get; }

    public WinPerfSettingsStore()
        : this(GetDefaultSettingsDirectory())
    {
    }

    public WinPerfSettingsStore(string settingsDirectory)
    {
        SettingsDirectory = settingsDirectory;
        SettingsPath = Path.Combine(settingsDirectory, SettingsFileName);
    }

    private static string GetDefaultSettingsDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, WinPerfProductEdition.DataDirectoryName);
    }

    private static string GetLegacySettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinPerf",
            SettingsFileName);
    }

    public WinPerfSettings Load()
    {
        if (TryLoad(SettingsPath, out var settings))
        {
            return settings;
        }

        var legacyPath = GetLegacySettingsPath();

        if (!SamePath(SettingsPath, legacyPath) && TryLoad(legacyPath, out settings))
        {
            Save(settings);
            return settings;
        }

        return new WinPerfSettings();
    }

    public void Save(WinPerfSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(SettingsDirectory);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private static bool TryLoad(string path, out WinPerfSettings settings)
    {
        settings = new WinPerfSettings();

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            settings = JsonSerializer.Deserialize<WinPerfSettings>(json, JsonOptions) ?? new WinPerfSettings();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool SamePath(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
