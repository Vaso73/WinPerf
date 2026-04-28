using System.Text.RegularExpressions;

namespace WinPerf.Tests;

public sealed class DashboardOmitSecondsTests
{
    private static string RepositoryRoot => FindRepositoryRoot();

    [Fact]
    public void Dashboard_ExposesOmitSecondsInputWithWarmupTooltip()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "WinPerf.App", "MainWindow.xaml"));

        var omitBox = ExtractControl(xaml, "TextBox", "OmitSecondsBox");

        Assert.Contains("Text=\"0\"", omitBox);
        Assert.Contains("TextChanged=\"DashboardInputChanged\"", omitBox);
        Assert.Contains("KeyUp=\"DashboardInputChanged\"", omitBox);
        Assert.Contains("LostFocus=\"DashboardInputChanged\"", omitBox);
        Assert.Contains("Warm-up seconds to ignore", omitBox);
    }

    [Fact]
    public void Dashboard_MapsOmitSecondsIntoIperfOptions()
    {
        var code = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "WinPerf.App", "MainWindow.xaml.cs"));

        Assert.Contains("OmitSeconds = ParseNonNegativeInt(OmitSecondsBox, \"Omit\")", code);
        Assert.Contains("OmitSeconds = TryGetNonNegativeIntArgumentValue(args, \"-O\")", code);
    }

    private static string ExtractControl(string xaml, string controlName, string xName)
    {
        var pattern = $@"<{controlName}\s+x:Name=""{Regex.Escape(xName)}""[\s\S]*?(/>|>)";
        var match = Regex.Match(xaml, pattern);

        Assert.True(match.Success, $"{controlName} {xName} was not found.");

        return match.Value;
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
