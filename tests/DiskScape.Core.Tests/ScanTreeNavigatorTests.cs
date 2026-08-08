using DiskScape.Core;

namespace DiskScape.Core.Tests;

public sealed class ScanTreeNavigatorTests
{
    [Fact]
    public async Task TryMove_SupportsSiblingTraversalAndZoomInOut()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "small", "s.bin"), 100);
            WriteFileWithSize(Path.Combine(root, "medium", "m.bin"), 200);
            WriteFileWithSize(Path.Combine(root, "large", "l.bin"), 300);

            var scanRoot = await new Scanner().ScanAsync(root);
            var navigator = new ScanTreeNavigator(scanRoot);

            Assert.True(navigator.TryMove(TreeNavigationCommand.ZoomIn));
            Assert.Equal("large", navigator.Current.Name, ignoreCase: true);

            Assert.True(navigator.TryMove(TreeNavigationCommand.NextSibling));
            Assert.Equal("medium", navigator.Current.Name, ignoreCase: true);

            Assert.True(navigator.TryMove(TreeNavigationCommand.NextSibling));
            Assert.Equal("small", navigator.Current.Name, ignoreCase: true);

            Assert.True(navigator.TryMove(TreeNavigationCommand.NextSibling));
            Assert.Equal("large", navigator.Current.Name, ignoreCase: true);

            Assert.True(navigator.TryMove(TreeNavigationCommand.PreviousSibling));
            Assert.Equal("small", navigator.Current.Name, ignoreCase: true);

            Assert.True(navigator.TryMove(TreeNavigationCommand.ZoomOut));
            Assert.True(ReferenceEquals(scanRoot, navigator.Current));
            Assert.False(navigator.TryMove(TreeNavigationCommand.ZoomOut));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TryMove_ZoomInReturnsFalseOnLeaf()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "only", "item.bin"), 42);

            var scanRoot = await new Scanner().ScanAsync(root);
            var navigator = new ScanTreeNavigator(scanRoot);

            Assert.True(navigator.TryMove(TreeNavigationCommand.ZoomIn)); // folder
            Assert.True(navigator.TryMove(TreeNavigationCommand.ZoomIn)); // file leaf
            Assert.False(navigator.TryMove(TreeNavigationCommand.ZoomIn));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "diskscape-nav-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFileWithSize(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }
}
