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
/// Arranges navigation-map containers as an organization chart or a saved free-form diagram and
/// draws obstacle-avoiding orthogonal connectors between them.
/// </summary>
/// <remarks>
/// <para>
/// The panel consumes the flat <see cref="NavigationMapItem.NodeKey"/> and
/// <see cref="NavigationMapItem.ParentKey"/> metadata prepared by <see cref="NavigationMapView"/>.
/// Manual layout starts with the same organization chart as hierarchical layout and replaces only
/// saved coordinates. It never collision-adjusts nodes: overlap is an editor decision and must not
/// cause unrelated cards to move unexpectedly.
/// </para>
/// <para>
/// Free-form connectors use a rectilinear visibility grid made from node edges, ports, and
/// obstacle-clearance lanes. Shortest-path cost penalizes bends and rewards reuse of existing
/// segments, so routes prefer nearby internal passages and shared trunks. Map edges remain valid
/// last-resort lanes but are no longer the sole fallback.
/// </para>
/// <para>
/// This panel is intentionally non-virtualizing. Stable hierarchy extents, connector topology,
/// selection, and search navigation require every node to participate in layout.
/// </para>
/// </remarks>
public class NavigationMapPanel : Panel
{
    /// <summary>Identifies the <see cref="LayoutMode"/> dependency property.</summary>
    public static readonly DependencyProperty LayoutModeProperty = DependencyProperty.Register(
        nameof(LayoutMode),
        typeof(NavigationMapLayoutMode),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(
            NavigationMapLayoutMode.Hierarchical,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange
                | FrameworkPropertyMetadataOptions.AffectsRender
        ),
        ValidateLayoutMode
    );

    /// <summary>Gets or sets the strategy used to place generated containers.</summary>
    public NavigationMapLayoutMode LayoutMode
    {
        get => (NavigationMapLayoutMode)GetValue(LayoutModeProperty);
        set => SetValue(LayoutModeProperty, value);
    }

    /// <summary>Identifies the <see cref="LayoutDefinition"/> dependency property.</summary>
    public static readonly DependencyProperty LayoutDefinitionProperty = DependencyProperty.Register(
        nameof(LayoutDefinition),
        typeof(NavigationMapLayout),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(null, OnLayoutDefinitionChanged)
    );

    /// <summary>Gets or sets the saved positions consulted by Manual layout.</summary>
    public NavigationMapLayout? LayoutDefinition
    {
        get => (NavigationMapLayout?)GetValue(LayoutDefinitionProperty);
        set => SetValue(LayoutDefinitionProperty, value);
    }

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

    /// <summary>Gets or sets the direction in which hierarchy levels advance.</summary>
    public NavigationMapDirection Direction
    {
        get => (NavigationMapDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>Identifies the <see cref="HorizontalSpacing"/> dependency property.</summary>
    public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
        nameof(HorizontalSpacing),
        typeof(double),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(
            32D,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange
                | FrameworkPropertyMetadataOptions.AffectsRender
        ),
        ValidateNonNegativeDouble
    );

    /// <summary>Gets or sets the horizontal layout gap, in device-independent pixels.</summary>
    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

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
        ValidateNonNegativeDouble
    );

