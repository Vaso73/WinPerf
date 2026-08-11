namespace WinPerf.App.Settings;

public sealed class WinPerfSettings
{
    public string? IperfExecutablePath { get; set; }
    public string? Iperf2ExecutablePath { get; set; }
    public string? SelectedEngine { get; set; }
    public string? LastServer { get; set; }
    public List<string> RecentServers { get; set; } = [];
    public List<string> RecentCustomCommands { get; set; } = [];
    public string? LanguageCode { get; set; }
    public double? DashboardEngineOutputHeight { get; set; }
    public double? DashboardLeftRailWidth { get; set; }
}
