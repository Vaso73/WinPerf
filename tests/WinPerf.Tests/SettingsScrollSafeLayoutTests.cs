namespace WinPerf.Tests;

public sealed class SettingsScrollSafeLayoutTests
{
    private static readonly string SettingsWindowXamlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App", "SettingsWindow.xaml"));

    [Fact]
    public void SettingsWindow_ContentArea_IsScrollable()
    {
        var xaml = File.ReadAllText(SettingsWindowXamlPath);

        Assert.Contains("<ScrollViewer Grid.Row=\"1\"", xaml);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml);
        Assert.Contains("HorizontalScrollBarVisibility=\"Disabled\"", xaml);
        Assert.DoesNotContain("<Border Grid.Row=\"1\"", xaml);
    }

    [Fact]
    public void SettingsWindow_SaveCancelFooter_RemainsOutsideScrollableContent()
    {
        var xaml = File.ReadAllText(SettingsWindowXamlPath);

        var scrollViewerStart = xaml.IndexOf("<ScrollViewer Grid.Row=\"1\"", StringComparison.Ordinal);
        var scrollViewerEnd = xaml.IndexOf("</ScrollViewer>", StringComparison.Ordinal);
        var footerStart = xaml.IndexOf("<Grid Grid.Row=\"2\" Margin=\"0,8,0,0\">", StringComparison.Ordinal);

        Assert.True(scrollViewerStart >= 0);
        Assert.True(scrollViewerEnd > scrollViewerStart);
        Assert.True(footerStart > scrollViewerEnd);
    }
}
