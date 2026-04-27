namespace WinPerf.Core.Iperf;

public sealed class IperfExecutableResolver
{
    private readonly Func<string, bool> _fileExists;

    public IperfExecutableResolver(Func<string, bool>? fileExists = null)
    {
        _fileExists = fileExists ?? File.Exists;
    }

    public IperfExecutableResolution Resolve(string appDirectory, IperfEngineSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDirectory);
        ArgumentNullException.ThrowIfNull(settings);

        var configuredPath = settings.ExecutablePath?.Trim();

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (_fileExists(configuredPath))
            {
                return new IperfExecutableResolution(
                    true,
                    configuredPath,
                    "Configured",
                    "Using configured iperf3 executable.");
            }

            return new IperfExecutableResolution(
                false,
                configuredPath,
                "ConfiguredMissing",
                "Configured iperf3 executable was not found.");
        }

        var bundledPath = Path.Combine(appDirectory, "tools", "iperf3", "iperf3.exe");

        if (_fileExists(bundledPath))
        {
            return new IperfExecutableResolution(
                true,
                bundledPath,
                "Bundled",
                "Using bundled iperf3 executable.");
        }

        return new IperfExecutableResolution(
            false,
            null,
            "NotConfigured",
            "iperf3.exe is not configured. Set the executable path in Settings or install it through WinPerf later.");
    }
}
