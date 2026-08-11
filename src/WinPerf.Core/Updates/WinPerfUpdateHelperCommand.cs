using System.Globalization;

namespace WinPerf.Core.Updates;

public sealed record WinPerfUpdateHelperRequest(
    string StagingDirectory,
    string InstallDirectory,
    int ParentProcessId);

public static class WinPerfUpdateHelperCommand
{
    public const string ApplySwitch = "--winperf-apply-update";

    public static IReadOnlyList<string> Build(WinPerfUpdateHelperRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ParentProcessId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        return
        [
            ApplySwitch,
            "--staging",
            Path.GetFullPath(request.StagingDirectory),
            "--install",
            Path.GetFullPath(request.InstallDirectory),
            "--parent-pid",
            request.ParentProcessId.ToString(CultureInfo.InvariantCulture)
        ];
    }

    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out WinPerfUpdateHelperRequest? request)
    {
        request = null;

        if (arguments.Count != 7
            || !string.Equals(arguments[0], ApplySwitch, StringComparison.Ordinal)
            || !string.Equals(arguments[1], "--staging", StringComparison.Ordinal)
            || !string.Equals(arguments[3], "--install", StringComparison.Ordinal)
            || !string.Equals(arguments[5], "--parent-pid", StringComparison.Ordinal)
            || !int.TryParse(
                arguments[6],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parentPid)
            || parentPid <= 0)
        {
            return false;
        }

        try
        {
            request = new(
                Path.GetFullPath(arguments[2]),
                Path.GetFullPath(arguments[4]),
                parentPid);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
