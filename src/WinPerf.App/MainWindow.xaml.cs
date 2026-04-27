using System.Text;
using System.Windows;
using System.Windows.Controls;
using WinPerf.App.Settings;
using WinPerf.Core.Iperf;

namespace WinPerf.App;

public partial class MainWindow : Window
{
    private readonly WinPerfSettingsStore _settingsStore = new();
    private readonly IperfExecutableResolver _executableResolver = new();
    private readonly IperfProcessRunner _processRunner = new();

    private WinPerfSettings _settings = new();
    private IperfExecutableResolution _engineResolution = new(false, null, "NotConfigured", "iperf3.exe is not configured.");
    private CancellationTokenSource? _currentRunCancellation;
    private readonly StringBuilder _engineOutput = new();

    public MainWindow()
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "MainWindow");

        _settings = _settingsStore.Load();
        RefreshEngineStatus();
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

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRunCancellation is not null)
        {
            return;
        }

        RefreshEngineStatus();

        if (!_engineResolution.IsConfigured || string.IsNullOrWhiteSpace(_engineResolution.ExecutablePath))
        {
            EngineOutputText.Text = "iperf3.exe is not configured. Open Settings and select iperf3.exe first.";
            return;
        }

        try
        {
            var options = new IperfTestOptions
            {
                Server = ServerBox.Text.Trim(),
                Port = ParsePositiveInt(PortBox, "Port"),
                Streams = ParsePositiveInt(StreamsBox, "Streams"),
                DurationSeconds = ParsePositiveInt(DurationBox, "Duration"),
                Mode = GetSelectedMode(),
                AddressFamily = IperfAddressFamily.IPv4
            };

            var command = IperfCommandBuilder.BuildClientCommand(_engineResolution.ExecutablePath, options);

            _currentRunCancellation = new CancellationTokenSource();
            SetRunState(isRunning: true);

            _engineOutput.Clear();
            AppendEngineOutput("Running command:");
            AppendEngineOutput(command.ToDisplayString());
            AppendEngineOutput(string.Empty);

            var result = await _processRunner.RunAsync(
                command,
                async (line, cancellationToken) =>
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        AppendEngineOutput(line.Text);
                    });
                },
                _currentRunCancellation.Token);

            AppendEngineOutput(string.Empty);
            AppendEngineOutput($"Process exited with code {result.ExitCode}.");
        }
        catch (OperationCanceledException)
        {
            AppendEngineOutput(string.Empty);
            AppendEngineOutput("Test stopped by user.");
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or FormatException)
        {
            EngineOutputText.Text = "Invalid test configuration:" + Environment.NewLine + ex.Message;
        }
        catch (Exception ex)
        {
            AppendEngineOutput(string.Empty);
            AppendEngineOutput("Failed to run iperf3:");
            AppendEngineOutput(ex.Message);
        }
        finally
        {
            _currentRunCancellation?.Dispose();
            _currentRunCancellation = null;
            SetRunState(isRunning: false);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _currentRunCancellation?.Cancel();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings.IperfExecutablePath, AppContext.BaseDirectory)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _settings.IperfExecutablePath = dialog.IperfExecutablePath;
            _settingsStore.Save(_settings);
            RefreshEngineStatus();
        }
    }

    private void RefreshEngineStatus()
    {
        _engineResolution = _executableResolver.Resolve(AppContext.BaseDirectory, new IperfEngineSettings
        {
            ExecutablePath = _settings.IperfExecutablePath
        });

        EngineStatusText.Text = _engineResolution.IsConfigured
            ? $"{_engineResolution.Source}: {_engineResolution.ExecutablePath}"
            : _engineResolution.Message;
    }

    private void AdvancedCommandButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AdvancedCommandWindow
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            EngineOutputText.Text =
                "Advanced command preview:" + Environment.NewLine +
                dialog.CommandText;
        }
    }

    private void CustomCommandButton_Click(object sender, RoutedEventArgs e)
    {
        var initialCommand = EngineOutputText.Text.StartsWith("Command preview:", StringComparison.Ordinal)
            ? EngineOutputText.Text.Replace("Command preview:", string.Empty, StringComparison.Ordinal).Trim()
            : "iperf3.exe -c 10.100.100.1 -p 5201 -t 10 -P 10 --json-stream -4";

        var dialog = new CustomCommandWindow(initialCommand)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            EngineOutputText.Text =
                "Custom command preview:" + Environment.NewLine +
                dialog.CommandText;
        }
    }

    private IperfMode GetSelectedMode()
    {
        var selectedText = (ModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

        return selectedText switch
        {
            "TCP Upload" => IperfMode.TcpUpload,
            "TCP Download" => IperfMode.TcpDownload,
            "TCP Bidirectional" => IperfMode.TcpBidirectional,
            "UDP Upload" => IperfMode.UdpUpload,
            "UDP Download" => IperfMode.UdpDownload,
            _ => IperfMode.TcpUpload
        };
    }

    private static int ParsePositiveInt(TextBox box, string fieldName)
    {
        if (!int.TryParse(box.Text.Trim(), out var value) || value < 1)
        {
            throw new FormatException($"{fieldName} must be a positive number.");
        }

        return value;
    }

    private void SetRunState(bool isRunning)
    {
        StartButton.IsEnabled = !isRunning;
        StopButton.IsEnabled = isRunning;
        AdvancedCommandButton.IsEnabled = !isRunning;
        CustomCommandButton.IsEnabled = !isRunning;
    }

    private void AppendEngineOutput(string text)
    {
        _engineOutput.AppendLine(text);

        const int maxChars = 12000;

        if (_engineOutput.Length > maxChars)
        {
            _engineOutput.Remove(0, _engineOutput.Length - maxChars);
        }

        EngineOutputText.Text = _engineOutput.ToString();
    }
}
