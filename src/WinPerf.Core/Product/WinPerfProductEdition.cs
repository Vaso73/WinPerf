namespace WinPerf.Core.Product;

public static class WinPerfProductEdition
{
#if WINPERF_FREE
    public static bool IsPublicFree { get; } = true;
    public static bool IsSponsorPro { get; } = false;
    public static bool SupportsSponsorProUpdates { get; } = false;
    public static string EditionName { get; } = "WinPerf Free";
    public static string DataDirectoryName { get; } = "free-data";
#else
    public static bool IsPublicFree { get; } = false;
    public static bool IsSponsorPro { get; } = true;
    public static bool SupportsSponsorProUpdates { get; } = true;
    public static string EditionName { get; } = "WinPerf Sponsor Pro";
    public static string DataDirectoryName { get; } = "data";
#endif
}
