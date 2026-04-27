using System.IO;
using System.Text.Json;
using System.Windows;

namespace WinPerf.App.Settings;

public static class WindowPlacementStore
{
    private const string LayoutFileName = "window-layout.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string SettingsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "data");

    private static string LayoutPath =>
        Path.Combine(SettingsDirectory, LayoutFileName);

    private static string LegacyLayoutPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinPerf",
            LayoutFileName);

    public static void Track(Window window, string key)
    {
        Apply(window, key);

        window.Closing += (_, _) =>
        {
            Save(window, key);
        };
    }

    private static void Apply(Window window, string key)
    {
        var layout = Load();

        if (!layout.TryGetValue(key, out var bounds))
        {
            return;
        }

        if (IsUsable(bounds.Width))
        {
            window.Width = Math.Max(window.MinWidth, bounds.Width);
        }

        if (IsUsable(bounds.Height))
        {
            window.Height = Math.Max(window.MinHeight, bounds.Height);
        }

        if (bounds.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    private static void Save(Window window, string key)
    {
        var layout = Load();
        var restoreBounds = window.RestoreBounds;

        var width = window.WindowState == WindowState.Maximized
            ? restoreBounds.Width
            : window.Width;

        var height = window.WindowState == WindowState.Maximized
            ? restoreBounds.Height
            : window.Height;

        if (!IsUsable(width) || !IsUsable(height))
        {
            return;
        }

        layout[key] = new SavedWindowBounds
        {
            Width = Math.Max(window.MinWidth, width),
            Height = Math.Max(window.MinHeight, height),
            IsMaximized = window.WindowState == WindowState.Maximized
        };

        Directory.CreateDirectory(SettingsDirectory);

        var json = JsonSerializer.Serialize(layout, JsonOptions);
        File.WriteAllText(LayoutPath, json);
    }

    private static Dictionary<string, SavedWindowBounds> Load()
    {
        if (TryLoad(LayoutPath, out var layout))
        {
            return layout;
        }

        if (!SamePath(LayoutPath, LegacyLayoutPath) && TryLoad(LegacyLayoutPath, out layout))
        {
            return layout;
        }

        return new Dictionary<string, SavedWindowBounds>(StringComparer.Ordinal);
    }

    private static bool TryLoad(string path, out Dictionary<string, SavedWindowBounds> layout)
    {
        layout = new Dictionary<string, SavedWindowBounds>(StringComparer.Ordinal);

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);

            layout = JsonSerializer.Deserialize<Dictionary<string, SavedWindowBounds>>(json, JsonOptions)
                     ?? new Dictionary<string, SavedWindowBounds>(StringComparer.Ordinal);

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

    private static bool IsUsable(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }

    private sealed class SavedWindowBounds
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }
    }
}
