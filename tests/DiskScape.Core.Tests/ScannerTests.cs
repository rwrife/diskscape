using DiskScape.Core;

namespace DiskScape.Core.Tests;

public sealed class ScannerTests
{
    [Fact]
    public async Task ScanAsync_AggregatesDescendantFileSizes_AndCountsFiles()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "a.txt"), 10);
            WriteFileWithSize(Path.Combine(root, "sub", "b.bin"), 20);
            WriteFileWithSize(Path.Combine(root, "sub", "nested", "c.log"), 30);

            var scanner = new Scanner(new ScannerOptions { MaxDegreeOfParallelism = 4 });
            var scanRoot = await scanner.ScanAsync(root);

            Assert.True(scanRoot.IsDirectory);
            Assert.Equal(60, scanRoot.TotalSize);
            Assert.Equal(3, scanRoot.FileCount);

            var sub = scanRoot.Children.Single(c => c.IsDirectory && c.Name == "sub");
            Assert.Equal(50, sub.TotalSize);
            Assert.Equal(2, sub.FileCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_ThrowsOperationCanceled_WhenCancellationRequestedDuringScan()
    {
        var root = CreateTempRoot();

        try
        {
            for (var d = 0; d < 30; d++)
            {
                var directory = Path.Combine(root, $"d-{d}");
                for (var f = 0; f < 100; f++)
                {
                    WriteFileWithSize(Path.Combine(directory, $"f-{f}.dat"), 2048);
                }
            }

            using var cts = new CancellationTokenSource();
            var progress = new Progress<ScanProgress>(p =>
            {
                if (p.ItemsScanned >= 50)
                {
                    cts.Cancel();
                }
            });

            var scanner = new Scanner(new ScannerOptions { MaxDegreeOfParallelism = 8 });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => scanner.ScanAsync(root, progress, cts.Token));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_SkipsUnreadableDirectories_AndSurfacesAccessDeniedWarning()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateTempRoot();
        var deniedDir = Path.Combine(root, "denied");

        try
        {
            WriteFileWithSize(Path.Combine(root, "readable.txt"), 64);
            WriteFileWithSize(Path.Combine(deniedDir, "secret.bin"), 128);

            File.SetUnixFileMode(
                deniedDir,
                UnixFileMode.None);

            var scanner = new Scanner();
            var scanRoot = await scanner.ScanAsync(root);

            Assert.Contains(scanRoot.Warnings, w =>
                w.Kind == ScanWarningKind.AccessDenied &&
                string.Equals(w.Path, deniedDir, StringComparison.Ordinal));

            Assert.Equal(64, scanRoot.TotalSize);
            Assert.Equal(1, scanRoot.FileCount);
        }
        finally
        {
            try
            {
                File.SetUnixFileMode(
                    deniedDir,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch
            {
                // best-effort cleanup if the directory was never created or mode-set failed.
            }

            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_DoesNotRecurseIntoReparsePoints_ByDefault()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateTempRoot();

        try
        {
            var targetDir = Path.Combine(root, "target");
            Directory.CreateDirectory(targetDir);
            WriteFileWithSize(Path.Combine(targetDir, "inside.bin"), 200);
            WriteFileWithSize(Path.Combine(root, "root.bin"), 100);

            var linkDir = Path.Combine(root, "target-link");
            try
            {
                Directory.CreateSymbolicLink(linkDir, targetDir);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
            {
                // Environment does not permit symlink creation.
                return;
            }

            var scanner = new Scanner();
            var scanRoot = await scanner.ScanAsync(root);

            // If the symlink target were traversed, total would double-count target/inside.bin.
            Assert.Equal(300, scanRoot.TotalSize);

            Assert.Contains(scanRoot.Warnings, w =>
                w.Kind == ScanWarningKind.ReparsePointSkipped &&
                string.Equals(w.Path, linkDir, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "diskscape-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFileWithSize(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }
}
