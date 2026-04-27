using System.Globalization;
using System.Text;
using System.Text.Json;
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

    private const int MaxRecentServers = 20;

    public MainWindow()
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "MainWindow");

        _settings = _settingsStore.Load();
        RefreshEngineStatus();
        PopulateRecentServers();
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
                Server = GetServerText(),
                Port = ParsePositiveInt(PortBox, "Port"),
                Streams = ParsePositiveInt(StreamsBox, "Streams"),
                DurationSeconds = ParsePositiveInt(DurationBox, "Duration"),
                Mode = GetSelectedMode(),
                AddressFamily = IperfAddressFamily.IPv4
            };

            var command = IperfCommandBuilder.BuildClientCommand(_engineResolution.ExecutablePath, options);

            SaveRecentServer(options.Server);

            _currentRunCancellation = new CancellationTokenSource();
            SetRunState(isRunning: true);
            ResetLiveMetrics();

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
                        if (line.Stream == IperfOutputStream.StandardOutput)
                        {
                            if (IperfJsonStreamParser.TryParseIntervalSample(line.Text, out var sample))
                            {
                                UpdateLiveMetrics(sample);
                                AppendEngineOutput(FormatIntervalSample(sample));
                                return;
                            }

                            if (TryFormatJsonStreamEvent(line.Text, out var eventMessage))
                            {
                                AppendEngineOutput(eventMessage);
                                return;
                            }
                        }

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

    private string GetServerText()
    {
        return ServerBox.Text.Trim();
    }

    private void PopulateRecentServers()
    {
        ServerBox.Items.Clear();

        var servers = (_settings.RecentServers ?? [])
            .Where(server => !string.IsNullOrWhiteSpace(server))
            .Select(server => server.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentServers)
            .ToList();

        foreach (var server in servers)
        {
            ServerBox.Items.Add(server);
        }

        ServerBox.Text = !string.IsNullOrWhiteSpace(_settings.LastServer)
            ? _settings.LastServer
            : servers.FirstOrDefault() ?? "10.100.100.1";
    }

    private void SaveRecentServer(string server)
    {
        server = server.Trim();

        if (string.IsNullOrWhiteSpace(server))
        {
            return;
        }

        var servers = new List<string> { server };

        servers.AddRange(
            (_settings.RecentServers ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Where(item => !string.Equals(item, server, StringComparison.OrdinalIgnoreCase)));

        _settings.LastServer = server;
        _settings.RecentServers = servers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentServers)
            .ToList();

        _settingsStore.Save(_settings);
        PopulateRecentServers();
        ServerBox.Text = server;
    }

    private void RemoveServerButton_Click(object sender, RoutedEventArgs e)
    {
        var server = GetServerText();

        if (string.IsNullOrWhiteSpace(server))
        {
            return;
        }

        var servers = (_settings.RecentServers ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Where(item => !string.Equals(item, server, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentServers)
            .ToList();

        _settings.RecentServers = servers;

        if (string.Equals(_settings.LastServer, server, StringComparison.OrdinalIgnoreCase))
        {
            _settings.LastServer = servers.FirstOrDefault();
        }

        _settingsStore.Save(_settings);
        PopulateRecentServers();

        ServerBox.Text = _settings.LastServer
            ?? servers.FirstOrDefault()
            ?? "10.100.100.1";
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
        RemoveServerButton.IsEnabled = !isRunning;
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
        EngineOutputText.ScrollToEnd();
    }

    private void ResetLiveMetrics()
    {
        ThroughputValueText.Text = "0 Mbps";
        JitterValueText.Text = "-- ms";
        LossValueText.Text = "-- %";
        LiveStatusText.Text = "Waiting for samples...";
    }

    private void UpdateLiveMetrics(IperfIntervalSample sample)
    {
        if (sample.MegabitsPerSecond is double megabitsPerSecond)
        {
            ThroughputValueText.Text = FormatMegabits(megabitsPerSecond);
        }

        JitterValueText.Text = sample.JitterMs is double jitterMs
            ? jitterMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms"
            : "n/a";

        LossValueText.Text = sample.LostPercent is double lostPercent
            ? lostPercent.ToString("0.0", CultureInfo.InvariantCulture) + " %"
            : "n/a";

        LiveStatusText.Text = sample.Seconds is double seconds
            ? "Last sample " + seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s"
            : "Receiving samples...";
    }

    private static string FormatMegabits(double megabitsPerSecond)
    {
        var format = megabitsPerSecond >= 100
            ? "0"
            : "0.0";

        return megabitsPerSecond.ToString(format, CultureInfo.InvariantCulture) + " Mbps";
    }

    private static string FormatIntervalSample(IperfIntervalSample sample)
    {
        var parts = new List<string>();

        parts.Add(sample.Seconds is double seconds
            ? "Interval " + seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s"
            : "Interval");

        if (sample.MegabitsPerSecond is double megabitsPerSecond)
        {
            parts.Add(FormatMegabits(megabitsPerSecond));
        }

        if (sample.JitterMs is double jitterMs)
        {
            parts.Add("jitter " + jitterMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms");
        }

        if (sample.LostPercent is double lostPercent)
        {
            parts.Add("loss " + lostPercent.ToString("0.0", CultureInfo.InvariantCulture) + " %");
        }

        return string.Join(" · ", parts);
    }

    private static bool TryFormatJsonStreamEvent(string text, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("event", out var eventElement))
            {
                return false;
            }

            var eventName = eventElement.GetString();

            message = eventName?.ToLowerInvariant() switch
            {
                "start" => "Test started.",
                "end" => "Test completed.",
                "error" => "iperf3 error: " + GetJsonEventDataText(root),
                null or "" => "iperf3 event received.",
                _ => "iperf3 event: " + eventName
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string GetJsonEventDataText(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data))
        {
            return "unknown error";
        }

        return data.ValueKind == JsonValueKind.String
            ? data.GetString() ?? "unknown error"
            : data.ToString();
    }
}
