using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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
    private readonly List<double> _throughputSamples = new();

    private const int MaxRecentServers = 20;
    private const int MaxThroughputSamples = 60;

    public MainWindow()
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "MainWindow");

        _settings = _settingsStore.Load();
        RefreshEngineStatus();
        PopulateRecentServers();
        ApplyDashboardLayout();

        Loaded += (_, _) => ApplyResponsiveDashboardLayout();
        Closing += (_, _) => SaveDashboardLayout();
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

                            if (IperfJsonStreamParser.TryParseEndSummarySample(line.Text, out var endSample))
                            {
                                UpdateLiveMetrics(endSample);
                                AppendEngineOutput(FormatEndSummarySample(endSample));
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

    private void DashboardContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveDashboardLayout();
    }

    private void ApplyResponsiveDashboardLayout()
    {
        if (!IsLoaded)
        {
            return;
        }

        var useStackedLayout = DashboardContentGrid.ActualWidth < 920;

        if (useStackedLayout)
        {
            Grid.SetRow(ConfigColumn, 0);
            Grid.SetRowSpan(ConfigColumn, 1);
            Grid.SetColumn(ConfigColumn, 0);
            Grid.SetColumnSpan(ConfigColumn, 2);

            Grid.SetRow(ResultsGrid, 1);
            Grid.SetRowSpan(ResultsGrid, 1);
            Grid.SetColumn(ResultsGrid, 0);
            Grid.SetColumnSpan(ResultsGrid, 2);
            ResultsGrid.Margin = new Thickness(0, 18, 0, 0);
            return;
        }

        Grid.SetRow(ConfigColumn, 0);
        Grid.SetRowSpan(ConfigColumn, 2);
        Grid.SetColumn(ConfigColumn, 0);
        Grid.SetColumnSpan(ConfigColumn, 1);

        Grid.SetRow(ResultsGrid, 0);
        Grid.SetRowSpan(ResultsGrid, 2);
        Grid.SetColumn(ResultsGrid, 1);
        Grid.SetColumnSpan(ResultsGrid, 1);
        ResultsGrid.Margin = new Thickness(18, 0, 0, 0);
    }

    private void ApplyDashboardLayout()
    {
        if (_settings.DashboardEngineOutputHeight is not double height)
        {
            return;
        }

        if (double.IsNaN(height) || double.IsInfinity(height) || height < EngineOutputRow.MinHeight)
        {
            return;
        }

        EngineOutputRow.Height = new GridLength(height, GridUnitType.Pixel);
        LiveThroughputRow.Height = new GridLength(1, GridUnitType.Star);
    }

    private void SaveDashboardLayout()
    {
        var height = EngineOutputRow.ActualHeight;

        if (double.IsNaN(height) || double.IsInfinity(height) || height < EngineOutputRow.MinHeight)
        {
            return;
        }

        _settings.DashboardEngineOutputHeight = Math.Round(height, 0);
        _settingsStore.Save(_settings);
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

        _throughputSamples.Clear();
        RenderThroughputChart();
    }

    private void UpdateLiveMetrics(IperfIntervalSample sample)
    {
        if (sample.MegabitsPerSecond is double megabitsPerSecond)
        {
            ThroughputValueText.Text = FormatMegabits(megabitsPerSecond);
            AddThroughputSample(megabitsPerSecond);
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

    private void ThroughputChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderThroughputChart();
    }

    private void AddThroughputSample(double megabitsPerSecond)
    {
        if (double.IsNaN(megabitsPerSecond) || double.IsInfinity(megabitsPerSecond) || megabitsPerSecond < 0)
        {
            return;
        }

        _throughputSamples.Add(megabitsPerSecond);

        if (_throughputSamples.Count > MaxThroughputSamples)
        {
            _throughputSamples.RemoveRange(0, _throughputSamples.Count - MaxThroughputSamples);
        }

        RenderThroughputChart();
    }

    private void RenderThroughputChart()
    {
        ThroughputChartLine.Points.Clear();
        ThroughputChartGridCanvas.Children.Clear();
        ThroughputChartMarkerCanvas.Children.Clear();

        var width = ThroughputChartCanvas.ActualWidth;
        var height = ThroughputChartCanvas.ActualHeight;

        if (width <= 80 || height <= 80)
        {
            ThroughputChartPlaceholder.Visibility = _throughputSamples.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            return;
        }

        ThroughputChartGridCanvas.Width = width;
        ThroughputChartGridCanvas.Height = height;
        ThroughputChartMarkerCanvas.Width = width;
        ThroughputChartMarkerCanvas.Height = height;

        const double leftPadding = 58;
        const double rightPadding = 18;
        const double topPadding = 28;
        const double bottomPadding = 38;

        var plotLeft = leftPadding;
        var plotTop = topPadding;
        var plotWidth = Math.Max(1, width - leftPadding - rightPadding);
        var plotHeight = Math.Max(1, height - topPadding - bottomPadding);
        var plotBottom = plotTop + plotHeight;

        var axis = CalculateThroughputAxis();

        DrawThroughputChartFrame(plotLeft, plotTop, plotWidth, plotHeight, axis.Min, axis.Max, axis.Step);

        if (_throughputSamples.Count == 0)
        {
            ThroughputChartPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        ThroughputChartPlaceholder.Visibility = Visibility.Collapsed;

        var points = new PointCollection();
        var axisRange = Math.Max(1, axis.Max - axis.Min);

        for (var i = 0; i < _throughputSamples.Count; i++)
        {
            var x = _throughputSamples.Count == 1
                ? plotLeft
                : plotLeft + (plotWidth * i / (_throughputSamples.Count - 1));

            var normalized = (_throughputSamples[i] - axis.Min) / axisRange;
            normalized = Math.Clamp(normalized, 0, 1);

            var y = plotBottom - (normalized * plotHeight);

            points.Add(new Point(x, y));
            DrawChartMarker(x, y);
        }

        ThroughputChartLine.Points = points;

        var accentBrush = TryFindResource("Accent") as Brush ?? Brushes.DeepSkyBlue;

        DrawChartText(
            "Bandwidth",
            plotLeft + 8,
            5,
            14,
            FontWeights.SemiBold,
            accentBrush);

        DrawChartText(
            BuildThroughputSummary(),
            plotLeft + 118,
            5,
            11,
            FontWeights.SemiBold,
            accentBrush);
    }

    private (double Min, double Max, double Step) CalculateThroughputAxis()
    {
        if (_throughputSamples.Count == 0)
        {
            return (0, 1000, 250);
        }

        var max = Math.Max(10, _throughputSamples.Max());
        var wantedMax = max * 1.04;
        var step = NiceStep(wantedMax / 4);
        var axisMax = Math.Ceiling(wantedMax / step) * step;

        if (axisMax <= 0)
        {
            axisMax = step * 4;
        }

        return (0, axisMax, step);
    }

    private static double NiceStep(double value)
    {
        if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
        {
            return 1;
        }

        var exponent = Math.Floor(Math.Log10(value));
        var fraction = value / Math.Pow(10, exponent);

        double niceFraction =
            fraction <= 1 ? 1 :
            fraction <= 2 ? 2 :
            fraction <= 2.5 ? 2.5 :
            fraction <= 5 ? 5 :
            10;

        return niceFraction * Math.Pow(10, exponent);
    }

    private void DrawThroughputChartFrame(
        double plotLeft,
        double plotTop,
        double plotWidth,
        double plotHeight,
        double axisMin,
        double axisMax,
        double axisStep)
    {
        var gridBrush = TryFindResource("BorderSoft") as Brush ?? Brushes.DimGray;
        var textBrush = TryFindResource("TextMuted") as Brush ?? Brushes.LightSlateGray;
        var axisBrush = TryFindResource("TextMuted") as Brush ?? Brushes.LightSlateGray;
        var axisRange = Math.Max(1, axisMax - axisMin);

        for (var value = axisMin; value <= axisMax + axisStep * 0.5; value += axisStep)
        {
            var normalized = (value - axisMin) / axisRange;
            var y = plotTop + plotHeight - normalized * plotHeight;
            var isAxisLine = Math.Abs(value - axisMin) < axisStep * 0.1;

            ThroughputChartGridCanvas.Children.Add(new Line
            {
                X1 = plotLeft,
                Y1 = y,
                X2 = plotLeft + plotWidth,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = isAxisLine ? 1.2 : 1,
                Opacity = isAxisLine ? 0.90 : 0.42
            });

            DrawChartText(
                value.ToString("0", CultureInfo.InvariantCulture),
                8,
                y - 8,
                10,
                FontWeights.Normal,
                textBrush);
        }

        const int verticalSteps = 10;

        for (var i = 0; i <= verticalSteps; i++)
        {
            var x = plotLeft + plotWidth * i / verticalSteps;
            var isAxisLine = i == 0;

            ThroughputChartGridCanvas.Children.Add(new Line
            {
                X1 = x,
                Y1 = plotTop,
                X2 = x,
                Y2 = plotTop + plotHeight,
                Stroke = gridBrush,
                StrokeThickness = isAxisLine ? 1.2 : 1,
                Opacity = isAxisLine ? 0.90 : 0.36
            });

            DrawChartText(
                i.ToString(CultureInfo.InvariantCulture),
                x - 3,
                plotTop + plotHeight + 8,
                10,
                FontWeights.Normal,
                textBrush);
        }

        DrawChartText("Mbps", 8, plotTop - 20, 10, FontWeights.SemiBold, axisBrush);
        DrawChartText("Time (sec)", plotLeft + plotWidth - 62, plotTop + plotHeight + 8, 10, FontWeights.SemiBold, axisBrush);
    }

    private void DrawChartMarker(double x, double y)
    {
        var markerBrush = TryFindResource("Accent") as Brush ?? Brushes.DeepSkyBlue;
        var markerStroke = TryFindResource("Panel") as Brush ?? Brushes.Black;

        var halo = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = markerBrush,
            Opacity = 0.22
        };

        Canvas.SetLeft(halo, x - 7);
        Canvas.SetTop(halo, y - 7);
        ThroughputChartMarkerCanvas.Children.Add(halo);

        var marker = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = markerBrush,
            Stroke = markerStroke,
            StrokeThickness = 1.4
        };

        Canvas.SetLeft(marker, x - 4);
        Canvas.SetTop(marker, y - 4);
        ThroughputChartMarkerCanvas.Children.Add(marker);
    }

    private string BuildThroughputSummary()
    {
        if (_throughputSamples.Count == 0)
        {
            return string.Empty;
        }

        var current = _throughputSamples[^1];
        var min = _throughputSamples.Min();
        var avg = _throughputSamples.Average();
        var max = _throughputSamples.Max();

        return FormatMegabits(current)
            + "   min " + FormatMegabits(min)
            + " · avg " + FormatMegabits(avg)
            + " · max " + FormatMegabits(max);
    }

    private void DrawChartText(
        string text,
        double x,
        double y,
        double fontSize,
        FontWeight fontWeight,
        Brush foreground)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight,
            Foreground = foreground
        };

        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        ThroughputChartGridCanvas.Children.Add(label);
    }

    private static string FormatMegabits(double megabitsPerSecond)
    {
        var format = megabitsPerSecond >= 100
            ? "0"
            : "0.0";

        return megabitsPerSecond.ToString(format, CultureInfo.InvariantCulture) + " Mbps";
    }

    private static string FormatEndSummarySample(IperfIntervalSample sample)
    {
        var parts = new List<string> { "Test completed" };

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

        return string.Join(" · ", parts) + ".";
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
