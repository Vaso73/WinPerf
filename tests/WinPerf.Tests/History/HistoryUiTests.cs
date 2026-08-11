namespace WinPerf.Tests.History;

public sealed class HistoryUiTests
{
    private static readonly string AppDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

    private static readonly string MainWindowXaml = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml"));
    private static readonly string MainWindowCode = File.ReadAllText(Path.Combine(AppDirectory, "MainWindow.xaml.cs"));
    private static readonly string HistoryDetailXaml = File.ReadAllText(Path.Combine(AppDirectory, "HistoryDetailWindow.xaml"));
    private static readonly string ConfirmDialogXaml = File.ReadAllText(Path.Combine(AppDirectory, "ConfirmDialogWindow.xaml"));

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
    public void HistoryPage_ExposesResultActionsAndImportExport()
    {
        Assert.Contains("x:Name=\"HistoryExportButton\"", MainWindowXaml);
        Assert.Contains("x:Name=\"HistoryImportButton\"", MainWindowXaml);
        Assert.Contains("x:Name=\"HistoryClearButton\"", MainWindowXaml);
        Assert.Contains("Click=\"HistoryExportButton_Click\"", MainWindowXaml);
        Assert.Contains("Click=\"HistoryImportButton_Click\"", MainWindowXaml);
        Assert.Contains("Click=\"HistoryClearButton_Click\"", MainWindowXaml);
        Assert.Contains("Click=\"HistoryDetailsButton_Click\"", MainWindowXaml);
        Assert.Contains("Click=\"HistoryCopyCommandButton_Click\"", MainWindowXaml);
        Assert.Contains("Click=\"HistoryDeleteButton_Click\"", MainWindowXaml);
    }

    [Fact]
    public void HistoryActions_UseDarkAppWindowsInsteadOfNativeConfirmationPopups()
    {
        Assert.Contains("new HistoryDetailWindow(item)", MainWindowCode);
        Assert.Contains("ConfirmDialogWindow.Confirm(", MainWindowCode);
        Assert.Contains("WindowStyle=\"None\"", HistoryDetailXaml);
        Assert.Contains("Style=\"{StaticResource ShellWindowBorder}\"", HistoryDetailXaml);
        Assert.Contains("WindowStyle=\"None\"", ConfirmDialogXaml);
        Assert.Contains("Style=\"{StaticResource ShellWindowBorder}\"", ConfirmDialogXaml);
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

    [Fact]
    public void HistorySummaries_LabelLastSampleWhenAverageIsShown()
    {
        Assert.Contains("Last {FormatMegabits(current)} · min", MainWindowCode);
        Assert.Contains("AddHistoryMetricLabel", MainWindowCode);
        Assert.Contains("\"Last \" + trimmed", MainWindowCode);
    }

    [Fact]
    public void HistoryCards_CarryEntryIdAndDetailsForActions()
    {
        Assert.Contains("entry.Id,", MainWindowCode);
        Assert.Contains("BuildHistoryDetails(entry)", MainWindowCode);
        Assert.Contains("public sealed record HistoryListItem(", MainWindowCode);
        Assert.Contains("Guid Id,", MainWindowCode);
        Assert.Contains("string Details", MainWindowCode);
    }
}
