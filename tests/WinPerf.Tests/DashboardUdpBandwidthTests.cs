using System.Text.RegularExpressions;
using WinPerf.Core.Iperf;

namespace WinPerf.Tests;

public sealed class DashboardUdpBandwidthTests
{
    private static string RepositoryRoot => FindRepositoryRoot();

    [Fact]
    public void Dashboard_ExposesUdpBandwidthInputWithTenMegabitDefault()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src",
                "WinPerf.App",
                "MainWindow.xaml"));

        var panel = ExtractControl(
            xaml,
            "StackPanel",
            "UdpBandwidthPanel");

        var textBox = ExtractControl(
            xaml,
            "TextBox",
            "UdpBandwidthBox");

        Assert.Contains("Visibility=\"Collapsed\"", panel);
        Assert.Contains("Text=\"10M\"", textBox);
        Assert.Contains(
            "TextChanged=\"DashboardInputChanged\"",
            textBox);
        Assert.Contains(
            "KeyUp=\"DashboardInputChanged\"",
            textBox);
        Assert.Contains(
            "LostFocus=\"DashboardInputChanged\"",
            textBox);
    }

    [Fact]
    public void Dashboard_MapsUdpBandwidthIntoRuntimeOptions()
    {
        var code = ReadMainWindowCode();

        Assert.Contains(
            "UdpBandwidth = NormalizeUdpBandwidth(UdpBandwidthBox.Text)",
            code);
    }

    [Fact]
    public void Dashboard_LoadsUdpBandwidthFromSavedProfile()
    {
        var code = ReadMainWindowCode();

        Assert.Contains(
            "UdpBandwidthBox.Text = NormalizeUdpBandwidth(profile.UdpBandwidth);",
            code);
    }

    [Fact]
    public void Dashboard_ShowsBandwidthOnlyForUdpModes()
    {
        var code = ReadMainWindowCode();

        Assert.Contains(
            "private void UpdateUdpBandwidthVisibility()",
            code);
        Assert.Contains("IperfMode.UdpUpload or", code);
        Assert.Contains("IperfMode.UdpDownload;", code);
        Assert.Contains(
            "UdpBandwidthPanel.Visibility = isUdp",
            code);
    }

    [Fact]
    public void Dashboard_NormalizesEmptyOrZeroBandwidthToTenMegabits()
    {
        var code = ReadMainWindowCode();

        Assert.Contains(
            "private static string NormalizeUdpBandwidth",
            code);
        Assert.Contains("value?.Trim()", code);
        Assert.Contains("\"0\"", code);
        Assert.Contains("? \"10M\"", code);
        Assert.Contains(
            "StringComparison.OrdinalIgnoreCase",
            code);
    }

    [Fact]
    public void IperfOptions_DefaultUdpBandwidthIsTenMegabits()
    {
        var options = new IperfTestOptions
        {
            Server = "10.100.100.221",
            Mode = IperfMode.UdpUpload
        };

        Assert.Equal("10M", options.UdpBandwidth);
    }

    [Fact]
    public void DefaultIperf2UdpUploadCommand_ContainsTenMegabitBandwidth()
    {
        var command = IperfCommandBuilder.BuildClientCommand(
            "iperf.exe",
            new IperfTestOptions
            {
                Engine = IperfEngine.Iperf2,
                Server = "10.100.100.221",
                Mode = IperfMode.UdpUpload
            });

        var arguments = command.Arguments.ToArray();
        var bandwidthIndex = Array.IndexOf(arguments, "-b");

        Assert.True(bandwidthIndex >= 0);
        Assert.True(bandwidthIndex + 1 < arguments.Length);
        Assert.Equal("10M", arguments[bandwidthIndex + 1]);
    }

    private static string ReadMainWindowCode()
    {
        return File.ReadAllText(
            Path.Combine(
                RepositoryRoot,
                "src",
                "WinPerf.App",
                "MainWindow.xaml.cs"));
    }

    private static string ExtractControl(
        string xaml,
        string controlName,
        string xName)
    {
        var pattern =
            $@"<{controlName}\s+x:Name=""{Regex.Escape(xName)}""[\s\S]*?(/>|>)";

        var match = Regex.Match(xaml, pattern);

        Assert.True(
            match.Success,
            $"{controlName} {xName} was not found.");

        return match.Value;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(
                Path.Combine(directory.FullName, "WinPerf.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository root.");
    }
}
