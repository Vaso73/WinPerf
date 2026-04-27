using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinPerf.App.Settings;
using WinPerf.Core.Iperf;
using WinPerf.Core.Profiles;

namespace WinPerf.App;

public partial class MainWindow : Window
{
    private readonly WinPerfSettingsStore _settingsStore = new();
    private readonly IperfExecutableResolver _executableResolver = new();
    private readonly IperfProcessRunner _processRunner = new();
    private readonly JsonSavedIperfProfileStore _profileStore = new(JsonSavedIperfProfileStore.GetDefaultFilePath());

    private WinPerfSettings _settings = new();
    private IperfExecutableResolution _engineResolution = new(false, null, "NotConfigured", "iperf3.exe is not configured.");
    private CancellationTokenSource? _currentRunCancellation;
    private readonly StringBuilder _engineOutput = new();
    private readonly List<double> _throughputSamples = new();
    private readonly List<IReadOnlyList<double>> _streamThroughputSamples = new();
    private readonly List<double> _reverseThroughputSamples = new();
    private readonly List<IReadOnlyList<double>> _reverseStreamThroughputSamples = new();
    private IperfMode? _activeMode;
    private SavedIperfProfilesDocument _profilesDocument = new();
    private bool _isLoadingProfileSelection;
    private bool _isApplyingDashboardProfile;
    private string? _activeCustomCommandArguments;
    private string? _activeCommandOverrideSource;

    private const string AdvancedCommandOverrideSource = "Advanced";
    private const string CustomCommandOverrideSource = "Custom";
    private const string UiDensityComfortable = "Comfortable";
    private const string UiDensityCompact = "Compact";
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
        ApplyUiDensity(resizeWindow: true);
        UpdateCommandOverrideUx();
        UpdateDashboardCommandPreview();

        Loaded += async (_, _) => await LoadDashboardProfilesAsync();
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
            SetRunState(isRunning: true);
            ResetLiveMetrics(options.Mode);

