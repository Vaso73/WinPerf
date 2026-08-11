namespace WinPerf.Core.Product;

public static class WinPerfProductEdition
{
#if WINPERF_FREE
    public static bool IsPublicFree { get; } = true;
    public static bool IsSponsorPro { get; } = false;
    public static bool SupportsSponsorProUpdates { get; } = false;
    public static bool SupportsIperf2 { get; } = false;
    public static bool SupportsUdp { get; } = false;
    public static bool SupportsBidirectional { get; } = false;
    public static bool SupportsServerMode { get; } = false;
    public static bool SupportsAdvancedCommands { get; } = false;
    public static bool SupportsCustomCommands { get; } = false;
    public static bool SupportsHistoryExportImport { get; } = false;
    public static int MaxStreams { get; } = 1;
    public static int MaxDurationSeconds { get; } = 10;
    public static int MaxSavedHistoryResults { get; } = 5;
    public static string EditionName { get; } = "WinPerf Free";
    public static string DataDirectoryName { get; } = "free-data";
#else
    public static bool IsPublicFree { get; } = false;
    public static bool IsSponsorPro { get; } = true;
    public static bool SupportsSponsorProUpdates { get; } = true;
    public static bool SupportsIperf2 { get; } = true;
    public static bool SupportsUdp { get; } = true;
    public static bool SupportsBidirectional { get; } = true;
    public static bool SupportsServerMode { get; } = true;
    public static bool SupportsAdvancedCommands { get; } = true;
    public static bool SupportsCustomCommands { get; } = true;
    public static bool SupportsHistoryExportImport { get; } = true;
    public static int MaxStreams { get; } = int.MaxValue;
    public static int MaxDurationSeconds { get; } = int.MaxValue;
    public static int MaxSavedHistoryResults { get; } = int.MaxValue;
    public static string EditionName { get; } = "WinPerf Sponsor Pro";
    public static string DataDirectoryName { get; } = "data";
#endif
}
