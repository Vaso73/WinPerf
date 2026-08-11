using WinPerf.Core.Updates;

namespace WinPerf.Tests.Updates;

public sealed class WinPerfUpdateHelperCommandTests
{
    [Fact]
    public void BuildAndParse_RoundTripsPathsContainingSpaces()
    {
        var expected = new WinPerfUpdateHelperRequest(
            Path.Combine(Path.GetTempPath(), "WinPerf staging", "one"),
            Path.Combine(Path.GetTempPath(), "WinPerf install"),
            1234);

        var arguments = WinPerfUpdateHelperCommand.Build(expected);

        Assert.True(WinPerfUpdateHelperCommand.TryParse(arguments, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("--other-switch")]
    [InlineData("--winperf-apply-update")]
    public void TryParse_RejectsIncompleteOrUnrelatedArguments(string firstArgument)
    {
        Assert.False(WinPerfUpdateHelperCommand.TryParse([firstArgument], out _));
    }
}
