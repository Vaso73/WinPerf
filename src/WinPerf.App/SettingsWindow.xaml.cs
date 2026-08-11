using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WinPerf.App.Settings;
using WinPerf.Core.Iperf;
using WinPerf.Core.Localization;
using WinPerf.Core.Product;

namespace WinPerf.App;

public partial class SettingsWindow : Window
{
    private readonly string _appDirectory;

    public SettingsWindow(
        string? currentIperf3Path,
        string? currentIperf2Path,
        string appDirectory,
        string? currentLanguageCode)
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "SettingsWindow");
        AppText.ApplyTo(this);

        _appDirectory = appDirectory;
        PopulateLanguageBox(currentLanguageCode);
        IperfPathBox.Text = currentIperf3Path ?? string.Empty;
        Iperf2PathBox.Text = currentIperf2Path ?? string.Empty;
        DataDirectoryText.Text = DataDirectory;
        PortableIperf3EngineDirectoryText.Text = PortableIperf3EngineDirectory;
        PortableIperf2EngineDirectoryText.Text = PortableIperf2EngineDirectory;
        ApplyProductEditionBoundary();
        IperfPathBox.Focus();
    }

    public event EventHandler<SettingsAppliedEventArgs>? Applied;

    public string SelectedLanguageCode { get; private set; } = LanguagePackService.DefaultLanguageCode;

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
            if (!WinPerfProductEdition.SupportsIperf2)
            {
                return null;
            }

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

    private string DataDirectory =>
        Path.Combine(_appDirectory, WinPerfProductEdition.DataDirectoryName);

    private void ApplyProductEditionBoundary()
    {
        if (WinPerfProductEdition.SupportsIperf2)
        {
            return;
        }

        Iperf2PathBox.Text = string.Empty;

        foreach (var element in new FrameworkElement[]
                 {
                     Iperf2SettingsLabel,
                     Iperf2PathBox,
                     BrowseIperf2Button,
                     ImportPortableIperf2Button,
                     ClearIperf2Button,
                     PortableIperf2EngineDirectoryLabel,
                     PortableIperf2EngineDirectoryText,
                     OpenPortableIperf2EngineDirectoryButton
                 })
        {
            element.Visibility = Visibility.Collapsed;
        }
    }

    private void PopulateLanguageBox(string? currentLanguageCode)
    {
        var languages = AppText.AvailableLanguages
            .Select(language => new LanguageChoice(
                language.LanguageCode,
                AppText.GetLanguageDisplayName(language)))
            .ToList();

        if (languages.Count == 0)
        {
            languages.Add(new LanguageChoice(
                LanguagePackService.DefaultLanguageCode,
                AppText.T("WinPerfLanguage.EnglishDisplay")));
        }

        var selectedCode = string.IsNullOrWhiteSpace(currentLanguageCode)
            ? LanguagePackService.DefaultLanguageCode
            : currentLanguageCode;

        if (!languages.Any(language =>
                string.Equals(language.Code, selectedCode, StringComparison.OrdinalIgnoreCase)))
        {
            selectedCode = LanguagePackService.DefaultLanguageCode;
        }

        LanguageBox.ItemsSource = languages;
        LanguageBox.DisplayMemberPath = nameof(LanguageChoice.DisplayName);
        LanguageBox.SelectedValuePath = nameof(LanguageChoice.Code);
        LanguageBox.SelectedValue = selectedCode;
        SelectedLanguageCode = selectedCode;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseExecutable(
            IperfPathBox,
            "Select iperf3.exe",
            "iperf3 executable (iperf3.exe)|iperf3.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*");
    }

    private void BrowseIperf2Button_Click(object sender, RoutedEventArgs e)
    {
        if (!WinPerfProductEdition.SupportsIperf2)
        {
            ValidationText.Text = AppText.T("Available in WinPerf Sponsor Pro.");
            return;
        }

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
            PortableIperf3ExecutablePath,
            "iperf3.exe");
    }

    private void ImportPortableIperf2Button_Click(object sender, RoutedEventArgs e)
    {
        if (!WinPerfProductEdition.SupportsIperf2)
        {
            ValidationText.Text = AppText.T("Available in WinPerf Sponsor Pro.");
            return;
        }

        ImportPortableEngine(
            Iperf2PathBox,
            PortableIperf2ExecutablePath,
            "iperf2 executable");
    }

    private void ImportPortableEngine(
        TextBox pathBox,
        string portableExecutablePath,
        string engineLabel)
    {
        var executablePath = pathBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(executablePath) ||
            !File.Exists(executablePath))
        {
            ValidationText.Text =
                AppText.F("Select an existing {0} first.", engineLabel);
            return;
        }

        try
        {
            var importedPath = PortableExecutableImporter.Import(
                executablePath,
                portableExecutablePath);

            pathBox.Text = string.Empty;
            ValidationText.Text =
                AppText.F("Imported portable engine: {0}", importedPath);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException)
        {
            ValidationText.Text =
                AppText.F("Portable import failed: {0}", ex.Message);
        }
    }

    private void OpenPortableEngineDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        OpenDirectory(PortableIperf3EngineDirectory, AppText.T("portable iperf3 engine folder"));
    }

    private void OpenPortableIperf2EngineDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (!WinPerfProductEdition.SupportsIperf2)
        {
            ValidationText.Text = AppText.T("Available in WinPerf Sponsor Pro.");
            return;
        }

        OpenDirectory(PortableIperf2EngineDirectory, AppText.T("portable iperf2 engine folder"));
    }

    private void OpenDataDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        OpenDirectory(DataDirectory, AppText.T("data folder"));
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

            ValidationText.Text = AppText.F("Opened {0}: {1}", label, directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ValidationText.Text = AppText.F("Could not open {0}: {1}", label, ex.Message);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        IperfPathBox.Text = string.Empty;
        ValidationText.Text = AppText.T("Manual iperf3 path cleared. WinPerf will use fallback detection.");
    }

    private void ClearIperf2Button_Click(object sender, RoutedEventArgs e)
    {
        if (!WinPerfProductEdition.SupportsIperf2)
        {
            ValidationText.Text = AppText.T("Available in WinPerf Sponsor Pro.");
            return;
        }

        Iperf2PathBox.Text = string.Empty;
        ValidationText.Text = AppText.T("Manual iperf2 path cleared. WinPerf will use fallback detection.");
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCaptureSettings())
        {
            return;
        }

        Applied?.Invoke(
            this,
            new SettingsAppliedEventArgs(
                IperfExecutablePath,
                Iperf2ExecutablePath,
                SelectedLanguageCode));

        PopulateLanguageBox(SelectedLanguageCode);
        AppText.ApplyTo(this);
        ValidationText.Text = AppText.T("Changes applied for this session. Save to keep them after restart.");
    }

    private bool TryCaptureSettings()
    {
        if (!string.IsNullOrWhiteSpace(IperfExecutablePath) && !File.Exists(IperfExecutablePath))
        {
            ValidationText.Text = AppText.T("Selected iperf3.exe path does not exist.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Iperf2ExecutablePath) && !File.Exists(Iperf2ExecutablePath))
        {
            ValidationText.Text = AppText.T("Selected iperf.exe / iperf2.exe path does not exist.");
            return false;
        }

        SelectedLanguageCode =
            LanguageBox.SelectedValue as string ??
            LanguagePackService.DefaultLanguageCode;

        return true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCaptureSettings())
        {
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
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

    private sealed record LanguageChoice(string Code, string DisplayName);
}

public sealed record SettingsAppliedEventArgs(
    string? IperfExecutablePath,
    string? Iperf2ExecutablePath,
    string LanguageCode);
