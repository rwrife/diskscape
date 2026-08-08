namespace DiskScape.Core;

public static class AiSuggestionRequestBuilder
{
    public static AiScanSnapshot BuildSnapshot(ScanNode root, int maxFolders = 12, int maxExtensionsPerFolder = 8)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (maxFolders < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFolders), maxFolders, "maxFolders must be at least 1.");
        }

        if (maxExtensionsPerFolder < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExtensionsPerFolder), maxExtensionsPerFolder, "maxExtensionsPerFolder must be at least 1.");
        }

        var folderNodes = new List<ScanNode>();
        CollectDirectories(root, folderNodes);

        var topFolders = folderNodes
            .Where(static node => node.TotalSize > 0)
            .OrderByDescending(static node => node.TotalSize)
            .Take(maxFolders)
            .Select(folder => BuildFolderSnapshot(folder, maxExtensionsPerFolder))
            .ToArray();

        return new AiScanSnapshot(
            RootName: root.Name,
            RootSizeBytes: root.TotalSize,
            RootFileCount: root.FileCount,
            Folders: topFolders);
    }

    private static void CollectDirectories(ScanNode node, List<ScanNode> directories)
    {
        foreach (var child in node.Children)
        {
            if (!child.IsDirectory)
            {
                continue;
            }

            directories.Add(child);
            CollectDirectories(child, directories);
        }
    }

    private static AiFolderSnapshot BuildFolderSnapshot(ScanNode folder, int maxExtensionsPerFolder)
    {
        var extensionStats = new Dictionary<string, ExtensionAccumulator>(StringComparer.OrdinalIgnoreCase);
        CollectExtensions(folder, extensionStats);

        var extensionSummary = extensionStats
            .OrderByDescending(static kvp => kvp.Value.TotalSizeBytes)
            .ThenByDescending(static kvp => kvp.Value.FileCount)
            .ThenBy(static kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Take(maxExtensionsPerFolder)
            .Select(static kvp => new AiExtensionSummary(kvp.Key, kvp.Value.FileCount, kvp.Value.TotalSizeBytes))
            .ToArray();

        return new AiFolderSnapshot(
            Name: folder.Name,
            SizeBytes: folder.TotalSize,
            FileCount: folder.FileCount,
            Extensions: extensionSummary);
    }

    private static void CollectExtensions(ScanNode node, Dictionary<string, ExtensionAccumulator> extensionStats)
    {
        foreach (var child in node.Children)
        {
            if (child.IsDirectory)
            {
                CollectExtensions(child, extensionStats);
                continue;
            }

            var extension = NormalizeExtension(Path.GetExtension(child.Name));
            if (!extensionStats.TryGetValue(extension, out var accumulator))
            {
                accumulator = new ExtensionAccumulator();
                extensionStats[extension] = accumulator;
            }

            accumulator.FileCount++;
            accumulator.TotalSizeBytes += Math.Max(0, child.TotalSize);
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "(no-extension)";
        }

        return extension.Trim().ToLowerInvariant();
    }

    private sealed class ExtensionAccumulator
    {
        public long FileCount { get; set; }
        public long TotalSizeBytes { get; set; }
    }
}

public sealed record AiScanSnapshot(string RootName, long RootSizeBytes, long RootFileCount, IReadOnlyList<AiFolderSnapshot> Folders);

public sealed record AiFolderSnapshot(string Name, long SizeBytes, long FileCount, IReadOnlyList<AiExtensionSummary> Extensions);

public sealed record AiExtensionSummary(string Extension, long FileCount, long TotalSizeBytes);
