using System.Windows;
using WinPerf.App.Settings;

namespace WinPerf.App;

public partial class AboutWindow : Window
{
    public AboutWindow(string versionText)
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "AboutWindow");
        VersionText.Text = versionText;
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
