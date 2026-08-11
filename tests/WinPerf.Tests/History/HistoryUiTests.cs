namespace WinPerf.Tests.History;

public sealed class HistoryUiTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    private static readonly string MainWindowXaml = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml"));
    private static readonly string MainWindowCode = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml.cs"));

    [Fact]
    public void MainWindow_HasClickableHistoryPage()
    {
        Assert.Contains("x:Name=\"HistoryNavButton\"", MainWindowXaml);
        Assert.Contains("Click=\"HistoryNavButton_Click\"", MainWindowXaml);
        Assert.DoesNotContain("History page will be added later.", MainWindowXaml);
        Assert.Contains("x:Name=\"HistoryContentPanel\"", MainWindowXaml);
        Assert.Contains("x:Name=\"HistoryItemsControl\"", MainWindowXaml);
        Assert.Contains("x:Name=\"HistoryEmptyText\"", MainWindowXaml);
    }

    [Fact]
    public void MainWindow_SavesCompletedTestResultsToPortableHistory()
    {
        Assert.Contains("JsonIperfHistoryStore.GetDefaultFilePath()", MainWindowCode);
        Assert.Contains("await SaveHistoryEntryAsync(options, result, outcome, summaryExitCode, commandDisplayText);", MainWindowCode);
        Assert.Contains("HistoryContentPanel.Visibility = Visibility.Visible;", MainWindowCode);
        Assert.Contains("ActivePage.History", MainWindowCode);
    }

    [Fact]
    public void HistoryCards_DoNotRepeatTitleInSummary()
    {
        Assert.Contains("BuildHistoryDisplaySummary(entry.Summary, title)", MainWindowCode);
        Assert.Contains("string.Equals(lines[0].Trim(), title, StringComparison.OrdinalIgnoreCase)", MainWindowCode);
    }
}
