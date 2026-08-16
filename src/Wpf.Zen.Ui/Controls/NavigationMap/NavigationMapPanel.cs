// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

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
public partial class NavigationMapPanel : Panel
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

        var forest = BuildForest();
        _measuredForest = forest;
        _measuredLayout = LayoutMode == NavigationMapLayoutMode.Manual
            ? CreateManualLayout(forest)
            : null;
        var desired = _measuredLayout?.Size ?? CalculateForestSize(forest);
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
        var forest = _measuredForest ?? BuildForest();
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
        var renderedHit = base.HitTestCore(hitTestParameters);
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

        for (var x = GridSize; x < RenderSize.Width; x += GridSize)
        {
            drawingContext.DrawLine(pen, new Point(x, 0D), new Point(x, RenderSize.Height));
        }

        for (var y = GridSize; y < RenderSize.Height; y += GridSize)
        {
            drawingContext.DrawLine(pen, new Point(0D, y), new Point(RenderSize.Width, y));
        }
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
            var horizontal = first.Y == second.Y;
            var offset = horizontal ? first.Y : first.X;
            var firstPosition = horizontal ? first.X : first.Y;
            var secondPosition = horizontal ? second.X : second.Y;
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
