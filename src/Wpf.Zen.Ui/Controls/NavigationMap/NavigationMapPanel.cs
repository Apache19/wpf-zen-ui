// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Media;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Arranges navigation-map item containers as a centered organization-chart forest and draws
/// direction-aware connectors.
/// </summary>
/// <remarks>
/// <para>
/// The panel consumes the flat <see cref="NavigationMapItem.NodeKey"/> and
/// <see cref="NavigationMapItem.ParentKey"/> metadata prepared by <see cref="NavigationMapView"/>.
/// Each subtree reserves the greater of its root breadth and the combined breadth of its children.
/// Breadth means width in top-to-bottom mode and height in left-to-right mode; this logical-axis
/// model prevents sibling branches from overlapping while keeping parents centered in either
/// direction without transforming node visuals.
/// </para>
/// <para>
/// This is intentionally a non-virtualizing panel. A site map must measure every node to calculate
/// stable subtree extents and connector endpoints. Place very large route inventories behind
/// filtering, grouping, or progressive disclosure rather than presenting thousands of nodes in a
/// single organization chart.
/// </para>
/// </remarks>
public class NavigationMapPanel : Panel
{
    /// <summary>Identifies the <see cref="Direction"/> dependency property.</summary>
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
        nameof(Direction),
        typeof(NavigationMapDirection),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(
            NavigationMapDirection.TopToBottom,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange
                | FrameworkPropertyMetadataOptions.AffectsRender
        ),
        ValidateDirection
    );

    /// <summary>Identifies the <see cref="HorizontalSpacing"/> dependency property.</summary>
    public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
        nameof(HorizontalSpacing),
        typeof(double),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(
            32D,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange
        ),
        ValidateHorizontalSpacing
    );

    /// <summary>Identifies the <see cref="VerticalSpacing"/> dependency property.</summary>
    public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
        nameof(VerticalSpacing),
        typeof(double),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(
            44D,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange
                | FrameworkPropertyMetadataOptions.AffectsRender
        ),
        ValidateVerticalSpacing
    );

    /// <summary>Identifies the <see cref="ConnectorBrush"/> dependency property.</summary>
    public static readonly DependencyProperty ConnectorBrushProperty = DependencyProperty.Register(
        nameof(ConnectorBrush),
        typeof(Brush),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>Identifies the <see cref="ConnectorThickness"/> dependency property.</summary>
    public static readonly DependencyProperty ConnectorThicknessProperty = DependencyProperty.Register(
        nameof(ConnectorThickness),
        typeof(double),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(
            1D,
            FrameworkPropertyMetadataOptions.AffectsRender,
            OnConnectorThicknessChanged
        ),
        ValidateConnectorThickness
    );

    private readonly List<ConnectorSegment> _connectorSegments = [];
    private Geometry? _connectorGeometry;
    private LayoutForest? _measuredForest;

    /// <summary>Gets the normalized connector topology retained by the most recent arrangement.</summary>
    /// <remarks>
    /// This internal view exists so layout tests can verify that shared trunks are physically
    /// emitted once; rendering correctness should not depend on sampling anti-aliased pixels.
    /// </remarks>
    internal IReadOnlyList<ConnectorSegment> ConnectorSegments => _connectorSegments;

    /// <summary>Gets or sets the direction in which hierarchy levels advance.</summary>
    /// <remarks>
    /// The default is <see cref="NavigationMapDirection.TopToBottom"/>. The panel performs a true
    /// axis-aware layout rather than applying a visual transform, so node content remains upright
    /// and hit testing continues to use ordinary WPF layout coordinates.
    /// </remarks>
    public NavigationMapDirection Direction
    {
        get => (NavigationMapDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>Gets or sets the horizontal layout gap.</summary>
    /// <remarks>
    /// This separates sibling subtrees in top-to-bottom mode and adjacent hierarchy levels in
    /// left-to-right mode.
    /// </remarks>
    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    /// <summary>Gets or sets the vertical layout gap.</summary>
    /// <remarks>
    /// This separates hierarchy levels in top-to-bottom mode and sibling subtrees in left-to-right
    /// mode.
    /// </remarks>
    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    /// <summary>Gets or sets the brush used to draw parent-child connectors.</summary>
    public Brush? ConnectorBrush
    {
        get => (Brush?)GetValue(ConnectorBrushProperty);
        set => SetValue(ConnectorBrushProperty, value);
    }

    /// <summary>Gets or sets the connector-line thickness.</summary>
    public double ConnectorThickness
    {
        get => (double)GetValue(ConnectorThicknessProperty);
        set => SetValue(ConnectorThicknessProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var measureConstraint = new Size(
            double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width,
            double.PositiveInfinity
        );

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(measureConstraint);
        }

        LayoutForest forest = BuildForest();
        // Measure establishes the hierarchy and logical subtree extents. Arrange only changes the
        // forest's origin when its final slot changes, so retaining this immutable topology avoids
        // rebuilding dictionaries and tree nodes twice during every normal WPF layout pass.
        _measuredForest = forest;
        return CalculateForestSize(forest);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // WPF normally measures before arranging. The fallback keeps the panel well-behaved when a
        // custom host invokes Arrange directly or layout is resumed from an unusual lifecycle.
        LayoutForest forest = _measuredForest ?? BuildForest();
        // Reuse only across the immediate Measure-to-Arrange handoff. Retaining the topology beyond
        // that point could make a later arrange-only call observe stale child metadata.
        _measuredForest = null;
        Size desiredSize = CalculateForestSize(forest);
        double availableBreadth = Direction == NavigationMapDirection.TopToBottom
            ? Math.Max(finalSize.Width, desiredSize.Width)
            : Math.Max(finalSize.Height, desiredSize.Height);
        double desiredBreadth = GetBreadth(desiredSize);
        double startBreadth = Math.Max(0D, (availableBreadth - desiredBreadth) / 2D);

        _connectorSegments.Clear();
        double rootBreadth = startBreadth;
        foreach (LayoutNode root in forest.Roots)
        {
            ArrangeSubtree(root, rootBreadth, forest.LevelOffsets);
            rootBreadth += root.SubtreeBreadth + BranchSpacing;
        }

        MergeConnectorSegments();
        // Geometry depends on the newly arranged physical node bounds. Keep segment calculation in
        // Arrange, but create the Freezable lazily only when WPF actually requests a render pass.
        _connectorGeometry = null;

        InvalidateVisual();
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        Brush? brush = ConnectorBrush;
        if (brush is null || _connectorSegments.Count == 0)
        {
            return;
        }

        // A Pen would widen every subfigure independently. Even after logical segment deduplication,
        // round caps and perpendicular joins would then overlap when rendered with a translucent
        // brush. Build one non-zero filled geometry instead: the brush is composited exactly once
        // across the union-like area, so trunks, branch joins, and end caps retain one opacity.
        _connectorGeometry ??= CreateConnectorGeometry();
        drawingContext.DrawGeometry(brush, null, _connectorGeometry);
    }

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
                    // A recycled or dynamically collapsed container must not continue exposing a
                    // depth from the previous visible graph through styling or automation helpers.
                    collapsedItem.SetLayoutPosition(0, true, 1, 1);
                }

                continue;
            }

            var node = new LayoutNode(child);
            nodes.Add(node);
            if (
                node.Key is { Length: > 0 } key
                && !firstNodeByKey.ContainsKey(key)
            )
            {
                // NavigationMap rejects duplicate keys. Directly supplied containers can still be
                // temporarily invalid while edited in a designer, so the first declaration remains
                // the deterministic parent target and every duplicate stays independently visible.
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
            LayoutNode root = roots[rootIndex];
            AssignDepthAndCollectLevelLengths(
                root,
                0,
                rootIndex + 1,
                roots.Count,
                levelLengths
            );
            CalculateSubtreeBreadth(root);
        }

        var levelOffsets = new double[levelLengths.Count];
        double depthOffset = 0D;
        for (int index = 0; index < levelLengths.Count; index++)
        {
            levelOffsets[index] = depthOffset;
            depthOffset += levelLengths[index] + LevelSpacing;
        }

        return new LayoutForest(roots, levelLengths, levelOffsets);
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
            var pathIndices = new Dictionary<LayoutNode, int>();
            LayoutNode? current = origin;
            while (current is not null && !completed.Contains(current))
            {
                if (pathIndices.TryGetValue(current, out int cycleStart))
                {
                    // Promote every cycle participant, not merely the first encountered edge. This
                    // avoids presenting arbitrary ancestry that changes with item declaration order.
                    for (int index = cycleStart; index < path.Count; index++)
                    {
                        path[index].Parent = null;
                    }

                    break;
                }

                pathIndices.Add(current, path.Count);
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
        for (int childIndex = 0; childIndex < node.Children.Count; childIndex++)
        {
            AssignDepthAndCollectLevelLengths(
                node.Children[childIndex],
                depth + 1,
                childIndex + 1,
                node.Children.Count,
                levelLengths
            );
        }
    }

    private double CalculateSubtreeBreadth(LayoutNode node)
    {
        double childrenBreadth = 0D;
        foreach (LayoutNode child in node.Children)
        {
            childrenBreadth += CalculateSubtreeBreadth(child);
        }

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

        double depthLength = forest.LevelLengths.Sum();
        if (forest.LevelLengths.Count > 1)
        {
            depthLength += LevelSpacing * (forest.LevelLengths.Count - 1);
        }

        return Direction == NavigationMapDirection.TopToBottom
            ? new Size(breadth, depthLength)
            : new Size(depthLength, breadth);
    }

    private void ArrangeSubtree(
        LayoutNode node,
        double subtreeBreadth,
        IReadOnlyList<double> levelOffsets)
    {
        Size nodeSize = node.Element.DesiredSize;
        double nodeBreadth = subtreeBreadth
            + ((node.SubtreeBreadth - GetBreadth(nodeSize)) / 2D);
        double nodeDepth = levelOffsets[node.Depth];
        Point nodePosition = ToPhysicalPoint(nodeBreadth, nodeDepth);
        node.Element.Arrange(new Rect(nodePosition, nodeSize));
        node.ArrangedBounds = GetArrangedBounds(node.Element);

        double childrenBreadth = subtreeBreadth
            + ((node.SubtreeBreadth - node.ChildrenBreadth) / 2D);
        foreach (LayoutNode child in node.Children)
        {
            ArrangeSubtree(child, childrenBreadth, levelOffsets);
            AddConnectorSegments(node.ArrangedBounds, child.ArrangedBounds);
            childrenBreadth += child.SubtreeBreadth + BranchSpacing;
        }
    }

    private void AddConnectorSegments(Rect parentBounds, Rect childBounds)
    {
        Point parent;
        Point child;
        Point firstCorner;
        Point secondCorner;
        if (Direction == NavigationMapDirection.TopToBottom)
        {
            parent = new Point(
                parentBounds.Left + (parentBounds.Width / 2D),
                parentBounds.Bottom
            );
            child = new Point(childBounds.Left + (childBounds.Width / 2D), childBounds.Top);
            double middleY = parent.Y + ((child.Y - parent.Y) / 2D);
            firstCorner = new Point(parent.X, middleY);
            secondCorner = new Point(child.X, middleY);
        }
        else
        {
            parent = new Point(
                parentBounds.Right,
                parentBounds.Top + (parentBounds.Height / 2D)
            );
            child = new Point(childBounds.Left, childBounds.Top + (childBounds.Height / 2D));
            double middleX = parent.X + ((child.X - parent.X) / 2D);
            firstCorner = new Point(middleX, parent.Y);
            secondCorner = new Point(middleX, child.Y);
        }

        AddConnectorSegment(parent, firstCorner);
        AddConnectorSegment(firstCorner, secondCorner);
        AddConnectorSegment(secondCorner, child);
    }

    private static Rect GetArrangedBounds(UIElement element)
    {
        // FrameworkElement.DesiredSize includes Margin, while RenderSize and VisualOffset describe
        // the actual arranged visual. Connector endpoints must use the latter or an asymmetric
        // container margin leaves lines attached to the invisible layout slot. RenderTransform is
        // intentionally excluded: like every WPF panel, this panel connects layout positions;
        // transient presentation transforms do not participate in measure or arrange.
        Vector offset = VisualTreeHelper.GetOffset(element);
        return new Rect(offset.X, offset.Y, element.RenderSize.Width, element.RenderSize.Height);
    }

    private void AddConnectorSegment(Point first, Point second)
    {
        if (first == second)
        {
            return;
        }

        _connectorSegments.Add(ConnectorSegment.Create(first, second));
    }

    /// <summary>
    /// Coalesces every collinear overlapping or touching interval after arrangement has produced
    /// the complete connector topology.
    /// </summary>
    /// <remarks>
    /// Sibling routes deliberately share their parent trunk and often part of their branch bar.
    /// Normalizing globally, rather than remembering only the most recently emitted edge, also
    /// handles declaration-order changes and coincident edges from separately arranged subtrees.
    /// Coordinates come from the same layout calculation, so exact line coordinates are the right
    /// identity: using a tolerance could incorrectly merge nearby one-pixel branches at high DPI.
    /// </remarks>
    private void MergeConnectorSegments()
    {
        if (_connectorSegments.Count < 2)
        {
            return;
        }

        var intervalsByLine = new Dictionary<ConnectorLine, List<ConnectorSegment>>();
        foreach (ConnectorSegment segment in _connectorSegments)
        {
            var line = new ConnectorLine(segment.IsHorizontal, segment.Offset);
            if (!intervalsByLine.TryGetValue(line, out List<ConnectorSegment>? intervals))
            {
                intervals = [];
                intervalsByLine.Add(line, intervals);
            }

            intervals.Add(segment);
        }

        _connectorSegments.Clear();
        foreach (List<ConnectorSegment> intervals in intervalsByLine.Values)
        {
            intervals.Sort(static (left, right) => left.Start.CompareTo(right.Start));
            ConnectorSegment merged = intervals[0];
            for (int index = 1; index < intervals.Count; index++)
            {
                ConnectorSegment candidate = intervals[index];
                if (candidate.Start <= merged.End)
                {
                    merged = merged.WithEnd(Math.Max(merged.End, candidate.End));
                    continue;
                }

                _connectorSegments.Add(merged);
                merged = candidate;
            }

            _connectorSegments.Add(merged);
        }
    }

    private Geometry CreateConnectorGeometry()
    {
        double radius = ConnectorThickness / 2D;
        var geometry = new GeometryGroup { FillRule = FillRule.Nonzero };
        var capCenters = new HashSet<Point>();

        foreach (ConnectorSegment segment in _connectorSegments)
        {
            Rect bounds = segment.IsHorizontal
                ? new Rect(segment.Start, segment.Offset - radius, segment.Length, ConnectorThickness)
                : new Rect(segment.Offset - radius, segment.Start, ConnectorThickness, segment.Length);
            geometry.Children.Add(new RectangleGeometry(bounds));

            // Each center is emitted once even when several branches meet there. The non-zero
            // geometry group also unions its area with the rectangles in the single drawing call.
            capCenters.Add(segment.StartPoint);
            capCenters.Add(segment.EndPoint);
        }

        foreach (Point center in capCenters)
        {
            geometry.Children.Add(new EllipseGeometry(center, radius, radius));
        }

        if (geometry.CanFreeze)
        {
            geometry.Freeze();
        }

        return geometry;
    }

    // Layout is calculated in logical breadth/depth coordinates. Mapping only at the arrange and
    // connector boundary keeps the tree algorithm identical in both directions and, importantly,
    // avoids LayoutTransform: transforms would rotate text and distort WPF's scrolling extent.
    private Point ToPhysicalPoint(double breadth, double depth) =>
        Direction == NavigationMapDirection.TopToBottom
            ? new Point(breadth, depth)
            : new Point(depth, breadth);

    private double GetBreadth(Size size) =>
        Direction == NavigationMapDirection.TopToBottom ? size.Width : size.Height;

    private double GetDepthLength(Size size) =>
        Direction == NavigationMapDirection.TopToBottom ? size.Height : size.Width;

    private double BranchSpacing =>
        Direction == NavigationMapDirection.TopToBottom ? HorizontalSpacing : VerticalSpacing;

    private double LevelSpacing =>
        Direction == NavigationMapDirection.TopToBottom ? VerticalSpacing : HorizontalSpacing;

    private static bool ValidateDirection(object? value) =>
        value is NavigationMapDirection.TopToBottom or NavigationMapDirection.LeftToRight;

    private static bool ValidateHorizontalSpacing(object? value) => IsFiniteNonNegativeDouble(value);

    private static bool ValidateVerticalSpacing(object? value) => IsFiniteNonNegativeDouble(value);

    private static bool ValidateConnectorThickness(object? value) => IsFinitePositiveDouble(value);

    private static void OnConnectorThicknessChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs _)
    {
        // The normalized centerline topology remains valid, but its filled geometry encodes the
        // old thickness. Clearing only this cache avoids a needless layout pass.
        ((NavigationMapPanel)dependencyObject)._connectorGeometry = null;
    }

    private static bool IsFinitePositiveDouble(object? value) =>
        value is double number && number > 0D && !double.IsInfinity(number) && !double.IsNaN(number);

    private static bool IsFiniteNonNegativeDouble(object? value) =>
        value is double number && number >= 0D && !double.IsInfinity(number) && !double.IsNaN(number);

    private sealed class LayoutNode
    {
        internal LayoutNode(UIElement element)
        {
            Element = element;
            if (element is NavigationMapItem item)
            {
                Key = item.NodeKey;
                ParentKey = item.ParentKey;
            }
        }

        internal UIElement Element { get; }

        internal string? Key { get; }

        internal string? ParentKey { get; }

        internal LayoutNode? Parent { get; set; }

        internal List<LayoutNode> Children { get; } = [];

        internal int Depth { get; set; }

        internal double ChildrenBreadth { get; set; }

        internal double SubtreeBreadth { get; set; }

        internal Rect ArrangedBounds { get; set; }
    }

    private sealed class LayoutForest(
        List<LayoutNode> roots,
        List<double> levelLengths,
        double[] levelOffsets)
    {
        internal List<LayoutNode> Roots { get; } = roots;

        internal List<double> LevelLengths { get; } = levelLengths;

        internal double[] LevelOffsets { get; } = levelOffsets;
    }

    /// <summary>Represents one normalized, non-zero, axis-aligned connector interval.</summary>
    internal readonly record struct ConnectorSegment(
        bool IsHorizontal,
        double Offset,
        double Start,
        double End)
    {
        internal double Length => End - Start;

        internal Point StartPoint =>
            IsHorizontal ? new Point(Start, Offset) : new Point(Offset, Start);

        internal Point EndPoint =>
            IsHorizontal ? new Point(End, Offset) : new Point(Offset, End);

        internal static ConnectorSegment Create(Point first, Point second)
        {
            bool isHorizontal = first.Y == second.Y;
            double offset = isHorizontal ? first.Y : first.X;
            double firstPosition = isHorizontal ? first.X : first.Y;
            double secondPosition = isHorizontal ? second.X : second.Y;
            return new ConnectorSegment(
                isHorizontal,
                offset,
                Math.Min(firstPosition, secondPosition),
                Math.Max(firstPosition, secondPosition)
            );
        }

        internal ConnectorSegment WithEnd(double end) => this with { End = end };
    }

    private readonly record struct ConnectorLine(bool IsHorizontal, double Offset);
}
