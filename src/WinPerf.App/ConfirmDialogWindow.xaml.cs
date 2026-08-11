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
        Title = title;
        TitleText.Text = title;
        HeadingText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;
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
