namespace WinPerf.Tests;

public sealed class CommandDisplayOrderTests
{
    [Fact]
    public void AdvancedBuilder_UsesCanonicalCommandOrderAndKeepsJsonStreamLast()
    {
        var code = ReadFile("src", "WinPerf.App", "AdvancedCommandWindow.xaml.cs");
        var method = ExtractBuildArgumentsMethod(code);

        AssertOrder(method, "AddPair(args, \"-p\", PortBox.Text.Trim());", "args.Add(\"-4\");");
        AssertOrder(method, "args.Add(\"-4\");", "args.Add(\"-R\");");
        AssertOrder(method, "args.Add(\"-R\");", "AddPair(args, \"-t\", DurationBox.Text.Trim());");
        AssertOrder(method, "AddPair(args, \"-t\", DurationBox.Text.Trim());", "AddPair(args, \"-P\", StreamsBox.Text.Trim());");
        AssertOrder(method, "AddPair(args, \"-P\", StreamsBox.Text.Trim());", "AddPair(args, \"-O\", OmitSecondsBox.Text.Trim());");
        AssertOrder(method, "args.AddRange(SplitExtraArguments(ExtraArgumentsBox.Text));", "args.Add(\"--json-stream\");");

        Assert.Equal(method.LastIndexOf("args.Add(\"--json-stream\");", StringComparison.Ordinal),
            method.IndexOf("args.Add(\"--json-stream\");", StringComparison.Ordinal));
    }

    [Fact]
    public void CustomCommandWindow_ExamplesUseCanonicalCommandOrder()
    {
        var xaml = ReadFile("src", "WinPerf.App", "CustomCommandWindow.xaml");

        Assert.DoesNotContain("Text=\"-c ", xaml);
        Assert.Contains("TCP upload:     -c &lt;server&gt; -p 5201 -4 -t 10 -P 10 --json-stream", xaml);
        Assert.Contains("TCP download:   -c &lt;server&gt; -p 5201 -4 -R -t 10 -P 10 --json-stream", xaml);
        Assert.Contains("UDP upload:     -c &lt;server&gt; -p 5201 -4 -u -b 0 -t 10 -P 10 --json-stream", xaml);

        Assert.DoesNotContain("-t 10 -P 10 --json-stream -4", xaml);
        Assert.DoesNotContain("-R -P 10 -t 10 -4 --json-stream", xaml);
    }

    private static void AssertOrder(string text, string first, string second)
    {
        var firstIndex = text.IndexOf(first, StringComparison.Ordinal);
        var secondIndex = text.IndexOf(second, StringComparison.Ordinal);

        Assert.True(firstIndex >= 0, $"Missing expected text: {first}");
        Assert.True(secondIndex >= 0, $"Missing expected text: {second}");
        Assert.True(firstIndex < secondIndex, $"Expected '{first}' before '{second}'.");
    }

    private static string ExtractBuildArgumentsMethod(string code)
    {
        var start = code.IndexOf("    private List<string> BuildArguments()", StringComparison.Ordinal);
        var end = code.IndexOf("    private string GetProfileName()", start, StringComparison.Ordinal);

        Assert.True(start >= 0, "BuildArguments method was not found.");
        Assert.True(end > start, "BuildArguments method end was not found.");

        return code[start..end];
    }

    private static string ReadFile(params string[] pathParts)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(pathParts).ToArray()));
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
