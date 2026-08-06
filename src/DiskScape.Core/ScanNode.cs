namespace DiskScape.Core;

public sealed class ScanNode
{
    private readonly List<ScanNode> _children = new();

    private ScanNode(string path, string name, bool isDirectory, long ownSize)
    {
        Path = path;
        Name = name;
        IsDirectory = isDirectory;
        OwnSize = ownSize;
        TotalSize = ownSize;
        FileCount = isDirectory ? 0 : 1;
    }

    public string Path { get; }
    public string Name { get; }
    public bool IsDirectory { get; }
    public long OwnSize { get; }

    /// <summary>
    /// Total bytes in this node, including descendants when this node is a directory.
    /// </summary>
    public long TotalSize { get; internal set; }

    /// <summary>
    /// Number of files under this node (including descendants for directories).
    /// </summary>
    public long FileCount { get; internal set; }

    public IReadOnlyList<ScanNode> Children => _children;

    /// <summary>
    /// Warnings encountered during scanning. Populated on the root node returned by Scanner.
    /// </summary>
    public IReadOnlyList<ScanWarning> Warnings { get; private set; } = Array.Empty<ScanWarning>();

    public static ScanNode CreateDirectory(string path) =>
        new(path, System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)) is { Length: > 0 } name ? name : path, isDirectory: true, ownSize: 0);

    public static ScanNode CreateFile(string path, long size) =>
        new(path, System.IO.Path.GetFileName(path), isDirectory: false, ownSize: size);

    internal void AddChild(ScanNode child) => _children.Add(child);

    internal void RemoveChildAt(int index) => _children.RemoveAt(index);

    internal void SubtractAggregate(long bytes, long files)
    {
        TotalSize = Math.Max(0, TotalSize - bytes);
        FileCount = Math.Max(0, FileCount - files);
    }

    internal void SetWarnings(IReadOnlyList<ScanWarning> warnings) => Warnings = warnings;
}
