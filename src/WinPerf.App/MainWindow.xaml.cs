using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WinPerf.App.Settings;
using WinPerf.Core.History;
using WinPerf.Core.Iperf;
using WinPerf.Core.Profiles;
using WinPerf.Core.Updates;

namespace WinPerf.App;

public partial class MainWindow : Window
{
    private readonly WinPerfSettingsStore _settingsStore = new();
    private readonly IperfExecutableResolver _executableResolver = new();
    private readonly IperfProcessRunner _processRunner = new();
    private readonly JsonSavedIperfProfileStore _profileStore = new(JsonSavedIperfProfileStore.GetDefaultFilePath());
    private readonly JsonIperfHistoryStore _historyStore = new(JsonIperfHistoryStore.GetDefaultFilePath());

    private WinPerfSettings _settings = new();
    private IperfExecutableResolution _engineResolution = new(false, null, "NotConfigured", "iperf3.exe is not configured.");
    private CancellationTokenSource? _currentRunCancellation;
    private CancellationTokenSource? _serverRunCancellation;
    private readonly StringBuilder _engineOutput = new();
    private readonly StringBuilder _serverOutput = new();
    private readonly List<double> _throughputSamples = new();
    private readonly List<IReadOnlyList<double>> _streamThroughputSamples = new();
    private readonly List<double> _reverseThroughputSamples = new();
    private readonly List<IReadOnlyList<double>> _reverseStreamThroughputSamples = new();
    private IperfMode? _activeMode;
    private IperfEngine _activeEngine = IperfEngine.Iperf3;
    private int _activeChartDurationSeconds = 10;
    private int _activeOmitSeconds;
    private int _omittedWarmupIntervalsReceived;
    private IperfIntervalSample? _iperf2UdpServerReport;
    private int _iperf2UdpServerReportCount;
    private SavedIperfProfilesDocument _profilesDocument = new();
    private bool _isLoadingProfileSelection;
    private bool _isApplyingDashboardProfile;
    private string? _activeCustomCommandArguments;
    private string? _activeCommandOverrideSource;

    private const string AdvancedCommandOverrideSource = "Advanced";
    private const string CustomCommandOverrideSource = "Custom";
    private const int MaxRecentServers = 20;
    private const int MaxThroughputSamples = 60;
    private const double DefaultDashboardEngineOutputHeight = 180;
    private const double MaxDashboardEngineOutputHeight = 260;

    public MainWindow()
    {
        InitializeComponent();
        AppVersionText.Text = ResolveAppVersionText();
        WindowPlacementStore.Track(this, "MainWindow");

        _settings = _settingsStore.Load();
        RefreshEngineStatus();
        RefreshIntegrationStatus();
        PopulateRecentServers();
        ApplyUnifiedCompactLayout();
        ApplyDashboardLayout();
        ShowDashboardPage();
        UpdateUdpBandwidthVisibility();
        UpdateCommandOverrideUx();
        UpdateDashboardCommandPreview();
        UpdateServerModeCommandPreview();

        Loaded += async (_, _) =>
        {
            ApplyUnifiedCompactLayout();
            ApplyDashboardLayout();
            await LoadDashboardProfilesAsync();
        };
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
            EngineOutputText.Text = $"{GetEngineExecutableDisplayName(GetSelectedEngine())} is not configured. Open Settings and select the executable first.";
            return;
        }

