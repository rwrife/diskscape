namespace DiskScape.Core;

public static class ScanTreeMutator
{
    public static bool TryRemovePath(ScanNode root, string path, out long reclaimedBytes, out long reclaimedFiles)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        reclaimedBytes = 0;
        reclaimedFiles = 0;

        var targetPath = NormalizePath(path);
        var rootPath = NormalizePath(root.Path);

        if (PathsEqual(targetPath, rootPath))
        {
            return false;
        }

        return TryRemovePathRecursive(root, targetPath, out reclaimedBytes, out reclaimedFiles);
    }

    private static bool TryRemovePathRecursive(ScanNode current, string targetPath, out long reclaimedBytes, out long reclaimedFiles)
    {
        for (var i = 0; i < current.Children.Count; i++)
        {
            var child = current.Children[i];

            if (PathsEqual(NormalizePath(child.Path), targetPath))
            {
                current.RemoveChildAt(i);
                reclaimedBytes = child.TotalSize;
                reclaimedFiles = child.FileCount;
                current.SubtractAggregate(reclaimedBytes, reclaimedFiles);
                return true;
            }

            if (!child.IsDirectory)
            {
                continue;
            }

            if (TryRemovePathRecursive(child, targetPath, out reclaimedBytes, out reclaimedFiles))
            {
                current.SubtractAggregate(reclaimedBytes, reclaimedFiles);
                return true;
            }
        }

        reclaimedBytes = 0;
        reclaimedFiles = 0;
        return false;
    }

    private static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Path.TrimEndingDirectorySeparator(fullPath);
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(left, right, comparison);
    }
}
