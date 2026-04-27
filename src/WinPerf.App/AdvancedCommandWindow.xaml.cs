using System.Text;
using System.Windows;
using System.Windows.Controls;
using WinPerf.App.Settings;
using WinPerf.Core.Iperf;
using WinPerf.Core.Profiles;

namespace WinPerf.App;

public partial class AdvancedCommandWindow : Window
{
    private readonly JsonSavedIperfProfileStore _profileStore = new(JsonSavedIperfProfileStore.GetDefaultFilePath());
    private SavedIperfProfilesDocument _profilesDocument = new();
    private bool _isLoadingProfile;
    private bool _profilesLoaded;

    public AdvancedCommandWindow()
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "AdvancedCommandWindow");
    }

    public string CommandText => PreviewBox.Text.Trim();

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadProfilesAsync();
        UpdatePreview();
    }

    private void AnyInputChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdatePreview();
    }

    private async void UseButton_Click(object sender, RoutedEventArgs e)
    {
        UpdatePreview();

        var validation = ValidateOptions();
        if (string.IsNullOrWhiteSpace(CommandText) || !string.IsNullOrWhiteSpace(validation))
        {
            MessageBox.Show(
                this,
                validation ?? "Fix the advanced command options first.",
                "WinPerf",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (ProfileBox.SelectedItem is SavedIperfProfile selectedProfile)
        {
            await SetLastSelectedProfileAsync(selectedProfile.Id);
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private async Task LoadProfilesAsync()
    {
        try
        {
            _profilesDocument = await _profileStore.LoadAsync();
            _profilesLoaded = true;

            RefreshProfileList(_profilesDocument.LastSelectedProfileId ?? _profilesDocument.DefaultProfileId);

            if (ProfileBox.SelectedItem is SavedIperfProfile selectedProfile)
            {
                ApplyProfileToInputs(selectedProfile);
                SetProfileStatus($"Loaded profile '{selectedProfile.Name}'.");
            }
            else
            {
                ProfileNameBox.Text = BuildSuggestedProfileName();
                SetProfileStatus("No saved profiles yet.");
            }
        }
        catch (Exception ex)
        {
            _profilesDocument = new SavedIperfProfilesDocument();
            _profilesLoaded = true;
            RefreshProfileList(null);
            SetProfileStatus($"Profile load failed: {ex.Message}", isError: true);
        }
    }

    private async void ProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_profilesLoaded || _isLoadingProfile)
        {
            return;
        }

        if (ProfileBox.SelectedItem is not SavedIperfProfile selectedProfile)
        {
            return;
        }

        ApplyProfileToInputs(selectedProfile);
        _profilesDocument = _profilesDocument with
        {
            LastSelectedProfileId = selectedProfile.Id
        };

        await TrySaveProfilesAsync($"Selected profile '{selectedProfile.Name}'.", showMessageOnError: false);
    }

    private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not SavedIperfProfile selectedProfile)
        {
            await SaveProfileAsNewAsync();
            return;
        }

        if (!TryCreateCurrentProfile(selectedProfile.Id, selectedProfile.CreatedAtUtc, out var updatedProfile))
        {
            return;
        }

        UpsertProfile(updatedProfile, setDefaultWhenFirst: false);

        if (await TrySaveProfilesAsync($"Saved profile '{updatedProfile.Name}'."))
        {
            RefreshProfileList(updatedProfile.Id);
        }
    }

    private async void SaveProfileAsNewButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveProfileAsNewAsync();
    }

    private async Task SaveProfileAsNewAsync()
    {
        var now = DateTimeOffset.UtcNow;

        if (!TryCreateCurrentProfile(Guid.NewGuid(), now, out var newProfile))
        {
            return;
        }

        UpsertProfile(newProfile, setDefaultWhenFirst: true);

        if (await TrySaveProfilesAsync($"Saved new profile '{newProfile.Name}'."))
        {
            RefreshProfileList(newProfile.Id);
        }
    }

    private async void SetDefaultProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not SavedIperfProfile selectedProfile)
        {
            MessageBox.Show(
                this,
                "Select a profile first.",
                "WinPerf",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _profilesDocument = _profilesDocument with
        {
            DefaultProfileId = selectedProfile.Id,
            LastSelectedProfileId = selectedProfile.Id
        };

        await TrySaveProfilesAsync($"Default profile set to '{selectedProfile.Name}'.");
    }

    private async void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not SavedIperfProfile selectedProfile)
        {
            MessageBox.Show(
                this,
                "Select a profile first.",
                "WinPerf",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Delete profile '{selectedProfile.Name}'?",
            "WinPerf",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var remainingProfiles = _profilesDocument.Profiles
            .Where(profile => profile.Id != selectedProfile.Id)
            .ToList();

        var defaultProfileId = remainingProfiles.Any(profile => profile.Id == _profilesDocument.DefaultProfileId)
            ? _profilesDocument.DefaultProfileId
            : remainingProfiles.FirstOrDefault()?.Id;

        var lastSelectedProfileId = remainingProfiles.Any(profile => profile.Id == _profilesDocument.LastSelectedProfileId)
            ? _profilesDocument.LastSelectedProfileId
            : defaultProfileId;

        _profilesDocument = _profilesDocument with
        {
            Profiles = remainingProfiles,
            DefaultProfileId = defaultProfileId,
            LastSelectedProfileId = lastSelectedProfileId
        };

        if (await TrySaveProfilesAsync($"Deleted profile '{selectedProfile.Name}'."))
        {
            RefreshProfileList(lastSelectedProfileId);

            if (ProfileBox.SelectedItem is SavedIperfProfile fallbackProfile)
            {
                ApplyProfileToInputs(fallbackProfile);
            }
        }
    }

    private void RefreshProfileList(Guid? selectedProfileId)
    {
        _isLoadingProfile = true;

        try
        {
            var profiles = _profilesDocument.Profiles
                .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ProfileBox.ItemsSource = profiles;

            var selected = profiles.FirstOrDefault(profile => profile.Id == selectedProfileId)
                ?? profiles.FirstOrDefault(profile => profile.Id == _profilesDocument.LastSelectedProfileId)
                ?? profiles.FirstOrDefault(profile => profile.Id == _profilesDocument.DefaultProfileId)
                ?? profiles.FirstOrDefault();

            ProfileBox.SelectedItem = selected;

            if (selected is not null)
            {
                ProfileNameBox.Text = selected.Name;
            }
            else if (string.IsNullOrWhiteSpace(ProfileNameBox.Text))
            {
                ProfileNameBox.Text = BuildSuggestedProfileName();
            }
        }
        finally
        {
            _isLoadingProfile = false;
        }
    }

    private async Task SetLastSelectedProfileAsync(Guid profileId)
    {
        _profilesDocument = _profilesDocument with
        {
            LastSelectedProfileId = profileId
        };

        await TrySaveProfilesAsync("Last selected profile saved.", showMessageOnError: false);
    }

    private async Task<bool> TrySaveProfilesAsync(string statusMessage, bool showMessageOnError = true)
    {
        try
        {
            await _profileStore.SaveAsync(_profilesDocument);
            SetProfileStatus(statusMessage, isSuccess: true);
            return true;
        }
        catch (Exception ex)
        {
            SetProfileStatus($"Profile save failed: {ex.Message}", isError: true);

            if (showMessageOnError)
            {
                MessageBox.Show(
                    this,
                    $"Profile save failed:{Environment.NewLine}{ex.Message}",
                    "WinPerf",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return false;
        }
    }

    private void UpsertProfile(SavedIperfProfile profile, bool setDefaultWhenFirst)
    {
        var existingDefaultProfileId = _profilesDocument.DefaultProfileId;
        var profiles = _profilesDocument.Profiles
            .Where(existingProfile => existingProfile.Id != profile.Id)
            .Append(profile)
            .ToList();

        var defaultProfileId = existingDefaultProfileId;

        if (setDefaultWhenFirst && defaultProfileId is null)
        {
            defaultProfileId = profile.Id;
        }

        _profilesDocument = _profilesDocument with
        {
            Profiles = profiles,
            DefaultProfileId = defaultProfileId,
            LastSelectedProfileId = profile.Id
        };
    }

    private bool TryCreateCurrentProfile(
        Guid profileId,
        DateTimeOffset createdAtUtc,
        out SavedIperfProfile profile)
    {
        profile = null!;

        UpdatePreview();

        var validation = ValidateOptions();
        if (!string.IsNullOrWhiteSpace(validation))
        {
            MessageBox.Show(
                this,
                validation,
                "WinPerf",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        try
        {
            profile = CreateProfileFromInputs(profileId, createdAtUtc);
            var profileErrors = SavedIperfProfileValidation.Validate(profile);

            if (profileErrors.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, profileErrors),
                    "WinPerf",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            MessageBox.Show(
                this,
                $"Profile values are invalid:{Environment.NewLine}{ex.Message}",
                "WinPerf",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    private SavedIperfProfile CreateProfileFromInputs(Guid profileId, DateTimeOffset createdAtUtc)
    {
        var now = DateTimeOffset.UtcNow;
        var reportInterval = string.IsNullOrWhiteSpace(IntervalBox.Text)
            ? (int?)null
            : int.Parse(IntervalBox.Text.Trim());

        var omitSeconds = string.IsNullOrWhiteSpace(OmitSecondsBox.Text)
            ? (int?)null
            : int.Parse(OmitSecondsBox.Text.Trim());

        var clientPort = string.IsNullOrWhiteSpace(ClientPortBox.Text)
            ? (int?)null
            : int.Parse(ClientPortBox.Text.Trim());

        var udpBandwidth = UdpBandwidthBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(udpBandwidth))
        {
            udpBandwidth = "0";
        }

        return new SavedIperfProfile
        {
            Id = profileId,
            Name = GetProfileName(),
            RunMode = IsServerMode() ? SavedIperfRunMode.Server : SavedIperfRunMode.Client,
            Protocol = SelectedText(ProtocolBox) == "UDP" ? SavedIperfProtocol.Udp : SavedIperfProtocol.Tcp,
            AddressFamily = GetSelectedAddressFamily(),
            Server = EmptyToNull(ServerAddressBox.Text),
            BindAddress = EmptyToNull(BindAddressBox.Text),
            Port = int.Parse(PortBox.Text.Trim()),
            Streams = int.Parse(StreamsBox.Text.Trim()),
            DurationSeconds = int.Parse(DurationBox.Text.Trim()),
            ReportIntervalSeconds = reportInterval,
            OmitSeconds = omitSeconds,
            ClientPort = clientPort,
            Dscp = EmptyToNull(DscpBox.Text),
            Reverse = ReverseBox.IsChecked == true,
            Bidirectional = BidirectionalBox.IsChecked == true,
            UdpBandwidth = udpBandwidth,
            BufferLength = EmptyToNull(BufferLengthBox.Text),
            TcpWindow = EmptyToNull(WindowSizeBox.Text),
            TcpMss = EmptyToNull(TcpMssBox.Text),
            TcpNoDelay = TcpNoDelayBox.IsChecked == true,
            ZeroCopy = ZeroCopyBox.IsChecked == true,
            ReportFormat = GetSelectedReportFormat(),
            UseJsonStream = JsonStreamBox.IsChecked == true,
            Verbose = VerboseBox.IsChecked == true,
            ServerOneOff = OneOffServerBox.IsChecked == true,
            GetServerOutput = GetServerOutputBox.IsChecked == true,
            ExtraArguments = EmptyToNull(ExtraArgumentsBox.Text),
            CreatedAtUtc = createdAtUtc.ToUniversalTime(),
            UpdatedAtUtc = now
        };
    }

    private void ApplyProfileToInputs(SavedIperfProfile profile)
    {
        _isLoadingProfile = true;

        try
        {
            ProfileNameBox.Text = profile.Name;

            SelectComboBoxText(
                RunModeBox,
                profile.RunMode == SavedIperfRunMode.Server ? "Server mode" : "Client mode",
                fallbackIndex: 0);

            SelectComboBoxText(
                ProtocolBox,
                profile.Protocol == SavedIperfProtocol.Udp ? "UDP" : "TCP",
                fallbackIndex: 0);

            SelectComboBoxText(
                IpVersionBox,
                profile.AddressFamily switch
                {
                    IperfAddressFamily.Default => "Default",
                    IperfAddressFamily.IPv6 => "IPv6",
                    _ => "IPv4"
                },
                fallbackIndex: 1);

            ServerAddressBox.Text = profile.Server ?? string.Empty;
            BindAddressBox.Text = profile.BindAddress ?? string.Empty;
            PortBox.Text = profile.Port.ToString();
            StreamsBox.Text = profile.Streams.ToString();
            DurationBox.Text = profile.DurationSeconds.ToString();
            IntervalBox.Text = profile.ReportIntervalSeconds?.ToString() ?? string.Empty;
            OmitSecondsBox.Text = profile.OmitSeconds?.ToString() ?? string.Empty;
            ClientPortBox.Text = profile.ClientPort?.ToString() ?? string.Empty;
            DscpBox.Text = profile.Dscp ?? string.Empty;
            ReverseBox.IsChecked = profile.Reverse;
            BidirectionalBox.IsChecked = profile.Bidirectional;
            UdpBandwidthBox.Text = profile.UdpBandwidth;
            BufferLengthBox.Text = profile.BufferLength ?? string.Empty;
            WindowSizeBox.Text = profile.TcpWindow ?? string.Empty;
            TcpMssBox.Text = profile.TcpMss ?? string.Empty;
            TcpNoDelayBox.IsChecked = profile.TcpNoDelay;
            ZeroCopyBox.IsChecked = profile.ZeroCopy;
            SelectComboBoxTag(FormatBox, profile.ReportFormat, fallbackIndex: 2);
            JsonStreamBox.IsChecked = profile.UseJsonStream;
            VerboseBox.IsChecked = profile.Verbose;
            OneOffServerBox.IsChecked = profile.ServerOneOff;
            GetServerOutputBox.IsChecked = profile.GetServerOutput;
            ExtraArgumentsBox.Text = profile.ExtraArguments ?? string.Empty;
        }
        finally
        {
            _isLoadingProfile = false;
        }

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var validation = ValidateOptions();

        if (!string.IsNullOrWhiteSpace(validation))
        {
            PreviewBox.Text = string.Empty;
            ValidationText.Text = validation;
            ValidationText.Foreground = (System.Windows.Media.Brush)FindResource("TextMuted");
            return;
        }

        var args = BuildArguments();

        PreviewBox.Text = string.Join(" ", args.Select(QuoteIfNeeded));
        ValidationText.Text = "Ready";
        ValidationText.Foreground = (System.Windows.Media.Brush)FindResource("AccentGreen");
    }

    private string? ValidateOptions()
    {
        if (!IsPositiveInt(PortBox.Text, out var port) || port is < 1 or > 65535)
        {
            return "Port must be between 1 and 65535.";
        }

        if (IsClientMode() && string.IsNullOrWhiteSpace(ServerAddressBox.Text))
        {
            return "Client mode requires a server address.";
        }

        if (IsClientMode() && !IsPositiveInt(StreamsBox.Text, out _))
        {
            return "Streams must be a positive number.";
        }

        if (IsClientMode() && !IsPositiveInt(DurationBox.Text, out _))
        {
            return "Duration must be a positive number.";
        }

        if (!string.IsNullOrWhiteSpace(IntervalBox.Text) && !IsPositiveInt(IntervalBox.Text, out _))
        {
            return "Report interval must be empty or a positive number.";
        }

        if (!string.IsNullOrWhiteSpace(OmitSecondsBox.Text)
            && (!int.TryParse(OmitSecondsBox.Text.Trim(), out var omitSeconds) || omitSeconds < 0))
        {
            return "Omit seconds must be empty, zero, or a positive number.";
        }

        if (!string.IsNullOrWhiteSpace(ClientPortBox.Text)
            && (!int.TryParse(ClientPortBox.Text.Trim(), out var clientPort) || clientPort is < 1 or > 65535))
        {
            return "Client port must be empty or between 1 and 65535.";
        }

        if (!string.IsNullOrWhiteSpace(TcpMssBox.Text) && !IsPositiveInt(TcpMssBox.Text, out _))
        {
            return "TCP MSS must be empty or a positive number.";
        }

        if (ReverseBox.IsChecked == true && BidirectionalBox.IsChecked == true)
        {
            return "Reverse and bidirectional cannot be enabled together.";
        }

        return null;
    }

    private List<string> BuildArguments()
    {
        var args = new List<string>();

        if (IsServerMode())
        {
            args.Add("-s");
        }
        else
        {
            args.Add("-c");
            args.Add(ServerAddressBox.Text.Trim());
        }

        AddPair(args, "-p", PortBox.Text.Trim());

        var bind = BindAddressBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(bind))
        {
            AddPair(args, "-B", bind);
        }

        switch (SelectedText(IpVersionBox))
        {
            case "IPv4":
                args.Add("-4");
                break;
            case "IPv6":
                args.Add("-6");
                break;
        }

        if (SelectedText(ProtocolBox) == "UDP")
        {
            args.Add("-u");

            if (!string.IsNullOrWhiteSpace(UdpBandwidthBox.Text))
            {
                AddPair(args, "-b", UdpBandwidthBox.Text.Trim());
            }
        }

        if (IsClientMode())
        {
            AddPair(args, "-P", StreamsBox.Text.Trim());
            AddPair(args, "-t", DurationBox.Text.Trim());

            if (ReverseBox.IsChecked == true)
            {
                args.Add("-R");
            }

            if (BidirectionalBox.IsChecked == true)
            {
                args.Add("--bidir");
            }
        }

        if (!string.IsNullOrWhiteSpace(IntervalBox.Text))
        {
            AddPair(args, "-i", IntervalBox.Text.Trim());
        }

        var dscp = DscpBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(dscp))
        {
            AddPair(args, "--dscp", dscp);
        }

        var format = SelectedTag(FormatBox);
        if (JsonStreamBox.IsChecked != true && !string.IsNullOrWhiteSpace(format))
        {
            AddPair(args, "-f", format);
        }

        if (VerboseBox.IsChecked == true)
        {
            args.Add("-V");
        }

        if (JsonStreamBox.IsChecked == true)
        {
            args.Add("--json-stream");
        }

        if (IsClientMode())
        {
            if (!string.IsNullOrWhiteSpace(OmitSecondsBox.Text))
            {
                AddPair(args, "-O", OmitSecondsBox.Text.Trim());
            }

            if (!string.IsNullOrWhiteSpace(ClientPortBox.Text))
            {
                AddPair(args, "--cport", ClientPortBox.Text.Trim());
            }

            if (GetServerOutputBox.IsChecked == true)
            {
                args.Add("--get-server-output");
            }
        }

        var bufferLength = BufferLengthBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(bufferLength))
        {
            AddPair(args, "-l", bufferLength);
        }

        var windowSize = WindowSizeBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(windowSize))
        {
            AddPair(args, "-w", windowSize);
        }

        var tcpMss = TcpMssBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(tcpMss))
        {
            AddPair(args, "-M", tcpMss);
        }

        if (TcpNoDelayBox.IsChecked == true)
        {
            args.Add("-N");
        }

        if (ZeroCopyBox.IsChecked == true)
        {
            args.Add("-Z");
        }

        if (IsServerMode() && OneOffServerBox.IsChecked == true)
        {
            args.Add("-1");
        }

        args.AddRange(SplitExtraArguments(ExtraArgumentsBox.Text));

        return args;
    }

    private string GetProfileName()
    {
        var name = ProfileNameBox.Text.Trim();
        return string.IsNullOrWhiteSpace(name)
            ? BuildSuggestedProfileName()
            : name;
    }

    private string BuildSuggestedProfileName()
    {
        var host = IsServerMode()
            ? "Server"
            : ServerAddressBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(host))
        {
            host = "iperf3";
        }

        var protocol = SelectedText(ProtocolBox);
        var direction = BidirectionalBox.IsChecked == true
            ? "bidir"
            : ReverseBox.IsChecked == true
                ? "download"
                : "upload";

        var duration = string.IsNullOrWhiteSpace(DurationBox.Text)
            ? "test"
            : $"{DurationBox.Text.Trim()}s";

        var streams = string.IsNullOrWhiteSpace(StreamsBox.Text)
            ? string.Empty
            : $" x{StreamsBox.Text.Trim()}";

        return $"{host} {protocol} {direction} {duration}{streams}".Trim();
    }

    private IperfAddressFamily GetSelectedAddressFamily()
    {
        return SelectedText(IpVersionBox) switch
        {
            "Default" => IperfAddressFamily.Default,
            "IPv6" => IperfAddressFamily.IPv6,
            _ => IperfAddressFamily.IPv4
        };
    }

    private string GetSelectedReportFormat()
    {
        var format = SelectedTag(FormatBox);
        return string.IsNullOrWhiteSpace(format) ? "M" : format;
    }

    private static string? EmptyToNull(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static void AddPair(List<string> args, string name, string value)
    {
        args.Add(name);
        args.Add(value);
    }

    private static IReadOnlyList<string> SplitExtraArguments(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return [];
        }

        var args = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in commandText.Trim())
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

    private bool IsClientMode() => SelectedText(RunModeBox) == "Client mode";

    private bool IsServerMode() => SelectedText(RunModeBox) == "Server mode";

    private static bool IsPositiveInt(string value, out int number)
    {
        return int.TryParse(value.Trim(), out number) && number > 0;
    }

    private static string SelectedText(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
    }

    private static string SelectedTag(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
    }

    private static void SelectComboBoxText(ComboBox comboBox, string text, int fallbackIndex)
    {
        for (var i = 0; i < comboBox.Items.Count; i++)
        {
            if ((comboBox.Items[i] as ComboBoxItem)?.Content?.ToString() == text)
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        comboBox.SelectedIndex = fallbackIndex;
    }

    private static void SelectComboBoxTag(ComboBox comboBox, string tag, int fallbackIndex)
    {
        for (var i = 0; i < comboBox.Items.Count; i++)
        {
            if ((comboBox.Items[i] as ComboBoxItem)?.Tag?.ToString() == tag)
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        comboBox.SelectedIndex = fallbackIndex;
    }

    private static string QuoteIfNeeded(string value)
    {
        return value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
    }

    private void SetProfileStatus(string message, bool isError = false, bool isSuccess = false)
    {
        ProfileStatusText.Text = message;
        ProfileStatusText.Foreground = isError
            ? System.Windows.Media.Brushes.OrangeRed
            : isSuccess
                ? (System.Windows.Media.Brush)FindResource("AccentGreen")
                : (System.Windows.Media.Brush)FindResource("TextMuted");
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
