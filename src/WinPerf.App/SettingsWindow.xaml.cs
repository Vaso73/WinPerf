using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace WinPerf.App;

public partial class SettingsWindow : Window
{
    private readonly string _appDirectory;

    public SettingsWindow(string? currentIperfPath, string appDirectory)
    {
        InitializeComponent();

        _appDirectory = appDirectory;
        IperfPathBox.Text = currentIperfPath ?? string.Empty;
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

    private string PortableEngineDirectory =>
        Path.Combine(_appDirectory, "tools", "iperf3");

    private string PortableExecutablePath =>
        Path.Combine(PortableEngineDirectory, "iperf3.exe");

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select iperf3.exe",
            Filter = "iperf3 executable (iperf3.exe)|iperf3.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(IperfExecutablePath))
        {
            var directory = Path.GetDirectoryName(IperfExecutablePath);

            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }

        if (dialog.ShowDialog(this) == true)
        {
            IperfPathBox.Text = dialog.FileName;
            ValidationText.Text = string.Empty;
        }
    }

    private void ImportPortableButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(IperfExecutablePath) || !File.Exists(IperfExecutablePath))
        {
            ValidationText.Text = "Select an existing iperf3.exe first.";
            return;
        }

        var sourceExe = Path.GetFullPath(IperfExecutablePath);
        var sourceDirectory = Path.GetDirectoryName(sourceExe);

        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            ValidationText.Text = "Selected iperf3.exe directory was not found.";
            return;
        }

        var destinationDirectory = Path.GetFullPath(PortableEngineDirectory);

        if (SameDirectory(sourceDirectory, destinationDirectory))
        {
            IperfPathBox.Text = string.Empty;
            ValidationText.Text = $"Already portable: {PortableExecutablePath}";
            return;
        }

        try
        {
            CopyDirectory(sourceDirectory, destinationDirectory);

            if (!File.Exists(PortableExecutablePath))
            {
                ValidationText.Text = "Portable import finished, but tools\\iperf3\\iperf3.exe was not found.";
                return;
            }

            IperfPathBox.Text = string.Empty;
            ValidationText.Text = $"Imported portable engine: {PortableExecutablePath}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ValidationText.Text = "Portable import failed: " + ex.Message;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        IperfPathBox.Text = string.Empty;
        ValidationText.Text = "Manual path cleared. WinPerf will use fallback detection.";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(IperfExecutablePath) && !File.Exists(IperfExecutablePath))
        {
            ValidationText.Text = "Selected iperf3.exe path does not exist.";
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
}
