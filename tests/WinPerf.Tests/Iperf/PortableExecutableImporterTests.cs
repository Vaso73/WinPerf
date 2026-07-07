using WinPerf.Core.Iperf;

namespace WinPerf.Tests.Iperf;

public sealed class PortableExecutableImporterTests
{
    [Fact]
    public void Import_CopiesOnlySelectedExecutableToCanonicalTarget()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var sourceDirectory = Path.Combine(root, "source");
            var targetDirectory = Path.Combine(root, "app", "tools", "iperf2");
            Directory.CreateDirectory(sourceDirectory);

            var source = Path.Combine(sourceDirectory, "iperf-2.2.1-win64.exe");
            var unrelated = Path.Combine(sourceDirectory, "unrelated.dll");
            var target = Path.Combine(targetDirectory, "iperf.exe");

            File.WriteAllText(source, "selected executable");
            File.WriteAllText(unrelated, "must not be copied");

            var imported = PortableExecutableImporter.Import(source, target);

            Assert.Equal(Path.GetFullPath(target), imported);
            Assert.Equal("selected executable", File.ReadAllText(target));
            Assert.False(File.Exists(Path.Combine(targetDirectory, "unrelated.dll")));
            Assert.Equal("selected executable", File.ReadAllText(source));
            Assert.Equal("must not be copied", File.ReadAllText(unrelated));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Import_OverwritesExistingCanonicalTarget()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var source = Path.Combine(root, "download", "iperf3-custom.exe");
            var target = Path.Combine(root, "app", "tools", "iperf3", "iperf3.exe");

            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            File.WriteAllText(source, "new executable");
            File.WriteAllText(target, "old executable");

            PortableExecutableImporter.Import(source, target);

            Assert.Equal("new executable", File.ReadAllText(target));
            Assert.Equal("new executable", File.ReadAllText(source));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Import_SourceEqualToTarget_IsSafeNoOp()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var target = Path.Combine(root, "tools", "iperf2", "iperf.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, "existing executable");

            var imported = PortableExecutableImporter.Import(target, target);

            Assert.Equal(Path.GetFullPath(target), imported);
            Assert.Equal("existing executable", File.ReadAllText(target));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Import_RejectsMissingSourceExecutable()
    {
        var root = CreateTemporaryDirectory();

        try
        {
            var source = Path.Combine(root, "missing.exe");
            var target = Path.Combine(root, "tools", "iperf2", "iperf.exe");

            Assert.Throws<FileNotFoundException>(
                () => PortableExecutableImporter.Import(source, target));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "WinPerf.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }
}
