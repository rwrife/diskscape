using DiskScape.Core;

namespace DiskScape.Core.Tests;

public sealed class SunburstLayoutTests
{
    [Fact]
    public async Task LayoutChildren_ProducesProportionalDepthOneArcs()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "a", "a.bin"), 600);
            WriteFileWithSize(Path.Combine(root, "b", "b.bin"), 300);
            WriteFileWithSize(Path.Combine(root, "c", "c.bin"), 100);

            var scanRoot = await new Scanner().ScanAsync(root);
            var segments = SunburstLayout.LayoutChildren(scanRoot, outerRadius: 180, innerRadius: 0);

            var depthOne = segments
                .Where(segment => segment.Depth == 1)
                .OrderBy(segment => segment.Node.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.Equal(3, depthOne.Length);

            var byName = depthOne.ToDictionary(segment => segment.Node.Name, segment => segment);
            Assert.InRange(byName["a"].SweepAngle, 215.9, 216.1);
            Assert.InRange(byName["b"].SweepAngle, 107.9, 108.1);
            Assert.InRange(byName["c"].SweepAngle, 35.9, 36.1);

            var sum = depthOne.Sum(segment => segment.SweepAngle);
            Assert.InRange(sum, 359.999999, 360.000001);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LayoutChildren_NestsChildArcsWithinParentBounds()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "a", "nested", "deep.bin"), 300);
            WriteFileWithSize(Path.Combine(root, "a", "top.bin"), 300);
            WriteFileWithSize(Path.Combine(root, "b", "b.bin"), 400);

            var scanRoot = await new Scanner().ScanAsync(root);
            var segments = SunburstLayout.LayoutChildren(scanRoot, outerRadius: 120, innerRadius: 20);

            var parent = segments.Single(segment => segment.Depth == 1 && string.Equals(segment.Node.Name, "a", StringComparison.OrdinalIgnoreCase));
            var descendants = segments
                .Where(segment => segment.Depth > parent.Depth && segment.Node.Path.StartsWith(parent.Node.Path, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.NotEmpty(descendants);
            Assert.All(descendants, segment =>
            {
                Assert.True(segment.StartAngle >= parent.StartAngle - 1e-6);
                Assert.True(segment.EndAngle <= parent.EndAngle + 1e-6);
                Assert.True(segment.InnerRadius >= parent.OuterRadius - 1e-6);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "diskscape-sunburst-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFileWithSize(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }
}
