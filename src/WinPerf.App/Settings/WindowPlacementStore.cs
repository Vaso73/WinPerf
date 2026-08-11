using System.IO;
using System.Text.Json;
using System.Windows;
using WinPerf.Core.Product;

namespace WinPerf.App.Settings;

public static class WindowPlacementStore
{
    private const string LayoutFileName = "window-layout.json";
    private const int CurrentLayoutDensityVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string SettingsDirectory =>
        Path.Combine(AppContext.BaseDirectory, WinPerfProductEdition.DataDirectoryName);

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

        var isCurrentDensityLayout = bounds.LayoutDensityVersion >= CurrentLayoutDensityVersion;

        if (IsUsable(bounds.Width))
        {
            window.Width = ClampRestoredDimension(
                bounds.Width,
                window.MinWidth,
                window.Width,
                SystemParameters.WorkArea.Width,
                isCurrentDensityLayout);
        }

        if (IsUsable(bounds.Height))
        {
            window.Height = ClampRestoredDimension(
                bounds.Height,
                window.MinHeight,
                window.Height,
                SystemParameters.WorkArea.Height,
                isCurrentDensityLayout);
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
            Width = ClampSavedDimension(width, window.MinWidth, SystemParameters.WorkArea.Width),
            Height = ClampSavedDimension(height, window.MinHeight, SystemParameters.WorkArea.Height),
            IsMaximized = window.WindowState == WindowState.Maximized,
            LayoutDensityVersion = CurrentLayoutDensityVersion
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

    private static double ClampRestoredDimension(
        double savedValue,
        double minimumValue,
        double defaultValue,
        double workAreaMaximum,
        bool isCurrentDensityLayout)
    {
        var lowerBound = IsUsable(minimumValue)
            ? minimumValue
            : 1;

        var defaultMaximum = IsUsable(defaultValue)
            ? Math.Max(lowerBound, defaultValue)
            : lowerBound;

        var upperBound = isCurrentDensityLayout && IsUsable(workAreaMaximum)
            ? Math.Max(defaultMaximum, workAreaMaximum)
            : defaultMaximum;

        return Math.Clamp(savedValue, lowerBound, upperBound);
    }

    private static double ClampSavedDimension(double value, double minimumValue, double workAreaMaximum)
    {
        var lowerBound = IsUsable(minimumValue)
            ? minimumValue
            : 1;

        var upperBound = IsUsable(workAreaMaximum)
            ? Math.Max(lowerBound, workAreaMaximum)
            : Math.Max(lowerBound, value);

        return Math.Clamp(value, lowerBound, upperBound);
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
        public int LayoutDensityVersion { get; set; }
    }
}
