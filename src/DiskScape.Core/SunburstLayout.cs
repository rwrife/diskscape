namespace DiskScape.Core;

/// <summary>
/// Computes sunburst layout primitives for a scan tree.
/// </summary>
public static class SunburstLayout
{
    private const double FullCircleDegrees = 360d;

    public static IReadOnlyList<SunburstSegment> LayoutChildren(ScanNode root, double outerRadius, double innerRadius = 0)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (outerRadius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outerRadius), "Outer radius must be greater than zero.");
        }

        if (innerRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(innerRadius), "Inner radius must be non-negative.");
        }

        if (innerRadius >= outerRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(innerRadius), "Inner radius must be smaller than outer radius.");
        }

        var maxDepth = GetMaxDepth(root);
        if (maxDepth == 0)
        {
            return Array.Empty<SunburstSegment>();
        }

        var ringThickness = (outerRadius - innerRadius) / maxDepth;
        var segments = new List<SunburstSegment>();

        LayoutLevel(
            GetRenderableChildren(root),
            depth: 1,
            maxDepth,
            parentStartAngle: 0,
            parentSweepAngle: FullCircleDegrees,
            innerRadius,
            ringThickness,
            segments);

        return segments;
    }

    private static void LayoutLevel(
        IReadOnlyList<ScanNode> siblings,
        int depth,
        int maxDepth,
        double parentStartAngle,
        double parentSweepAngle,
        double baseInnerRadius,
        double ringThickness,
        List<SunburstSegment> segments)
    {
        if (siblings.Count == 0 || depth > maxDepth)
        {
            return;
        }

        var total = siblings.Sum(node => node.TotalSize);
        if (total <= 0)
        {
            return;
        }

        var currentAngle = parentStartAngle;
        for (var i = 0; i < siblings.Count; i++)
        {
            var node = siblings[i];
            var sweep = i == siblings.Count - 1
                ? (parentStartAngle + parentSweepAngle) - currentAngle
                : parentSweepAngle * (node.TotalSize / (double)total);

            var inner = baseInnerRadius + ((depth - 1) * ringThickness);
            var outer = inner + ringThickness;

            segments.Add(new SunburstSegment(
                node,
                depth,
                currentAngle,
                sweep,
                inner,
                outer));

            var children = GetRenderableChildren(node);
            if (children.Count > 0)
            {
                LayoutLevel(
                    children,
                    depth + 1,
                    maxDepth,
                    currentAngle,
                    sweep,
                    baseInnerRadius,
                    ringThickness,
                    segments);
            }

            currentAngle += sweep;
        }
    }

    private static IReadOnlyList<ScanNode> GetRenderableChildren(ScanNode node) =>
        node.Children
            .Where(child => child.TotalSize > 0)
            .OrderByDescending(child => child.TotalSize)
            .ThenBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static int GetMaxDepth(ScanNode node)
    {
        var children = GetRenderableChildren(node);
        if (children.Count == 0)
        {
            return 0;
        }

        var childDepth = children.Max(GetMaxDepth);
        return childDepth + 1;
    }
}
