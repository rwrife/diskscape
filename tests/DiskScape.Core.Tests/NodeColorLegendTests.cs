using DiskScape.Core;

namespace DiskScape.Core.Tests;

public sealed class NodeColorLegendTests
{
    [Theory]
    [InlineData(".mp4", FileTypeGroup.Media)]
    [InlineData(".cs", FileTypeGroup.Code)]
    [InlineData("zip", FileTypeGroup.Archive)]
    [InlineData(".msix", FileTypeGroup.Installer)]
    [InlineData(".pdf", FileTypeGroup.Document)]
    [InlineData(".dll", FileTypeGroup.Binary)]
    [InlineData(".log", FileTypeGroup.System)]
    [InlineData(".unknownext", FileTypeGroup.Other)]
    public void ClassifyExtension_MapsKnownGroups(string extension, FileTypeGroup expected)
    {
        var actual = NodeColorLegend.ClassifyExtension(extension);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task BuildDominantGroups_PicksLargestSubtreeContribution()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "media", "clip.mp4"), 500);
            WriteFileWithSize(Path.Combine(root, "code", "app.cs"), 100);
            WriteFileWithSize(Path.Combine(root, "mixed", "archive.zip"), 250);
            WriteFileWithSize(Path.Combine(root, "mixed", "helper.cs"), 50);

            var scanRoot = await new Scanner().ScanAsync(root);
            var groups = NodeColorLegend.BuildDominantGroups(scanRoot);

            var mixedNode = scanRoot.Children.Single(child => string.Equals(child.Name, "mixed", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(FileTypeGroup.Archive, groups[mixedNode]);

            Assert.Equal(FileTypeGroup.Media, groups[scanRoot]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Colors_DefinesPaletteForAllGroups()
    {
        foreach (var group in Enum.GetValues<FileTypeGroup>())
        {
            Assert.True(NodeColorLegend.Colors.ContainsKey(group));
            Assert.StartsWith("#", NodeColorLegend.Colors[group]);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "diskscape-color-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFileWithSize(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }
}
