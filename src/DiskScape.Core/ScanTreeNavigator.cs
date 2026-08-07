namespace DiskScape.Core;

public enum TreeNavigationCommand
{
    PreviousSibling,
    NextSibling,
    ZoomIn,
    ZoomOut
}

/// <summary>
/// UI-free navigation state for keyboard traversal of a scan tree.
/// </summary>
public sealed class ScanTreeNavigator
{
    private readonly Dictionary<ScanNode, ScanNode?> _parents = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ScanNode, IReadOnlyList<ScanNode>> _children = new(ReferenceEqualityComparer.Instance);

    public ScanTreeNavigator(ScanNode root, ScanNode? current = null)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        IndexTree(root, parent: null);

        Current = current ?? root;
        if (!_parents.ContainsKey(Current))
        {
            throw new ArgumentException("Current node must exist in the provided root tree.", nameof(current));
        }
    }

    public ScanNode Root { get; }

    public ScanNode Current { get; private set; }

    public bool TryMove(TreeNavigationCommand command) =>
        command switch
        {
            TreeNavigationCommand.PreviousSibling => TryMoveSibling(-1),
            TreeNavigationCommand.NextSibling => TryMoveSibling(1),
            TreeNavigationCommand.ZoomIn => TryZoomIn(),
            TreeNavigationCommand.ZoomOut => TryZoomOut(),
            _ => false
        };

    private void IndexTree(ScanNode node, ScanNode? parent)
    {
        _parents[node] = parent;

        var orderedChildren = node.Children
            .Where(child => child.TotalSize > 0)
            .OrderByDescending(child => child.TotalSize)
            .ThenBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _children[node] = orderedChildren;

        foreach (var child in orderedChildren)
        {
            IndexTree(child, node);
        }
    }

    private bool TryMoveSibling(int delta)
    {
        if (!_parents.TryGetValue(Current, out var parent) || parent is null)
        {
            return false;
        }

        var siblings = _children[parent];
        if (siblings.Count <= 1)
        {
            return false;
        }

        var index = IndexOf(siblings, Current);
        if (index < 0)
        {
            return false;
        }

        var next = (index + delta + siblings.Count) % siblings.Count;
        Current = siblings[next];
        return true;
    }

    private bool TryZoomIn()
    {
        var children = _children[Current];
        if (children.Count == 0)
        {
            return false;
        }

        Current = children[0];
        return true;
    }

    private bool TryZoomOut()
    {
        if (!_parents.TryGetValue(Current, out var parent) || parent is null)
        {
            return false;
        }

        Current = parent;
        return true;
    }

    private static int IndexOf(IReadOnlyList<ScanNode> nodes, ScanNode target)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], target))
            {
                return i;
            }
        }

        return -1;
    }
}
