namespace WinPerf.Core.Iperf;

public static class PortableExecutableImporter
{
    public static string Import(
        string sourceExecutablePath,
        string targetExecutablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExecutablePath);

        var sourcePath = Path.GetFullPath(sourceExecutablePath);
        var targetPath = Path.GetFullPath(targetExecutablePath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                "Selected executable was not found.",
                sourcePath);
        }

        var targetDirectory = Path.GetDirectoryName(targetPath);

        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new ArgumentException(
                "Portable executable target directory is invalid.",
                nameof(targetExecutablePath));
        }

        Directory.CreateDirectory(targetDirectory);

        if (!string.Equals(
                sourcePath,
                targetPath,
                StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        CopySiblingDllDependencies(sourcePath, targetDirectory);

        return targetPath;
    }

    private static void CopySiblingDllDependencies(
        string sourceExecutablePath,
        string targetDirectory)
    {
        var sourceDirectory = Path.GetDirectoryName(sourceExecutablePath);

        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return;
        }

        foreach (var sourceDllPath in Directory.EnumerateFiles(sourceDirectory, "*.dll"))
        {
            var targetDllPath = Path.Combine(
                targetDirectory,
                Path.GetFileName(sourceDllPath));

            if (string.Equals(
                    Path.GetFullPath(sourceDllPath),
                    Path.GetFullPath(targetDllPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(sourceDllPath, targetDllPath, overwrite: true);
        }
    }
}
