using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DiskScape.Core;

public sealed class ReportExporter
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string ExportCsv(ScanNode root, ReportScope scope = ReportScope.WholeTree, ScanNode? currentNode = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        var scopeRoot = ResolveScopeRoot(root, scope, currentNode);
        var reportRoot = BuildReportTree(scopeRoot);

        var builder = new StringBuilder();
        builder.AppendLine("Path,SizeBytes,SizeHumanReadable,FileCount,PercentOfRoot,IsDirectory");

        foreach (var row in Flatten(reportRoot))
        {
            builder.Append(EscapeCsv(row.Path));
            builder.Append(',');
            builder.Append(row.SizeBytes.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(EscapeCsv(row.SizeHumanReadable));
            builder.Append(',');
            builder.Append(row.FileCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(row.PercentOfRoot.ToString("0.####", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(row.IsDirectory ? "true" : "false");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    public string ExportJson(ScanNode root, ReportScope scope = ReportScope.WholeTree, ScanNode? currentNode = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        var scopeRoot = ResolveScopeRoot(root, scope, currentNode);
        var reportRoot = BuildReportTree(scopeRoot);
        return JsonSerializer.Serialize(reportRoot, JsonSerializerOptions);
    }

    public ReportNode BuildReportTree(ScanNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var rootTotalSize = Math.Max(0, root.TotalSize);
        return BuildReportNode(root, rootTotalSize, isRoot: true);
    }

    private static ScanNode ResolveScopeRoot(ScanNode root, ReportScope scope, ScanNode? currentNode)
    {
        return scope switch
        {
            ReportScope.WholeTree => root,
            ReportScope.CurrentSubtree => currentNode ?? throw new ArgumentNullException(
                nameof(currentNode),
                "currentNode is required when scope is CurrentSubtree."),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported report scope.")
        };
    }

    private static ReportNode BuildReportNode(ScanNode node, long rootTotalSize, bool isRoot)
    {
        var children = new List<ReportNode>(node.Children.Count);
        foreach (var child in node.Children)
        {
            children.Add(BuildReportNode(child, rootTotalSize, isRoot: false));
        }

        return new ReportNode
        {
            Path = node.Path,
            SizeBytes = node.TotalSize,
            SizeHumanReadable = FormatSize(node.TotalSize),
            FileCount = node.FileCount,
            PercentOfRoot = CalculatePercent(node.TotalSize, rootTotalSize, isRoot),
            IsDirectory = node.IsDirectory,
            Children = children
        };
    }

    private static IEnumerable<ReportNode> Flatten(ReportNode node)
    {
        yield return node;

        foreach (var child in node.Children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private static double CalculatePercent(long sizeBytes, long rootTotalSize, bool isRoot)
    {
        if (rootTotalSize <= 0)
        {
            return isRoot ? 100d : 0d;
        }

        return Math.Round(sizeBytes * 100d / rootTotalSize, 4, MidpointRounding.AwayFromZero);
    }

    private static string FormatSize(long bytes)
    {
        var normalizedBytes = Math.Max(0, bytes);
        if (normalizedBytes < 1024)
        {
            return $"{normalizedBytes} B";
        }

        var units = new[] { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };
        var unitIndex = 0;
        var size = (double)normalizedBytes;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private static string EscapeCsv(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