        try
        {
            var commandOverrideArguments = _activeCustomCommandArguments;
            var hasCommandOverride = !string.IsNullOrWhiteSpace(commandOverrideArguments);

            var options = hasCommandOverride
                ? BuildCustomCommandOptions(commandOverrideArguments!)
                : BuildDashboardTestOptions();

            var command = hasCommandOverride
                ? new IperfCommand(_engineResolution.ExecutablePath, SplitCommandLine(commandOverrideArguments!))
                : IperfCommandBuilder.BuildClientCommand(_engineResolution.ExecutablePath, options);
            var commandDisplayText = hasCommandOverride
                ? commandOverrideArguments!
                : string.Join(" ", command.Arguments.Select(QuoteIfNeeded));

            if (!hasCommandOverride)
            {
                SaveRecentServer(options.Server);
            }

            _currentRunCancellation = new CancellationTokenSource();
            _activeMode = options.Mode;
            _activeEngine = options.Engine;
            _activeChartDurationSeconds = Math.Max(1, options.DurationSeconds);
            _activeOmitSeconds = Math.Max(0, options.OmitSeconds);
            _omittedWarmupIntervalsReceived = 0;
            SetRunState(isRunning: true);
            ResetLiveMetrics(options.Mode);

            _engineOutput.Clear();
            AppendEngineOutput("Running command:");
            AppendEngineOutput(commandDisplayText);
            AppendEngineOutput(string.Empty);

            if (_activeOmitSeconds > 0)
            {
                AppendEngineOutput($"Warm-up: omitting first {_activeOmitSeconds}s before live metrics.");
                LiveStatusText.Text = $"Warm-up: omitting first {_activeOmitSeconds}s...";
                ShowWarmupChartPlaceholder(0, _activeOmitSeconds, null);
            }
            else
            {
                ShowWaitingChartPlaceholder();
            }

            var result = await _processRunner.RunAsync(
                command,
                async (line, cancellationToken) =>
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (line.Stream == IperfOutputStream.StandardOutput)
                        {
                            if (TryHandleStructuredIperfOutput(options, line.Text))
                            {
                                return;
                            }
                        }

                        AppendEngineOutput(line.Text);
                    });
                },
                _currentRunCancellation.Token);

            var receivedIperf2UdpReportCount =
                ReconcileFinalIperf2UdpServerReport(
                    options,
                    result);

            var requiresCompleteIperf2UdpReport =
                options.Engine == IperfEngine.Iperf2 &&
                options.Mode is (
                    IperfMode.UdpUpload or
                    IperfMode.UdpDownload);

            var hasAuthoritativeIperf2UdpServerResult =
                requiresCompleteIperf2UdpReport &&
                receivedIperf2UdpReportCount == options.Streams;

            var outcome =
                IperfRunResultClassifier.Classify(
                    options.Engine,
                    result,
                    hasAuthoritativeIperf2UdpServerResult);

            if (requiresCompleteIperf2UdpReport &&
                receivedIperf2UdpReportCount != options.Streams &&
                outcome.Kind != IperfRunOutcomeKind.Failed)
            {
                outcome = new IperfRunOutcome(
                    IperfRunOutcomeKind.Failed,
                    $"Test failed: incomplete iperf2 UDP server report ({receivedIperf2UdpReportCount}/{options.Streams} streams).");
            }
            var summaryExitCode =
                outcome.Kind == IperfRunOutcomeKind.Failed
                    ? result.ExitCode == 0 ? 1 : result.ExitCode
                    : 0;

            AppendEngineOutput(string.Empty);
            AppendEngineOutput($"Process exited with code {result.ExitCode}.");
            AppendEngineOutput(outcome.Message);
            UpdateLastSummary(options, summaryExitCode);
            await SaveHistoryEntryAsync(options, result, outcome, summaryExitCode, commandDisplayText);
            UpdateRunOutcomeStatus(outcome);
        }
        catch (OperationCanceledException)
        {
            AppendEngineOutput(string.Empty);
            AppendEngineOutput("Test stopped by user.");
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or FormatException or NotSupportedException)
        {
            EngineOutputText.Text = "Invalid test configuration:" + Environment.NewLine + ex.Message;
        }
        catch (Exception ex)
        {
            AppendEngineOutput(string.Empty);
            AppendEngineOutput($"Failed to run {GetEngineDisplayName(GetSelectedEngine())}:");
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

    private void DashboardNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowDashboardPage();
    }

    private void ServerModeNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowServerModePage();
    }

    private async void HistoryNavButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowHistoryPageAsync();
    }

    private void ShowDashboardPage()
    {
        DashboardContentPanel.Visibility = Visibility.Visible;
        ServerModeContentPanel.Visibility = Visibility.Collapsed;
        HistoryContentPanel.Visibility = Visibility.Collapsed;
        SetSidebarNavigation(ActivePage.Dashboard);
        RefreshEngineStatus();
    }

    private void ShowServerModePage()
    {
        DashboardContentPanel.Visibility = Visibility.Collapsed;
        ServerModeContentPanel.Visibility = Visibility.Visible;
        HistoryContentPanel.Visibility = Visibility.Collapsed;
        SetSidebarNavigation(ActivePage.ServerMode);
        UpdateServerModeCommandPreview();
    }

    private async Task ShowHistoryPageAsync()
    {
        DashboardContentPanel.Visibility = Visibility.Collapsed;
        ServerModeContentPanel.Visibility = Visibility.Collapsed;
        HistoryContentPanel.Visibility = Visibility.Visible;
        SetSidebarNavigation(ActivePage.History);
        await RefreshHistoryPageAsync();
    }

    private void SetSidebarNavigation(ActivePage activePage)
    {
        if (DashboardNavButton is null || ServerModeNavButton is null || HistoryNavButton is null)
        {
            return;
        }

        DashboardNavButton.Style = FindResource(activePage == ActivePage.Dashboard ? "SidebarNavSelectedButton" : "SidebarNavButton") as Style;
        ServerModeNavButton.Style = FindResource(activePage == ActivePage.ServerMode ? "SidebarNavSelectedButton" : "SidebarNavButton") as Style;
        HistoryNavButton.Style = FindResource(activePage == ActivePage.History ? "SidebarNavSelectedButton" : "SidebarNavButton") as Style;
    }

    private void AppMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (AppMenuButton.ContextMenu is null)
        {
            return;
        }

        AppMenuButton.ContextMenu.PlacementTarget = AppMenuButton;
        AppMenuButton.ContextMenu.IsOpen = true;
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsWindow();
    }

    private void UpdatesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenSponsorProUpdatesWindow();
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenAboutWindow();
    }

    private void OpenSettingsWindow()
    {
        var dialog = new SettingsWindow(_settings.IperfExecutablePath, _settings.Iperf2ExecutablePath, AppContext.BaseDirectory)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _settings.IperfExecutablePath = dialog.IperfExecutablePath;
            _settings.Iperf2ExecutablePath = dialog.Iperf2ExecutablePath;
            _settingsStore.Save(_settings);
            RefreshEngineStatus();
            RefreshIntegrationStatus();
            UpdateDashboardCommandPreview();
        }
    }

    private void OpenAboutWindow()
    {
        var dialog = new AboutWindow(ResolveAppVersionText())
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private void OpenSponsorProUpdatesWindow()
    {
        var dialog = new SponsorProUpdatesWindow(ResolveAppVersionText())
        {
            Owner = this
        };

        dialog.ShowDialog();
        RefreshIntegrationStatus();
    }

    private async void StartServerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_serverRunCancellation is not null)
        {
            return;
        }

        try
        {
            var options = BuildServerModeOptions();
            var resolution = ResolveIntegration(options.Engine);

            if (!resolution.IsConfigured || string.IsNullOrWhiteSpace(resolution.ExecutablePath))
            {
                ServerOutputText.Text = $"{GetEngineExecutableDisplayName(options.Engine)} is not configured. Open Settings and select the executable first.";
                ServerModeStatusText.Text = "Server cannot start: engine missing.";
                return;
            }

            var command = IperfCommandBuilder.BuildServerCommand(resolution.ExecutablePath, options);
            var commandDisplayText = string.Join(" ", command.Arguments.Select(QuoteIfNeeded));

            _serverRunCancellation = new CancellationTokenSource();
            _serverOutput.Clear();
            SetServerModeRunState(isRunning: true, options);
            AppendServerOutput("Running server command:");
            AppendServerOutput(commandDisplayText);
            AppendServerOutput(string.Empty);

            var result = await _processRunner.RunAsync(
                command,
                async (line, cancellationToken) =>
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        AppendServerOutput(line.Text);
                    });
                },
                _serverRunCancellation.Token);

            AppendServerOutput(string.Empty);
            AppendServerOutput($"Server process exited with code {result.ExitCode}.");
            ServerModeStatusText.Text = result.ExitCode == 0
                ? "Server stopped."
                : $"Server stopped with exit code {result.ExitCode}.";
        }
        catch (OperationCanceledException)
        {
            AppendServerOutput(string.Empty);
            AppendServerOutput("Server stopped by user.");
            ServerModeStatusText.Text = "Server stopped by user.";
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or FormatException or NotSupportedException)
        {
            ServerOutputText.Text = "Invalid server configuration:" + Environment.NewLine + ex.Message;
            ServerModeStatusText.Text = "Server configuration is invalid.";
        }
        catch (Exception ex)
        {
            AppendServerOutput(string.Empty);
            AppendServerOutput("Failed to run server:");
            AppendServerOutput(ex.Message);
            ServerModeStatusText.Text = "Server failed to start or stopped unexpectedly.";
        }
        finally
        {
            _serverRunCancellation?.Dispose();
            _serverRunCancellation = null;
            SetServerModeRunState(isRunning: false, null);
        }
    }

    private void StopServerButton_Click(object sender, RoutedEventArgs e)
    {
        _serverRunCancellation?.Cancel();
    }

    private void ServerModeInputChanged(object sender, RoutedEventArgs e)
    {
        if (ServerEngineBox is null ||
            ServerPortBox is null ||
            ServerOneOffBox is null ||
            ServerOneOffUnavailableText is null)
        {
            return;
        }

        if (ReferenceEquals(sender, ServerEngineBox))
        {
            NormalizeServerModeForSelectedEngine();
        }
        else
        {
            UpdateServerOneOffAvailability();
        }

        UpdateServerModeCommandPreview();
    }

    private void NormalizeServerModeForSelectedEngine()
    {
        var selectedEngine = GetSelectedServerEngine();

        if (selectedEngine == IperfEngine.Iperf2)
        {
            ServerOneOffBox.IsChecked = false;
        }

        UpdateServerOneOffAvailability();

        if (selectedEngine == IperfEngine.Iperf2 &&
            string.Equals(ServerPortBox.Text.Trim(), "5201", StringComparison.Ordinal))
        {
            ServerPortBox.Text = "5001";
        }
        else if (selectedEngine == IperfEngine.Iperf3 &&
                 string.Equals(ServerPortBox.Text.Trim(), "5001", StringComparison.Ordinal))
        {
            ServerPortBox.Text = "5201";
        }
    }

    private IperfServerOptions BuildServerModeOptions()
    {
        return new IperfServerOptions
        {
            Engine = GetSelectedServerEngine(),
            Protocol = GetSelectedServerProtocol(),
            Port = ParsePositiveInt(ServerPortBox, "Server port"),
            AddressFamily = IperfAddressFamily.IPv4,
            OneOff = ServerOneOffBox.IsChecked == true
        };
    }

    private void UpdateServerModeCommandPreview()
    {
        if (ServerOutputText is null)
        {
            return;
        }

        if (_serverRunCancellation is not null)
        {
            return;
        }

        try
        {
            var options = BuildServerModeOptions();
            var resolution = ResolveIntegration(options.Engine);
            var executablePath = resolution.IsConfigured && !string.IsNullOrWhiteSpace(resolution.ExecutablePath)
                ? resolution.ExecutablePath
                : GetEngineExecutableDisplayName(options.Engine);
            var command = IperfCommandBuilder.BuildServerCommand(executablePath, options);

            ServerOutputText.Text =
                "Server command preview:" + Environment.NewLine +
                string.Join(" ", command.Arguments.Select(QuoteIfNeeded));
            ServerModeStatusText.Text = "Stopped. Ready to start local server.";
            SetServerModeStatusChip(isRunning: false, isError: false);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or ArgumentOutOfRangeException or NotSupportedException)
        {
            ServerOutputText.Text =
                "Server command preview unavailable:" + Environment.NewLine +
                ex.Message;
            ServerModeStatusText.Text = "Server configuration is invalid.";
            SetServerModeStatusChip(isRunning: false, isError: true);
        }
    }

    private void SetServerModeRunState(bool isRunning, IperfServerOptions? options)
    {
        StartServerButton.IsEnabled = !isRunning;
        StopServerButton.IsEnabled = isRunning;
        ServerEngineBox.IsEnabled = !isRunning;
        ServerProtocolBox.IsEnabled = !isRunning;
        ServerPortBox.IsEnabled = !isRunning;
        UpdateServerOneOffAvailability();

        if (isRunning && options is not null)
        {
            ServerModeStatusText.Text =
                $"{GetEngineDisplayName(options.Engine)} {options.Protocol.ToString().ToUpperInvariant()} server listening on port {options.Port}.";
            SetServerModeStatusChip(isRunning: true, isError: false);
        }
        else
        {
            SetServerModeStatusChip(isRunning: false, isError: false);
        }
    }

    private void UpdateServerOneOffAvailability()
    {
        if (ServerOneOffBox is null || ServerOneOffUnavailableText is null)
        {
            return;
        }

        var isIperf3 = GetSelectedServerEngine() == IperfEngine.Iperf3;

        ServerOneOffBox.Visibility = isIperf3 ? Visibility.Visible : Visibility.Collapsed;
        ServerOneOffUnavailableText.Visibility = isIperf3 ? Visibility.Collapsed : Visibility.Visible;
        ServerOneOffBox.IsEnabled = _serverRunCancellation is null && isIperf3;

        if (!isIperf3)
        {
            ServerOneOffBox.IsChecked = false;
        }
    }

    private void SetServerModeStatusChip(bool isRunning, bool isError)
    {
        if (isRunning)
        {
            ServerModeStatusChipText.Text = "Running";
            SetIntegrationChipState(ServerModeStatusChip, ServerModeStatusChipText, isReady: true);
            return;
        }

        if (isError)
        {
            ServerModeStatusChipText.Text = "Invalid";
            SetIntegrationChipState(ServerModeStatusChip, ServerModeStatusChipText, isReady: false);
            return;
        }

        ServerModeStatusChipText.Text = "Stopped";
        ServerModeStatusChip.Background = GetThemeBrush("PanelSoft", Brushes.DarkSlateBlue);
        ServerModeStatusChip.BorderBrush = GetThemeBrush("BorderSoft", Brushes.SlateBlue);
        ServerModeStatusChipText.Foreground = GetThemeBrush("TextMuted", Brushes.LightSlateGray);
    }

    private void AppendServerOutput(string text)
    {
        _serverOutput.AppendLine(text);

        const int maxChars = 12000;

        if (_serverOutput.Length > maxChars)
        {
            _serverOutput.Remove(0, _serverOutput.Length - maxChars);
        }

        ServerOutputText.Text = _serverOutput.ToString();
        ServerOutputText.ScrollToEnd();
    }

    private void ApplyDashboardLayout()
    {
        if (GetSavedDashboardEngineOutputHeight() is double height &&
            !double.IsNaN(height) &&
            !double.IsInfinity(height) &&
            height >= EngineOutputRow.MinHeight)
        {
            EngineOutputRow.Height = new GridLength(ClampDashboardEngineOutputHeight(height), GridUnitType.Pixel);
            LiveThroughputRow.Height = new GridLength(1, GridUnitType.Star);
        }

        if (GetSavedDashboardLeftRailWidth() is double width &&
            !double.IsNaN(width) &&
            !double.IsInfinity(width) &&
            width >= LeftRailColumn.MinWidth)
        {
            LeftRailColumn.Width = new GridLength(
                Math.Min(width, LeftRailColumn.MaxWidth),
                GridUnitType.Pixel);
        }
    }

    private void ApplyUnifiedCompactLayout()
    {
        LeftRailColumn.MinWidth = 280;
        LeftRailColumn.MaxWidth = 500;

        if (GetSavedDashboardLeftRailWidth() is not double savedLeftRailWidth)
        {
            LeftRailColumn.Width = new GridLength(360);
        }
        else
        {
            LeftRailColumn.Width = new GridLength(
                Math.Clamp(savedLeftRailWidth, LeftRailColumn.MinWidth, LeftRailColumn.MaxWidth));
        }

        DashboardContentPanel.Margin = new Thickness(18);
        MetricsRow.Height = new GridLength(150);
        LiveThroughputRow.MinHeight = 260;
        EngineOutputRow.MinHeight = 110;
        EngineOutputRow.MaxHeight = MaxDashboardEngineOutputHeight;

        if (GetSavedDashboardEngineOutputHeight() is not double savedEngineOutputHeight)
        {
            EngineOutputRow.Height = new GridLength(DefaultDashboardEngineOutputHeight);
        }
        else
        {
            EngineOutputRow.Height = new GridLength(ClampDashboardEngineOutputHeight(savedEngineOutputHeight));
        }

        MinWidth = 760;
        MinHeight = 520;
    }

    private double? GetSavedDashboardEngineOutputHeight()
    {
        return _settings.DashboardEngineOutputHeight;
    }

    private double? GetSavedDashboardLeftRailWidth()
    {
        return _settings.DashboardLeftRailWidth;
    }

    private void SetSavedDashboardEngineOutputHeight(double height)
    {
        var clampedHeight = ClampDashboardEngineOutputHeight(height);
        _settings.DashboardEngineOutputHeight = clampedHeight;
    }

    private double ClampDashboardEngineOutputHeight(double height)
    {
        return Math.Clamp(height, EngineOutputRow.MinHeight, MaxDashboardEngineOutputHeight);
    }

    private void SetSavedDashboardLeftRailWidth(double width)
    {
        _settings.DashboardLeftRailWidth = width;
    }

    private void SaveDashboardLayout()
    {
        CaptureDashboardLayout();
        _settingsStore.Save(_settings);
    }

    private void CaptureDashboardLayout()
    {
        var height = EngineOutputRow.ActualHeight;

        if (!double.IsNaN(height) && !double.IsInfinity(height) && height >= EngineOutputRow.MinHeight)
        {
            SetSavedDashboardEngineOutputHeight(Math.Round(height, 0));
        }

        var width = LeftRailColumn.ActualWidth;

        if (!double.IsNaN(width) && !double.IsInfinity(width) && width >= LeftRailColumn.MinWidth)
        {
            SetSavedDashboardLeftRailWidth(
                Math.Round(
                    Math.Min(width, LeftRailColumn.MaxWidth),
                    0));
        }

    }

    private void UpdateRunOutcomeStatus(IperfRunOutcome outcome)
    {
        if (outcome.Kind == IperfRunOutcomeKind.Completed)
        {
            return;
        }

        LiveStatusText.Text = outcome.Kind switch
        {
            IperfRunOutcomeKind.CompletedWithWarning =>
                "Test completed with warning.",
            IperfRunOutcomeKind.Failed =>
                "Test failed.",
            _ =>
                LiveStatusText.Text
        };

        var separator = string.IsNullOrWhiteSpace(LastSummaryText.Text)
            ? string.Empty
            : Environment.NewLine;

        LastSummaryText.Text += separator + outcome.Message;
    }

    private void UpdateLastSummary(IperfTestOptions options, int exitCode)
    {
        var lines = new List<string>
        {
            $"{GetEngineDisplayName(options.Engine)} · {FormatModeLabel(options.Mode)} · {options.Server}:{options.Port}"
        };

        var isIperf2Udp =
            options.Engine == IperfEngine.Iperf2 &&
            options.Mode is (
                IperfMode.UdpUpload or
                IperfMode.UdpDownload);

        var udpServerReport =
            isIperf2Udp
                ? _iperf2UdpServerReport
                : null;

        if (udpServerReport?.MegabitsPerSecond is double receivedMegabits)
        {
            var sentSuffix = _throughputSamples.Count > 0
                ? $" · sent avg {FormatMegabits(_throughputSamples.Average())}"
                : string.Empty;

            lines.Add(
                $"Received {FormatMegabits(receivedMegabits)}{sentSuffix}");
        }
        else if (isIperf2Udp)
        {
            lines.Add(
                $"Server result unavailable ({_iperf2UdpServerReportCount}/{options.Streams} streams).");
        }
        else if (_throughputSamples.Count > 0)
        {
            var current = _throughputSamples[^1];
            var min = _throughputSamples.Min();
            var avg = _throughputSamples.Average();
            var max = _throughputSamples.Max();

            if (options.Mode == IperfMode.TcpBidirectional &&
                _reverseThroughputSamples.Count > 0)
            {
                var reverseCurrent = _reverseThroughputSamples[^1];
                var reverseMin = _reverseThroughputSamples.Min();
                var reverseAvg = _reverseThroughputSamples.Average();
                var reverseMax = _reverseThroughputSamples.Max();

                lines.Add(
                    $"Upload last {FormatMegabits(current)} · min {FormatMegabits(min)} · avg {FormatMegabits(avg)} · max {FormatMegabits(max)}");
                lines.Add(
                    $"Download last {FormatMegabits(reverseCurrent)} · min {FormatMegabits(reverseMin)} · avg {FormatMegabits(reverseAvg)} · max {FormatMegabits(reverseMax)}");
            }
            else
            {
                lines.Add(
                    $"Last {FormatMegabits(current)} · min {FormatMegabits(min)} · avg {FormatMegabits(avg)} · max {FormatMegabits(max)}");
            }
        }
        else
        {
            lines.Add("No throughput samples.");
        }

        if (options.Mode is IperfMode.UdpUpload or IperfMode.UdpDownload)
        {
            var udpParts = new List<string>();

            if (udpServerReport is not null)
            {
                if (udpServerReport.JitterMs is double jitterMs)
                {
                    udpParts.Add(
                        "jitter " +
                        jitterMs.ToString(
                            "0.000",
                            CultureInfo.InvariantCulture) +
                        " ms");
                }

                if (udpServerReport.EffectiveLostPercent is double lostPercent)
                {
                    var lossText =
                        "loss " +
                        lostPercent.ToString(
                            "0.0",
                            CultureInfo.InvariantCulture) +
                        " %";

                    if (udpServerReport.LostDatagrams is long lost &&
                        udpServerReport.TotalDatagrams is long total)
                    {
                        lossText += $" ({lost}/{total})";
                    }

                    udpParts.Add(lossText);
                }
            }
            else if (!isIperf2Udp)
            {
                if (!string.IsNullOrWhiteSpace(JitterValueText.Text) &&
                    !string.Equals(
                        JitterValueText.Text,
                        "n/a",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        JitterValueText.Text,
                        "-- ms",
                        StringComparison.OrdinalIgnoreCase))
                {
                    udpParts.Add(
                        "jitter " + JitterValueText.Text);
                }

                if (!string.IsNullOrWhiteSpace(LossValueText.Text) &&
                    !string.Equals(
                        LossValueText.Text,
                        "n/a",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        LossValueText.Text,
                        "-- %",
                        StringComparison.OrdinalIgnoreCase))
                {
                    udpParts.Add(
                        "loss " + LossValueText.Text);
                }
            }

            if (udpParts.Count > 0)
            {
                lines.Add(
                    string.Join(" · ", udpParts));
            }
        }

        lines.Add($"{options.DurationSeconds}s · {options.Streams} stream(s) · {(exitCode == 0 ? "OK" : $"Exit {exitCode}")}");

        LastSummaryText.Text = string.Join(Environment.NewLine, lines);
    }

    private async Task SaveHistoryEntryAsync(
        IperfTestOptions options,
        IperfRunResult result,
        IperfRunOutcome outcome,
        int summaryExitCode,
        string commandPreview)
    {
        try
        {
            var entry = BuildHistoryEntry(
                options,
                result,
                outcome,
                summaryExitCode,
                commandPreview);

            await _historyStore.AddAsync(entry);

            if (HistoryContentPanel.Visibility == Visibility.Visible)
            {
                await RefreshHistoryPageAsync();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            AppendEngineOutput(string.Empty);
            AppendEngineOutput($"History save failed: {ex.Message}");
        }
    }

    private IperfHistoryEntry BuildHistoryEntry(
        IperfTestOptions options,
        IperfRunResult result,
        IperfRunOutcome outcome,
        int summaryExitCode,
        string commandPreview)
    {
        var throughputSamples = _throughputSamples.ToList();
        var reverseThroughputSamples = _reverseThroughputSamples.ToList();

        double? averageMbps = throughputSamples.Count > 0
            ? throughputSamples.Average()
            : null;
        double? minimumMbps = throughputSamples.Count > 0
            ? throughputSamples.Min()
            : null;
        double? maximumMbps = throughputSamples.Count > 0
            ? throughputSamples.Max()
            : null;

        if (options.Engine == IperfEngine.Iperf2 &&
            options.Mode is IperfMode.UdpUpload or IperfMode.UdpDownload &&
            _iperf2UdpServerReport?.MegabitsPerSecond is double receivedMbps)
        {
            averageMbps = receivedMbps;
            minimumMbps ??= receivedMbps;
            maximumMbps ??= receivedMbps;
        }

        return new IperfHistoryEntry
        {
            StartedAtUtc = result.StartedAtUtc,
            FinishedAtUtc = result.FinishedAtUtc,
            Engine = options.Engine,
            Mode = options.Mode,
            Server = options.Server,
            Port = options.Port,
            Streams = options.Streams,
            DurationSeconds = options.DurationSeconds,
            OmitSeconds = options.OmitSeconds,
            UdpBandwidth = options.Mode is IperfMode.UdpUpload or IperfMode.UdpDownload
                ? options.UdpBandwidth
                : null,
            ExitCode = summaryExitCode,
            Succeeded = outcome.Kind != IperfRunOutcomeKind.Failed && summaryExitCode == 0,
            AverageMbps = averageMbps,
            MinimumMbps = minimumMbps,
            MaximumMbps = maximumMbps,
            ReverseAverageMbps = reverseThroughputSamples.Count > 0
                ? reverseThroughputSamples.Average()
                : null,
            CommandPreview = commandPreview,
            Summary = LastSummaryText.Text
        };
    }

    private async Task RefreshHistoryPageAsync()
    {
        try
        {
            var document = await _historyStore.LoadAsync();
            var entries = document.Entries
                .Select(CreateHistoryListItem)
                .ToList();

            HistoryItemsControl.ItemsSource = entries;
            HistoryEmptyText.Text = "No saved history yet. Run a test and WinPerf will save the result here.";
            HistoryEmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryStatusText.Text = entries.Count == 1
                ? "1 saved result"
                : $"{entries.Count} saved results";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            HistoryItemsControl.ItemsSource = null;
            HistoryEmptyText.Visibility = Visibility.Visible;
            HistoryEmptyText.Text = "History could not be loaded. Check the portable data folder.";
            HistoryStatusText.Text = ex.Message;
        }
    }

    private void HistoryDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetHistoryListItem(sender, out var item))
        {
            return;
        }

        var dialog = new HistoryDetailWindow(item)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private void HistoryCopyCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetHistoryListItem(sender, out var item))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.CommandPreview) ||
            string.Equals(item.CommandPreview, "Command unavailable.", StringComparison.OrdinalIgnoreCase))
        {
            HistoryStatusText.Text = "No command saved for this result.";
            return;
        }

        Clipboard.SetText(item.CommandPreview);
        HistoryStatusText.Text = "Command copied.";
    }

    private async void HistoryDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetHistoryListItem(sender, out var item))
        {
            return;
        }

        if (!ConfirmDialogWindow.Confirm(
                this,
                "Delete history result?",
                $"Delete this saved result?\n\n{item.Title}\n{item.FinishedLocalText}",
                "Delete"))
        {
            return;
        }

        try
        {
            var deleted = await _historyStore.DeleteAsync(item.Id);
            await RefreshHistoryPageAsync();
            HistoryStatusText.Text = deleted ? "Result deleted." : "Result was not found.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            HistoryStatusText.Text = $"Delete failed: {ex.Message}";
        }
    }

    private async void HistoryClearButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var document = await _historyStore.LoadAsync();
            if (document.Entries.Count == 0)
            {
                HistoryStatusText.Text = "History is already empty.";
                return;
            }

            if (!ConfirmDialogWindow.Confirm(
                    this,
                    "Clear all history?",
                    $"Delete all {document.Entries.Count} saved history results from this portable runtime?",
                    "Clear all"))
            {
                return;
            }

            await _historyStore.ClearAsync();
            await RefreshHistoryPageAsync();
            HistoryStatusText.Text = "History cleared.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            HistoryStatusText.Text = $"Clear failed: {ex.Message}";
        }
    }

    private async void HistoryExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var document = await _historyStore.LoadAsync();
            var dialog = new SaveFileDialog
            {
                Title = "Export WinPerf history",
                FileName = $"WinPerf-history-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                DefaultExt = ".json",
                Filter = "WinPerf history (*.json)|*.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var exportStore = new JsonIperfHistoryStore(dialog.FileName);
            await exportStore.SaveAsync(document);
            HistoryStatusText.Text = document.Entries.Count == 1
                ? "Exported 1 result."
                : $"Exported {document.Entries.Count} results.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            HistoryStatusText.Text = $"Export failed: {ex.Message}";
        }
    }

    private async void HistoryImportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import WinPerf history",
                DefaultExt = ".json",
                Filter = "WinPerf history (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var importStore = new JsonIperfHistoryStore(dialog.FileName);
            var importedDocument = await importStore.LoadAsync();
            var mergedCount = await _historyStore.MergeAsync(importedDocument);
            await RefreshHistoryPageAsync();
            HistoryStatusText.Text = importedDocument.Entries.Count == 1
                ? $"Imported 1 result. History now has {mergedCount} results."
                : $"Imported {importedDocument.Entries.Count} results. History now has {mergedCount} results.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            HistoryStatusText.Text = $"Import failed: {ex.Message}";
        }
    }

    private static bool TryGetHistoryListItem(object sender, out HistoryListItem item)
    {
        if (sender is FrameworkElement { DataContext: HistoryListItem historyItem })
        {
            item = historyItem;
            return true;
        }

        item = default!;
        return false;
    }

    private HistoryListItem CreateHistoryListItem(IperfHistoryEntry entry)
    {
        var title = $"{GetEngineDisplayName(entry.Engine)} · {FormatModeLabel(entry.Mode)} · {entry.Server}:{entry.Port}";
        var finishedLocalText = entry.FinishedAtUtc
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        var statusText = entry.Succeeded ? "OK" : $"Exit {entry.ExitCode}";
        var statusBrush = (Brush)FindResource(entry.Succeeded ? "AccentGreen" : "MissingChipForeground");
        var summary = BuildHistoryDisplaySummary(entry.Summary, title);
        var commandPreview = string.IsNullOrWhiteSpace(entry.CommandPreview)
            ? "Command unavailable."
            : entry.CommandPreview;
        var details = BuildHistoryDetails(entry);

        return new HistoryListItem(
            entry.Id,
            title,
            finishedLocalText,
            statusText,
            statusBrush,
            summary,
            commandPreview,
            details);
    }

    private static string BuildHistoryDetails(IperfHistoryEntry entry)
    {
        var lines = new List<string>
        {
            $"Engine: {GetEngineDisplayName(entry.Engine)}",
            $"Mode: {FormatModeLabel(entry.Mode)}",
            $"Server: {entry.Server}",
            $"Port: {entry.Port}",
            $"Streams: {entry.Streams}",
            $"Duration: {entry.DurationSeconds}s",
            $"Omit: {entry.OmitSeconds}s",
            $"Exit code: {entry.ExitCode}",
            $"Status: {(entry.Succeeded ? "OK" : "Failed")}"
        };

        if (!string.IsNullOrWhiteSpace(entry.UdpBandwidth))
        {
            lines.Add($"UDP bandwidth: {entry.UdpBandwidth}");
        }

        if (entry.AverageMbps is double averageMbps)
        {
            lines.Add($"Average: {FormatMegabits(averageMbps)}");
        }

        if (entry.MinimumMbps is double minimumMbps)
        {
            lines.Add($"Minimum: {FormatMegabits(minimumMbps)}");
        }

        if (entry.MaximumMbps is double maximumMbps)
        {
            lines.Add($"Maximum: {FormatMegabits(maximumMbps)}");
        }

        if (entry.ReverseAverageMbps is double reverseAverageMbps)
        {
            lines.Add($"Reverse average: {FormatMegabits(reverseAverageMbps)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildHistoryDisplaySummary(string? summary, string title)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return "No summary saved.";
        }

        var lines = summary
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count > 0 && string.Equals(lines[0].Trim(), title, StringComparison.OrdinalIgnoreCase))
        {
            lines.RemoveAt(0);
        }

        for (var i = 0; i < lines.Count; i++)
        {
            lines[i] = AddHistoryMetricLabel(lines[i]);
        }

        return lines.Count == 0
            ? "No summary saved."
            : string.Join(Environment.NewLine, lines);
    }

    private static string AddHistoryMetricLabel(string line)
    {
        var trimmed = line.TrimStart();

        return trimmed.Length > 0 &&
               char.IsDigit(trimmed[0]) &&
               trimmed.Contains(" Mbps", StringComparison.OrdinalIgnoreCase) &&
               !trimmed.Contains(" avg ", StringComparison.OrdinalIgnoreCase)
            ? line
            : trimmed.Length > 0 &&
              char.IsDigit(trimmed[0]) &&
              trimmed.Contains(" Mbps", StringComparison.OrdinalIgnoreCase)
                ? line[..(line.Length - trimmed.Length)] + "Last " + trimmed
                : line;
    }

    private static string FormatModeLabel(IperfMode mode)
    {
        return mode switch
        {
            IperfMode.TcpUpload => "TCP Upload",
            IperfMode.TcpDownload => "TCP Download",
            IperfMode.TcpBidirectional => "TCP Bidirectional",
            IperfMode.UdpUpload => "UDP Upload",
            IperfMode.UdpDownload => "UDP Download",
            _ => mode.ToString()
        };
    }

    private static string ResolveAppVersionText()
    {
        var version =
            Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            typeof(MainWindow).Assembly.GetName().Version?.ToString() ??
            "unknown";

        var metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        if (metadataIndex >= 0)
        {
            version = version[..metadataIndex];
        }

        return $"WinPerf v{version}";
    }

    private static IperfEngine ParseStoredEngine(string? value)
    {
        return string.Equals(value, "iperf2", StringComparison.OrdinalIgnoreCase)
            ? IperfEngine.Iperf2
            : IperfEngine.Iperf3;
    }

    private void RefreshEngineStatus()
    {
        var selectedEngine = GetSelectedEngine();

        _engineResolution = _executableResolver.Resolve(AppContext.BaseDirectory, new IperfEngineSettings
        {
            Engine = selectedEngine,
            ExecutablePath = _settings.IperfExecutablePath,
            Iperf3ExecutablePath = _settings.IperfExecutablePath,
            Iperf2ExecutablePath = _settings.Iperf2ExecutablePath
        });

        if (_engineResolution.IsConfigured)
        {
            var source = string.IsNullOrWhiteSpace(_engineResolution.Source)
                ? "Configured"
                : _engineResolution.Source;

            EngineStatusText.Text = $"Engine  ●  {GetEngineDisplayName(selectedEngine)}  ●  Ready  ●  {source}";
            EngineStatusText.ToolTip = _engineResolution.ExecutablePath;
            return;
        }

        EngineStatusText.Text = $"Engine  ●  {GetEngineDisplayName(selectedEngine)}  ●  Not configured";
        EngineStatusText.ToolTip = _engineResolution.Message;
    }

    private void RefreshIntegrationStatus()
    {
        var iperf3 = ResolveIntegration(IperfEngine.Iperf3);
        var iperf2 = ResolveIntegration(IperfEngine.Iperf2);

        UpdateIntegrationRow(
            Iperf3IntegrationStatusText,
            Iperf3IntegrationStatusChip,
            Iperf3IntegrationDetailText,
            Iperf3IntegrationPathText,
            "iperf3 throughput engine",
            iperf3);

        UpdateIntegrationRow(
            Iperf2IntegrationStatusText,
            Iperf2IntegrationStatusChip,
            Iperf2IntegrationDetailText,
            Iperf2IntegrationPathText,
            "iperf2 compatibility engine",
            iperf2);
    }

    private IperfExecutableResolution ResolveIntegration(IperfEngine engine)
    {
        return _executableResolver.Resolve(AppContext.BaseDirectory, new IperfEngineSettings
        {
            Engine = engine,
            ExecutablePath = _settings.IperfExecutablePath,
            Iperf3ExecutablePath = _settings.IperfExecutablePath,
            Iperf2ExecutablePath = _settings.Iperf2ExecutablePath
        });
    }

    private void UpdateIntegrationRow(
        TextBlock statusText,
        Border statusChip,
        TextBlock detailText,
        TextBlock pathText,
        string description,
        IperfExecutableResolution resolution)
    {
        if (resolution.IsConfigured)
        {
            var source = string.IsNullOrWhiteSpace(resolution.Source)
                ? "Configured"
                : resolution.Source;

            statusText.Text = "Ready";
            SetIntegrationChipState(statusChip, statusText, isReady: true);
            var displayPath = FormatIntegrationPath(resolution.ExecutablePath);
            detailText.Text = source;
            detailText.ToolTip = $"{description}: {displayPath}";
            pathText.Text = displayPath;
            pathText.ToolTip = resolution.ExecutablePath;
            return;
        }

        statusText.Text = "Missing";
        SetIntegrationChipState(statusChip, statusText, isReady: false);
        detailText.Text = description + " not configured";
        detailText.ToolTip = resolution.Message;
        pathText.Text = resolution.Message;
        pathText.ToolTip = resolution.Message;
    }

    private void SetIntegrationChipState(Border chip, TextBlock text, bool isReady)
    {
        if (isReady)
        {
            chip.Background = GetThemeBrush("SuccessChipBackground", Brushes.DarkGreen);
            chip.BorderBrush = GetThemeBrush("AccentGreen", Brushes.LightGreen);
            text.Foreground = GetThemeBrush("AccentGreen", Brushes.LightGreen);
            return;
        }

        chip.Background = GetThemeBrush("MissingChipBackground", Brushes.DarkRed);
        chip.BorderBrush = GetThemeBrush("MissingChipBorder", Brushes.IndianRed);
        text.Foreground = GetThemeBrush("MissingChipForeground", Brushes.LightCoral);
    }

    private Brush GetThemeBrush(string resourceKey, Brush fallback)
    {
        return FindResource(resourceKey) as Brush ?? fallback;
    }

    private static string FormatIntegrationPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Not configured";
        }

        var appDirectory = AppContext.BaseDirectory.TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar);

        if (path.StartsWith(appDirectory, StringComparison.OrdinalIgnoreCase))
        {
            var relative = path[appDirectory.Length..].TrimStart(
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar);

            return relative.Replace(System.IO.Path.DirectorySeparatorChar, '\\');
        }

        return path;
    }

    private void CommandMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (CommandMenuButton.ContextMenu is null)
        {
            return;
        }

        CommandMenuButton.ContextMenu.PlacementTarget = CommandMenuButton;
        CommandMenuButton.ContextMenu.IsOpen = true;
    }

    private async void AdvancedCommandMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await OpenAdvancedCommandWindowAsync();
    }

    private void CustomCommandMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenCustomCommandWindow();
    }

    private async Task OpenAdvancedCommandWindowAsync()
    {
        var dialog = new AdvancedCommandWindow
        {
            Owner = this
        };

        var dialogResult = dialog.ShowDialog();
        await LoadDashboardProfilesAsync();

        if (dialogResult == true)
        {
            SetCommandOverride(AdvancedCommandOverrideSource, NormalizeCustomCommandText(dialog.CommandText));
        }
    }

    private void OpenCustomCommandWindow()
    {
        var initialCommand = !string.IsNullOrWhiteSpace(_activeCustomCommandArguments)
            ? _activeCustomCommandArguments
            : BuildDashboardCommandArgumentsPreview();

        var dialog = new CustomCommandWindow(initialCommand)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            SetCommandOverride(CustomCommandOverrideSource, NormalizeCustomCommandText(dialog.CommandText));
        }
    }

    private IperfTestOptions BuildDashboardTestOptions()
    {
        return new IperfTestOptions
        {
            Engine = GetSelectedEngine(),
            Server = GetServerText(),
            Port = ParsePositiveInt(PortBox, "Port"),
            Streams = ParsePositiveInt(StreamsBox, "Streams"),
            DurationSeconds = ParsePositiveInt(DurationBox, "Duration"),
            OmitSeconds = ParseNonNegativeInt(OmitSecondsBox, "Omit"),
            Mode = GetSelectedMode(),
            AddressFamily = IperfAddressFamily.IPv4,
            UdpBandwidth = NormalizeUdpBandwidth(UdpBandwidthBox.Text)
        };
    }

    private string BuildDashboardCommandArgumentsPreview()
    {
        try
        {
            var options = BuildDashboardTestOptions();
            var command = IperfCommandBuilder.BuildClientCommand(GetEngineExecutableDisplayName(options.Engine), options);
            return string.Join(" ", command.Arguments.Select(QuoteIfNeeded));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or ArgumentOutOfRangeException or NotSupportedException)
        {
            return string.Empty;
        }
    }

    private IperfTestOptions BuildCustomCommandOptions(string commandArguments)
    {
        var args = SplitCommandLine(commandArguments);
        var mode = InferCustomCommandMode(args);

        return new IperfTestOptions
        {
            Engine = GetSelectedEngine(),
            Server = TryGetArgumentValue(args, "-c") ?? GetServerText(),
            Port = TryGetPositiveIntArgumentValue(args, "-p") ?? ParsePositiveInt(PortBox, "Port"),
            Streams = TryGetPositiveIntArgumentValue(args, "-P") ?? ParsePositiveInt(StreamsBox, "Streams"),
            DurationSeconds = TryGetPositiveIntArgumentValue(args, "-t") ?? ParsePositiveInt(DurationBox, "Duration"),
            OmitSeconds = TryGetNonNegativeIntArgumentValue(args, "-O") ?? ParseNonNegativeInt(OmitSecondsBox, "Omit"),
            Mode = mode,
            AddressFamily = args.Contains("-6", StringComparer.Ordinal)
                ? IperfAddressFamily.IPv6
                : IperfAddressFamily.IPv4,
            UdpBandwidth = TryGetArgumentValue(args, "-b") ?? "0"
        };
    }

    private static IperfMode InferCustomCommandMode(IReadOnlyList<string> args)
    {
        var isUdp = args.Contains("-u", StringComparer.Ordinal);
        var isReverse = args.Contains("-R", StringComparer.Ordinal);
        var isBidirectional = args.Contains("--bidir", StringComparer.Ordinal);

        if (isBidirectional)
        {
            return IperfMode.TcpBidirectional;
        }

        if (isUdp && isReverse)
        {
            return IperfMode.UdpDownload;
        }

        if (isUdp)
        {
            return IperfMode.UdpUpload;
        }

        return isReverse ? IperfMode.TcpDownload : IperfMode.TcpUpload;
    }

    private static int? TryGetPositiveIntArgumentValue(IReadOnlyList<string> args, string name)
    {
        var value = TryGetArgumentValue(args, name);
        return int.TryParse(value, out var number) && number > 0 ? number : null;
    }

    private static int? TryGetNonNegativeIntArgumentValue(IReadOnlyList<string> args, string name)
    {
        var value = TryGetArgumentValue(args, name);
        return int.TryParse(value, out var number) && number >= 0 ? number : null;
    }

    private static string? TryGetArgumentValue(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static IReadOnlyList<string> SplitCommandLine(string commandText)
    {
        commandText = NormalizeCustomCommandText(commandText);

        var args = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in commandText)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            args.Add(current.ToString());
        }

        return args;
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

    private static string QuoteIfNeeded(string value)
    {
        return value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
    }

    private string GetServerText()
    {
        return ServerBox.Text.Trim();
    }

    private async Task LoadDashboardProfilesAsync()
    {
        try
        {
            _profilesDocument = await _profileStore.LoadAsync();
            RefreshDashboardProfileList(
                _profilesDocument.LastSelectedProfileId
                ?? _profilesDocument.DefaultProfileId);

            if (DashboardProfileBox.SelectedItem is SavedIperfProfile selectedProfile)
            {
                ApplyProfileToDashboard(selectedProfile);
                SetDashboardProfileStatus($"Loaded profile '{selectedProfile.Name}'.");
            }
            else
            {
                SetDashboardProfileStatus("No saved profiles found.");
            }
        }
        catch (Exception ex)
        {
            _profilesDocument = new SavedIperfProfilesDocument();
            RefreshDashboardProfileList(null);
            SetDashboardProfileStatus($"Profile load failed: {ex.Message}");
        }
    }

    private async void DashboardProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingProfileSelection)
        {
            return;
        }

        if (DashboardProfileBox.SelectedItem is not SavedIperfProfile selectedProfile)
        {
            return;
        }

        ApplyProfileToDashboard(selectedProfile);

        _profilesDocument = _profilesDocument with
        {
            LastSelectedProfileId = selectedProfile.Id
        };

        try
        {
            await _profileStore.SaveAsync(_profilesDocument);
            SetDashboardProfileStatus($"Selected profile '{selectedProfile.Name}'.");
        }
        catch (Exception ex)
        {
            SetDashboardProfileStatus($"Profile selection was not saved: {ex.Message}");
        }
    }

    private void RefreshDashboardProfileList(Guid? selectedProfileId)
    {
        _isLoadingProfileSelection = true;

        try
        {
            var profiles = _profilesDocument.Profiles
                .Where(profile => profile.RunMode == SavedIperfRunMode.Client)
                .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DashboardProfileBox.ItemsSource = profiles;

            var selected = profiles.FirstOrDefault(profile => profile.Id == selectedProfileId)
                ?? profiles.FirstOrDefault(profile => profile.Id == _profilesDocument.LastSelectedProfileId)
                ?? profiles.FirstOrDefault(profile => profile.Id == _profilesDocument.DefaultProfileId)
                ?? profiles.FirstOrDefault();

            DashboardProfileBox.SelectedItem = selected;
        }
        finally
        {
            _isLoadingProfileSelection = false;
        }
    }

    private void ApplyProfileToDashboard(SavedIperfProfile profile)
    {
        ClearCommandOverride(updatePreview: false);
        if (profile.RunMode != SavedIperfRunMode.Client)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(profile.Server))
        {
            ServerBox.Text = profile.Server.Trim();
        }

        _isApplyingDashboardProfile = true;

        try
        {
            PortBox.Text = profile.Port.ToString();
            StreamsBox.Text = profile.Streams.ToString();
            DurationBox.Text = profile.DurationSeconds.ToString();
            OmitSecondsBox.Text = profile.OmitSeconds?.ToString() ?? "0";
            UdpBandwidthBox.Text = NormalizeUdpBandwidth(profile.UdpBandwidth);
            SelectMode(profile.ToIperfMode());
        }
        finally
        {
            _isApplyingDashboardProfile = false;
        }

        UpdateUdpBandwidthVisibility();
        UpdateDashboardCommandPreview();
    }

    private void SelectMode(IperfMode mode)
    {
        var label = FormatModeLabel(mode);

        for (var i = 0; i < ModeBox.Items.Count; i++)
        {
            if ((ModeBox.Items[i] as ComboBoxItem)?.Content?.ToString() == label)
            {
                ModeBox.SelectedIndex = i;
                return;
            }
        }

        ModeBox.SelectedIndex = 0;
    }

    private void SetDashboardProfileStatus(string message)
    {
        DashboardProfileStatusText.Text = message;
    }

    private void DashboardInputChanged(object sender, RoutedEventArgs e)
    {
        if (_isApplyingDashboardProfile)
        {
            return;
        }

        UpdateUdpBandwidthVisibility();

        if (NormalizeUnsupportedDashboardModeForSelectedEngine())
        {
            UpdateUdpBandwidthVisibility();
            _activeCustomCommandArguments = null;
            Dispatcher.BeginInvoke(UpdateDashboardCommandPreview, DispatcherPriority.Background);
            return;
        }

        _activeCustomCommandArguments = null;
        Dispatcher.BeginInvoke(UpdateDashboardCommandPreview, DispatcherPriority.Background);
    }

    private void UpdateUdpBandwidthVisibility()
    {
        if (UdpBandwidthPanel is null ||
            UdpBandwidthBox is null ||
            ModeBox is null)
        {
            return;
        }

        var isUdp = GetSelectedMode() is
            IperfMode.UdpUpload or
            IperfMode.UdpDownload;

        UdpBandwidthPanel.Visibility = isUdp
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (isUdp)
        {
            UdpBandwidthBox.Text =
                NormalizeUdpBandwidth(UdpBandwidthBox.Text);
        }
    }

    private static string NormalizeUdpBandwidth(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrWhiteSpace(normalized) ||
               string.Equals(
                   normalized,
                   "0",
                   StringComparison.OrdinalIgnoreCase)
            ? "10M"
            : normalized;
    }

    private void SetCommandOverride(string source, string arguments)
    {
        _activeCommandOverrideSource = source;
        _activeCustomCommandArguments = arguments;
        UpdateCommandOverrideUx();
        UpdateDashboardCommandPreview();
    }

    private void ClearCommandOverrideButton_Click(object sender, RoutedEventArgs e)
    {
        ClearCommandOverride(updatePreview: true);
    }

    private void ClearCommandOverride(bool updatePreview)
    {
        if (string.IsNullOrWhiteSpace(_activeCustomCommandArguments) &&
            string.IsNullOrWhiteSpace(_activeCommandOverrideSource))
        {
            return;
        }

        _activeCustomCommandArguments = null;
        _activeCommandOverrideSource = null;
        UpdateCommandOverrideUx();

        if (updatePreview)
        {
            UpdateDashboardCommandPreview();
        }
    }

    private void UpdateCommandOverrideUx()
    {
        if (!IsLoaded)
        {
            return;
        }

        var hasCommandOverride = !string.IsNullOrWhiteSpace(_activeCustomCommandArguments);
        CommandOverridePanel.Visibility = hasCommandOverride
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!hasCommandOverride)
        {
            CommandOverrideBadgeText.Text = string.Empty;
            return;
        }

        var source = string.IsNullOrWhiteSpace(_activeCommandOverrideSource)
            ? "Command"
            : _activeCommandOverrideSource;

        CommandOverrideBadgeText.Text = $"{source} command override active";
        CommandOverridePanel.ToolTip = "Start will run these generated/custom arguments instead of the dashboard fields.";
    }

    private string GetCommandOverridePreviewTitle()
    {
        return string.Equals(_activeCommandOverrideSource, AdvancedCommandOverrideSource, StringComparison.Ordinal)
            ? "Advanced command preview:"
            : "Custom command preview:";
    }

    private void UpdateDashboardCommandPreview()
    {
        if (!IsLoaded || _currentRunCancellation is not null || _isApplyingDashboardProfile)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_activeCustomCommandArguments))
        {
            EngineOutputText.Text =
                GetCommandOverridePreviewTitle() + Environment.NewLine +
                _activeCustomCommandArguments;
            return;
        }

        try
        {
            var selectedEngine = GetSelectedEngine();
            var executablePath = _engineResolution.IsConfigured && !string.IsNullOrWhiteSpace(_engineResolution.ExecutablePath)
                ? _engineResolution.ExecutablePath
                : GetEngineExecutableDisplayName(selectedEngine);

            var command = IperfCommandBuilder.BuildClientCommand(executablePath, BuildDashboardTestOptions());

            EngineOutputText.Text =
                "Command preview:" + Environment.NewLine +
                string.Join(" ", command.Arguments.Select(QuoteIfNeeded));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or ArgumentOutOfRangeException or NotSupportedException)
        {
            EngineOutputText.Text =
                string.IsNullOrWhiteSpace(GetServerText())
                    ? "Enter a server to preview the iperf command."
                    : "Command preview unavailable:" + Environment.NewLine + ex.Message;
        }
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
            : servers.FirstOrDefault() ?? string.Empty;
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
            ?? string.Empty;
    }

    private IperfEngine GetSelectedEngine()
    {
        var selectedText = (EngineBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

        return selectedText switch
        {
            "iperf2" => IperfEngine.Iperf2,
            _ => IperfEngine.Iperf3
        };
    }

    private IperfEngine GetSelectedServerEngine()
    {
        var selectedText = (ServerEngineBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

        return selectedText switch
        {
            "iperf2" => IperfEngine.Iperf2,
            _ => IperfEngine.Iperf3
        };
    }

    private IperfServerProtocol GetSelectedServerProtocol()
    {
        var selectedText = (ServerProtocolBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

        return selectedText switch
        {
            "UDP" => IperfServerProtocol.Udp,
            _ => IperfServerProtocol.Tcp
        };
    }

    private void SelectEngine(IperfEngine engine)
    {
        var label = GetEngineDisplayName(engine);

        for (var i = 0; i < EngineBox.Items.Count; i++)
        {
            if ((EngineBox.Items[i] as ComboBoxItem)?.Content?.ToString() == label)
            {
                EngineBox.SelectedIndex = i;
                return;
            }
        }

        EngineBox.SelectedIndex = 0;
    }

    private void EngineSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EngineBox is null || PortBox is null)
        {
            return;
        }

        var selectedEngine = GetSelectedEngine();

        NormalizeUnsupportedDashboardModeForSelectedEngine();

        if (selectedEngine == IperfEngine.Iperf2 &&
            string.Equals(PortBox.Text.Trim(), "5201", StringComparison.Ordinal))
        {
            PortBox.Text = "5001";
        }
        else if (selectedEngine == IperfEngine.Iperf3 &&
                 string.Equals(PortBox.Text.Trim(), "5001", StringComparison.Ordinal))
        {
            PortBox.Text = "5201";
        }

        _settings.SelectedEngine = GetEngineDisplayName(selectedEngine);
        _settingsStore.Save(_settings);

        ClearCommandOverride(updatePreview: false);
        RefreshEngineStatus();
        RefreshIntegrationStatus();
        Dispatcher.BeginInvoke(UpdateDashboardCommandPreview, DispatcherPriority.Background);
    }

    private bool NormalizeUnsupportedDashboardModeForSelectedEngine()
    {
        if (GetSelectedEngine() == IperfEngine.Iperf2 &&
            !IsSupportedIperf2DashboardMode(GetSelectedMode()))
        {
            SelectMode(IperfMode.TcpUpload);
            return true;
        }

        return false;
    }

    private static bool IsSupportedIperf2DashboardMode(IperfMode mode)
    {
        return mode is IperfMode.TcpUpload or IperfMode.UdpUpload;
    }

    private static string GetEngineDisplayName(IperfEngine engine)
    {
        return engine switch
        {
            IperfEngine.Iperf2 => "iperf2",
            _ => "iperf3"
        };
    }

    private static string GetEngineExecutableDisplayName(IperfEngine engine)
    {
        return engine switch
        {
            IperfEngine.Iperf2 => "iperf.exe",
            _ => "iperf3.exe"
        };
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

    private static int ParseNonNegativeInt(TextBox box, string fieldName)
    {
        if (!int.TryParse(box.Text.Trim(), out var value) || value < 0)
        {
            throw new FormatException($"{fieldName} must be zero or a positive number.");
        }

        return value;
    }

    private void SetRunState(bool isRunning)
    {
        StartButton.IsEnabled = !isRunning;
        StopButton.IsEnabled = isRunning;
        EngineBox.IsEnabled = !isRunning;
        CommandMenuButton.IsEnabled = !isRunning;
        RemoveServerButton.IsEnabled = !isRunning;
    }

    private bool TryHandleStructuredIperfOutput(IperfTestOptions options, string text)
    {
        if (options.Engine == IperfEngine.Iperf2)
        {
            if (Iperf2TextParser.TryParseUdpServerReport(
                    text,
                    out _))
            {
                AppendEngineOutput(text);
                return true;
            }

            var preferIperf2SumLine = options.Streams > 1;
            if (Iperf2TextParser.TryParseIntervalSample(text, out var iperf2Sample, preferIperf2SumLine, options.DurationSeconds))
            {
                UpdateLiveMetrics(iperf2Sample);
                AppendEngineOutput(FormatIntervalSample(iperf2Sample));
                return true;
            }

            return false;
        }

        if (IperfJsonStreamParser.TryParseIntervalSample(text, out var sample))
        {
            if (sample.Omitted)
            {
                HandleOmittedWarmupSample(sample);
                return true;
            }

            UpdateLiveMetrics(sample);
            AppendEngineOutput(FormatIntervalSample(sample));
            return true;
        }

        if (IperfJsonStreamParser.TryParseEndSummarySample(text, out var endSample))
        {
            UpdateLiveMetrics(endSample);
            AppendEngineOutput(FormatEndSummarySample(endSample));
            return true;
        }

        if (TryFormatJsonStreamEvent(text, out var eventMessage))
        {
            AppendEngineOutput(eventMessage);
            return true;
        }

        return false;
    }

    private int ReconcileFinalIperf2UdpServerReport(
        IperfTestOptions options,
        IperfRunResult result)
    {
        if (options.Engine != IperfEngine.Iperf2 ||
            options.Mode is not (
                IperfMode.UdpUpload or
                IperfMode.UdpDownload))
        {
            return 0;
        }

        var reports = new List<IperfIntervalSample>();

        foreach (var line in result.Output)
        {
            if (line.Stream != IperfOutputStream.StandardOutput)
            {
                continue;
            }

            if (line.Text
                .TrimStart()
                .StartsWith(
                    "[SUM]",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (Iperf2TextParser.TryParseUdpServerReport(
                    line.Text,
                    out var parsedReport))
            {
                reports.Add(parsedReport);
            }
        }

        _iperf2UdpServerReportCount = reports.Count;

        if (!Iperf2TextParser.TryAggregateUdpServerReports(
                reports,
                options.Streams,
                out var aggregate))
        {
            _iperf2UdpServerReport = null;
            ApplyMissingIperf2UdpServerReport(
                reports.Count,
                options.Streams);

            return reports.Count;
        }

        _iperf2UdpServerReport = aggregate;
        ApplyIperf2UdpServerReport(aggregate);

        return reports.Count;
    }

    private void ApplyMissingIperf2UdpServerReport(
        int receivedReportCount,
        int expectedReportCount)
    {
        ThroughputValueText.Text = "unavailable";
        ThroughputCaptionText.Text = "Server result missing";
        JitterValueText.Text = "unavailable";
        LossValueText.Text = "unavailable";
        LiveStatusText.Text =
            $"Incomplete server report: {receivedReportCount}/{expectedReportCount} streams";
    }

    private void ApplyIperf2UdpServerReport(
        IperfIntervalSample report)
    {
        if (report.MegabitsPerSecond is double receivedMegabits)
        {
            ThroughputValueText.Text =
                FormatMegabits(receivedMegabits);
            ThroughputCaptionText.Text = "Server received total";
            LiveStatusText.Text =
                $"Server received total {FormatMegabits(receivedMegabits)} · chart shows sent rate";
        }

        if (report.JitterMs is double jitterMs)
        {
            JitterValueText.Text =
                jitterMs.ToString(
                    "0.000",
                    CultureInfo.InvariantCulture) +
                " ms";
        }

        if (report.EffectiveLostPercent is double lostPercent)
        {
            LossValueText.Text =
                lostPercent.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture) +
                " %";
        }
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

    private void ResetLiveMetrics(IperfMode mode)
    {
        _iperf2UdpServerReport = null;
        _iperf2UdpServerReportCount = 0;

        var isIperf2Udp =
            _activeEngine == IperfEngine.Iperf2 &&
            mode is (
                IperfMode.UdpUpload or
                IperfMode.UdpDownload);

        ThroughputValueText.Text =
            isIperf2Udp
                ? "pending"
                : "0 Mbps";

        ThroughputCaptionText.Text =
            isIperf2Udp
                ? "Awaiting server result"
                : "Live total average";

        if (mode is IperfMode.UdpUpload or IperfMode.UdpDownload)
        {
            JitterValueText.Text = "pending";
            LossValueText.Text = "pending";
        }
        else
        {
            JitterValueText.Text = "n/a";
            LossValueText.Text = "n/a";
        }

        LiveStatusText.Text = "Waiting for samples...";
        ShowWaitingChartPlaceholder();

        _throughputSamples.Clear();
        _streamThroughputSamples.Clear();
        _reverseThroughputSamples.Clear();
        _reverseStreamThroughputSamples.Clear();
        RenderThroughputChart();
    }

    private void UpdateLiveMetrics(IperfIntervalSample sample)
    {
        if (sample.MegabitsPerSecond is double megabitsPerSecond)
        {
            var keepIperf2UdpResultPending =
                _activeEngine == IperfEngine.Iperf2 &&
                _activeMode is (
                    IperfMode.UdpUpload or
                    IperfMode.UdpDownload);

            if (!keepIperf2UdpResultPending)
            {
                ThroughputValueText.Text =
                    _activeMode == IperfMode.TcpBidirectional &&
                    sample.ReverseMegabitsPerSecond is double reverseMegabitsPerSecond
                        ? FormatBidirectionalMegabits(
                            megabitsPerSecond,
                            reverseMegabitsPerSecond)
                        : FormatMegabits(megabitsPerSecond);
            }

            AddThroughputSample(
                megabitsPerSecond,
                sample.StreamMegabitsPerSecond,
                sample.ReverseMegabitsPerSecond,
                sample.ReverseStreamMegabitsPerSecond);
        }

        if (sample.JitterMs is double jitterMs)
        {
            JitterValueText.Text = jitterMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms";
        }
        else if (_activeMode is not (IperfMode.UdpUpload or IperfMode.UdpDownload))
        {
            JitterValueText.Text = "n/a";
        }

        if (sample.EffectiveLostPercent is double lostPercent)
        {
            LossValueText.Text = lostPercent.ToString("0.0", CultureInfo.InvariantCulture) + " %";
        }
        else if (_activeMode is not (IperfMode.UdpUpload or IperfMode.UdpDownload))
        {
            LossValueText.Text = "n/a";
        }

        LiveStatusText.Text = sample.Seconds is double seconds
            ? "Last sample " + seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s"
            : "Receiving samples...";
    }

    private void HandleOmittedWarmupSample(IperfIntervalSample sample)
    {
        _omittedWarmupIntervalsReceived++;

        var elapsed = _activeOmitSeconds > 0
            ? Math.Min(_activeOmitSeconds, _omittedWarmupIntervalsReceived)
            : _omittedWarmupIntervalsReceived;

        var throughputSuffix = sample.MegabitsPerSecond is double megabitsPerSecond
            ? " · " + FormatMegabits(megabitsPerSecond) + " ignored"
            : string.Empty;

        LiveStatusText.Text = _activeOmitSeconds > 0
            ? $"Warm-up {elapsed}/{_activeOmitSeconds}s omitted{throughputSuffix}"
            : $"Warm-up sample omitted{throughputSuffix}";

        if (_activeOmitSeconds > 0)
        {
            ShowWarmupChartPlaceholder(elapsed, _activeOmitSeconds, sample.MegabitsPerSecond);
        }

        if (_activeOmitSeconds > 0 &&
            (_omittedWarmupIntervalsReceived == 1 ||
             _omittedWarmupIntervalsReceived % 5 == 0 ||
             elapsed >= _activeOmitSeconds))
        {
            AppendEngineOutput($"Warm-up {elapsed}/{_activeOmitSeconds}s omitted{throughputSuffix}.");
        }
    }

    private void ThroughputChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderThroughputChart();
    }

    private void ShowWaitingChartPlaceholder()
    {
        ThroughputChartPlaceholder.Text = "Waiting for throughput samples...";
        ThroughputChartPlaceholder.Foreground = TryFindResource("TextMuted") as Brush ?? Brushes.LightSlateGray;
        ThroughputChartPlaceholder.Visibility = Visibility.Visible;
    }

    private void ShowWarmupChartPlaceholder(int elapsedSeconds, int totalSeconds, double? ignoredMegabitsPerSecond)
    {
        var throughputText = ignoredMegabitsPerSecond is double mbps
            ? Environment.NewLine + FormatMegabits(mbps) + " ignored"
            : string.Empty;

        ThroughputChartPlaceholder.Text =
            $"Warm-up {elapsedSeconds}/{totalSeconds}s" +
            Environment.NewLine +
            "Ignoring warm-up samples. Live chart starts after warm-up." +
            throughputText;

        ThroughputChartPlaceholder.Foreground = TryFindResource("AccentAmber") as Brush ?? Brushes.Orange;
        ThroughputChartPlaceholder.Visibility = Visibility.Visible;
    }

    private void AddThroughputSample(
        double megabitsPerSecond,
        IReadOnlyList<double> streamMegabitsPerSecond,
        double? reverseMegabitsPerSecond,
        IReadOnlyList<double> reverseStreamMegabitsPerSecond)
    {
        if (double.IsNaN(megabitsPerSecond) || double.IsInfinity(megabitsPerSecond) || megabitsPerSecond < 0)
        {
            return;
        }

        _throughputSamples.Add(megabitsPerSecond);
        _streamThroughputSamples.Add(
            streamMegabitsPerSecond
                .Where(value => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0)
                .ToList());

        if (reverseMegabitsPerSecond is double reverseValue &&
            !double.IsNaN(reverseValue) &&
            !double.IsInfinity(reverseValue) &&
            reverseValue >= 0)
        {
            _reverseThroughputSamples.Add(reverseValue);
            _reverseStreamThroughputSamples.Add(
                reverseStreamMegabitsPerSecond
                    .Where(value => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0)
                    .ToList());
        }

        if (_throughputSamples.Count > MaxThroughputSamples)
        {
            var removeCount = _throughputSamples.Count - MaxThroughputSamples;
            _throughputSamples.RemoveRange(0, removeCount);
            _streamThroughputSamples.RemoveRange(0, Math.Min(removeCount, _streamThroughputSamples.Count));
        }

        if (_reverseThroughputSamples.Count > MaxThroughputSamples)
        {
            var removeCount = _reverseThroughputSamples.Count - MaxThroughputSamples;
            _reverseThroughputSamples.RemoveRange(0, removeCount);
            _reverseStreamThroughputSamples.RemoveRange(0, Math.Min(removeCount, _reverseStreamThroughputSamples.Count));
        }

        RenderThroughputChart();
    }

    private void RenderThroughputChart()
    {
        ThroughputChartLine.Points.Clear();
        ThroughputChartGridCanvas.Children.Clear();
        ThroughputChartStreamCanvas.Children.Clear();
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
        ThroughputChartStreamCanvas.Width = width;
        ThroughputChartStreamCanvas.Height = height;
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
        var timeAxisMaxSeconds = Math.Max(1, _activeChartDurationSeconds);

        DrawThroughputChartFrame(plotLeft, plotTop, plotWidth, plotHeight, axis.Min, axis.Max, axis.Step, timeAxisMaxSeconds);

        if (_throughputSamples.Count == 0)
        {
            ThroughputChartPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        ThroughputChartPlaceholder.Visibility = Visibility.Collapsed;

        var axisRange = Math.Max(1, axis.Max - axis.Min);

        DrawStreamThroughputLines(plotLeft, plotBottom, plotWidth, plotHeight, axis.Min, axisRange, timeAxisMaxSeconds);
        DrawReverseThroughputLine(plotLeft, plotBottom, plotWidth, plotHeight, axis.Min, axisRange, timeAxisMaxSeconds);

        var points = new PointCollection();

        for (var i = 0; i < _throughputSamples.Count; i++)
        {
            var x = CalculateSampleX(plotLeft, plotWidth, i, _throughputSamples.Count, timeAxisMaxSeconds);

            var normalized = (_throughputSamples[i] - axis.Min) / axisRange;
            normalized = Math.Clamp(normalized, 0, 1);

            var y = plotBottom - (normalized * plotHeight);

            points.Add(new Point(x, y));
            DrawChartMarker(x, y);
        }

        ThroughputChartLine.Points = points;

        var accentBrush = TryFindResource("Accent") as Brush ?? Brushes.DeepSkyBlue;

        DrawChartText(
            "Total bandwidth",
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

        var max = Math.Max(
            10,
            Math.Max(
                _throughputSamples.DefaultIfEmpty(0).Max(),
                _reverseThroughputSamples.DefaultIfEmpty(0).Max()));
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

    private static IReadOnlyList<int> BuildTimeAxisTicks(int timeAxisMaxSeconds)
    {
        timeAxisMaxSeconds = Math.Max(1, timeAxisMaxSeconds);

        var step = Math.Max(1, (int)NiceStep(timeAxisMaxSeconds / 5.0));
        var ticks = new List<int>();

        for (var seconds = 0; seconds < timeAxisMaxSeconds; seconds += step)
        {
            ticks.Add(seconds);
        }

        if (ticks.Count == 0 || ticks[^1] != timeAxisMaxSeconds)
        {
            ticks.Add(timeAxisMaxSeconds);
        }

        return ticks;
    }

    private static double CalculateSampleX(
        double plotLeft,
        double plotWidth,
        int sampleIndex,
        int sampleCount,
        int timeAxisMaxSeconds)
    {
        timeAxisMaxSeconds = Math.Max(1, timeAxisMaxSeconds);
        sampleCount = Math.Max(1, sampleCount);

        var elapsedSeconds = Math.Min(timeAxisMaxSeconds, sampleIndex + 1);
        return plotLeft + plotWidth * elapsedSeconds / timeAxisMaxSeconds;
    }

    private void DrawThroughputChartFrame(
        double plotLeft,
        double plotTop,
        double plotWidth,
        double plotHeight,
        double axisMin,
        double axisMax,
        double axisStep,
        int timeAxisMaxSeconds)
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

        foreach (var seconds in BuildTimeAxisTicks(timeAxisMaxSeconds))
        {
            var x = plotLeft + plotWidth * seconds / timeAxisMaxSeconds;
            var isAxisLine = seconds == 0;

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
                seconds.ToString(CultureInfo.InvariantCulture),
                x - 6,
                plotTop + plotHeight + 8,
                10,
                FontWeights.Normal,
                textBrush);
        }

        DrawChartText("Mbps", 8, plotTop - 20, 10, FontWeights.SemiBold, axisBrush);
        DrawChartText("Time (sec)", plotLeft + plotWidth - 62, plotTop + plotHeight + 8, 10, FontWeights.SemiBold, axisBrush);
    }

    private void DrawReverseThroughputLine(
        double plotLeft,
        double plotBottom,
        double plotWidth,
        double plotHeight,
        double axisMin,
        double axisRange,
        int timeAxisMaxSeconds)
    {
        if (_reverseThroughputSamples.Count < 2)
        {
            return;
        }

        var points = new PointCollection();

        for (var i = 0; i < _reverseThroughputSamples.Count; i++)
        {
            var x = CalculateSampleX(plotLeft, plotWidth, i, _reverseThroughputSamples.Count, timeAxisMaxSeconds);

            var normalized = (_reverseThroughputSamples[i] - axisMin) / axisRange;
            normalized = Math.Clamp(normalized, 0, 1);

            var y = plotBottom - normalized * plotHeight;
            points.Add(new Point(x, y));
        }

        var reverseBrush = TryFindResource("AccentAmber") as Brush ?? Brushes.Orange;

        ThroughputChartStreamCanvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = reverseBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            Opacity = 0.92
        });

        DrawChartText(
            "Download",
            plotLeft + 8,
            20,
            11,
            FontWeights.SemiBold,
            reverseBrush);
    }

    private void DrawStreamThroughputLines(
        double plotLeft,
        double plotBottom,
        double plotWidth,
        double plotHeight,
        double axisMin,
        double axisRange,
        int timeAxisMaxSeconds)
    {
        var streamCount = Math.Max(
            _streamThroughputSamples
                .Select(sample => sample.Count)
                .DefaultIfEmpty(0)
                .Max(),
            _reverseStreamThroughputSamples
                .Select(sample => sample.Count)
                .DefaultIfEmpty(0)
                .Max());

        if (streamCount <= 1 || _streamThroughputSamples.Count <= 1)
        {
            return;
        }

        var streamMax = Math.Max(
            _streamThroughputSamples
                .SelectMany(sample => sample)
                .DefaultIfEmpty(0)
                .Max(),
            _reverseStreamThroughputSamples
                .SelectMany(sample => sample)
                .DefaultIfEmpty(0)
                .Max());

        if (streamMax <= 0 || double.IsNaN(streamMax) || double.IsInfinity(streamMax))
        {
            return;
        }

        var streamAxisMax = NiceStep(streamMax * 1.15);
        var streamBandHeight = Math.Max(42, plotHeight * 0.28);
        var streamBandTop = plotBottom - streamBandHeight;
        var streamBandBottom = plotBottom;

        var gridBrush = TryFindResource("BorderSoft") as Brush ?? Brushes.DimGray;
        var textBrush = TryFindResource("TextMuted") as Brush ?? Brushes.LightSlateGray;

        ThroughputChartStreamCanvas.Children.Add(new Line
        {
            X1 = plotLeft,
            Y1 = streamBandTop,
            X2 = plotLeft + plotWidth,
            Y2 = streamBandTop,
            Stroke = gridBrush,
            StrokeThickness = 1,
            Opacity = 0.70
        });

        DrawChartText(
            BuildPerStreamScaleLabel(streamAxisMax, streamCount),
            plotLeft + 8,
            streamBandTop + 4,
            10,
            FontWeights.SemiBold,
            textBrush);

        DrawStreamSet(_streamThroughputSamples, streamCount, plotLeft, plotWidth, streamBandBottom, streamBandHeight, streamAxisMax, timeAxisMaxSeconds, dashed: false);
        DrawStreamSet(_reverseStreamThroughputSamples, streamCount, plotLeft, plotWidth, streamBandBottom, streamBandHeight, streamAxisMax, timeAxisMaxSeconds, dashed: true);
    }

    private void DrawStreamSet(
        IReadOnlyList<IReadOnlyList<double>> samples,
        int streamCount,
        double plotLeft,
        double plotWidth,
        double streamBandBottom,
        double streamBandHeight,
        double streamAxisMax,
        int timeAxisMaxSeconds,
        bool dashed)
    {
        if (samples.Count <= 1)
        {
            return;
        }

        for (var streamIndex = 0; streamIndex < streamCount; streamIndex++)
        {
            var points = new PointCollection();

            for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
            {
                var streams = samples[sampleIndex];

                if (streamIndex >= streams.Count)
                {
                    continue;
                }

                var x = CalculateSampleX(plotLeft, plotWidth, sampleIndex, samples.Count, timeAxisMaxSeconds);

                var normalized = streams[streamIndex] / streamAxisMax;
                normalized = Math.Clamp(normalized, 0, 1);

                var y = streamBandBottom - normalized * (streamBandHeight - 16);
                points.Add(new Point(x, y));
            }

            if (points.Count < 2)
            {
                continue;
            }

            ThroughputChartStreamCanvas.Children.Add(new Polyline
            {
                Points = points,
                Stroke = CreateStreamBrush(streamIndex + (dashed ? 5 : 0)),
                StrokeThickness = dashed ? 1.05 : 1.25,
                StrokeDashArray = dashed ? new DoubleCollection { 4, 3 } : null,
                StrokeLineJoin = PenLineJoin.Round,
                Opacity = dashed ? 0.66 : 0.82
            });
        }
    }

    private string BuildPerStreamScaleLabel(double streamAxisMax, int streamCount)
    {
        var latestStreams =
            _streamThroughputSamples.LastOrDefault(sample => sample.Count > 0) ??
            _reverseStreamThroughputSamples.LastOrDefault(sample => sample.Count > 0);

        var streamValues = latestStreams?
            .Where(value => value > 0)
            .ToArray();

        if (streamValues is null || streamValues.Length == 0)
        {
            return "Per-stream: " +
                   streamCount.ToString(CultureInfo.InvariantCulture) +
                   " streams · scale 0-" +
                   FormatMegabits(streamAxisMax);
        }

        return "Per-stream: " +
               streamValues.Length.ToString(CultureInfo.InvariantCulture) +
               " streams · avg " +
               FormatMegabits(streamValues.Average()) +
               " · min " +
               FormatMegabits(streamValues.Min()) +
               " · max " +
               FormatMegabits(streamValues.Max()) +
               " · scale 0-" +
               FormatMegabits(streamAxisMax);
    }

    private static Brush CreateStreamBrush(int streamIndex)
    {
        var palette = new[]
        {
            Color.FromRgb(125, 211, 252),
            Color.FromRgb(167, 139, 250),
            Color.FromRgb(52, 211, 153),
            Color.FromRgb(251, 191, 36),
            Color.FromRgb(248, 113, 113),
            Color.FromRgb(45, 212, 191),
            Color.FromRgb(244, 114, 182),
            Color.FromRgb(129, 140, 248),
            Color.FromRgb(190, 242, 100),
            Color.FromRgb(251, 146, 60)
        };

        return new SolidColorBrush(palette[streamIndex % palette.Length]);
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

        if (_activeMode == IperfMode.TcpBidirectional && _reverseThroughputSamples.Count > 0)
        {
            var reverseCurrent = _reverseThroughputSamples[^1];
            var reverseAvg = _reverseThroughputSamples.Average();

            return "↑ " + FormatMegabits(current)
                + " · ↓ " + FormatMegabits(reverseCurrent)
                + "   ↑ avg " + FormatMegabits(avg)
                + " · ↓ avg " + FormatMegabits(reverseAvg);
        }

        return "total " + FormatMegabits(current)
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

    private static string FormatBidirectionalMegabits(double uploadMegabitsPerSecond, double downloadMegabitsPerSecond)
    {
        return "↑ " + FormatMegabitsNumber(uploadMegabitsPerSecond) +
               " / ↓ " + FormatMegabitsNumber(downloadMegabitsPerSecond) +
               " Mbps";
    }


    private static string FormatMegabitsNumber(double megabitsPerSecond)
    {
        var format = megabitsPerSecond >= 100
            ? "0"
            : "0.0";

        return megabitsPerSecond.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string FormatMegabits(double megabitsPerSecond)
    {
        var format = megabitsPerSecond >= 100
            ? "0"
            : "0.0";

        return megabitsPerSecond.ToString(format, CultureInfo.InvariantCulture) + " Mbps";
    }

    private string FormatEndSummarySample(IperfIntervalSample sample)
    {
        var parts = new List<string> { "Test completed" };

        var uploadMegabitsPerSecond = sample.MegabitsPerSecond;
        var downloadMegabitsPerSecond = sample.ReverseMegabitsPerSecond;

        if (_activeMode == IperfMode.TcpBidirectional &&
            !downloadMegabitsPerSecond.HasValue &&
            _reverseThroughputSamples.Count > 0)
        {
            downloadMegabitsPerSecond = _reverseThroughputSamples[^1];
        }

        if (_activeMode == IperfMode.TcpBidirectional &&
            uploadMegabitsPerSecond is double upload &&
            downloadMegabitsPerSecond is double download)
        {
            parts.Add("upload " + FormatMegabits(upload));
            parts.Add("download " + FormatMegabits(download));
        }
        else if (uploadMegabitsPerSecond is double megabitsPerSecond)
        {
            parts.Add(FormatMegabits(megabitsPerSecond));
        }
        else if (downloadMegabitsPerSecond is double reverseMegabitsPerSecond)
        {
            parts.Add("download " + FormatMegabits(reverseMegabitsPerSecond));
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

        if (sample.MegabitsPerSecond is double megabitsPerSecond &&
            sample.ReverseMegabitsPerSecond is double reverseMegabitsPerSecond)
        {
            parts.Add("upload " + FormatMegabits(megabitsPerSecond));
            parts.Add("download " + FormatMegabits(reverseMegabitsPerSecond));
        }
        else if (sample.MegabitsPerSecond is double megabitsPerSecondOnly)
        {
            parts.Add(FormatMegabits(megabitsPerSecondOnly));
        }
        else if (sample.ReverseMegabitsPerSecond is double reverseMegabitsPerSecondOnly)
        {
            parts.Add("download " + FormatMegabits(reverseMegabitsPerSecondOnly));
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

    private enum ActivePage
    {
        Dashboard,
        ServerMode,
        History
    }

    public sealed record HistoryListItem(
        Guid Id,
        string Title,
        string FinishedLocalText,
        string StatusText,
        Brush StatusBrush,
        string Summary,
        string CommandPreview,
        string Details);
}
