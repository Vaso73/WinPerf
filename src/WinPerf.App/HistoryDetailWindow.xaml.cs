using System.Windows;
using WinPerf.App.Settings;

namespace WinPerf.App;

public partial class HistoryDetailWindow : Window
{
    private readonly string _commandPreview;

    public HistoryDetailWindow(MainWindow.HistoryListItem item)
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "HistoryDetailWindow");

        _commandPreview = item.CommandPreview;
        TitleText.Text = item.Title;
        MetaText.Text = $"{item.FinishedLocalText} · {item.StatusText}";
        DetailsText.Text = item.Details;
        SummaryBox.Text = item.Summary;
        CommandBox.Text = item.CommandPreview;
    }

    private void CopyCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_commandPreview) &&
            !string.Equals(_commandPreview, "Command unavailable.", StringComparison.OrdinalIgnoreCase))
        {
            Clipboard.SetText(_commandPreview);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
