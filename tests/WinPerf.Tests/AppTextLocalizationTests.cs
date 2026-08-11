namespace WinPerf.Tests;

public sealed class AppTextLocalizationTests
{
    [Fact]
    public void AppText_TakesLogicalChildrenSnapshotBeforeTranslatingRuns()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "WinPerf.App",
                "AppText.cs"));

        Assert.Contains("var logicalChildren = LogicalTreeHelper.GetChildren(root)", code);
        Assert.Contains(".OfType<DependencyObject>()", code);
        Assert.Contains(".ToList();", code);
        Assert.Contains("foreach (var logicalChild in logicalChildren)", code);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WinPerf.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
