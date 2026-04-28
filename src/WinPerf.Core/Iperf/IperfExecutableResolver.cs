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

        var configuredPath = GetConfiguredExecutablePath(settings)?.Trim();
        var engineName = GetEngineDisplayName(settings.Engine);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (_fileExists(configuredPath))
            {
                return new IperfExecutableResolution(
                    true,
                    configuredPath,
                    "Configured",
                    $"Using configured {engineName} executable.");
            }

            return new IperfExecutableResolution(
                false,
                configuredPath,
                "ConfiguredMissing",
                $"Configured {engineName} executable was not found.");
        }

        foreach (var bundledPath in GetBundledExecutableCandidates(appDirectory, settings.Engine))
        {
            if (_fileExists(bundledPath))
            {
                return new IperfExecutableResolution(
                    true,
                    bundledPath,
                    "Bundled",
                    $"Using bundled {engineName} executable.");
            }
        }

        return new IperfExecutableResolution(
            false,
            null,
            "NotConfigured",
            $"{GetDefaultExecutableName(settings.Engine)} is not configured. Set the executable path in Settings or install it through WinPerf later.");
    }

    private static string? GetConfiguredExecutablePath(IperfEngineSettings settings)
    {
        return settings.Engine == IperfEngine.Iperf2
            ? settings.Iperf2ExecutablePath
            : settings.Iperf3ExecutablePath ?? settings.ExecutablePath;
    }

    private static IEnumerable<string> GetBundledExecutableCandidates(string appDirectory, IperfEngine engine)
    {
        return engine switch
        {
            IperfEngine.Iperf3 =>
            [
                Path.Combine(appDirectory, "tools", "iperf3", "iperf3.exe")
            ],

            IperfEngine.Iperf2 =>
            [
                Path.Combine(appDirectory, "tools", "iperf2", "iperf.exe"),
                Path.Combine(appDirectory, "tools", "iperf2", "iperf2.exe")
            ],

            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported iperf engine.")
        };
    }

    private static string GetEngineDisplayName(IperfEngine engine)
    {
        return engine switch
        {
            IperfEngine.Iperf3 => "iperf3",
            IperfEngine.Iperf2 => "iperf2",
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported iperf engine.")
        };
    }

    private static string GetDefaultExecutableName(IperfEngine engine)
    {
        return engine switch
        {
            IperfEngine.Iperf3 => "iperf3.exe",
            IperfEngine.Iperf2 => "iperf.exe",
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported iperf engine.")
        };
    }
}
