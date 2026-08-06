namespace DiskScape.Core;

public sealed class ReportNode
{
    public string Path { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string SizeHumanReadable { get; init; } = string.Empty;
    public long FileCount { get; init; }
    public double PercentOfRoot { get; init; }
    public bool IsDirectory { get; init; }
    public List<ReportNode> Children { get; init; } = new();
}
