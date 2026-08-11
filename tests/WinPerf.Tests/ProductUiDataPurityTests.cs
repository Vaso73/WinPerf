namespace WinPerf.Tests;

public sealed class ProductUiDataPurityTests
{
    [Fact]
    public void AppUi_DoesNotEmbedLocalLabAddressesOrReadyMadeLocalProfiles()
    {
        var appDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WinPerf.App"));

        var productFiles = Directory
            .EnumerateFiles(appDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var file in productFiles)
        {
            var text = File.ReadAllText(file);

            Assert.DoesNotContain("10.100.", text);
            Assert.DoesNotContain("TCP upload 10s x10", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UDP upload 10s x10", text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
