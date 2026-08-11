using System.Windows;
using WinPerf.App.Settings;

namespace WinPerf.App;

public partial class ConfirmDialogWindow : Window
{
    public ConfirmDialogWindow(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel")
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "ConfirmDialogWindow");
        AppText.ApplyTo(this);
        Title = title;
        TitleText.Text = title;
        HeadingText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = AppText.T(confirmText);
        if (string.IsNullOrWhiteSpace(cancelText))
        {
            CancelButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            CancelButton.Content = AppText.T(cancelText);
        }
    }

    public static bool Confirm(
        Window owner,
        string title,
        string message,
        string confirmText = "Confirm")
    {
        var dialog = new ConfirmDialogWindow(title, message, confirmText)
        {
            Owner = owner
        };

        return dialog.ShowDialog() == true;
    }

    public static void ShowMessage(
        Window owner,
        string title,
        string message,
        string buttonText = "OK")
    {
        var dialog = new ConfirmDialogWindow(title, message, buttonText, string.Empty)
        {
            Owner = owner
        };

        dialog.ShowDialog();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