            _engineOutput.Clear();
            AppendEngineOutput("Running command:");
            AppendEngineOutput(commandDisplayText);
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
            UpdateLastSummary(options, result.ExitCode);
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
        OpenSettingsWindow();
    }

    private void EngineStatusText_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenSettingsWindow();
    }

    private void OpenSettingsWindow()
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
            UpdateDashboardCommandPreview();
        }
    }

    private void ApplyDashboardLayout()
    {
        if (_settings.DashboardEngineOutputHeight is double height &&
            !double.IsNaN(height) &&
            !double.IsInfinity(height) &&
            height >= EngineOutputRow.MinHeight)
        {
            EngineOutputRow.Height = new GridLength(height, GridUnitType.Pixel);
            LiveThroughputRow.Height = new GridLength(1, GridUnitType.Star);
        }

        if (_settings.DashboardLeftRailWidth is double width &&
            !double.IsNaN(width) &&
            !double.IsInfinity(width) &&
            width >= LeftRailColumn.MinWidth)
        {
            LeftRailColumn.Width = new GridLength(
                Math.Min(width, LeftRailColumn.MaxWidth),
                GridUnitType.Pixel);
        }
    }

    private void SaveDashboardLayout()
    {
        var height = EngineOutputRow.ActualHeight;

        if (!double.IsNaN(height) && !double.IsInfinity(height) && height >= EngineOutputRow.MinHeight)
        {
            _settings.DashboardEngineOutputHeight = Math.Round(height, 0);
        }

        var width = LeftRailColumn.ActualWidth;

        if (!double.IsNaN(width) && !double.IsInfinity(width) && width >= LeftRailColumn.MinWidth)
        {
            _settings.DashboardLeftRailWidth = Math.Round(
                Math.Min(width, LeftRailColumn.MaxWidth),
                0);
        }

        _settingsStore.Save(_settings);
    }

    private void UpdateLastSummary(IperfTestOptions options, int exitCode)
    {
        var lines = new List<string>
        {
            $"{FormatModeLabel(options.Mode)} · {options.Server}:{options.Port}"
        };

        if (_throughputSamples.Count > 0)
        {
            var current = _throughputSamples[^1];
            var min = _throughputSamples.Min();
            var avg = _throughputSamples.Average();
            var max = _throughputSamples.Max();

            if (options.Mode == IperfMode.TcpBidirectional && _reverseThroughputSamples.Count > 0)
            {
                var reverseCurrent = _reverseThroughputSamples[^1];
                var reverseMin = _reverseThroughputSamples.Min();
                var reverseAvg = _reverseThroughputSamples.Average();
                var reverseMax = _reverseThroughputSamples.Max();

                lines.Add($"Upload {FormatMegabits(current)} · min {FormatMegabits(min)} · avg {FormatMegabits(avg)} · max {FormatMegabits(max)}");
                lines.Add($"Download {FormatMegabits(reverseCurrent)} · min {FormatMegabits(reverseMin)} · avg {FormatMegabits(reverseAvg)} · max {FormatMegabits(reverseMax)}");
            }
            else
            {
                lines.Add(
                    $"{FormatMegabits(current)} · min {FormatMegabits(min)} · avg {FormatMegabits(avg)} · max {FormatMegabits(max)}");
            }
        }
        else
        {
            lines.Add("No throughput samples.");
        }

        if (options.Mode is IperfMode.UdpUpload or IperfMode.UdpDownload)
        {
            var udpParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(JitterValueText.Text) &&
                !string.Equals(JitterValueText.Text, "n/a", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(JitterValueText.Text, "-- ms", StringComparison.OrdinalIgnoreCase))
            {
                udpParts.Add("jitter " + JitterValueText.Text);
            }

            if (!string.IsNullOrWhiteSpace(LossValueText.Text) &&
                !string.Equals(LossValueText.Text, "n/a", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(LossValueText.Text, "-- %", StringComparison.OrdinalIgnoreCase))
            {
                udpParts.Add("loss " + LossValueText.Text);
            }

            if (udpParts.Count > 0)
            {
                lines.Add(string.Join(" · ", udpParts));
            }
        }

        lines.Add($"{options.DurationSeconds}s · {options.Streams} stream(s) · {(exitCode == 0 ? "OK" : $"Exit {exitCode}")}");

        LastSummaryText.Text = string.Join(Environment.NewLine, lines);
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

    private void UiDensityButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.UiDensity = IsCompactUiDensity()
            ? UiDensityComfortable
            : UiDensityCompact;

        ApplyUiDensity(resizeWindow: true);
        _settingsStore.Save(_settings);
    }

    private bool IsCompactUiDensity()
    {
        return string.Equals(
            NormalizeUiDensity(_settings.UiDensity),
            UiDensityCompact,
            StringComparison.Ordinal);
    }

    private void ApplyUiDensity(bool resizeWindow)
    {
        var density = NormalizeUiDensity(_settings.UiDensity);
        _settings.UiDensity = density;

        var isCompact = string.Equals(density, UiDensityCompact, StringComparison.Ordinal);
        var scale = isCompact ? 0.92 : 1.0;

        DashboardBodyGrid.LayoutTransform = isCompact
            ? new ScaleTransform(scale, scale)
            : null;

        LeftRailColumn.MinWidth = isCompact ? 280 : 320;
        LeftRailColumn.MaxWidth = isCompact ? 500 : 560;

        var leftRailTarget = isCompact ? 360 : 410;
        var leftRailWidth = LeftRailColumn.Width.IsAbsolute
            ? LeftRailColumn.Width.Value
            : leftRailTarget;

        if (isCompact)
        {
            leftRailWidth = Math.Min(leftRailWidth, leftRailTarget);
        }
        else if (leftRailWidth < 380)
        {
            leftRailWidth = leftRailTarget;
        }

        LeftRailColumn.Width = new GridLength(
            Math.Clamp(leftRailWidth, LeftRailColumn.MinWidth, LeftRailColumn.MaxWidth));

        DashboardContentPanel.Margin = isCompact
            ? new Thickness(18)
            : new Thickness(26);

        MetricsRow.Height = new GridLength(isCompact ? 150 : 170);
        LiveThroughputRow.MinHeight = isCompact ? 180 : 220;
        EngineOutputRow.Height = new GridLength(isCompact ? 120 : 150);
        EngineOutputRow.MinHeight = isCompact ? 80 : 95;

        MinWidth = isCompact ? 760 : 820;
        MinHeight = isCompact ? 520 : 560;

        UiDensityButton.Content = isCompact
            ? "UI: Compact"
            : "UI: Comfortable";

        UiDensityButton.ToolTip = isCompact
            ? "Click to switch to comfortable UI density."
            : "Click to switch to compact UI density.";

        if (resizeWindow && WindowState == WindowState.Normal)
        {
            Width = Math.Clamp(Width, MinWidth, isCompact ? 1080 : 1220);
            Height = Math.Clamp(Height, MinHeight, isCompact ? 720 : 800);
        }

        RenderThroughputChart();
    }

    private static string NormalizeUiDensity(string? value)
    {
        return string.Equals(value, UiDensityComfortable, StringComparison.OrdinalIgnoreCase)
            ? UiDensityComfortable
            : UiDensityCompact;
    }

    private void RefreshEngineStatus()
    {
        _engineResolution = _executableResolver.Resolve(AppContext.BaseDirectory, new IperfEngineSettings
        {
            ExecutablePath = _settings.IperfExecutablePath
        });

        if (_engineResolution.IsConfigured)
        {
            var source = string.IsNullOrWhiteSpace(_engineResolution.Source)
                ? "Configured"
                : _engineResolution.Source;

            EngineStatusText.Text = $"Engine  ●  Ready  ●  {source}";
            EngineStatusText.ToolTip = _engineResolution.ExecutablePath;
            return;
        }

        EngineStatusText.Text = "Engine  ●  Not configured";
        EngineStatusText.ToolTip = _engineResolution.Message;
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
            Server = GetServerText(),
            Port = ParsePositiveInt(PortBox, "Port"),
            Streams = ParsePositiveInt(StreamsBox, "Streams"),
            DurationSeconds = ParsePositiveInt(DurationBox, "Duration"),
            Mode = GetSelectedMode(),
            AddressFamily = IperfAddressFamily.IPv4
        };
    }

    private string BuildDashboardCommandArgumentsPreview()
    {
        var command = IperfCommandBuilder.BuildClientCommand("iperf3.exe", BuildDashboardTestOptions());
        return string.Join(" ", command.Arguments.Select(QuoteIfNeeded));
    }

    private IperfTestOptions BuildCustomCommandOptions(string commandArguments)
    {
        var args = SplitCommandLine(commandArguments);
        var mode = InferCustomCommandMode(args);

        return new IperfTestOptions
        {
            Server = TryGetArgumentValue(args, "-c") ?? GetServerText(),
            Port = TryGetPositiveIntArgumentValue(args, "-p") ?? ParsePositiveInt(PortBox, "Port"),
            Streams = TryGetPositiveIntArgumentValue(args, "-P") ?? ParsePositiveInt(StreamsBox, "Streams"),
            DurationSeconds = TryGetPositiveIntArgumentValue(args, "-t") ?? ParsePositiveInt(DurationBox, "Duration"),
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
            SelectMode(profile.ToIperfMode());
        }
        finally
        {
            _isApplyingDashboardProfile = false;
        }

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

        _activeCustomCommandArguments = null;
        Dispatcher.BeginInvoke(UpdateDashboardCommandPreview, DispatcherPriority.Background);
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
            var executablePath = _engineResolution.IsConfigured && !string.IsNullOrWhiteSpace(_engineResolution.ExecutablePath)
                ? _engineResolution.ExecutablePath
                : "iperf3.exe";

            var options = new IperfTestOptions
            {
                Server = GetServerText(),
                Port = ParsePositiveInt(PortBox, "Port"),
                Streams = ParsePositiveInt(StreamsBox, "Streams"),
                DurationSeconds = ParsePositiveInt(DurationBox, "Duration"),
                Mode = GetSelectedMode(),
                AddressFamily = IperfAddressFamily.IPv4
            };

            var command = IperfCommandBuilder.BuildClientCommand(executablePath, options);

            EngineOutputText.Text =
                "Command preview:" + Environment.NewLine +
                string.Join(" ", command.Arguments.Select(QuoteIfNeeded));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or ArgumentOutOfRangeException)
        {
            EngineOutputText.Text =
                "Command preview unavailable:" + Environment.NewLine +
                ex.Message;
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
        CommandMenuButton.IsEnabled = !isRunning;
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

    private void ResetLiveMetrics(IperfMode mode)
    {
        ThroughputValueText.Text = "0 Mbps";

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
            ThroughputValueText.Text = _activeMode == IperfMode.TcpBidirectional &&
                                       sample.ReverseMegabitsPerSecond is double reverseMegabitsPerSecond
                ? FormatBidirectionalMegabits(megabitsPerSecond, reverseMegabitsPerSecond)
                : FormatMegabits(megabitsPerSecond);

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

        if (sample.LostPercent is double lostPercent)
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

    private void ThroughputChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderThroughputChart();
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

        DrawThroughputChartFrame(plotLeft, plotTop, plotWidth, plotHeight, axis.Min, axis.Max, axis.Step);

        if (_throughputSamples.Count == 0)
        {
            ThroughputChartPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        ThroughputChartPlaceholder.Visibility = Visibility.Collapsed;

        var axisRange = Math.Max(1, axis.Max - axis.Min);

        DrawStreamThroughputLines(plotLeft, plotBottom, plotWidth, plotHeight, axis.Min, axisRange);
        DrawReverseThroughputLine(plotLeft, plotBottom, plotWidth, plotHeight, axis.Min, axisRange);

        var points = new PointCollection();

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

    private void DrawReverseThroughputLine(
        double plotLeft,
        double plotBottom,
        double plotWidth,
        double plotHeight,
        double axisMin,
        double axisRange)
    {
        if (_reverseThroughputSamples.Count < 2)
        {
            return;
        }

        var points = new PointCollection();

        for (var i = 0; i < _reverseThroughputSamples.Count; i++)
        {
            var x = _reverseThroughputSamples.Count == 1
                ? plotLeft
                : plotLeft + (plotWidth * i / (_reverseThroughputSamples.Count - 1));

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
        double axisRange)
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
            "Streams scale 0-" + FormatMegabits(streamAxisMax),
            plotLeft + 8,
            streamBandTop + 4,
            10,
            FontWeights.SemiBold,
            textBrush);

        DrawStreamSet(_streamThroughputSamples, streamCount, plotLeft, plotWidth, streamBandBottom, streamBandHeight, streamAxisMax, dashed: false);
        DrawStreamSet(_reverseStreamThroughputSamples, streamCount, plotLeft, plotWidth, streamBandBottom, streamBandHeight, streamAxisMax, dashed: true);
    }

    private void DrawStreamSet(
        IReadOnlyList<IReadOnlyList<double>> samples,
        int streamCount,
        double plotLeft,
        double plotWidth,
        double streamBandBottom,
        double streamBandHeight,
        double streamAxisMax,
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

                var x = samples.Count == 1
                    ? plotLeft
                    : plotLeft + (plotWidth * sampleIndex / (samples.Count - 1));

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
}
