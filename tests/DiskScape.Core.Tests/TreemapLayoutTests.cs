using DiskScape.Core;

namespace DiskScape.Core.Tests;

public sealed class TreemapLayoutTests
{
    [Fact]
    public async Task LayoutChildren_ProducesAreaProportionalTiles_WithoutOverlap()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "a", "a.bin"), 600);
            WriteFileWithSize(Path.Combine(root, "b", "b.bin"), 300);
            WriteFileWithSize(Path.Combine(root, "c", "c.bin"), 100);

            var scanRoot = await new Scanner().ScanAsync(root);
            var width = 1200d;
            var height = 800d;
            var containerArea = width * height;

            var tiles = TreemapLayout.LayoutChildren(scanRoot, width, height)
                .OrderByDescending(t => t.Node.TotalSize)
                .ToArray();

            Assert.Equal(3, tiles.Length);

            AssertAllTilesInBounds(tiles, width, height);
            AssertNoOverlaps(tiles);

            var areaByName = tiles.ToDictionary(t => t.Node.Name, t => t.Area / containerArea, StringComparer.OrdinalIgnoreCase);

            Assert.InRange(areaByName["a"], 0.59, 0.61);
            Assert.InRange(areaByName["b"], 0.29, 0.31);
            Assert.InRange(areaByName["c"], 0.09, 0.11);

            var totalTileArea = tiles.Sum(t => t.Area);
            Assert.InRange(totalTileArea, containerArea * 0.999999, containerArea * 1.000001);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LayoutChildren_IgnoresZeroSizeChildren()
    {
        var root = CreateTempRoot();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "empty"));
            WriteFileWithSize(Path.Combine(root, "data", "payload.bin"), 128);

            var scanRoot = await new Scanner().ScanAsync(root);
            var tiles = TreemapLayout.LayoutChildren(scanRoot, 600, 400);

            Assert.Single(tiles);
            Assert.Equal("data", tiles[0].Node.Name, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    public void Layout_ThrowsForNegativeDimensions(double width, double height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TreemapLayout.Layout(Array.Empty<ScanNode>(), width, height));
    }

    private static void AssertAllTilesInBounds(IEnumerable<TreemapTile> tiles, double width, double height)
    {
        foreach (var tile in tiles)
        {
            Assert.True(tile.X >= 0);
            Assert.True(tile.Y >= 0);
            Assert.True(tile.Width >= 0);
            Assert.True(tile.Height >= 0);
            Assert.True(tile.X + tile.Width <= width + 1e-6);
            Assert.True(tile.Y + tile.Height <= height + 1e-6);
        }
    }

    private static void AssertNoOverlaps(IReadOnlyList<TreemapTile> tiles)
    {
        for (var i = 0; i < tiles.Count; i++)
        {
            for (var j = i + 1; j < tiles.Count; j++)
            {
                var overlapWidth = Math.Min(tiles[i].X + tiles[i].Width, tiles[j].X + tiles[j].Width) -
                    Math.Max(tiles[i].X, tiles[j].X);
                var overlapHeight = Math.Min(tiles[i].Y + tiles[i].Height, tiles[j].Y + tiles[j].Height) -
                    Math.Max(tiles[i].Y, tiles[j].Y);

                var overlapArea = Math.Max(0, overlapWidth) * Math.Max(0, overlapHeight);
                Assert.True(overlapArea <= 1e-6, $"Tiles '{tiles[i].Node.Name}' and '{tiles[j].Node.Name}' overlap by {overlapArea}.");
            }
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "diskscape-layout-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFileWithSize(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }
}
