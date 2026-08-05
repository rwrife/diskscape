namespace DiskScape.Core;

/// <summary>
/// Computes squarified treemap layouts for scan nodes.
/// </summary>
public static class TreemapLayout
{
    public static IReadOnlyList<TreemapTile> LayoutChildren(ScanNode parent, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return Layout(parent.Children, width, height);
    }

    public static IReadOnlyList<TreemapTile> Layout(IReadOnlyList<ScanNode> nodes, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be non-negative.");
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-negative.");
        }

        if (width == 0 || height == 0 || nodes.Count == 0)
        {
            return Array.Empty<TreemapTile>();
        }

        var layoutNodes = nodes
            .Where(n => n.TotalSize > 0)
            .OrderByDescending(n => n.TotalSize)
            .Select(n => new LayoutNode(n, n.TotalSize))
            .ToArray();

        if (layoutNodes.Length == 0)
        {
            return Array.Empty<TreemapTile>();
        }

        var totalSize = layoutNodes.Sum(n => n.Size);
        var totalArea = width * height;
        var areaScale = totalArea / totalSize;

        var pending = new Queue<LayoutNode>(layoutNodes.Select(n => n with { Area = n.Size * areaScale }));
        var result = new List<TreemapTile>(layoutNodes.Length);
        var row = new List<LayoutNode>();
        var container = new Rect(0, 0, width, height);

        while (pending.Count > 0)
        {
            var next = pending.Peek();
            if (row.Count == 0)
            {
                row.Add(pending.Dequeue());
                continue;
            }

            var shortSide = Math.Min(container.Width, container.Height);
            var currentScore = WorstAspectRatio(row, shortSide);
            var candidateScore = WorstAspectRatio(row.Append(next), shortSide);

            if (candidateScore <= currentScore)
            {
                row.Add(pending.Dequeue());
            }
            else
            {
                LayoutRow(row, ref container, result);
                row.Clear();
            }
        }

        if (row.Count > 0)
        {
            LayoutRow(row, ref container, result);
        }

        return result;
    }

    private static double WorstAspectRatio(IEnumerable<LayoutNode> row, double shortSide)
    {
        if (shortSide <= 0)
        {
            return double.PositiveInfinity;
        }

        var areas = row.Select(r => r.Area).ToArray();
        if (areas.Length == 0)
        {
            return double.PositiveInfinity;
        }

        var sum = areas.Sum();
        if (sum <= 0)
        {
            return double.PositiveInfinity;
        }

        var maxArea = areas.Max();
        var minArea = areas.Min();
        if (minArea <= 0)
        {
            return double.PositiveInfinity;
        }

        var sideSquared = shortSide * shortSide;
        var sumSquared = sum * sum;

        var ratioA = (sideSquared * maxArea) / sumSquared;
        var ratioB = sumSquared / (sideSquared * minArea);
        return Math.Max(ratioA, ratioB);
    }

    private static void LayoutRow(List<LayoutNode> row, ref Rect container, List<TreemapTile> tiles)
    {
        if (row.Count == 0 || container.Width <= 0 || container.Height <= 0)
        {
            return;
        }

        var rowArea = row.Sum(r => r.Area);
        if (rowArea <= 0)
        {
            return;
        }

        if (container.Width >= container.Height)
        {
            var rowHeight = rowArea / container.Width;
            var x = container.X;

            foreach (var item in row)
            {
                var itemWidth = rowHeight <= 0 ? 0 : item.Area / rowHeight;
                tiles.Add(new TreemapTile(item.Node, x, container.Y, itemWidth, rowHeight));
                x += itemWidth;
            }

            container = new Rect(
                container.X,
                container.Y + rowHeight,
                container.Width,
                Math.Max(0, container.Height - rowHeight));
        }
        else
        {
            var rowWidth = rowArea / container.Height;
            var y = container.Y;

            foreach (var item in row)
            {
                var itemHeight = rowWidth <= 0 ? 0 : item.Area / rowWidth;
                tiles.Add(new TreemapTile(item.Node, container.X, y, rowWidth, itemHeight));
                y += itemHeight;
            }

            container = new Rect(
                container.X + rowWidth,
                container.Y,
                Math.Max(0, container.Width - rowWidth),
                container.Height);
        }
    }

    private readonly record struct LayoutNode(ScanNode Node, long Size)
    {
        public double Area { get; init; }
    }

    private readonly record struct Rect(double X, double Y, double Width, double Height);
}