    /// <summary>Gets or sets the vertical layout gap, in device-independent pixels.</summary>
    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    /// <summary>Identifies the <see cref="ConnectorBrush"/> dependency property.</summary>
    public static readonly DependencyProperty ConnectorBrushProperty = DependencyProperty.Register(
        nameof(ConnectorBrush),
        typeof(Brush),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>Gets or sets the brush used to draw ordinary parent-child connectors.</summary>
    public Brush? ConnectorBrush
    {
        get => (Brush?)GetValue(ConnectorBrushProperty);
        set => SetValue(ConnectorBrushProperty, value);
    }

    /// <summary>Identifies the <see cref="ConnectorThickness"/> dependency property.</summary>
    public static readonly DependencyProperty ConnectorThicknessProperty = DependencyProperty.Register(
        nameof(ConnectorThickness),
        typeof(double),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(1D, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryPropertyChanged),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the ordinary connector thickness.</summary>
    public double ConnectorThickness
    {
        get => (double)GetValue(ConnectorThicknessProperty);
        set => SetValue(ConnectorThicknessProperty, value);
    }

    /// <summary>Identifies the <see cref="HighlightedConnectorBrush"/> dependency property.</summary>
    public static readonly DependencyProperty HighlightedConnectorBrushProperty = DependencyProperty.Register(
        nameof(HighlightedConnectorBrush),
        typeof(Brush),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>Gets or sets the brush used for connectors incident to the primary selection.</summary>
    public Brush? HighlightedConnectorBrush
    {
        get => (Brush?)GetValue(HighlightedConnectorBrushProperty);
        set => SetValue(HighlightedConnectorBrushProperty, value);
    }

    /// <summary>Identifies the <see cref="HighlightedConnectorThickness"/> dependency property.</summary>
    public static readonly DependencyProperty HighlightedConnectorThicknessProperty =
        DependencyProperty.Register(
            nameof(HighlightedConnectorThickness),
            typeof(double),
            typeof(NavigationMapPanel),
            new FrameworkPropertyMetadata(3D, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryPropertyChanged),
            ValidatePositiveDouble
        );

    /// <summary>Gets or sets the thickness of highlighted, directly related connectors.</summary>
    public double HighlightedConnectorThickness
    {
        get => (double)GetValue(HighlightedConnectorThicknessProperty);
        set => SetValue(HighlightedConnectorThicknessProperty, value);
    }

    /// <summary>Identifies the <see cref="HighlightedNodeKey"/> dependency property.</summary>
    public static readonly DependencyProperty HighlightedNodeKeyProperty = DependencyProperty.Register(
        nameof(HighlightedNodeKey),
        typeof(string),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnGeometryPropertyChanged)
    );

    /// <summary>Gets or sets the node whose parent and immediate child connectors are highlighted.</summary>
    public string? HighlightedNodeKey
    {
        get => (string?)GetValue(HighlightedNodeKeyProperty);
        set => SetValue(HighlightedNodeKeyProperty, value);
    }

    /// <summary>Identifies the <see cref="RoutingClearance"/> dependency property.</summary>
    public static readonly DependencyProperty RoutingClearanceProperty = DependencyProperty.Register(
        nameof(RoutingClearance),
        typeof(double),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(
            10D,
            FrameworkPropertyMetadataOptions.AffectsArrange
                | FrameworkPropertyMetadataOptions.AffectsRender
        ),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the preferred distance between a routed line and a node.</summary>
    public double RoutingClearance
    {
        get => (double)GetValue(RoutingClearanceProperty);
        set => SetValue(RoutingClearanceProperty, value);
    }

    /// <summary>Identifies the <see cref="IsEditing"/> dependency property.</summary>
    public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register(
        nameof(IsEditing),
        typeof(bool),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>
    /// Gets or sets a value indicating whether editor-only presentation such as the grid is visible.
    /// </summary>
    public bool IsEditing
    {
        get => (bool)GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowGrid"/> dependency property.</summary>
    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid),
        typeof(bool),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>Gets or sets a value indicating whether the editable map draws its grid.</summary>
    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>Identifies the <see cref="GridSize"/> dependency property.</summary>
    public static readonly DependencyProperty GridSizeProperty = DependencyProperty.Register(
        nameof(GridSize),
        typeof(double),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(16D, FrameworkPropertyMetadataOptions.AffectsRender),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the distance between adjacent editor grid lines.</summary>
    public double GridSize
    {
        get => (double)GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    /// <summary>Identifies the <see cref="GridBrush"/> dependency property.</summary>
    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush),
        typeof(Brush),
        typeof(NavigationMapPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>Gets or sets the brush used for editor grid lines.</summary>
    public Brush? GridBrush
    {
        get => (Brush?)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    private readonly List<ConnectorRoute> _connectorRoutes = [];
    private readonly List<ConnectorSegment> _connectorSegments = [];
    private Geometry? _connectorGeometry;
    private Geometry? _highlightedConnectorGeometry;
    private LayoutForest? _measuredForest;
    private LayoutResult? _measuredLayout;
    private Rect? _selectionRectangle;
    private Size _heldExtent;
    private bool _isExtentHeld;

    /// <summary>Gets normalized connector centerlines retained by the latest arrangement.</summary>
    internal IReadOnlyList<ConnectorSegment> ConnectorSegments => _connectorSegments;

    /// <summary>Gets individual routed edges before global visual merging.</summary>
    internal IReadOnlyList<ConnectorRoute> ConnectorRoutes => _connectorRoutes;

    /// <summary>
    /// Keeps the scrolling extent from contracting while a node drag is active. The extent may
    /// still grow as the pointer moves outwards, but it is released only after mouse-up so dragging
    /// back towards the origin cannot make ScrollViewer clamp its offsets underneath the pointer.
    /// </summary>
    internal void BeginExtentHold()
    {
        if (_isExtentHeld)
        {
            return;
        }

        _heldExtent = new Size(
            Math.Max(DesiredSize.Width, RenderSize.Width),
            Math.Max(DesiredSize.Height, RenderSize.Height)
        );
        _isExtentHeld = true;
    }

    /// <summary>Releases the temporary drag extent and permits the map to shrink normally.</summary>
    internal void EndExtentHold()
    {
        if (!_isExtentHeld)
        {
            return;
        }

        _isExtentHeld = false;
        _heldExtent = default;
        InvalidateMeasure();
    }

    /// <summary>Updates the editor marquee, expressed in unscaled panel coordinates.</summary>
    internal void SetSelectionRectangle(Rect? rectangle)
    {
        if (_selectionRectangle == rectangle)
        {
            return;
        }

        _selectionRectangle = rectangle;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var constraint = new Size(
            double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width,
            double.PositiveInfinity
        );
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(constraint);
        }

        LayoutForest forest = BuildForest();
        _measuredForest = forest;
        _measuredLayout = LayoutMode == NavigationMapLayoutMode.Manual
            ? CreateManualLayout(forest)
            : null;
        Size desired = _measuredLayout?.Size ?? CalculateForestSize(forest);
        if (!_isExtentHeld)
        {
            return desired;
        }

        // Updating the held maximum allows outward dragging to extend the ScrollViewer while the
        // reverse movement remains stable until EndExtentHold deliberately releases the extent.
        _heldExtent = new Size(
            Math.Max(_heldExtent.Width, desired.Width),
            Math.Max(_heldExtent.Height, desired.Height)
        );
        return _heldExtent;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        LayoutForest forest = _measuredForest ?? BuildForest();
        _measuredForest = null;
        _connectorRoutes.Clear();
        _connectorSegments.Clear();

        if (LayoutMode == NavigationMapLayoutMode.Manual)
        {
            ArrangePositionedLayout(forest, _measuredLayout ?? CreateManualLayout(forest));
        }
        else
        {
            ArrangeHierarchicalLayout(forest, finalSize);
        }

        _measuredLayout = null;
        RouteConnectors(forest);
        NormalizeConnectorSegments();
        ClearGeometryCaches();
        InvalidateVisual();
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        DrawEditorGrid(drawingContext);

        if (ConnectorBrush is { } connectorBrush && _connectorSegments.Count > 0)
        {
            _connectorGeometry ??= CreateConnectorGeometry(_connectorSegments, ConnectorThickness);
            drawingContext.DrawGeometry(connectorBrush, null, _connectorGeometry);
        }

        if (
            HighlightedConnectorBrush is { } highlightedBrush
            && HighlightedNodeKey is { Length: > 0 } highlightedKey
        )
        {
            _highlightedConnectorGeometry ??= CreateHighlightedConnectorGeometry(highlightedKey);
            if (_highlightedConnectorGeometry != Geometry.Empty)
            {
                drawingContext.DrawGeometry(highlightedBrush, null, _highlightedConnectorGeometry);
            }
        }

        DrawSelectionRectangle(drawingContext);
    }

    /// <inheritdoc />
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        HitTestResult? renderedHit = base.HitTestCore(hitTestParameters);
        if (renderedHit is not null)
        {
            return renderedHit;
        }

        // UIElement's default point hit test only considers retained drawing content. A panel with
        // a hidden grid therefore has no hit target between cards, even though that area is the
        // editor canvas. Make the arranged panel rectangle an explicit input surface; ScrollBar and
        // outer template chrome remain separate visuals and are not accidentally classified here.
        return new Rect(RenderSize).Contains(hitTestParameters.HitPoint)
            ? new PointHitTestResult(this, hitTestParameters.HitPoint)
            : null;
    }

    private void DrawSelectionRectangle(DrawingContext drawingContext)
    {
        if (_selectionRectangle is not { } rectangle || HighlightedConnectorBrush is not { } brush)
        {
            return;
        }

        var pen = new Pen(brush, 1D);
        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        // Reusing the accent connector brush keeps the marquee theme-aware without adding another
        // public styling surface. Opacity is applied by DrawingContext and never mutates a shared
        // or frozen theme brush.
        drawingContext.PushOpacity(0.14D);
        drawingContext.DrawRectangle(brush, null, rectangle);
        drawingContext.Pop();
        drawingContext.DrawRectangle(null, pen, rectangle);
    }

    private void DrawEditorGrid(DrawingContext drawingContext)
    {
        if (!IsEditing || !ShowGrid || GridBrush is not { } brush)
        {
            return;
        }

        var pen = new Pen(brush, 1D);
        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        for (double x = GridSize; x < RenderSize.Width; x += GridSize)
        {
            drawingContext.DrawLine(pen, new Point(x, 0D), new Point(x, RenderSize.Height));
        }

        for (double y = GridSize; y < RenderSize.Height; y += GridSize)
        {
            drawingContext.DrawLine(pen, new Point(0D, y), new Point(RenderSize.Width, y));
        }
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

    private static bool ValidateLayoutMode(object? value) =>
        value is NavigationMapLayoutMode.Hierarchical or NavigationMapLayoutMode.Manual;

    private static bool ValidatePositiveDouble(object? value) =>
        value is double number && number > 0D && !double.IsInfinity(number) && !double.IsNaN(number);

    private static bool ValidateNonNegativeDouble(object? value) =>
        value is double number && number >= 0D && !double.IsInfinity(number) && !double.IsNaN(number);

    private static void OnGeometryPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs _)
    {
        ((NavigationMapPanel)dependencyObject).ClearGeometryCaches();
    }

    private static void OnLayoutDefinitionChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var panel = (NavigationMapPanel)dependencyObject;
        if (args.OldValue is NavigationMapLayout oldLayout)
        {
            PropertyChangedEventManager.RemoveHandler(
                oldLayout,
                panel.OnLayoutDefinitionContentChanged,
                nameof(NavigationMapLayout.Placements)
            );
        }

        if (args.NewValue is NavigationMapLayout newLayout)
        {
            // Layout definitions are commonly owned by a long-lived view model. A weak event keeps
            // that model from retaining a discarded generated items panel.
            PropertyChangedEventManager.AddHandler(
                newLayout,
                panel.OnLayoutDefinitionContentChanged,
                nameof(NavigationMapLayout.Placements)
            );
        }

        panel.InvalidateMeasure();
    }

    private void OnLayoutDefinitionContentChanged(object? sender, PropertyChangedEventArgs e) =>
        InvalidateMeasure();

    private sealed class OrthogonalRouter
    {
        // A bend is much more visually expensive than a few extra device-independent pixels. The
        // former value (14) let the router enter a nearby side port with two turns instead of using
        // the facing top/bottom port with one turn, producing the hooked routes seen around rows of
        // cards. Seventy-two DIPs is approximately one normal card height: it remains a preference,
        // not a hard constraint, but makes a materially shorter or reusable path justify each turn.
        private const double BendPenalty = 72D;
        private const double ReusedLengthFactor = 0.22D;
        private const double ReservedLengthFactor = 4D;
        private const double ReservedEdgePenalty = 36D;
        private readonly IReadOnlyList<LayoutNode> _nodes;
        private readonly double _clearance;
        private readonly NavigationMapDirection? _constrainedDirection;
        private readonly Rect[] _obstacles;
        private readonly double[] _xCoordinates;
        private readonly double[] _yCoordinates;
        private readonly Dictionary<double, int> _xIndices;
        private readonly Dictionary<double, int> _yIndices;
        private readonly bool[] _validVertices;
        private readonly bool[] _rightEdges;
        private readonly bool[] _downEdges;
        private readonly HashSet<GridEdge> _networkEdges = [];
        private readonly HashSet<GridEdge> _reservedEdges = [];
        private readonly List<ConnectorSegment> _networkSegments = [];
        private readonly List<ConnectorSegment> _reservedSegments = [];
        private readonly MinimumQueue _queue = new();
        private readonly double[] _distances;
        private readonly int[] _previous;
        private readonly int[] _sourceOwners;
        private readonly int[] _stateGenerations;
        private int _searchGeneration;

        internal OrthogonalRouter(
            IReadOnlyList<LayoutNode> nodes,
            double clearance,
            NavigationMapDirection? constrainedDirection)
        {
            _nodes = nodes;
            _clearance = clearance;
            _constrainedDirection = constrainedDirection;
            _obstacles = nodes.Select(node => Inflate(node.ArrangedBounds, clearance)).ToArray();

            var xs = new SortedSet<double>();
            var ys = new SortedSet<double>();
            foreach (Rect obstacle in _obstacles)
            {
                xs.Add(obstacle.Left);
                xs.Add(obstacle.Left + (obstacle.Width / 2D));
                xs.Add(obstacle.Right);
                ys.Add(obstacle.Top);
                ys.Add(obstacle.Top + (obstacle.Height / 2D));
                ys.Add(obstacle.Bottom);
            }

            xs.Add(Math.Max(0D, nodes.Min(static node => node.ArrangedBounds.Left) - clearance));
            ys.Add(Math.Max(0D, nodes.Min(static node => node.ArrangedBounds.Top) - clearance));
            xs.Add(nodes.Max(static node => node.ArrangedBounds.Right) + clearance);
            ys.Add(nodes.Max(static node => node.ArrangedBounds.Bottom) + clearance);
            _xCoordinates = [.. xs];
            _yCoordinates = [.. ys];
            _xIndices = _xCoordinates
                .Select((value, index) => (value, index))
                .ToDictionary(static pair => pair.value, static pair => pair.index);
            _yIndices = _yCoordinates
                .Select((value, index) => (value, index))
                .ToDictionary(static pair => pair.value, static pair => pair.index);
            _validVertices = new bool[_xCoordinates.Length * _yCoordinates.Length];
            _rightEdges = new bool[_validVertices.Length];
            _downEdges = new bool[_validVertices.Length];
            _distances = new double[_validVertices.Length * 2];
            _previous = new int[_validVertices.Length * 2];
            _sourceOwners = new int[_validVertices.Length * 2];
            _stateGenerations = new int[_validVertices.Length * 2];
            int width = _xCoordinates.Length;
            int height = _yCoordinates.Length;
            int[] blockedPoints = new int[(width + 1) * (height + 1)];
            int[] blockedHorizontalEdges = new int[(width + 1) * (height + 1)];
            int[] blockedVerticalEdges = new int[(width + 1) * (height + 1)];
            foreach (Rect obstacle in _obstacles)
            {
                int left = _xIndices[obstacle.Left];
                int right = _xIndices[obstacle.Right];
                int top = _yIndices[obstacle.Top];
                int bottom = _yIndices[obstacle.Bottom];
                AddCoverageRectangle(blockedPoints, width, left + 1, top + 1, right - 1, bottom - 1);
                AddCoverageRectangle(blockedHorizontalEdges, width, left, top + 1, right - 1, bottom - 1);
                AddCoverageRectangle(blockedVerticalEdges, width, left + 1, top, right - 1, bottom - 1);
            }

            AccumulateCoverage(blockedPoints, width, height);
            AccumulateCoverage(blockedHorizontalEdges, width, height);
            AccumulateCoverage(blockedVerticalEdges, width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int vertex = GetVertex(x, y);
                    int coverageIndex = (y * (width + 1)) + x;
                    _validVertices[vertex] = blockedPoints[coverageIndex] == 0;
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int vertex = GetVertex(x, y);
                    int coverageIndex = (y * (width + 1)) + x;
                    _rightEdges[vertex] = x + 1 < width
                        && _validVertices[vertex]
                        && _validVertices[GetVertex(x + 1, y)]
                        && blockedHorizontalEdges[coverageIndex] == 0;
                    _downEdges[vertex] = y + 1 < height
                        && _validVertices[vertex]
                        && _validVertices[GetVertex(x, y + 1)]
                        && blockedVerticalEdges[coverageIndex] == 0;
                }
            }
        }

        /// <summary>
        /// Starts one parent's outgoing connector network. Lines committed by siblings remain cheap
        /// to reuse, while lines reserved by other parents receive an overlap penalty.
        /// </summary>
        internal void BeginNetwork()
        {
            _networkEdges.Clear();
            _networkSegments.Clear();
        }

        internal IReadOnlyList<Point> FindRoute(LayoutNode parent, LayoutNode child)
        {
            if (
                _constrainedDirection is null
                && TryCreateDirectFacingRoute(parent, child, out IReadOnlyList<Point> directRoute)
            )
            {
                return directRoute;
            }

            List<Port> sources = CreatePorts(parent, child, outgoing: true);
            List<Port> targets = CreatePorts(child, parent, outgoing: false);
            // First treat another semantic network's exact grid edges as unavailable. Only retry
            // with a large overlap penalty when those reservations disconnect the graph entirely.
            IReadOnlyList<Point>? path = FindShortestPath(sources, targets, avoidReservedEdges: true)
                ?? FindShortestPath(sources, targets, avoidReservedEdges: false);
            if (path is not null)
            {
                return path;
            }

            // Overlapping or very closely spaced imported nodes can invalidate every clearance
            // port. Preserve centered edge attachments and a deterministic orthogonal shape until
            // the user separates the cards. Manual routing derives the facing edges from the actual
            // coordinates; only the generated hierarchical layout retains a direction contract.
            if (_constrainedDirection is { } direction)
            {
                Point sourceAnchor = GetDirectionalAnchor(parent.ArrangedBounds, direction, outgoing: true);
                Point targetAnchor = GetDirectionalAnchor(child.ArrangedBounds, direction, outgoing: false);
                return CreateFallbackRoute(sourceAnchor, targetAnchor, direction == NavigationMapDirection.LeftToRight);
            }

            Point sourceCenter = Center(parent.ArrangedBounds);
            Point targetCenter = Center(child.ArrangedBounds);
            bool horizontal = Math.Abs(targetCenter.X - sourceCenter.X)
                >= Math.Abs(targetCenter.Y - sourceCenter.Y);
            Point manualSourceAnchor = horizontal
                ? new Point(
                    targetCenter.X >= sourceCenter.X ? parent.ArrangedBounds.Right : parent.ArrangedBounds.Left,
                    sourceCenter.Y)
                : new Point(
                    sourceCenter.X,
                    targetCenter.Y >= sourceCenter.Y ? parent.ArrangedBounds.Bottom : parent.ArrangedBounds.Top);
            Point manualTargetAnchor = horizontal
                ? new Point(
                    targetCenter.X >= sourceCenter.X ? child.ArrangedBounds.Left : child.ArrangedBounds.Right,
                    targetCenter.Y)
                : new Point(
                    targetCenter.X,
                    targetCenter.Y >= sourceCenter.Y ? child.ArrangedBounds.Top : child.ArrangedBounds.Bottom);
            return CreateFallbackRoute(manualSourceAnchor, manualTargetAnchor, horizontal);
        }

        internal void Commit(IReadOnlyList<Point> points)
        {
            for (int index = 1; index < points.Count; index++)
            {
                Point first = points[index - 1];
                Point second = points[index];
                if (first == second)
                {
                    continue;
                }

                ConnectorSegment segment = ConnectorSegment.Create(first, second);
                _networkSegments.Add(segment);
                _reservedSegments.Add(segment);

                if (
                    first.Y == second.Y
                    && _yIndices.TryGetValue(first.Y, out int row)
                )
                {
                    double minimum = Math.Min(first.X, second.X);
                    double maximum = Math.Max(first.X, second.X);
                    for (int x = 0; x + 1 < _xCoordinates.Length; x++)
                    {
                        if (_xCoordinates[x] < minimum || _xCoordinates[x + 1] > maximum)
                        {
                            continue;
                        }

                        GridEdge edge = GridEdge.Create(GetVertex(x, row), GetVertex(x + 1, row));
                        _networkEdges.Add(edge);
                        _reservedEdges.Add(edge);
                    }
                }
                else if (
                    first.X == second.X
                    && _xIndices.TryGetValue(first.X, out int column)
                )
                {
                    double minimum = Math.Min(first.Y, second.Y);
                    double maximum = Math.Max(first.Y, second.Y);
                    for (int verticalIndex = 0; verticalIndex + 1 < _yCoordinates.Length; verticalIndex++)
                    {
                        if (_yCoordinates[verticalIndex] < minimum || _yCoordinates[verticalIndex + 1] > maximum)
                        {
                            continue;
                        }

                        GridEdge edge = GridEdge.Create(
                            GetVertex(column, verticalIndex),
                            GetVertex(column, verticalIndex + 1)
                        );
                        _networkEdges.Add(edge);
                        _reservedEdges.Add(edge);
                    }
                }
            }
        }

        private List<Port> CreatePorts(LayoutNode owner, LayoutNode destination, bool outgoing)
        {
            if (_constrainedDirection is { } direction)
            {
                return CreateDirectionalPorts(owner, direction, outgoing);
            }

            Rect bounds = owner.ArrangedBounds;
            Point center = Center(bounds);
            Point destinationCenter = Center(destination.ArrangedBounds);
            var ports = new List<Port>
            {
                new(new Point(bounds.Left, center.Y), new Point(bounds.Left - _clearance, center.Y), Axis.Horizontal),
                new(new Point(bounds.Right, center.Y), new Point(bounds.Right + _clearance, center.Y), Axis.Horizontal),
                new(new Point(center.X, bounds.Top), new Point(center.X, bounds.Top - _clearance), Axis.Vertical),
                new(new Point(center.X, bounds.Bottom), new Point(center.X, bounds.Bottom + _clearance), Axis.Vertical),
            };

            // Every Manual card exposes four centered edge ports. Ordering provides deterministic
            // tie-breaking near the destination, while Dijkstra remains free to pick another side
            // when that produces a shorter path or avoids a card/semantic-network reservation.
            return ports
                .Where(port => IsPortAvailable(owner, port))
                .OrderBy(port => Math.Abs(port.Outside.X - destinationCenter.X)
                    + Math.Abs(port.Outside.Y - destinationCenter.Y))
                .ToList();
        }

        private List<Port> CreateDirectionalPorts(
            LayoutNode owner,
            NavigationMapDirection direction,
            bool outgoing)
        {
            Point anchor = GetDirectionalAnchor(owner.ArrangedBounds, direction, outgoing);
            Axis axis = direction == NavigationMapDirection.TopToBottom
                ? Axis.Vertical
                : Axis.Horizontal;
            Vector offset = direction switch
            {
                NavigationMapDirection.TopToBottom when outgoing => new Vector(0D, _clearance),
                NavigationMapDirection.TopToBottom => new Vector(0D, -_clearance),
                NavigationMapDirection.LeftToRight when outgoing => new Vector(_clearance, 0D),
                _ => new Vector(-_clearance, 0D),
            };
            var port = new Port(anchor, anchor + offset, axis);
            return IsPortAvailable(owner, port) ? [port] : [];
        }

        private bool IsPortAvailable(LayoutNode owner, Port port) =>
            port.Outside.X >= 0D
                && port.Outside.Y >= 0D
                && _obstacles
                    .Where((_, index) => !ReferenceEquals(_nodes[index], owner))
                    .All(obstacle => !IsInside(port.Outside, obstacle))
                && IsSegmentAvailableToNetwork(ConnectorSegment.Create(port.Anchor, port.Outside));

        private bool TryCreateDirectFacingRoute(
            LayoutNode source,
            LayoutNode target,
            out IReadOnlyList<Point> route)
        {
            const double epsilon = 0.0001D;
            Point sourceCenter = Center(source.ArrangedBounds);
            Point targetCenter = Center(target.ArrangedBounds);
            Point sourceAnchor;
            Point targetAnchor;
            if (Math.Abs(sourceCenter.X - targetCenter.X) < epsilon)
            {
                bool targetIsBelow = targetCenter.Y >= sourceCenter.Y;
                sourceAnchor = new Point(
                    sourceCenter.X,
                    targetIsBelow ? source.ArrangedBounds.Bottom : source.ArrangedBounds.Top);
                targetAnchor = new Point(
                    targetCenter.X,
                    targetIsBelow ? target.ArrangedBounds.Top : target.ArrangedBounds.Bottom);
                if ((targetIsBelow && sourceAnchor.Y > targetAnchor.Y)
                    || (!targetIsBelow && sourceAnchor.Y < targetAnchor.Y))
                {
                    route = [];
                    return false;
                }
            }
            else if (Math.Abs(sourceCenter.Y - targetCenter.Y) < epsilon)
            {
                bool targetIsRight = targetCenter.X >= sourceCenter.X;
                sourceAnchor = new Point(
                    targetIsRight ? source.ArrangedBounds.Right : source.ArrangedBounds.Left,
                    sourceCenter.Y);
                targetAnchor = new Point(
                    targetIsRight ? target.ArrangedBounds.Left : target.ArrangedBounds.Right,
                    targetCenter.Y);
                if ((targetIsRight && sourceAnchor.X > targetAnchor.X)
                    || (!targetIsRight && sourceAnchor.X < targetAnchor.X))
                {
                    route = [];
                    return false;
                }
            }
            else
            {
                route = [];
                return false;
            }

            // This narrow shortcut fixes the clearance dead zone between two aligned cards. It is
            // intentionally limited to one unobstructed center-to-center segment, so it cannot
            // reintroduce the arbitrary projected endpoints that previously made attachments look
            // off-center. Non-aligned relationships always go through the orthogonal router.
            ConnectorSegment segment = ConnectorSegment.Create(sourceAnchor, targetAnchor);
            bool unobstructed = _nodes
                .Select((node, index) => (node, index))
                .Where(pair => !ReferenceEquals(pair.node, source) && !ReferenceEquals(pair.node, target))
                .All(pair => !SegmentIntersectsRectangleInterior(
                    sourceAnchor,
                    targetAnchor,
                    _obstacles[pair.index]));
            if (!unobstructed || !IsSegmentAvailableToNetwork(segment))
            {
                route = [];
                return false;
            }

            route = [sourceAnchor, targetAnchor];
            return true;
        }

        private bool IsSegmentAvailableToNetwork(ConnectorSegment candidate) =>
            !_reservedSegments.Any(reserved =>
                OverlapsCollinearly(candidate, reserved)
                && !_networkSegments.Any(network => OverlapsCollinearly(candidate, network)));

        private static Point GetDirectionalAnchor(
            Rect bounds,
            NavigationMapDirection direction,
            bool outgoing)
        {
            Point center = Center(bounds);
            return direction switch
            {
                NavigationMapDirection.TopToBottom when outgoing => new Point(center.X, bounds.Bottom),
                NavigationMapDirection.TopToBottom => new Point(center.X, bounds.Top),
                NavigationMapDirection.LeftToRight when outgoing => new Point(bounds.Right, center.Y),
                _ => new Point(bounds.Left, center.Y),
            };
        }

        private static IReadOnlyList<Point> CreateFallbackRoute(
            Point sourceAnchor,
            Point targetAnchor,
            bool horizontal)
        {
            if (horizontal)
            {
                double lane = (sourceAnchor.X + targetAnchor.X) / 2D;
                return SimplifyPath(
                    [sourceAnchor, new Point(lane, sourceAnchor.Y), new Point(lane, targetAnchor.Y), targetAnchor]
                );
            }

            double verticalLane = (sourceAnchor.Y + targetAnchor.Y) / 2D;
            return SimplifyPath(
                [sourceAnchor, new Point(sourceAnchor.X, verticalLane), new Point(targetAnchor.X, verticalLane), targetAnchor]
            );
        }

        private IReadOnlyList<Point>? FindShortestPath(
            IReadOnlyList<Port> sources,
            IReadOnlyList<Port> targets,
            bool avoidReservedEdges)
        {
            if (sources.Count == 0 || targets.Count == 0)
            {
                return null;
            }

            BeginSearch();
            for (int index = 0; index < sources.Count; index++)
            {
                Port port = sources[index];
                if (!TryGetVertex(port.Outside, out int vertex))
                {
                    continue;
                }

                int state = GetState(vertex, port.Axis);
                SetSearchState(state, _clearance, -1, index);
                _queue.Enqueue(state, _clearance);
            }

            var targetsByVertex = new Dictionary<int, List<int>>();
            for (int index = 0; index < targets.Count; index++)
            {
                if (!TryGetVertex(targets[index].Outside, out int vertex))
                {
                    continue;
                }

                if (!targetsByVertex.TryGetValue(vertex, out List<int>? indices))
                {
                    indices = [];
                    targetsByVertex.Add(vertex, indices);
                }

                indices.Add(index);
            }

            double bestCost = double.PositiveInfinity;
            int bestState = -1;
            int bestTarget = -1;
            while (_queue.TryDequeue(out int state, out double queuedCost))
            {
                if (queuedCost != GetDistance(state) || queuedCost > bestCost)
                {
                    continue;
                }

                int vertex = state / 2;
                Axis incoming = (Axis)(state % 2);
                if (targetsByVertex.TryGetValue(vertex, out List<int>? targetIndices))
                {
                    foreach (int targetIndex in targetIndices)
                    {
                        double cost = queuedCost + _clearance;
                        if (incoming != targets[targetIndex].Axis)
                        {
                            cost += BendPenalty;
                        }

                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            bestState = state;
                            bestTarget = targetIndex;
                        }
                    }
                }

                foreach (
                    (int neighbor, Axis axis, double length) in GetNeighbors(
                        vertex,
                        avoidReservedEdges
                    )
                )
                {
                    int nextState = GetState(neighbor, axis);
                    double cost = queuedCost + GetSegmentCost(vertex, neighbor, length);
                    if (incoming != axis)
                    {
                        cost += BendPenalty;
                    }

                    if (cost >= GetDistance(nextState))
                    {
                        continue;
                    }

                    SetSearchState(nextState, cost, state, _sourceOwners[state]);
                    _queue.Enqueue(nextState, cost);
                }
            }

            if (bestState < 0 || _sourceOwners[bestState] < 0)
            {
                return null;
            }

            var vertices = new List<Point>();
            for (int state = bestState; state >= 0; state = _previous[state])
            {
                vertices.Add(GetPoint(state / 2));
            }

            vertices.Reverse();
            Port source = sources[_sourceOwners[bestState]];
            Port target = targets[bestTarget];
            var result = new List<Point>(vertices.Count + 4) { source.Anchor, source.Outside };
            result.AddRange(vertices);
            result.Add(target.Outside);
            result.Add(target.Anchor);
            return SimplifyPath(result);
        }

        private void BeginSearch()
        {
            _queue.Clear();
            if (_searchGeneration == int.MaxValue)
            {
                Array.Clear(_stateGenerations, 0, _stateGenerations.Length);
                _searchGeneration = 1;
            }
            else
            {
                _searchGeneration++;
            }
        }

        private double GetDistance(int state) =>
            _stateGenerations[state] == _searchGeneration
                ? _distances[state]
                : double.PositiveInfinity;

        private void SetSearchState(int state, double distance, int previous, int sourceOwner)
        {
            _stateGenerations[state] = _searchGeneration;
            _distances[state] = distance;
            _previous[state] = previous;
            _sourceOwners[state] = sourceOwner;
        }

        private IEnumerable<(int Neighbor, Axis Axis, double Length)> GetNeighbors(
            int vertex,
            bool avoidReservedEdges)
        {
            int width = _xCoordinates.Length;
            int x = vertex % width;
            int y = vertex / width;
            if (x > 0)
            {
                int neighbor = GetVertex(x - 1, y);
                if (
                    _rightEdges[neighbor]
                    && CanTraverseReservedEdge(vertex, neighbor, avoidReservedEdges)
                )
                {
                    yield return (neighbor, Axis.Horizontal, _xCoordinates[x] - _xCoordinates[x - 1]);
                }
            }

            if (x + 1 < width)
            {
                int neighbor = GetVertex(x + 1, y);
                if (
                    _rightEdges[vertex]
                    && CanTraverseReservedEdge(vertex, neighbor, avoidReservedEdges)
                )
                {
                    yield return (neighbor, Axis.Horizontal, _xCoordinates[x + 1] - _xCoordinates[x]);
                }
            }

            if (y > 0)
            {
                int neighbor = GetVertex(x, y - 1);
                if (
                    _downEdges[neighbor]
                    && CanTraverseReservedEdge(vertex, neighbor, avoidReservedEdges)
                )
                {
                    yield return (neighbor, Axis.Vertical, _yCoordinates[y] - _yCoordinates[y - 1]);
                }
            }

            if (y + 1 < _yCoordinates.Length)
            {
                int neighbor = GetVertex(x, y + 1);
                if (
                    _downEdges[vertex]
                    && CanTraverseReservedEdge(vertex, neighbor, avoidReservedEdges)
                )
                {
                    yield return (neighbor, Axis.Vertical, _yCoordinates[y + 1] - _yCoordinates[y]);
                }
            }
        }

        private bool CanTraverseReservedEdge(
            int firstVertex,
            int secondVertex,
            bool avoidReservedEdges)
        {
            if (!avoidReservedEdges)
            {
                return true;
            }

            GridEdge edge = GridEdge.Create(firstVertex, secondVertex);
            return _networkEdges.Contains(edge) || !_reservedEdges.Contains(edge);
        }

        private static void AddCoverageRectangle(
            int[] difference,
            int width,
            int left,
            int top,
            int right,
            int bottom)
        {
            if (left > right || top > bottom)
            {
                return;
            }

            int stride = width + 1;
            difference[(top * stride) + left]++;
            difference[(top * stride) + right + 1]--;
            difference[((bottom + 1) * stride) + left]--;
            difference[((bottom + 1) * stride) + right + 1]++;
        }

        private static void AccumulateCoverage(int[] difference, int width, int height)
        {
            int stride = width + 1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * stride) + x;
                    int left = x == 0 ? 0 : difference[index - 1];
                    int top = y == 0 ? 0 : difference[index - stride];
                    int diagonal = x == 0 || y == 0 ? 0 : difference[index - stride - 1];
                    difference[index] += left + top - diagonal;
                }
            }
        }

