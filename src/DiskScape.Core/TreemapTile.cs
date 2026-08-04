namespace DiskScape.Core;

/// <summary>
/// Represents a single treemap tile for a node, in container-relative coordinates.
/// </summary>
public readonly record struct TreemapTile(
    ScanNode Node,
    double X,
    double Y,
    double Width,
    double Height)
{
    public double Area => Width * Height;
}
