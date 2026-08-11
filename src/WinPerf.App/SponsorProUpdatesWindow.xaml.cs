using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinPerf.App.Settings;
using WinPerf.App.Updates;
using WinPerf.Core.Product;
using WinPerf.Core.Updates;

namespace WinPerf.App;

public partial class SponsorProUpdatesWindow : Window
{
    private readonly SponsorProSessionStore _sessionStore = new();
    private readonly string _versionText;
    private CancellationTokenSource? _operationCancellation;
    private SponsorProSession? _session;
    private WinPerfUpdateManifest? _availableManifest;

    public SponsorProUpdatesWindow(string versionText)
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "SponsorProUpdatesWindow");
        AppText.ApplyTo(this);

        _versionText = versionText;
        InstalledVersionText.Text = versionText;
        ProductText.Text = WinPerfUpdateService.ProductId;

        SetChip(UpdateCoreStatusChip, UpdateCoreStatusText, ChipState.Ready, AppText.T("Ready"));
        RefreshSponsorProStatus();
        ResetUpdateState();
        ApplyEditionBoundary();

        Closed += (_, _) =>
        {
            _operationCancellation?.Cancel();
            _operationCancellation?.Dispose();
        };
    }

    private void RefreshSponsorProStatus()
    {
        _session = _sessionStore.Load();

        if (!WinPerfProductEdition.SupportsSponsorProUpdates)
        {
            AccountBadgeText.Text = "GH";
            AccountTitleText.Text = WinPerfProductEdition.EditionName;
            AccountStatusText.Text = AppText.T("Sponsor Pro updates are available only in WinPerf Sponsor Pro.");
            SponsorProAccountButton.Content = AppText.T("Sign in with GitHub");
            SetChip(AccountStatusChip, AccountStatusChipText, ChipState.Missing, AppText.T("Free"));
            StatusText.Text = AppText.T("This edition does not use the private Sponsor Pro update channel.");
            return;
        }

        if (_session?.IsUsable == true)
        {
            var login = string.IsNullOrWhiteSpace(_session.GithubLogin)
                ? "GitHub account"
                : _session.GithubLogin;
            var tier = string.IsNullOrWhiteSpace(_session.SponsorTier)
                ? "Sponsor Pro"
                : _session.SponsorTier;

            AccountBadgeText.Text = "GH";
            AccountTitleText.Text = $"GitHub: {login}";
            AccountStatusText.Text = $"{tier} active · expires {_session.ExpiresAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
            SponsorProAccountButton.Content = AppText.T("Sign out");
            SetChip(AccountStatusChip, AccountStatusChipText, ChipState.Ready, AppText.T("Active"));
            StatusText.Text = AppText.T("Sponsor Pro account is connected. You can check for private updates.");
            return;
        }

        AccountBadgeText.Text = "GH";
        AccountTitleText.Text = AppText.T("Not signed in");
        AccountStatusText.Text = AppText.T("Sign in with GitHub to use Sponsor Pro updates.");
        SponsorProAccountButton.Content = AppText.T("Sign in with GitHub");
        SetChip(AccountStatusChip, AccountStatusChipText, ChipState.Missing, AppText.T("Missing"));
        StatusText.Text = AppText.T("Ready. Sign in to enable Sponsor Pro update checks.");
    }

    private void ResetUpdateState()
    {
        _availableManifest = null;
        LatestVersionText.Text = AppText.T("Not checked yet");
        InstallUpdateButton.IsEnabled = false;
        SetChip(UpdateStateChip, UpdateStateText, ChipState.Idle, AppText.T("Idle"));
    }

    private void ApplyEditionBoundary()
    {
        if (WinPerfProductEdition.SupportsSponsorProUpdates)
        {
            return;
        }

        SponsorProAccountButton.IsEnabled = false;
        CheckUpdatesButton.IsEnabled = false;
        InstallUpdateButton.IsEnabled = false;
        LatestVersionText.Text = AppText.T("Sponsor Pro updates are available only in WinPerf Sponsor Pro.");
        SetChip(UpdateStateChip, UpdateStateText, ChipState.Missing, AppText.T("Free"));
    }

    private async void SponsorProAccountButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshSponsorProStatus();
        if (!WinPerfProductEdition.SupportsSponsorProUpdates)
        {
            return;
        }

        if (_session?.IsUsable == true)
        {
            SignOutOfSponsorPro();
            return;
        }

        var cancellationToken = BeginOperation();
        SetBusy(true);
        StatusText.Text = AppText.T("Opening GitHub Sponsor Pro login...");

        try
        {
            using var service = new WinPerfUpdateService();
            var start = await service.StartLoginAsync(cancellationToken);
            Process.Start(new ProcessStartInfo
            {
                FileName = start.LoginUrl.AbsoluteUri,
                UseShellExecute = true
            });

            StatusText.Text = AppText.T("Waiting for GitHub authorization...");
            var result = await service.PollLoginAsync(start, cancellationToken);
            if (result.Success && result.Session is not null)
            {
                _sessionStore.Save(result.Session);
                RefreshSponsorProStatus();
                ResetUpdateState();
                return;
            }

            StatusText.Text = AppText.T("Sponsor Pro login failed or was not authorized.");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = AppText.T("Sponsor Pro login was cancelled.");
        }
        catch (WinPerfUpdateServiceException ex) when (ex.ErrorCode == "product_not_found")
        {
            SetChip(AccountStatusChip, AccountStatusChipText, ChipState.Missing, AppText.T("Error"));
            StatusText.Text = AppText.T("WinPerf is not enabled on the Sponsor Pro update server yet.");
        }
        catch (WinPerfUpdateServiceException ex)
        {
            SetChip(AccountStatusChip, AccountStatusChipText, ChipState.Missing, AppText.T("Error"));
            StatusText.Text = AppText.F("Sponsor Pro login failed: {0}", ex.ErrorCode ?? ex.Message);
        }
        catch (Exception)
        {
            StatusText.Text = AppText.T("Sponsor Pro login failed. Check your connection and try again.");
        }
        finally
        {
            SetBusy(false);
            ApplyEditionBoundary();
        }
    }

    private void SignOutOfSponsorPro()
    {
        _operationCancellation?.Cancel();
        var cleared = _sessionStore.Clear();
        RefreshSponsorProStatus();
        ResetUpdateState();
        StatusText.Text = cleared
            ? AppText.T("Signed out locally. Your GitHub browser session is unchanged.")
            : AppText.T("Could not remove the local Sponsor Pro session.");
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshSponsorProStatus();
        if (!WinPerfProductEdition.SupportsSponsorProUpdates)
        {
            StatusText.Text = AppText.T("This edition does not use the private Sponsor Pro update channel.");
            return;
        }

        if (_session?.IsUsable != true)
        {
            StatusText.Text = AppText.T("Sign in with GitHub Sponsor Pro before checking for updates.");
            SetChip(UpdateStateChip, UpdateStateText, ChipState.Missing, AppText.T("Login"));
            return;
        }

        var cancellationToken = BeginOperation();
        SetBusy(true);
        StatusText.Text = AppText.T("Checking Sponsor Pro update channel...");
        SetChip(UpdateStateChip, UpdateStateText, ChipState.Idle, AppText.T("Checking"));

        try
        {
            using var service = new WinPerfUpdateService();
            var result = await service.CheckAsync(ParseVersion(_versionText), cancellationToken);

            if (result.Status == UpdateCheckStatus.UpToDate)
            {
                _availableManifest = null;
                LatestVersionText.Text = result.LatestVersion?.ToString() ?? AppText.T("Current version is up to date");
                InstallUpdateButton.IsEnabled = false;
                SetChip(UpdateStateChip, UpdateStateText, ChipState.Ready, AppText.T("Current"));
                StatusText.Text = AppText.T("WinPerf is up to date on the Sponsor Pro channel.");
                return;
            }

            if (result.Status == UpdateCheckStatus.UpdateAvailable &&
                result.Manifest is not null &&
                result.LatestVersion is not null)
            {
                _availableManifest = result.Manifest;
                LatestVersionText.Text = $"v{result.LatestVersion} · {result.Manifest.AssetName}";
                InstallUpdateButton.IsEnabled = true;
                SetChip(UpdateStateChip, UpdateStateText, ChipState.Ready, AppText.T("Available"));
                StatusText.Text = AppText.T("Update found. You can install it now.");
                return;
            }

            _availableManifest = null;
            LatestVersionText.Text = result.ErrorCode ?? AppText.T("Update check failed");
            InstallUpdateButton.IsEnabled = false;
            SetChip(UpdateStateChip, UpdateStateText, ChipState.Missing, AppText.T("Error"));
            StatusText.Text = AppText.T("Update check failed or server response was invalid.");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = AppText.T("Update check was cancelled.");
        }
        catch (Exception)
        {
            _availableManifest = null;
            LatestVersionText.Text = AppText.T("Update check failed");
            InstallUpdateButton.IsEnabled = false;
            SetChip(UpdateStateChip, UpdateStateText, ChipState.Missing, AppText.T("Error"));
            StatusText.Text = AppText.T("Update check failed. Check your connection and try again.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshSponsorProStatus();
        if (!WinPerfProductEdition.SupportsSponsorProUpdates)
        {
            StatusText.Text = AppText.T("This edition does not use the private Sponsor Pro update channel.");
            return;
        }

        if (_session?.IsUsable != true)
        {
            StatusText.Text = AppText.T("Sign in with GitHub Sponsor Pro before installing updates.");
            SetChip(UpdateStateChip, UpdateStateText, ChipState.Missing, AppText.T("Login"));
            return;
        }

        if (_availableManifest is null)
        {
            StatusText.Text = AppText.T("Check for updates before installing.");
            return;
        }

        if (!ConfirmDialogWindow.Confirm(
                this,
                AppText.T("Install WinPerf update?"),
                AppText.T("WinPerf will close, replace only WinPerf.exe, and restart. Portable data, tools, language packs, profiles and history stay in place."),
                AppText.T("Install update")))
        {
            return;
        }

        var cancellationToken = BeginOperation();
        SetBusy(true);
        StatusText.Text = AppText.T("Preparing Sponsor Pro update download...");

        try
        {
            using var service = new WinPerfUpdateService();
            var ticket = await service.RequestDownloadTicketAsync(_session, _availableManifest, cancellationToken);
            StatusText.Text = AppText.T("Downloading and validating WinPerf update...");
            var staged = await service.DownloadAndStageAsync(
                ticket,
                _availableManifest,
                WinPerfUpdateHelper.UpdatesRoot,
                cancellationToken);

            StatusText.Text = AppText.T("Starting update helper. WinPerf will restart after installation.");
            WinPerfUpdateHelper.Launch(staged, AppContext.BaseDirectory);
            Application.Current.Shutdown();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = AppText.T("Update installation was cancelled.");
        }
        catch (HttpRequestException)
        {
            _sessionStore.Clear();
            RefreshSponsorProStatus();
            StatusText.Text = AppText.T("Sponsor Pro session expired. Sign in again and retry the update.");
        }
        catch (Exception)
        {
            StatusText.Text = AppText.T("Update installation failed. WinPerf.exe was not changed.");
        }
        finally
        {
            SetBusy(false);
            ApplyEditionBoundary();
        }
    }

    private CancellationToken BeginOperation()
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        return _operationCancellation.Token;
    }

    private void SetBusy(bool busy)
    {
        BusyProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        SponsorProAccountButton.IsEnabled = !busy && WinPerfProductEdition.SupportsSponsorProUpdates;
        CheckUpdatesButton.IsEnabled = !busy && WinPerfProductEdition.SupportsSponsorProUpdates;
        InstallUpdateButton.IsEnabled = !busy
            && WinPerfProductEdition.SupportsSponsorProUpdates
            && _availableManifest is not null
            && _session?.IsUsable == true;
    }

    private void SetChip(Border chip, TextBlock text, ChipState state, string label)
    {
        text.Text = label;

        var (background, border, foreground) = state switch
        {
            ChipState.Ready => (
                GetThemeBrush("SuccessChipBackground", Brushes.DarkGreen),
                GetThemeBrush("AccentGreen", Brushes.LightGreen),
                GetThemeBrush("AccentGreen", Brushes.LightGreen)),
            ChipState.Missing => (
                GetThemeBrush("MissingChipBackground", Brushes.DarkRed),
                GetThemeBrush("MissingChipBorder", Brushes.IndianRed),
                GetThemeBrush("MissingChipForeground", Brushes.LightCoral)),
            _ => (
                GetThemeBrush("PanelSoft", Brushes.DarkSlateGray),
                GetThemeBrush("BorderSoft", Brushes.SlateGray),
                GetThemeBrush("TextMuted", Brushes.LightSlateGray))
        };

        chip.Background = background;
        chip.BorderBrush = border;
        text.Foreground = foreground;
    }

    private Brush GetThemeBrush(string resourceKey, Brush fallback)
    {
        return FindResource(resourceKey) as Brush ?? fallback;
    }

    private static Version ParseVersion(string versionText)
    {
        var match = Regex.Match(versionText, @"\d+(?:\.\d+){1,3}", RegexOptions.CultureInvariant);
        return match.Success && Version.TryParse(match.Value, out var version)
            ? version
            : new Version(0, 0, 0);
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

    private enum ChipState
    {
        Idle,
        Ready,
        Missing
    }
}
