using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WinPerf.App.Settings;

namespace WinPerf.App;

public partial class SettingsWindow : Window
{
    private readonly string _appDirectory;

    public SettingsWindow(string? currentIperf3Path, string? currentIperf2Path, string appDirectory)
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "SettingsWindow");

        _appDirectory = appDirectory;
        IperfPathBox.Text = currentIperf3Path ?? string.Empty;
        Iperf2PathBox.Text = currentIperf2Path ?? string.Empty;
        DataDirectoryText.Text = DataDirectory;
        PortableIperf3EngineDirectoryText.Text = PortableIperf3EngineDirectory;
        PortableIperf2EngineDirectoryText.Text = PortableIperf2EngineDirectory;
        IperfPathBox.Focus();
    }

    public string? IperfExecutablePath
    {
        get
        {
            var value = IperfPathBox.Text.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public string? Iperf2ExecutablePath
    {
        get
        {
            var value = Iperf2PathBox.Text.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    private string PortableIperf3EngineDirectory =>
        Path.Combine(_appDirectory, "tools", "iperf3");

    private string PortableIperf3ExecutablePath =>
        Path.Combine(PortableIperf3EngineDirectory, "iperf3.exe");

    private string PortableIperf2EngineDirectory =>
        Path.Combine(_appDirectory, "tools", "iperf2");

    private string PortableIperf2ExecutablePath =>
        Path.Combine(PortableIperf2EngineDirectory, "iperf.exe");

    private string PortableIperf2AlternateExecutablePath =>
        Path.Combine(PortableIperf2EngineDirectory, "iperf2.exe");

    private string DataDirectory =>
        Path.Combine(_appDirectory, "data");

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseExecutable(
            IperfPathBox,
            "Select iperf3.exe",
            "iperf3 executable (iperf3.exe)|iperf3.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*");
    }

    private void BrowseIperf2Button_Click(object sender, RoutedEventArgs e)
    {
        BrowseExecutable(
            Iperf2PathBox,
            "Select iperf2 executable",
            "Executable files (*.exe)|*.exe|Common iperf2 names (iperf.exe;iperf2.exe)|iperf.exe;iperf2.exe|All files (*.*)|*.*");
    }

    private void BrowseExecutable(TextBox target, string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        var currentPath = target.Text.Trim();

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var directory = Path.GetDirectoryName(currentPath);

            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }

        if (dialog.ShowDialog(this) == true)
        {
            target.Text = dialog.FileName;
            ValidationText.Text = string.Empty;
        }
    }

    private void ImportPortableButton_Click(object sender, RoutedEventArgs e)
    {
        ImportPortableEngine(
            IperfPathBox,
            PortableIperf3EngineDirectory,
            [PortableIperf3ExecutablePath],
            "iperf3.exe");
    }

    private void ImportPortableIperf2Button_Click(object sender, RoutedEventArgs e)
    {
        ImportPortableEngine(
            Iperf2PathBox,
            PortableIperf2EngineDirectory,
            [PortableIperf2ExecutablePath, PortableIperf2AlternateExecutablePath],
            "iperf.exe / iperf2.exe");
    }

    private void ImportPortableEngine(
        TextBox pathBox,
        string portableEngineDirectory,
        IReadOnlyList<string> expectedPortableExecutables,
        string engineLabel)
    {
        var executablePath = pathBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            ValidationText.Text = $"Select an existing {engineLabel} first.";
            return;
        }

        var sourceExe = Path.GetFullPath(executablePath);
        var sourceDirectory = Path.GetDirectoryName(sourceExe);

        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            ValidationText.Text = $"Selected {engineLabel} directory was not found.";
            return;
        }

        var destinationDirectory = Path.GetFullPath(portableEngineDirectory);

        if (SameDirectory(sourceDirectory, destinationDirectory))
        {
            pathBox.Text = string.Empty;
            ValidationText.Text = $"Already portable: {destinationDirectory}";
            return;
        }

        try
        {
            CopyDirectory(sourceDirectory, destinationDirectory);

            if (!expectedPortableExecutables.Any(File.Exists))
            {
                ValidationText.Text = $"Portable import finished, but {engineLabel} was not found in {destinationDirectory}.";
                return;
            }

            pathBox.Text = string.Empty;
            ValidationText.Text = $"Imported portable engine: {destinationDirectory}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ValidationText.Text = "Portable import failed: " + ex.Message;
        }
    }

    private void OpenPortableEngineDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        OpenDirectory(PortableIperf3EngineDirectory, "portable iperf3 engine folder");
    }

    private void OpenPortableIperf2EngineDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        OpenDirectory(PortableIperf2EngineDirectory, "portable iperf2 engine folder");
    }

    private void OpenDataDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        OpenDirectory(DataDirectory, "data folder");
    }

    private void OpenDirectory(string directory, string label)
    {
        try
        {
            Directory.CreateDirectory(directory);

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });

            ValidationText.Text = $"Opened {label}: {directory}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ValidationText.Text = $"Could not open {label}: " + ex.Message;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        IperfPathBox.Text = string.Empty;
        ValidationText.Text = "Manual iperf3 path cleared. WinPerf will use fallback detection.";
    }

    private void ClearIperf2Button_Click(object sender, RoutedEventArgs e)
    {
        Iperf2PathBox.Text = string.Empty;
        ValidationText.Text = "Manual iperf2 path cleared. WinPerf will use fallback detection.";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(IperfExecutablePath) && !File.Exists(IperfExecutablePath))
        {
            ValidationText.Text = "Selected iperf3.exe path does not exist.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(Iperf2ExecutablePath) && !File.Exists(Iperf2ExecutablePath))
        {
            ValidationText.Text = "Selected iperf.exe / iperf2.exe path does not exist.";
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationFile = Path.Combine(destinationDirectory, relativePath);
            var destinationParent = Path.GetDirectoryName(destinationFile);

            if (!string.IsNullOrWhiteSpace(destinationParent))
            {
                Directory.CreateDirectory(destinationParent);
            }

            File.Copy(file, destinationFile, overwrite: true);
        }
    }

    private static bool SameDirectory(string left, string right)
    {
        var leftFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
        var rightFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));

        return string.Equals(leftFull, rightFull, StringComparison.OrdinalIgnoreCase);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