        private double GetSegmentCost(int firstVertex, int secondVertex, double length)
        {
            GridEdge edge = GridEdge.Create(firstVertex, secondVertex);
            if (_networkEdges.Contains(edge))
            {
                return length * ReusedLengthFactor;
            }

            return _reservedEdges.Contains(edge)
                ? (length * ReservedLengthFactor) + ReservedEdgePenalty
                : length;
        }

        private bool TryGetVertex(Point point, out int vertex)
        {
            if (_xIndices.TryGetValue(point.X, out int x) && _yIndices.TryGetValue(point.Y, out int y))
            {
                vertex = GetVertex(x, y);
                return _validVertices[vertex];
            }

            vertex = -1;
            return false;
        }

        private int GetVertex(int x, int y) => (y * _xCoordinates.Length) + x;

        private Point GetPoint(int vertex) =>
            new(_xCoordinates[vertex % _xCoordinates.Length], _yCoordinates[vertex / _xCoordinates.Length]);

        private static int GetState(int vertex, Axis axis) => (vertex * 2) + (int)axis;

        private static bool IsInside(Point point, Rect bounds)
        {
            const double epsilon = 0.0001D;
            return point.X > bounds.Left + epsilon
                && point.X < bounds.Right - epsilon
                && point.Y > bounds.Top + epsilon
                && point.Y < bounds.Bottom - epsilon;
        }

