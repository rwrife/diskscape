using DiskScape.Core;

namespace DiskScape.Core.Tests;

public sealed class ScanTreeMutatorTests
{
    [Fact]
    public async Task TryRemovePath_RemovesFile_AndUpdatesAncestorAggregates()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "a.txt"), 10);
            WriteFileWithSize(Path.Combine(root, "sub", "b.bin"), 20);
            WriteFileWithSize(Path.Combine(root, "sub", "nested", "c.log"), 30);

            var scanRoot = await new Scanner().ScanAsync(root);

            var removed = ScanTreeMutator.TryRemovePath(
                scanRoot,
                Path.Combine(root, "sub", "b.bin"),
                out var reclaimedBytes,
                out var reclaimedFiles);

            Assert.True(removed);
            Assert.Equal(20, reclaimedBytes);
            Assert.Equal(1, reclaimedFiles);
            Assert.Equal(40, scanRoot.TotalSize);
            Assert.Equal(2, scanRoot.FileCount);

            var sub = scanRoot.Children.Single(c => c.IsDirectory && c.Name == "sub");
            Assert.Equal(30, sub.TotalSize);
            Assert.Equal(1, sub.FileCount);
            Assert.DoesNotContain(sub.Children, c => string.Equals(c.Name, "b.bin", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TryRemovePath_RemovesDirectorySubtree_AndUpdatesAncestorAggregates()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "a.txt"), 10);
            WriteFileWithSize(Path.Combine(root, "sub", "b.bin"), 20);
            WriteFileWithSize(Path.Combine(root, "sub", "nested", "c.log"), 30);

            var scanRoot = await new Scanner().ScanAsync(root);

            var removed = ScanTreeMutator.TryRemovePath(
                scanRoot,
                Path.Combine(root, "sub"),
                out var reclaimedBytes,
                out var reclaimedFiles);

            Assert.True(removed);
            Assert.Equal(50, reclaimedBytes);
            Assert.Equal(2, reclaimedFiles);
            Assert.Equal(10, scanRoot.TotalSize);
            Assert.Equal(1, scanRoot.FileCount);
            Assert.DoesNotContain(scanRoot.Children, c => string.Equals(c.Name, "sub", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TryRemovePath_ReturnsFalse_WhenTargetPathIsMissing()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "a.txt"), 10);
            WriteFileWithSize(Path.Combine(root, "sub", "b.bin"), 20);

            var scanRoot = await new Scanner().ScanAsync(root);

            var removed = ScanTreeMutator.TryRemovePath(
                scanRoot,
                Path.Combine(root, "does-not-exist.bin"),
                out var reclaimedBytes,
                out var reclaimedFiles);

            Assert.False(removed);
            Assert.Equal(0, reclaimedBytes);
            Assert.Equal(0, reclaimedFiles);
            Assert.Equal(30, scanRoot.TotalSize);
            Assert.Equal(2, scanRoot.FileCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "diskscape-mutator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFileWithSize(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }
}
