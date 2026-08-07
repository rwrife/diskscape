namespace DiskScape.Core;

public sealed record FileTypeLegendEntry(FileTypeGroup Group, string DisplayName, string HexColor);

/// <summary>
/// Shared file-type grouping and color legend logic that both treemap and sunburst renderers can consume.
/// </summary>
public static class NodeColorLegend
{
    private static readonly IReadOnlyDictionary<FileTypeGroup, string> GroupColors =
        new Dictionary<FileTypeGroup, string>
        {
            [FileTypeGroup.Folder] = "#5C6BC0",
            [FileTypeGroup.Media] = "#00ACC1",
            [FileTypeGroup.Code] = "#43A047",
            [FileTypeGroup.Archive] = "#8E24AA",
            [FileTypeGroup.Installer] = "#FB8C00",
            [FileTypeGroup.Document] = "#6D4C41",
            [FileTypeGroup.Binary] = "#546E7A",
            [FileTypeGroup.System] = "#E53935",
            [FileTypeGroup.Other] = "#9E9E9E"
        };

    private static readonly IReadOnlyList<FileTypeLegendEntry> LegendEntries =
        new[]
        {
            new FileTypeLegendEntry(FileTypeGroup.Folder, "Folders", GroupColors[FileTypeGroup.Folder]),
            new FileTypeLegendEntry(FileTypeGroup.Media, "Media", GroupColors[FileTypeGroup.Media]),
            new FileTypeLegendEntry(FileTypeGroup.Code, "Code", GroupColors[FileTypeGroup.Code]),
            new FileTypeLegendEntry(FileTypeGroup.Archive, "Archives", GroupColors[FileTypeGroup.Archive]),
            new FileTypeLegendEntry(FileTypeGroup.Installer, "Installers", GroupColors[FileTypeGroup.Installer]),
            new FileTypeLegendEntry(FileTypeGroup.Document, "Documents", GroupColors[FileTypeGroup.Document]),
            new FileTypeLegendEntry(FileTypeGroup.Binary, "Binary/Data", GroupColors[FileTypeGroup.Binary]),
            new FileTypeLegendEntry(FileTypeGroup.System, "System", GroupColors[FileTypeGroup.System]),
            new FileTypeLegendEntry(FileTypeGroup.Other, "Other", GroupColors[FileTypeGroup.Other])
        };

    private static readonly IReadOnlyDictionary<string, FileTypeGroup> ExtensionGroups =
        BuildExtensionGroups();

    public static IReadOnlyDictionary<FileTypeGroup, string> Colors => GroupColors;

    public static IReadOnlyList<FileTypeLegendEntry> Legend => LegendEntries;

    public static FileTypeGroup ClassifyNode(ScanNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.IsDirectory)
        {
            return FileTypeGroup.Folder;
        }

        return ClassifyExtension(Path.GetExtension(node.Name));
    }

    public static FileTypeGroup ClassifyExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return FileTypeGroup.Other;
        }

        var normalized = extension.StartsWith('.')
            ? extension.ToLowerInvariant()
            : $".{extension.ToLowerInvariant()}";

        return ExtensionGroups.TryGetValue(normalized, out var group)
            ? group
            : FileTypeGroup.Other;
    }

    /// <summary>
    /// Produces a dominant file-type group for each node based on subtree size contribution.
    /// </summary>
    public static IReadOnlyDictionary<ScanNode, FileTypeGroup> BuildDominantGroups(ScanNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var groups = new Dictionary<ScanNode, FileTypeGroup>(ReferenceEqualityComparer.Instance);
        ComputeBreakdown(root, groups);
        return groups;
    }

    private static Dictionary<FileTypeGroup, long> ComputeBreakdown(ScanNode node, IDictionary<ScanNode, FileTypeGroup> groups)
    {
        if (!node.IsDirectory)
        {
            var group = ClassifyNode(node);
            groups[node] = group;
            return new Dictionary<FileTypeGroup, long> { [group] = Math.Max(0, node.TotalSize) };
        }

        var aggregate = new Dictionary<FileTypeGroup, long>();
        foreach (var child in node.Children)
        {
            if (child.TotalSize <= 0)
            {
                continue;
            }

            var breakdown = ComputeBreakdown(child, groups);
            foreach (var entry in breakdown)
            {
                aggregate[entry.Key] = aggregate.TryGetValue(entry.Key, out var existing)
                    ? existing + entry.Value
                    : entry.Value;
            }
        }

        var dominant = aggregate.Count == 0
            ? FileTypeGroup.Folder
            : aggregate
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .First()
                .Key;

        groups[node] = dominant;
        return aggregate;
    }

    private static IReadOnlyDictionary<string, FileTypeGroup> BuildExtensionGroups()
    {
        var map = new Dictionary<string, FileTypeGroup>(StringComparer.OrdinalIgnoreCase);

        AddRange(map, FileTypeGroup.Media, ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".webp", ".mp4", ".mkv", ".mov", ".mp3", ".wav", ".flac");
        AddRange(map, FileTypeGroup.Code, ".cs", ".csproj", ".sln", ".slnx", ".json", ".xml", ".yml", ".yaml", ".md", ".txt", ".js", ".ts", ".tsx", ".jsx", ".py", ".java", ".go", ".rs", ".cpp", ".h", ".hpp", ".sql", ".css", ".html");
        AddRange(map, FileTypeGroup.Archive, ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".iso");
        AddRange(map, FileTypeGroup.Installer, ".msi", ".msix", ".exe", ".appx", ".appxbundle", ".msixbundle", ".dmg", ".pkg");
        AddRange(map, FileTypeGroup.Document, ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".rtf", ".odt");
        AddRange(map, FileTypeGroup.Binary, ".dll", ".so", ".dylib", ".bin", ".dat", ".db", ".sqlite", ".cache");
        AddRange(map, FileTypeGroup.System, ".sys", ".log", ".etl", ".dmp", ".tmp");

        return map;
    }

    private static void AddRange(IDictionary<string, FileTypeGroup> map, FileTypeGroup group, params string[] extensions)
    {
        foreach (var extension in extensions)
        {
            map[extension] = group;
        }
    }
}