        private static Rect Inflate(Rect bounds, double amount)
        {
            bounds.Inflate(amount, amount);
            return bounds;
        }

        private readonly record struct Port(Point Anchor, Point Outside, Axis Axis);

        private readonly record struct GridEdge(int First, int Second)
        {
            internal static GridEdge Create(int first, int second) =>
                first <= second ? new GridEdge(first, second) : new GridEdge(second, first);
        }

        private enum Axis
        {
            Horizontal,
            Vertical,
        }
    }

    /// <summary>
    /// Binary heap used instead of <c>PriorityQueue&lt;T&gt;</c>, which is unavailable on the .NET
    /// Framework target. Duplicate states are intentional; stale entries are ignored on dequeue.
    /// </summary>
    private sealed class MinimumQueue
    {
        private readonly List<(int State, double Cost)> _items = [];

        internal void Clear() => _items.Clear();

        internal void Enqueue(int state, double cost)
        {
            _items.Add((state, cost));
            int index = _items.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_items[parent].Cost <= cost)
                {
                    break;
                }

                _items[index] = _items[parent];
                index = parent;
            }

            _items[index] = (state, cost);
        }

        internal bool TryDequeue(out int state, out double cost)
        {
            if (_items.Count == 0)
            {
                state = -1;
                cost = 0D;
                return false;
            }

            (state, cost) = _items[0];
            (int State, double Cost) tail = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            if (_items.Count == 0)
            {
                return true;
            }

            int index = 0;
            while (true)
            {
                int left = (index * 2) + 1;
                if (left >= _items.Count)
                {
                    break;
                }

                int right = left + 1;
                int child = right < _items.Count && _items[right].Cost < _items[left].Cost ? right : left;
                if (_items[child].Cost >= tail.Cost)
                {
                    break;
                }

                _items[index] = _items[child];
                index = child;
            }

            _items[index] = tail;
            return true;
        }
    }

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
        List<LayoutNode> nodes,
        List<LayoutNode> roots,
        List<double> levelLengths,
        double[] levelOffsets)
    {
        internal List<LayoutNode> Nodes { get; } = nodes;

        internal List<LayoutNode> Roots { get; } = roots;

        internal List<double> LevelLengths { get; } = levelLengths;

        internal double[] LevelOffsets { get; } = levelOffsets;
    }

    private sealed class LayoutResult(Size size, Dictionary<LayoutNode, Point> positions)
    {
        internal Size Size { get; } = size;

        internal Dictionary<LayoutNode, Point> Positions { get; } = positions;
    }

    private readonly record struct EdgeIdentity(LayoutNode Parent, LayoutNode Child);

    /// <summary>Represents one routed semantic parent-child edge.</summary>
    internal sealed class ConnectorRoute(
        string? parentKey,
        string? childKey,
        IReadOnlyList<ConnectorSegment> segments)
    {
        internal string? ParentKey { get; } = parentKey;

        internal string? ChildKey { get; } = childKey;

        internal IReadOnlyList<ConnectorSegment> Segments { get; } = segments;
    }

    /// <summary>Represents one normalized, non-zero, axis-aligned connector interval.</summary>
    internal readonly record struct ConnectorSegment(
        bool IsHorizontal,
        double Offset,
        double Start,
        double End)
    {
        internal double Length => End - Start;

        internal Point StartPoint => IsHorizontal ? new Point(Start, Offset) : new Point(Offset, Start);

        internal Point EndPoint => IsHorizontal ? new Point(End, Offset) : new Point(Offset, End);

        internal static ConnectorSegment Create(Point first, Point second)
        {
            bool horizontal = first.Y == second.Y;
            double offset = horizontal ? first.Y : first.X;
            double firstPosition = horizontal ? first.X : first.Y;
            double secondPosition = horizontal ? second.X : second.Y;
            return new ConnectorSegment(
                horizontal,
                offset,
                Math.Min(firstPosition, secondPosition),
                Math.Max(firstPosition, secondPosition)
            );
        }

        internal ConnectorSegment WithEnd(double end) => this with { End = end };
    }

    private readonly record struct ConnectorLine(bool IsHorizontal, double Offset);
}