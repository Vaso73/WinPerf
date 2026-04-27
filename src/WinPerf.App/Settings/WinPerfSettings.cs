namespace WinPerf.App.Settings;

public sealed class WinPerfSettings
{
    public string? IperfExecutablePath { get; set; }
    public string? LastServer { get; set; }
    public List<string> RecentServers { get; set; } = [];
}
