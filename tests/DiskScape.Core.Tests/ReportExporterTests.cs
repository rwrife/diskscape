using System.Text.Json;
using DiskScape.Core;

namespace DiskScape.Core.Tests;

public sealed class ReportExporterTests
{
    [Fact]
    public void ExportCsv_EscapesCommasAndQuotesInPaths()
    {
        var scanRoot = ScanNode.CreateDirectory("root,with\"quote\"");
        var exporter = new ReportExporter();

        var csv = exporter.ExportCsv(scanRoot);

        Assert.StartsWith("Path,SizeBytes,SizeHumanReadable,FileCount,PercentOfRoot,IsDirectory", csv, StringComparison.Ordinal);
        Assert.Contains("\"root,with\"\"quote\"\"\",0,0 B,0,100,true", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportJson_RoundTripsEquivalentStructure()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "a.txt"), 10);
            WriteFileWithSize(Path.Combine(root, "sub", "b.bin"), 20);
            WriteFileWithSize(Path.Combine(root, "sub", "nested", "c.log"), 30);

            var scanRoot = await new Scanner().ScanAsync(root);
            var exporter = new ReportExporter();

            var expected = exporter.BuildReportTree(scanRoot);
            var json = exporter.ExportJson(scanRoot);
            var roundTripped = JsonSerializer.Deserialize<ReportNode>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.NotNull(roundTripped);
            AssertEquivalent(expected, roundTripped!);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExportCsv_CurrentSubtreeScope_ExportsOnlySubtree_AndNormalizesPercent()
    {
        var root = CreateTempRoot();

        try
        {
            WriteFileWithSize(Path.Combine(root, "a.txt"), 10);
            WriteFileWithSize(Path.Combine(root, "sub", "b.bin"), 30);

            var scanRoot = await new Scanner().ScanAsync(root);
            var currentSubtree = scanRoot.Children.Single(c => c.IsDirectory && c.Name == "sub");

            var exporter = new ReportExporter();
            var csv = exporter.ExportCsv(scanRoot, ReportScope.CurrentSubtree, currentSubtree);

            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimEnd('\r'))
                .ToArray();

            Assert.Equal(3, lines.Length); // header + subtree root + one file child
            Assert.DoesNotContain(lines, line => line.Contains(Path.Combine(root, "a.txt"), StringComparison.Ordinal));
            Assert.Contains(",100,true", lines[1], StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExportCsv_CurrentSubtreeScope_RequiresCurrentNode()
    {
        var scanRoot = ScanNode.CreateDirectory("root");
        var exporter = new ReportExporter();

        Assert.Throws<ArgumentNullException>(() => exporter.ExportCsv(scanRoot, ReportScope.CurrentSubtree));
    }

    private static void AssertEquivalent(ReportNode expected, ReportNode actual)
    {
        Assert.Equal(expected.Path, actual.Path);
        Assert.Equal(expected.SizeBytes, actual.SizeBytes);
        Assert.Equal(expected.SizeHumanReadable, actual.SizeHumanReadable);
        Assert.Equal(expected.FileCount, actual.FileCount);
        Assert.Equal(expected.IsDirectory, actual.IsDirectory);
        Assert.Equal(expected.PercentOfRoot, actual.PercentOfRoot, 4);

        Assert.Equal(expected.Children.Count, actual.Children.Count);
        for (var i = 0; i < expected.Children.Count; i++)
        {
            AssertEquivalent(expected.Children[i], actual.Children[i]);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "diskscape-report-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFileWithSize(string path, int bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }
}
