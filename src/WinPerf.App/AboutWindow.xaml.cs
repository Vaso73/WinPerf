using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinPerf.App.Settings;
using WinPerf.App.Updates;
using WinPerf.Core.Product;
using WinPerf.Core.Updates;

namespace WinPerf.App;

public partial class AboutWindow : Window
{
    private readonly SponsorProSessionStore _sessionStore = new();
    private readonly string _versionText;
    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _avatarCancellation;
    private SponsorProSession? _session;
    private string? _avatarRequestedLogin;
    private Version? _availableVersion;
    private WinPerfUpdateManifest? _availableManifest;

    public AboutWindow(string versionText)
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "AboutWindow");
        AppText.ApplyTo(this);
        _versionText = versionText;
        VersionText.Text = versionText;
        EditionText.Text = WinPerfProductEdition.EditionName;
        CheckForUpdatesButton.IsEnabled = WinPerfProductEdition.SupportsSponsorProUpdates;
        InstallUpdateButton.IsEnabled = false;
        RefreshSponsorProStatus();
        ResetUpdateState();

        Closed += (_, _) =>
        {
            _operationCancellation?.Cancel();
            _operationCancellation?.Dispose();
            ResetAccountAvatar();
        };
    }

    private void RefreshSponsorProStatus()
    {
        _session = _sessionStore.Load();
        if (!WinPerfProductEdition.SupportsSponsorProUpdates)
        {
            ResetAccountAvatar();
            AccountTitleText.Text = WinPerfProductEdition.EditionName;
            AccountStatusText.Text = AppText.T("Sponsor Pro updates are available only in WinPerf Sponsor Pro.");
            SponsorProAccountButton.Content = AppText.T("Sign in with GitHub");
            SponsorProAccountButton.IsEnabled = false;
            StatusText.Text = AppText.T("This edition does not use the private Sponsor Pro update channel.");
            return;
        }

        if (_session?.IsUsable == true)
        {
            var login = string.IsNullOrWhiteSpace(_session.GithubLogin)
                ? AppText.T("GitHub account")
                : _session.GithubLogin;
            var tier = string.IsNullOrWhiteSpace(_session.SponsorTier)
                ? "Sponsor Pro"
                : _session.SponsorTier;

            AccountTitleText.Text = $"GitHub: {login}";
            AccountStatusText.Text = AppText.F("{0} active · updates channel ready", tier);
            SponsorProAccountButton.Content = AppText.T("Sign out");
            StatusText.Text = AppText.F(
                "Connected to {0} / {1}.",
                WinPerfUpdateService.ProductId,
                WinPerfUpdateService.Channel);
            if (!string.IsNullOrWhiteSpace(_session.GithubLogin))
            {
                _ = LoadAccountAvatarAsync(_session.GithubLogin);
            }
            else
            {
                ResetAccountAvatar();
            }
            return;
        }

        ResetAccountAvatar();
        AccountTitleText.Text = AppText.T("Not signed in");
        AccountStatusText.Text = AppText.T("Sign in with GitHub to use Sponsor Pro updates.");
        SponsorProAccountButton.Content = AppText.T("Sign in with GitHub");
        StatusText.Text = AppText.T("Single portable WinPerf.exe. Updates use the private Sponsor Pro channel.");
    }

    private void ResetUpdateState()
    {
        _availableVersion = null;
        _availableManifest = null;
        InstallUpdateButton.IsEnabled = false;
    }

    private async Task LoadAccountAvatarAsync(string githubLogin)
    {
        if (string.Equals(_avatarRequestedLogin, githubLogin, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _avatarCancellation?.Cancel();
        _avatarCancellation?.Dispose();
        _avatarCancellation = new CancellationTokenSource();
        var cancellationToken = _avatarCancellation.Token;
        _avatarRequestedLogin = githubLogin;
        AccountAvatar.Fill = null;
        AccountAvatar.Visibility = Visibility.Collapsed;
        AccountGitHubIcon.Visibility = Visibility.Visible;

        try
        {
            using var service = new GitHubAvatarService();
            var bytes = await service.DownloadAsync(githubLogin, cancellationToken);
            if (bytes is null)
            {
                return;
            }

            using var stream = new MemoryStream(bytes, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            cancellationToken.ThrowIfCancellationRequested();
            if (_session?.IsUsable != true
                || !string.Equals(_session.GithubLogin, githubLogin, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AccountAvatar.Fill = new ImageBrush(bitmap);
            AccountAvatar.Visibility = Visibility.Visible;
            AccountGitHubIcon.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
    }

    private void ResetAccountAvatar()
    {
        _avatarCancellation?.Cancel();
        _avatarCancellation?.Dispose();
        _avatarCancellation = null;
        _avatarRequestedLogin = null;
        AccountAvatar.Fill = null;
        AccountAvatar.Visibility = Visibility.Collapsed;
        AccountGitHubIcon.Visibility = Visibility.Visible;
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
            StatusText.Text = AppText.T("WinPerf is not enabled on the Sponsor Pro update server yet.");
        }
        catch (WinPerfUpdateServiceException ex)
        {
            StatusText.Text = AppText.F("Sponsor Pro login failed: {0}", ex.ErrorCode ?? ex.Message);
        }
        catch (Exception)
        {
            StatusText.Text = AppText.T("Sponsor Pro login failed. Check your connection and try again.");
        }
        finally
        {
            SetBusy(false);
            RefreshSponsorProStatus();
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

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
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
            return;
        }

        var cancellationToken = BeginOperation();
        SetBusy(true);
        _availableVersion = null;
        _availableManifest = null;
        InstallUpdateButton.IsEnabled = false;
        StatusText.Text = AppText.T("Checking Sponsor Pro update channel...");

        try
        {
            using var service = new WinPerfUpdateService();
            var result = await service.CheckAsync(ParseVersion(_versionText), cancellationToken);

            if (result.Status == UpdateCheckStatus.UpToDate)
            {
                StatusText.Text = AppText.T("WinPerf is up to date on the Sponsor Pro channel.");
                return;
            }

            if (result.Status == UpdateCheckStatus.UpdateAvailable
                && result.LatestVersion is not null
                && result.Manifest is not null)
            {
                _availableVersion = result.LatestVersion;
                _availableManifest = result.Manifest;
                InstallUpdateButton.IsEnabled = true;
                StatusText.Text = AppText.F("Update available: {0}", result.LatestVersion);
                return;
            }

            StatusText.Text = result.ErrorCode ?? AppText.T("Update check failed or server response was invalid.");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = AppText.T("Update check was cancelled.");
        }
        catch (Exception)
        {
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
        if (_session?.IsUsable != true)
        {
            StatusText.Text = AppText.T("Sign in with GitHub Sponsor Pro before installing updates.");
            return;
        }

        if (_availableManifest is null || _availableVersion is null)
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
        CheckForUpdatesButton.IsEnabled = !busy && WinPerfProductEdition.SupportsSponsorProUpdates;
        InstallUpdateButton.IsEnabled = !busy
            && WinPerfProductEdition.SupportsSponsorProUpdates
            && _availableManifest is not null
            && _session?.IsUsable == true;
        CloseButton.IsEnabled = !busy;
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
}
