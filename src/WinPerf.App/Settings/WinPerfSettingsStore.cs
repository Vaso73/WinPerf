using System.IO;
using System.Text.Json;

namespace WinPerf.App.Settings;

public sealed class WinPerfSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SettingsDirectory { get; }
    public string SettingsPath { get; }

    public WinPerfSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinPerf"))
    {
    }

    public WinPerfSettingsStore(string settingsDirectory)
    {
        SettingsDirectory = settingsDirectory;
        SettingsPath = Path.Combine(settingsDirectory, "settings.json");
    }

    public WinPerfSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new WinPerfSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<WinPerfSettings>(json, JsonOptions) ?? new WinPerfSettings();
        }
        catch (JsonException)
        {
            return new WinPerfSettings();
        }
        catch (IOException)
        {
            return new WinPerfSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new WinPerfSettings();
        }
    }

    public void Save(WinPerfSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(SettingsDirectory);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
