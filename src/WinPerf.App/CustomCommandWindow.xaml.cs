using System.Windows;
using WinPerf.App.Settings;

namespace WinPerf.App;

public partial class CustomCommandWindow : Window
{
    public CustomCommandWindow(string initialCommand)
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "CustomCommandWindow");

        if (!string.IsNullOrWhiteSpace(initialCommand))
        {
            CommandBox.Text = initialCommand;
        }

        CommandBox.Focus();
        CommandBox.SelectAll();
    }

    public string CommandText => CommandBox.Text.Trim();

    private void UseButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CommandText))
        {
            MessageBox.Show(
                this,
                "Enter an iperf3 command first.",
                "WinPerf",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
