using System.Windows;
using WinPerf.App.Settings;

namespace WinPerf.App;

public partial class CustomCommandWindow : Window
{
    private const int MaxRecentCustomCommands = 20;

    private readonly WinPerfSettingsStore _settingsStore = new();
    private WinPerfSettings _settings = new();

    public CustomCommandWindow(string initialCommand)
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "CustomCommandWindow");

        _settings = _settingsStore.Load();
        PopulateRecentCommands();

        if (!string.IsNullOrWhiteSpace(initialCommand))
        {
            CommandBox.Text = NormalizeCustomCommandText(initialCommand);
        }

        CommandBox.Focus();
    }

    public string CommandText => NormalizeCustomCommandText(CommandBox.Text);

    private void PopulateRecentCommands()
    {
        CommandBox.Items.Clear();

        foreach (var command in GetRecentCommands())
        {
            CommandBox.Items.Add(command);
        }
    }

    private List<string> GetRecentCommands()
    {
        return (_settings.RecentCustomCommands ?? [])
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Select(NormalizeCustomCommandText)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentCustomCommands)
            .ToList();
    }

    private void SaveRecentCommand(string command)
    {
        command = NormalizeCustomCommandText(command);

        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var commands = new List<string> { command };

        commands.AddRange(
            GetRecentCommands()
                .Where(item => !string.Equals(item, command, StringComparison.OrdinalIgnoreCase)));

        _settings.RecentCustomCommands = commands
            .Take(MaxRecentCustomCommands)
            .ToList();

        _settingsStore.Save(_settings);
        PopulateRecentCommands();
        CommandBox.Text = command;
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.RecentCustomCommands = [];
        _settingsStore.Save(_settings);
        PopulateRecentCommands();
    }

    private static string NormalizeCustomCommandText(string commandText)
    {
        commandText = commandText.Trim();

        if (string.IsNullOrWhiteSpace(commandText))
        {
            return string.Empty;
        }

        const string executableName = "iperf3.exe";
        var executableIndex = commandText.IndexOf(executableName, StringComparison.OrdinalIgnoreCase);

        if (executableIndex >= 0)
        {
            commandText = commandText[(executableIndex + executableName.Length)..];
        }

        return commandText.Trim().TrimStart('"').Trim();
    }

    private void UseButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CommandText))
        {
            MessageBox.Show(
                this,
                "Enter iperf3 arguments first.",
                "WinPerf",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        SaveRecentCommand(CommandText);
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
}
