using System.Windows;
using WinPerf.App.Settings;
using WinPerf.App.Updates;
using WinPerf.Core.Updates;

namespace WinPerf.App;

public partial class AboutWindow : Window
{
    private readonly SponsorProSessionStore _sessionStore = new();
    private readonly string _versionText;

    public AboutWindow(string versionText)
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "AboutWindow");
        AppText.ApplyTo(this);
        _versionText = versionText;
        VersionText.Text = versionText;
        RefreshSponsorProStatus();
    }

    private void RefreshSponsorProStatus()
    {
        var session = _sessionStore.Load();
        if (session?.IsUsable == true)
        {
            var login = string.IsNullOrWhiteSpace(session.GithubLogin)
                ? "GitHub account"
                : session.GithubLogin;
            var tier = string.IsNullOrWhiteSpace(session.SponsorTier)
                ? "Sponsor Pro"
                : session.SponsorTier;

            AccountTitleText.Text = $"GitHub: {login}";
            AccountStatusText.Text = $"{tier} active · updates channel ready";
            SponsorProAccountButton.Content = AppText.T("Sponsor Pro / Updates");
            StatusText.Text = $"Connected to {WinPerfUpdateService.ProductId} / {WinPerfUpdateService.Channel}.";
            return;
        }

        AccountTitleText.Text = AppText.T("Not signed in");
        AccountStatusText.Text = AppText.T("Sign in with GitHub to use Sponsor Pro updates.");
        SponsorProAccountButton.Content = AppText.T("Sponsor Pro / Updates");
        StatusText.Text = AppText.T("Single portable WinPerf.exe. Updates use the private Sponsor Pro channel.");
    }

    private void SponsorProAccountButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSponsorProUpdatesWindow();
    }

    private void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSponsorProUpdatesWindow();
    }

    private void OpenSponsorProUpdatesWindow()
    {
        var dialog = new SponsorProUpdatesWindow(_versionText)
        {
            Owner = this
        };

        dialog.ShowDialog();
        RefreshSponsorProStatus();
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
