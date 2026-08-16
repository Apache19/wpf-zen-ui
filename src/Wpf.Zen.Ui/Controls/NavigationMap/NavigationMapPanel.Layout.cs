// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Contains hierarchy construction, automatic/manual placement, connector aggregation, and
/// geometry creation for <see cref="NavigationMapPanel"/>.
/// </summary>
public partial class NavigationMapPanel
{
    private LayoutForest BuildForest()
    {
        var nodes = new List<LayoutNode>(InternalChildren.Count);
        var firstNodeByKey = new Dictionary<string, LayoutNode>(StringComparer.Ordinal);
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                if (child is NavigationMapItem collapsedItem)
                {
                    collapsedItem.SetLayoutPosition(0, true, 1, 1);
                }

                continue;
            }

            var node = new LayoutNode(child);
            nodes.Add(node);
            if (node.Key is { Length: > 0 } key && !firstNodeByKey.ContainsKey(key))
            {
                firstNodeByKey.Add(key, node);
            }
        }

        foreach (LayoutNode node in nodes)
        {
            if (
                node.ParentKey is { Length: > 0 } parentKey
                && firstNodeByKey.TryGetValue(parentKey, out LayoutNode? parent)
                && !ReferenceEquals(parent, node)
            )
            {
                node.Parent = parent;
            }
        }

        BreakCycles(nodes);
        foreach (LayoutNode node in nodes)
        {
            node.Parent?.Children.Add(node);
        }

        var roots = nodes.Where(static node => node.Parent is null).ToList();
        var levelLengths = new List<double>();
        for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
        {
            AssignDepthAndCollectLevelLengths(
                roots[rootIndex],
                0,
                rootIndex + 1,
                roots.Count,
                levelLengths
            );
            CalculateSubtreeBreadth(roots[rootIndex]);
        }

        var levelOffsets = new double[levelLengths.Count];
        double offset = 0D;
        for (int index = 0; index < levelLengths.Count; index++)
        {
            levelOffsets[index] = offset;
            offset += levelLengths[index] + LevelSpacing;
        }

        return new LayoutForest(nodes, roots, levelLengths, levelOffsets);
    }

    private static void BreakCycles(IReadOnlyList<LayoutNode> nodes)
    {
        var completed = new HashSet<LayoutNode>();
        foreach (LayoutNode origin in nodes)
        {
            if (completed.Contains(origin))
            {
                continue;
            }

            var path = new List<LayoutNode>();
            var indices = new Dictionary<LayoutNode, int>();
            LayoutNode? current = origin;
            while (current is not null && !completed.Contains(current))
            {
                if (indices.TryGetValue(current, out int cycleStart))
                {
                    for (int index = cycleStart; index < path.Count; index++)
                    {
                        path[index].Parent = null;
                    }

                    break;
                }

                indices.Add(current, path.Count);
                path.Add(current);
                current = current.Parent;
            }

            foreach (LayoutNode visited in path)
            {
                completed.Add(visited);
            }
        }
    }

    private void AssignDepthAndCollectLevelLengths(
        LayoutNode node,
        int depth,
        int positionInSet,
        int sizeOfSet,
        List<double> levelLengths)
    {
        node.Depth = depth;
        if (node.Element is NavigationMapItem item)
        {
            item.SetLayoutPosition(depth, depth == 0, positionInSet, sizeOfSet);
        }

        while (levelLengths.Count <= depth)
        {
            levelLengths.Add(0D);
        }

        levelLengths[depth] = Math.Max(levelLengths[depth], GetDepthLength(node.Element.DesiredSize));
        for (int index = 0; index < node.Children.Count; index++)
        {
            AssignDepthAndCollectLevelLengths(
                node.Children[index],
                depth + 1,
                index + 1,
                node.Children.Count,
                levelLengths
            );
        }
    }

    private double CalculateSubtreeBreadth(LayoutNode node)
    {
        double childrenBreadth = node.Children.Sum(CalculateSubtreeBreadth);
        if (node.Children.Count > 1)
        {
            childrenBreadth += BranchSpacing * (node.Children.Count - 1);
        }

        node.ChildrenBreadth = childrenBreadth;
        node.SubtreeBreadth = Math.Max(GetBreadth(node.Element.DesiredSize), childrenBreadth);
        return node.SubtreeBreadth;
    }

    private Size CalculateForestSize(LayoutForest forest)
    {
        double breadth = forest.Roots.Sum(static root => root.SubtreeBreadth);
        if (forest.Roots.Count > 1)
        {
            breadth += BranchSpacing * (forest.Roots.Count - 1);
        }

        double depth = forest.LevelLengths.Sum();
        if (forest.LevelLengths.Count > 1)
        {
            depth += LevelSpacing * (forest.LevelLengths.Count - 1);
        }

        return Direction == NavigationMapDirection.TopToBottom
            ? new Size(breadth, depth)
            : new Size(depth, breadth);
    }

    private LayoutResult CreateHierarchicalPositionedLayout(LayoutForest forest)
    {
        Size size = CalculateForestSize(forest);
        var positions = new Dictionary<LayoutNode, Point>();
        double breadth = 0D;
        foreach (LayoutNode root in forest.Roots)
        {
            CollectSubtreePositions(root, breadth, forest.LevelOffsets, positions);
            breadth += root.SubtreeBreadth + BranchSpacing;
        }

        return new LayoutResult(size, positions);
    }

    private void CollectSubtreePositions(
        LayoutNode node,
        double subtreeBreadth,
        IReadOnlyList<double> levelOffsets,
        Dictionary<LayoutNode, Point> positions)
    {
        Size size = node.Element.DesiredSize;
        double breadth = subtreeBreadth + ((node.SubtreeBreadth - GetBreadth(size)) / 2D);
        positions[node] = ToPhysicalPoint(breadth, levelOffsets[node.Depth]);

        double childBreadth = subtreeBreadth + ((node.SubtreeBreadth - node.ChildrenBreadth) / 2D);
        foreach (LayoutNode child in node.Children)
        {
            CollectSubtreePositions(child, childBreadth, levelOffsets, positions);
            childBreadth += child.SubtreeBreadth + BranchSpacing;
        }
    }

    private LayoutResult CreateManualLayout(LayoutForest forest)
    {
        LayoutResult automatic = CreateHierarchicalPositionedLayout(forest);
        var positions = new Dictionary<LayoutNode, Point>(automatic.Positions);
        if (LayoutDefinition is { } definition)
        {
            foreach (LayoutNode node in forest.Nodes)
            {
                if (
                    node.Key is { Length: > 0 } key
                    && definition.TryGetPlacement(key, out NavigationMapPlacement? placement)
                    && placement is { IsPinned: true }
                )
                {
                    positions[node] = GetManualSlotPosition(node.Element, placement.Position);
                }
            }
        }

        // ScrollViewer content has a non-negative origin. Imported negative coordinates are shifted
        // as one rigid layout so their relative geometry remains intact and no card is clipped.
        double minimumX = positions.Count == 0 ? 0D : positions.Min(static pair => pair.Value.X);
        double minimumY = positions.Count == 0 ? 0D : positions.Min(static pair => pair.Value.Y);
        var shift = new Vector(Math.Max(0D, -minimumX), Math.Max(0D, -minimumY));
        if (shift != default)
        {
            foreach (LayoutNode node in positions.Keys.ToArray())
            {
                positions[node] += shift;
            }
        }

        double width = 0D;
        double height = 0D;
        foreach (KeyValuePair<LayoutNode, Point> pair in positions)
        {
            width = Math.Max(width, pair.Value.X + pair.Key.Element.DesiredSize.Width);
            height = Math.Max(height, pair.Value.Y + pair.Key.Element.DesiredSize.Height);
        }

        return new LayoutResult(new Size(width, height), positions);
    }

    private static Point GetManualSlotPosition(UIElement element, Point visualCenter)
    {
        Size slot = element.DesiredSize;
        if (element is not FrameworkElement frameworkElement)
        {
            return new Point(visualCenter.X - (slot.Width / 2D), visualCenter.Y - (slot.Height / 2D));
        }

        Thickness margin = frameworkElement.Margin;
        double width = Math.Max(0D, slot.Width - margin.Left - margin.Right);
        double height = Math.Max(0D, slot.Height - margin.Top - margin.Bottom);
        return new Point(
            visualCenter.X - (width / 2D) - margin.Left,
            visualCenter.Y - (height / 2D) - margin.Top
        );
    }

    private void ArrangeHierarchicalLayout(LayoutForest forest, Size finalSize)
    {
        Size desired = CalculateForestSize(forest);
        double availableBreadth = Direction == NavigationMapDirection.TopToBottom
            ? Math.Max(finalSize.Width, desired.Width)
            : Math.Max(finalSize.Height, desired.Height);
        double breadth = Math.Max(0D, (availableBreadth - GetBreadth(desired)) / 2D);
        foreach (LayoutNode root in forest.Roots)
        {
            ArrangeSubtree(root, breadth, forest.LevelOffsets);
            breadth += root.SubtreeBreadth + BranchSpacing;
        }
    }

    private void ArrangeSubtree(LayoutNode node, double subtreeBreadth, IReadOnlyList<double> levelOffsets)
    {
        Size size = node.Element.DesiredSize;
        double breadth = subtreeBreadth + ((node.SubtreeBreadth - GetBreadth(size)) / 2D);
        node.Element.Arrange(new Rect(ToPhysicalPoint(breadth, levelOffsets[node.Depth]), size));
        node.ArrangedBounds = GetArrangedBounds(node.Element);

        double childBreadth = subtreeBreadth + ((node.SubtreeBreadth - node.ChildrenBreadth) / 2D);
        foreach (LayoutNode child in node.Children)
        {
            ArrangeSubtree(child, childBreadth, levelOffsets);
            childBreadth += child.SubtreeBreadth + BranchSpacing;
        }
    }

    private static void ArrangePositionedLayout(LayoutForest forest, LayoutResult layout)
    {
        foreach (LayoutNode node in forest.Nodes)
        {
            node.Element.Arrange(new Rect(layout.Positions[node], node.Element.DesiredSize));
            node.ArrangedBounds = GetArrangedBounds(node.Element);
        }
    }

    private void RouteConnectors(LayoutForest forest)
    {
        if (forest.Nodes.Count < 2)
        {
            return;
        }

        var router = new OrthogonalRouter(
            forest.Nodes,
            RoutingClearance,
            LayoutMode == NavigationMapLayoutMode.Hierarchical ? Direction : null
        );
        foreach (LayoutNode parent in forest.Nodes)
        {
            if (parent.Children.Count == 0)
            {
                continue;
            }

            // Sharing is local to one immediate fan-out. Treating the entire forest as one network
            // made an already-routed trunk artificially cheap for unrelated roots and for the next
            // hierarchy level, which visually merged independent semantic relationships.
            router.BeginNetwork();
            var routedEdges = new HashSet<EdgeIdentity>();
            int firstRouteIndex = _connectorRoutes.Count;
            TryRouteCollinearSiblingBus(parent, forest.Nodes, routedEdges);
            for (int index = firstRouteIndex; index < _connectorRoutes.Count; index++)
            {
                foreach (ConnectorSegment segment in _connectorRoutes[index].Segments)
                {
                    router.Commit([segment.StartPoint, segment.EndPoint]);
                }
            }

            foreach (LayoutNode child in parent.Children)
            {
                if (routedEdges.Contains(new EdgeIdentity(parent, child)))
                {
                    continue;
                }

                IReadOnlyList<Point> points = router.FindRoute(parent, child);
                AddConnectorRoute(parent, child, points);
                router.Commit(points);
            }
        }
    }

    /// <summary>
    /// Recognizes the useful editor case where one parent and several children share a visual row
    /// or column. A bus is clearer than independent detours and is emitted once after normalization.
    /// </summary>
    private void TryRouteCollinearSiblingBus(
        LayoutNode parent,
        IReadOnlyList<LayoutNode> allNodes,
        HashSet<EdgeIdentity> routedEdges)
    {
        if (parent.Children.Count < 2)
        {
            return;
        }

        var group = new List<LayoutNode> { parent };
        group.AddRange(parent.Children);
        HashSet<LayoutNode> members = [.. group];
        IEnumerable<bool> orientations = LayoutMode == NavigationMapLayoutMode.Hierarchical
            ? [Direction == NavigationMapDirection.TopToBottom]
            : new[] { true, false }.OrderBy(horizontal => GetCollinearityRatio(group, horizontal));
        foreach (bool horizontal in orientations)
        {
            // Direction is a hierarchy-layout input, not an editor-routing constraint. In Manual
            // mode the cards may form either a row or a column regardless of the layout from which
            // their initial positions came, so detect the bus axis from the current coordinates.
            if (GetCollinearityRatio(group, horizontal) > 0.5D)
            {
                continue;
            }

            double beforeLane = horizontal
                ? group.Min(static node => node.ArrangedBounds.Top) - RoutingClearance
                : group.Min(static node => node.ArrangedBounds.Left) - RoutingClearance;
            double afterLane = horizontal
                ? group.Max(static node => node.ArrangedBounds.Bottom) + RoutingClearance
                : group.Max(static node => node.ArrangedBounds.Right) + RoutingClearance;
            foreach (double lane in new[] { beforeLane, afterLane }.OrderBy(static candidate => candidate < 0D))
            {
                if (lane < 0D || !IsBusClear(group, members, allNodes, horizontal, lane))
                {
                    continue;
                }

                foreach (LayoutNode child in parent.Children)
                {
                    Point parentAnchor = GetBusAnchor(parent.ArrangedBounds, horizontal, lane);
                    Point childAnchor = GetBusAnchor(child.ArrangedBounds, horizontal, lane);
                    IReadOnlyList<Point> points = horizontal
                        ? [parentAnchor, new Point(parentAnchor.X, lane), new Point(childAnchor.X, lane), childAnchor]
                        : [parentAnchor, new Point(lane, parentAnchor.Y), new Point(lane, childAnchor.Y), childAnchor];
                    AddConnectorRoute(parent, child, SimplifyPath(points));
                    routedEdges.Add(new EdgeIdentity(parent, child));
                }

                return;
            }
        }
    }

    private static double GetCollinearityRatio(IReadOnlyList<LayoutNode> group, bool horizontal)
    {
        double centerMinimum = group.Min(node => horizontal
            ? Center(node.ArrangedBounds).Y
            : Center(node.ArrangedBounds).X);
        double centerMaximum = group.Max(node => horizontal
            ? Center(node.ArrangedBounds).Y
            : Center(node.ArrangedBounds).X);
        double maximumLength = group.Max(node => horizontal
            ? node.ArrangedBounds.Height
            : node.ArrangedBounds.Width);
        return maximumLength > 0D
            ? (centerMaximum - centerMinimum) / maximumLength
            : double.PositiveInfinity;
    }

    private bool IsBusClear(
        IReadOnlyList<LayoutNode> group,
        HashSet<LayoutNode> members,
        IReadOnlyList<LayoutNode> allNodes,
        bool horizontal,
        double lane)
    {
        double minimum = group.Min(node => horizontal ? Center(node.ArrangedBounds).X : Center(node.ArrangedBounds).Y);
        double maximum = group.Max(node => horizontal ? Center(node.ArrangedBounds).X : Center(node.ArrangedBounds).Y);
        Point busStart = horizontal ? new Point(minimum, lane) : new Point(lane, minimum);
        Point busEnd = horizontal ? new Point(maximum, lane) : new Point(lane, maximum);
        var proposedSegments = new List<ConnectorSegment> { ConnectorSegment.Create(busStart, busEnd) };
        foreach (LayoutNode member in group)
        {
            Point anchor = GetBusAnchor(member.ArrangedBounds, horizontal, lane);
            Point junction = horizontal ? new Point(anchor.X, lane) : new Point(lane, anchor.Y);
            if (anchor != junction)
            {
                proposedSegments.Add(ConnectorSegment.Create(anchor, junction));
            }
        }

        // The dedicated sibling-bus shortcut does not run through Dijkstra, so apply the same net
        // isolation rule explicitly. A later parent may cross an existing line, but it must not lay
        // a positive-length trunk directly on top of an unrelated parent network.
        if (
            proposedSegments.Any(proposed =>
                _connectorRoutes
                    .SelectMany(static route => route.Segments)
                    .Any(existing => OverlapsCollinearly(proposed, existing))
            )
        )
        {
            return false;
        }

        foreach (LayoutNode obstacle in allNodes)
        {
            if (members.Contains(obstacle))
            {
                continue;
            }

            Rect bounds = obstacle.ArrangedBounds;
            bounds.Inflate(RoutingClearance, RoutingClearance);
            if (SegmentIntersectsRectangleInterior(busStart, busEnd, bounds))
            {
                return false;
            }

            foreach (LayoutNode member in group)
            {
                Point anchor = GetBusAnchor(member.ArrangedBounds, horizontal, lane);
                Point junction = horizontal ? new Point(anchor.X, lane) : new Point(lane, anchor.Y);
                if (SegmentIntersectsRectangleInterior(anchor, junction, bounds))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool OverlapsCollinearly(ConnectorSegment first, ConnectorSegment second) =>
        first.IsHorizontal == second.IsHorizontal
        && first.Offset == second.Offset
        && Math.Min(first.End, second.End) > Math.Max(first.Start, second.Start);

    private static Point GetBusAnchor(Rect bounds, bool horizontal, double lane)
    {
        Point center = Center(bounds);
        return horizontal
            ? new Point(center.X, lane < bounds.Top ? bounds.Top : bounds.Bottom)
            : new Point(lane < bounds.Left ? bounds.Left : bounds.Right, center.Y);
    }

    private void AddConnectorRoute(LayoutNode parent, LayoutNode child, IReadOnlyList<Point> points)
    {
        var segments = new List<ConnectorSegment>();
        for (int index = 1; index < points.Count; index++)
        {
            if (points[index - 1] == points[index])
            {
                continue;
            }

            ConnectorSegment segment = ConnectorSegment.Create(points[index - 1], points[index]);
            segments.Add(segment);
            _connectorSegments.Add(segment);
        }

        _connectorRoutes.Add(new ConnectorRoute(parent.Key, child.Key, segments));
    }

    private void NormalizeConnectorSegments()
    {
        List<ConnectorSegment> normalized = NormalizeSegments(_connectorSegments);
        _connectorSegments.Clear();
        _connectorSegments.AddRange(normalized);
    }

    private static List<ConnectorSegment> NormalizeSegments(IEnumerable<ConnectorSegment> source)
    {
        var byLine = new Dictionary<ConnectorLine, List<ConnectorSegment>>();
        foreach (ConnectorSegment segment in source)
        {
            var line = new ConnectorLine(segment.IsHorizontal, segment.Offset);
            if (!byLine.TryGetValue(line, out List<ConnectorSegment>? intervals))
            {
                intervals = [];
                byLine.Add(line, intervals);
            }

            intervals.Add(segment);
        }

        var result = new List<ConnectorSegment>();
        foreach (List<ConnectorSegment> intervals in byLine.Values)
        {
            intervals.Sort(static (left, right) => left.Start.CompareTo(right.Start));
            ConnectorSegment merged = intervals[0];
            for (int index = 1; index < intervals.Count; index++)
            {
                ConnectorSegment candidate = intervals[index];
                if (candidate.Start <= merged.End)
                {
                    merged = merged.WithEnd(Math.Max(merged.End, candidate.End));
                }
                else
                {
                    result.Add(merged);
                    merged = candidate;
                }
            }

            result.Add(merged);
        }

        return result;
    }

    private Geometry CreateHighlightedConnectorGeometry(string nodeKey)
    {
        // Route identity is retained after ordinary lines are merged. Filtering therefore covers
        // only the selected node's immediate parent and children, never another hierarchy level.
        List<ConnectorSegment> segments = NormalizeSegments(
            _connectorRoutes
                .Where(route =>
                    StringComparer.Ordinal.Equals(route.ParentKey, nodeKey)
                    || StringComparer.Ordinal.Equals(route.ChildKey, nodeKey)
                )
                .SelectMany(static route => route.Segments)
        );
        return segments.Count == 0
            ? Geometry.Empty
            : CreateConnectorGeometry(segments, HighlightedConnectorThickness);
    }

    private static Geometry CreateConnectorGeometry(
        IReadOnlyList<ConnectorSegment> segments,
        double thickness)
    {
        double radius = thickness / 2D;
        var geometry = new GeometryGroup { FillRule = FillRule.Nonzero };
        var caps = new HashSet<Point>();
        foreach (ConnectorSegment segment in segments)
        {
            Rect bounds = segment.IsHorizontal
                ? new Rect(segment.Start, segment.Offset - radius, segment.Length, thickness)
                : new Rect(segment.Offset - radius, segment.Start, thickness, segment.Length);
            geometry.Children.Add(new RectangleGeometry(bounds));
            caps.Add(segment.StartPoint);
            caps.Add(segment.EndPoint);
        }

        foreach (Point center in caps)
        {
            geometry.Children.Add(new EllipseGeometry(center, radius, radius));
        }

        if (geometry.CanFreeze)
        {
            geometry.Freeze();
        }

        return geometry;
    }

    private static Rect GetArrangedBounds(UIElement element)
    {
        // DesiredSize contains Margin, whereas visual offset and RenderSize describe the actual
        // card. Routing against the slot would make endpoints appear to stop before the card edge.
        Vector offset = VisualTreeHelper.GetOffset(element);
        return new Rect(offset.X, offset.Y, element.RenderSize.Width, element.RenderSize.Height);
    }

    private static Point Center(Rect bounds) =>
        new(bounds.Left + (bounds.Width / 2D), bounds.Top + (bounds.Height / 2D));

    private static bool SegmentIntersectsRectangleInterior(Point first, Point second, Rect bounds)
    {
        const double epsilon = 0.0001D;
        if (Math.Abs(first.X - second.X) < epsilon)
        {
            return first.X > bounds.Left + epsilon
                && first.X < bounds.Right - epsilon
                && Math.Max(first.Y, second.Y) > bounds.Top + epsilon
                && Math.Min(first.Y, second.Y) < bounds.Bottom - epsilon;
        }

        return first.Y > bounds.Top + epsilon
            && first.Y < bounds.Bottom - epsilon
            && Math.Max(first.X, second.X) > bounds.Left + epsilon
            && Math.Min(first.X, second.X) < bounds.Right - epsilon;
    }

    private static IReadOnlyList<Point> SimplifyPath(IReadOnlyList<Point> path)
    {
        var result = new List<Point>(path.Count);
        foreach (Point point in path)
        {
            if (result.Count > 0 && result[^1] == point)
            {
                continue;
            }

            while (result.Count >= 2)
            {
                Point first = result[^2];
                Point second = result[^1];
                bool collinear = (first.X == second.X && second.X == point.X)
                    || (first.Y == second.Y && second.Y == point.Y);
                if (!collinear)
                {
                    break;
                }

                result.RemoveAt(result.Count - 1);
            }

            result.Add(point);
        }

        return result;
    }

    private void ClearGeometryCaches()
    {
        _connectorGeometry = null;
        _highlightedConnectorGeometry = null;
    }
}
