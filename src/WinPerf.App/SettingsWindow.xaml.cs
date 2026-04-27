using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace WinPerf.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(string? currentIperfPath)
    {
        InitializeComponent();
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
}
