namespace WinPerf.Core.Updates;

public sealed class UpdateInstallationException(
    string message,
    bool rollbackSucceeded,
    string recoveryDirectory,
    Exception innerException) : IOException(message, innerException)
{
    public bool RollbackSucceeded { get; } = rollbackSucceeded;
    public string RecoveryDirectory { get; } = recoveryDirectory;
}

public sealed class WinPerfUpdateInstaller
{
    internal Action<int>? ReplacementCompletedForTesting { get; init; }

    public void Apply(string stagingDirectory, string installDirectory, string backupRoot)
    {
        var staging = NormalizeDirectory(stagingDirectory, mustExist: true);
        var install = NormalizeDirectory(installDirectory, mustExist: true);
        var backups = NormalizeDirectory(backupRoot, mustExist: false);

        RequireSeparateTrees(staging, install, backups);
        ValidateStaging(staging);
        ValidateInstallTargets(install);

        if (!File.Exists(Path.Combine(install, "WinPerf.exe")))
        {
            throw new InvalidDataException("installed_executable_missing");
        }

        Directory.CreateDirectory(backups);
        RejectReparsePoint(backups);

        var recovery = Path.Combine(backups, $"winperf-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(recovery);
        var existed = new Dictionary<string, bool>(StringComparer.Ordinal);
        var replacementCount = 0;

        try
        {
            foreach (var relativePath in WinPerfUpdateService.PackageFiles)
            {
                var target = ResolveContainedPath(install, relativePath);
                var backup = ResolveContainedPath(recovery, relativePath);

                if (File.Exists(target))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(target, backup, overwrite: false);
                    existed[relativePath] = true;
                }
                else
                {
                    existed[relativePath] = false;
                }
            }

            foreach (var relativePath in WinPerfUpdateService.PackageFiles)
            {
                var source = ResolveContainedPath(staging, relativePath);
                var target = ResolveContainedPath(install, relativePath);
                ReplaceFile(source, target);
                replacementCount++;
                ReplacementCompletedForTesting?.Invoke(replacementCount);
            }

            Directory.Delete(recovery, recursive: true);
        }
        catch (Exception installError)
        {
            var rollbackSucceeded = RollBack(install, recovery, existed);
            throw new UpdateInstallationException(
                rollbackSucceeded
                    ? "update_install_failed_rolled_back"
                    : "update_install_failed_recovery_required",
                rollbackSucceeded,
                recovery,
                installError);
        }
    }

    private static void ValidateStaging(string staging)
    {
        RejectReparsePoint(staging);

        if (Directory.EnumerateDirectories(staging).Any())
        {
            throw new InvalidDataException("staging_directory_contract_invalid");
        }

        var actualFiles = Directory
            .EnumerateFiles(staging)
            .Select(path => Path.GetRelativePath(staging, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expectedFiles = WinPerfUpdateService.PackageFiles
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (!actualFiles.SequenceEqual(expectedFiles, StringComparer.Ordinal))
        {
            throw new InvalidDataException("staging_contract_invalid");
        }

        foreach (var relativePath in WinPerfUpdateService.PackageFiles)
        {
            var file = ResolveContainedPath(staging, relativePath);
            RejectReparsePoint(file);

            if (new FileInfo(file).Length <= 0)
            {
                throw new InvalidDataException("staging_file_empty");
            }
        }
    }

    private static void ValidateInstallTargets(string install)
    {
        RejectReparsePoint(install);

        foreach (var relativePath in WinPerfUpdateService.PackageFiles)
        {
            var target = ResolveContainedPath(install, relativePath);
            if (File.Exists(target))
            {
                RejectReparsePoint(target);
            }
        }
    }

    private static bool RollBack(
        string install,
        string recovery,
        IReadOnlyDictionary<string, bool> existed)
    {
        var succeeded = true;

        foreach (var relativePath in WinPerfUpdateService.PackageFiles.Reverse())
        {
            try
            {
                if (!existed.TryGetValue(relativePath, out var wasPresent))
                {
                    continue;
                }

                var target = ResolveContainedPath(install, relativePath);

                if (wasPresent)
                {
                    var backup = ResolveContainedPath(recovery, relativePath);
                    ReplaceFile(backup, target);
                }
                else if (File.Exists(target))
                {
                    File.Delete(target);
                }
            }
            catch
            {
                succeeded = false;
            }
        }

        if (succeeded)
        {
            try
            {
                if (Directory.Exists(recovery))
                {
                    Directory.Delete(recovery, recursive: true);
                }
            }
            catch
            {
                succeeded = false;
            }
        }

        return succeeded;
    }

    private static void ReplaceFile(string source, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + $".winperf-new-{Guid.NewGuid():N}";

        try
        {
            File.Copy(source, temporary, overwrite: false);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            catch
            {
            }
        }
    }

    private static string NormalizeDirectory(string value, bool mustExist)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("directory_required", nameof(value));
        }

        var result = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));

        if (mustExist && !Directory.Exists(result))
        {
            throw new DirectoryNotFoundException(result);
        }

        return result;
    }

    private static void RequireSeparateTrees(params string[] paths)
    {
        for (var left = 0; left < paths.Length; left++)
        {
            for (var right = left + 1; right < paths.Length; right++)
            {
                if (IsSameOrChild(paths[left], paths[right])
                    || IsSameOrChild(paths[right], paths[left]))
                {
                    throw new InvalidDataException("update_directories_overlap");
                }
            }
        }
    }

    private static bool IsSameOrChild(string candidate, string parent)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return candidate.Equals(parent, comparison)
            || candidate.StartsWith(parent + Path.DirectorySeparatorChar, comparison);
    }

    private static string ResolveContainedPath(string root, string relativePath)
    {
        var result = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!IsSameOrChild(result, root) || result.Equals(root, comparison))
        {
            throw new InvalidDataException("update_path_invalid");
        }

        return result;
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("update_reparse_point_invalid");
        }
    }
}
