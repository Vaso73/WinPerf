using System.Diagnostics;
using System.IO;
using System.Text.Json;
using WinPerf.Core.Updates;

namespace WinPerf.App.Updates;

internal static class WinPerfUpdateHelper
{
    private const string HelperDirectoryName = "update-helpers";
    private const string UpdatesDirectoryName = "updates";
    private const string CleanupSwitch = "--winperf-cleanup-update-helper";
    private const string ResultSwitch = "--winperf-update-result";
    private const int ParentExitTimeoutMilliseconds = 120_000;

    public static string? StartupResult { get; private set; }
    public static string? StartupRecoveryDirectory { get; private set; }
    public static string UpdatesRoot => Path.Combine(UpdateTempRoot, UpdatesDirectoryName);

    private static string UpdateTempRoot => Path.Combine(Path.GetTempPath(), "WinPerf");
    private static string HelperRoot => Path.Combine(UpdateTempRoot, HelperDirectoryName);

    public static bool IsApplyRequest(IReadOnlyList<string> arguments) =>
        WinPerfUpdateHelperCommand.TryParse(arguments, out _);

    public static int RunApply(IReadOnlyList<string> arguments)
    {
        if (!WinPerfUpdateHelperCommand.TryParse(arguments, out var request) || request is null)
        {
            return 70;
        }

        var helperDirectory = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? string.Empty;
        if (!IsTrustedDirectChild(helperDirectory, HelperRoot))
        {
            return 71;
        }

        var result = "failed";
        var parentExited = false;

        try
        {
            RequireTrustedStaging(request.StagingDirectory);
            WaitForParent(request.ParentProcessId);
            parentExited = true;

            new WinPerfUpdateInstaller().Apply(
                request.StagingDirectory,
                request.InstallDirectory,
                Path.Combine(helperDirectory, "recovery"));

            result = "success";
            TryDeleteDirectory(request.StagingDirectory);
            return 0;
        }
        catch (Exception error)
        {
            if (error is UpdateInstallationException { RollbackSucceeded: false })
            {
                result = "recovery-required";
            }

            TryWriteResult(helperDirectory, error);
            return 72;
        }
        finally
        {
            if (parentExited)
            {
                RestartInstalledApplication(request.InstallDirectory, helperDirectory, Environment.ProcessId, result);
            }
        }
    }

    public static void Launch(StagedWinPerfUpdate staged, string installDirectory)
    {
        ArgumentNullException.ThrowIfNull(staged);
        RequireTrustedStaging(staged.StagingDirectory);

        var currentExecutable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExecutable) || !File.Exists(currentExecutable))
        {
            throw new InvalidOperationException("current_executable_missing");
        }

        var helperDirectory = Path.Combine(HelperRoot, $"helper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(helperDirectory);
        var helperExecutable = Path.Combine(helperDirectory, "WinPerf.UpdateHelper.exe");

        try
        {
            File.Copy(currentExecutable, helperExecutable, overwrite: false);
            var request = new WinPerfUpdateHelperRequest(staged.StagingDirectory, installDirectory, Environment.ProcessId);
            var startInfo = new ProcessStartInfo
            {
                FileName = helperExecutable,
                WorkingDirectory = helperDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in WinPerfUpdateHelperCommand.Build(request))
            {
                startInfo.ArgumentList.Add(argument);
            }

            if (Process.Start(startInfo) is null)
            {
                throw new InvalidOperationException("update_helper_start_failed");
            }
        }
        catch
        {
            TryDeleteDirectory(helperDirectory);
            throw;
        }
    }

    public static void ScheduleCleanup(IReadOnlyList<string> arguments)
    {
        if (!TryParseCleanup(arguments, out var helperDirectory, out var helperPid, out var result))
        {
            return;
        }

        StartupResult = result;
        if (result == "recovery-required")
        {
            StartupRecoveryDirectory = helperDirectory;
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var helper = Process.GetProcessById(helperPid);
                await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            }
            catch
            {
            }

            TryDeleteDirectory(helperDirectory);
        });
    }

    private static void RestartInstalledApplication(
        string installDirectory,
        string helperDirectory,
        int helperPid,
        string result)
    {
        try
        {
            var target = Path.Combine(Path.GetFullPath(installDirectory), "WinPerf.exe");
            if (!File.Exists(target))
            {
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = target,
                WorkingDirectory = Path.GetDirectoryName(target)!,
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add(CleanupSwitch);
            startInfo.ArgumentList.Add(helperDirectory);
            startInfo.ArgumentList.Add("--helper-pid");
            startInfo.ArgumentList.Add(helperPid.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add(ResultSwitch);
            startInfo.ArgumentList.Add(result);
            Process.Start(startInfo);
        }
        catch
        {
        }
    }

    private static bool TryParseCleanup(
        IReadOnlyList<string> arguments,
        out string helperDirectory,
        out int helperPid,
        out string result)
    {
        helperDirectory = string.Empty;
        helperPid = 0;
        result = string.Empty;

        if (arguments.Count != 6
            || arguments[0] != CleanupSwitch
            || arguments[2] != "--helper-pid"
            || arguments[4] != ResultSwitch
            || !int.TryParse(arguments[3], out helperPid)
            || helperPid <= 0
            || arguments[5] is not ("success" or "failed" or "recovery-required"))
        {
            return false;
        }

        try
        {
            helperDirectory = Path.GetFullPath(arguments[1]);
        }
        catch
        {
            return false;
        }

        result = arguments[5];
        return IsTrustedDirectChild(helperDirectory, HelperRoot);
    }

    private static void RequireTrustedStaging(string stagingDirectory)
    {
        if (!IsTrustedDirectChild(Path.GetFullPath(stagingDirectory), UpdatesRoot))
        {
            throw new InvalidDataException("update_staging_untrusted");
        }
    }

    private static void WaitForParent(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.WaitForExit(ParentExitTimeoutMilliseconds))
            {
                throw new TimeoutException("update_parent_exit_timeout");
            }
        }
        catch (ArgumentException)
        {
        }
    }

    private static bool IsTrustedDirectChild(string candidate, string parent)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var normalizedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        return string.Equals(Path.GetDirectoryName(normalizedCandidate), normalizedParent, comparison);
    }

    private static void TryWriteResult(string helperDirectory, Exception error)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                status = "failed",
                error = error.Message,
                type = error.GetType().Name
            });
            File.WriteAllText(Path.Combine(helperDirectory, "update-result.json"), payload);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
