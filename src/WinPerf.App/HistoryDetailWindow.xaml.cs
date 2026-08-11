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
        AppText.ApplyTo(this);

        _commandPreview = item.CommandPreview;
        TitleText.Text = item.Title;
        MetaText.Text = $"{item.FinishedLocalText} · {item.StatusText}";
        DetailsText.Text = item.Details;
        SummaryBox.Text = LocalizeStoredSummaryText(item.Summary);
        CommandBox.Text = item.CommandPreview;
    }

    private static string LocalizeStoredSummaryText(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return summary;
        }

        var lines = summary
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(LocalizeStoredSummaryLine);

        return string.Join(Environment.NewLine, lines);
    }

    private static string LocalizeStoredSummaryLine(string line)
    {
        var trimmed = line.TrimStart();
        var leadingWhitespace = line[..(line.Length - trimmed.Length)];

        return TryReplacePrefix(trimmed, "Last ", AppText.T("Last") + " ", out var localizedLine) ||
               TryReplacePrefix(trimmed, "Upload last ", AppText.T("Upload last") + " ", out localizedLine) ||
               TryReplacePrefix(trimmed, "Download last ", AppText.T("Download last") + " ", out localizedLine)
            ? leadingWhitespace + localizedLine
            : line;
    }

    private static bool TryReplacePrefix(string value, string prefix, string replacement, out string updated)
    {
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            value.Contains(" Mbps", StringComparison.OrdinalIgnoreCase))
        {
            updated = replacement + value[prefix.Length..];
            return true;
        }

        updated = value;
        return false;
    }

    private void CopyCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_commandPreview) &&
            !string.Equals(_commandPreview, AppText.T("Command unavailable."), StringComparison.OrdinalIgnoreCase))
        {
            Clipboard.SetText(_commandPreview);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
