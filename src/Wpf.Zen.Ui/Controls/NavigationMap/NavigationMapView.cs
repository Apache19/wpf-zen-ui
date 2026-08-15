// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays a flat semantic navigation graph as an automatic organization chart or an editable,
/// persistent free-form diagram.
/// </summary>
/// <remarks>
/// <para>
/// Navigation data and presentation data deliberately remain separate. Nodes keep route identity
/// and ancestry, while <see cref="NavigationMapLayout"/> stores editor coordinates that can be
/// serialized for one workspace. Manual mode falls back to the standard hierarchy for every node
/// without saved coordinates; it never performs implicit collision displacement.
/// </para>
/// <para>
/// The editor supports additive click and marquee selection, group dragging, keyboard movement,
/// grid snapping, alignment to a primary node, pointer-edge auto-scrolling, middle-button canvas
/// panning, and zoom. Search is exposed as control state and methods instead of embedding a fixed
/// toolbar, allowing an application to present its own Fluent search UI. The default template
/// supplies the required scrolling and zoom host.
/// </para>
/// <para>
/// A selected node highlights only its incident parent and child edges. This preserves immediate
/// relationship tracing without visually implying that every descendant is directly connected.
/// </para>
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ui:NavigationMapView
///     IsEditing="True"
///     ItemsSource="{Binding NavigationMap}"
///     LayoutDefinition="{Binding SavedLayout}"
///     LayoutMode="Manual"
///     SnapToGrid="True" /&gt;
/// </code>
/// </example>
[TemplatePart(Name = ScrollViewerPartName, Type = typeof(ScrollViewer))]
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(NavigationMapItem))]
public class NavigationMapView : ItemsControl
{
    private const string ScrollViewerPartName = "PART_ScrollViewer";

    /// <summary>Identifies the <see cref="LayoutMode"/> dependency property.</summary>
    public static readonly DependencyProperty LayoutModeProperty = DependencyProperty.Register(
        nameof(LayoutMode),
        typeof(NavigationMapLayoutMode),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(NavigationMapLayoutMode.Hierarchical, OnLayoutModeChanged),
        ValidateLayoutMode
    );

    /// <summary>Gets or sets the strategy used to place navigation-map nodes.</summary>
    public NavigationMapLayoutMode LayoutMode
    {
        get => (NavigationMapLayoutMode)GetValue(LayoutModeProperty);
        set => SetValue(LayoutModeProperty, value);
    }

    /// <summary>Identifies the <see cref="LayoutDefinition"/> dependency property.</summary>
    public static readonly DependencyProperty LayoutDefinitionProperty = DependencyProperty.Register(
        nameof(LayoutDefinition),
        typeof(NavigationMapLayout),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(null, OnLayoutDefinitionChanged)
    );

    /// <summary>Gets or sets the presentation-only positions used by Manual layout.</summary>
    /// <remarks>
    /// Every view creates an independent definition by default. Assign a view-model-owned instance
    /// when a layout must survive page recreation or be saved to external storage.
    /// </remarks>
    public NavigationMapLayout? LayoutDefinition
    {
        get => (NavigationMapLayout?)GetValue(LayoutDefinitionProperty);
        set => SetValue(LayoutDefinitionProperty, value);
    }

    /// <summary>Identifies the <see cref="IsEditing"/> dependency property.</summary>
    public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register(
        nameof(IsEditing),
        typeof(bool),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(false, OnIsEditingChanged, CoerceIsEditing)
    );

    /// <summary>Gets or sets a value indicating whether Manual layout can be edited.</summary>
    /// <remarks>
    /// Editing is effective only in <see cref="NavigationMapLayoutMode.Manual"/>. Node invocation
    /// is suppressed while active so drag and selection gestures cannot navigate away.
    /// </remarks>
    public bool IsEditing
    {
        get => (bool)GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    /// <summary>Identifies the <see cref="Direction"/> dependency property.</summary>
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
        nameof(Direction),
        typeof(NavigationMapDirection),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(
            NavigationMapDirection.TopToBottom,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange
                | FrameworkPropertyMetadataOptions.AffectsRender,
            OnAutomaticLayoutInputChanged
        ),
        ValidateDirection
    );

    /// <summary>Gets or sets the direction in which hierarchy levels advance.</summary>
    public NavigationMapDirection Direction
    {
        get => (NavigationMapDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>Identifies the <see cref="NodeWidth"/> dependency property.</summary>
    public static readonly DependencyProperty NodeWidthProperty = DependencyProperty.Register(
        nameof(NodeWidth),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(
            184D,
            FrameworkPropertyMetadataOptions.AffectsMeasure,
            OnAutomaticLayoutInputChanged
        ),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the width assigned to each default node container.</summary>
    public double NodeWidth
    {
        get => (double)GetValue(NodeWidthProperty);
        set => SetValue(NodeWidthProperty, value);
    }

    /// <summary>Identifies the <see cref="HorizontalSpacing"/> dependency property.</summary>
    public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
        nameof(HorizontalSpacing),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(
            32D,
            FrameworkPropertyMetadataOptions.AffectsMeasure,
            OnAutomaticLayoutInputChanged
        ),
        ValidateNonNegativeDouble
    );

    /// <summary>Gets or sets the horizontal map-layout gap.</summary>
    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    /// <summary>Identifies the <see cref="VerticalSpacing"/> dependency property.</summary>
    public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
        nameof(VerticalSpacing),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(
            44D,
            FrameworkPropertyMetadataOptions.AffectsMeasure,
            OnAutomaticLayoutInputChanged
        ),
        ValidateNonNegativeDouble
    );

    /// <summary>Gets or sets the vertical map-layout gap.</summary>
    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    /// <summary>Identifies the <see cref="ConnectorBrush"/> dependency property.</summary>
    public static readonly DependencyProperty ConnectorBrushProperty = DependencyProperty.Register(
        nameof(ConnectorBrush),
        typeof(Brush),
        typeof(NavigationMapView),
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
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(1D, FrameworkPropertyMetadataOptions.AffectsRender),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the ordinary connector-line thickness.</summary>
    public double ConnectorThickness
    {
        get => (double)GetValue(ConnectorThicknessProperty);
        set => SetValue(ConnectorThicknessProperty, value);
    }

    /// <summary>Identifies the <see cref="HighlightedConnectorBrush"/> dependency property.</summary>
    public static readonly DependencyProperty HighlightedConnectorBrushProperty = DependencyProperty.Register(
        nameof(HighlightedConnectorBrush),
        typeof(Brush),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>Gets or sets the brush for connectors directly related to the primary selection.</summary>
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
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(3D, FrameworkPropertyMetadataOptions.AffectsRender),
            ValidatePositiveDouble
        );

    /// <summary>Gets or sets the thickness of connectors related to the primary selection.</summary>
    public double HighlightedConnectorThickness
    {
        get => (double)GetValue(HighlightedConnectorThicknessProperty);
        set => SetValue(HighlightedConnectorThicknessProperty, value);
    }

    /// <summary>Identifies the <see cref="RoutingClearance"/> dependency property.</summary>
    public static readonly DependencyProperty RoutingClearanceProperty = DependencyProperty.Register(
        nameof(RoutingClearance),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(10D, FrameworkPropertyMetadataOptions.AffectsArrange),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the preferred gap between a routed connector and an unrelated node.</summary>
    public double RoutingClearance
    {
        get => (double)GetValue(RoutingClearanceProperty);
        set => SetValue(RoutingClearanceProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowRouteKey"/> dependency property.</summary>
    public static readonly DependencyProperty ShowRouteKeyProperty = DependencyProperty.Register(
        nameof(ShowRouteKey),
        typeof(bool),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(false)
    );

    /// <summary>Gets or sets a value indicating whether the default node template shows map keys.</summary>
    public bool ShowRouteKey
    {
        get => (bool)GetValue(ShowRouteKeyProperty);
        set => SetValue(ShowRouteKeyProperty, value);
    }

    /// <summary>Identifies the <see cref="SnapToGrid"/> dependency property.</summary>
    public static readonly DependencyProperty SnapToGridProperty = DependencyProperty.Register(
        nameof(SnapToGrid),
        typeof(bool),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(true, OnSnapToGridChanged)
    );

    /// <summary>Gets or sets a value indicating whether drag and keyboard positions snap to the grid.</summary>
    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    /// <summary>Identifies the <see cref="ShowGrid"/> dependency property.</summary>
    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid),
        typeof(bool),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(true)
    );

    /// <summary>Gets or sets a value indicating whether editing displays alignment grid lines.</summary>
    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>Identifies the <see cref="GridSize"/> dependency property.</summary>
    public static readonly DependencyProperty GridSizeProperty = DependencyProperty.Register(
        nameof(GridSize),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(16D, OnAutomaticLayoutInputChanged),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the editor grid interval in device-independent map coordinates.</summary>
    public double GridSize
    {
        get => (double)GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    /// <summary>Identifies the <see cref="ZoomFactor"/> dependency property.</summary>
    public static readonly DependencyProperty ZoomFactorProperty = DependencyProperty.Register(
        nameof(ZoomFactor),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(1D, null, CoerceZoomFactor),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the visual scale applied to the complete map and its scrolling extent.</summary>
    public double ZoomFactor
    {
        get => (double)GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    /// <summary>Identifies the <see cref="MinimumZoomFactor"/> dependency property.</summary>
    public static readonly DependencyProperty MinimumZoomFactorProperty = DependencyProperty.Register(
        nameof(MinimumZoomFactor),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(0.2D, OnZoomLimitChanged),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the smallest permitted <see cref="ZoomFactor"/>.</summary>
    public double MinimumZoomFactor
    {
        get => (double)GetValue(MinimumZoomFactorProperty);
        set => SetValue(MinimumZoomFactorProperty, value);
    }

    /// <summary>Identifies the <see cref="MaximumZoomFactor"/> dependency property.</summary>
    public static readonly DependencyProperty MaximumZoomFactorProperty = DependencyProperty.Register(
        nameof(MaximumZoomFactor),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(3D, OnZoomLimitChanged, CoerceMaximumZoomFactor),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the largest permitted <see cref="ZoomFactor"/>.</summary>
    public double MaximumZoomFactor
    {
        get => (double)GetValue(MaximumZoomFactorProperty);
        set => SetValue(MaximumZoomFactorProperty, value);
    }

    /// <summary>Identifies the <see cref="ZoomStep"/> dependency property.</summary>
    public static readonly DependencyProperty ZoomStepProperty = DependencyProperty.Register(
        nameof(ZoomStep),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(0.1D),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the amount used by wheel, zoom-in, and zoom-out operations.</summary>
    public double ZoomStep
    {
        get => (double)GetValue(ZoomStepProperty);
        set => SetValue(ZoomStepProperty, value);
    }

    /// <summary>Identifies the <see cref="AutoScrollEdgeSize"/> dependency property.</summary>
    public static readonly DependencyProperty AutoScrollEdgeSizeProperty = DependencyProperty.Register(
        nameof(AutoScrollEdgeSize),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(56D),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the viewport-edge band that activates drag auto-scrolling.</summary>
    public double AutoScrollEdgeSize
    {
        get => (double)GetValue(AutoScrollEdgeSizeProperty);
        set => SetValue(AutoScrollEdgeSizeProperty, value);
    }

    /// <summary>Identifies the <see cref="MaximumAutoScrollSpeed"/> dependency property.</summary>
    public static readonly DependencyProperty MaximumAutoScrollSpeedProperty =
        DependencyProperty.Register(
            nameof(MaximumAutoScrollSpeed),
            typeof(double),
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(720D),
            ValidatePositiveDouble
        );

    /// <summary>Gets or sets the maximum drag auto-scroll speed in viewport pixels per second.</summary>
    public double MaximumAutoScrollSpeed
    {
        get => (double)GetValue(MaximumAutoScrollSpeedProperty);
        set => SetValue(MaximumAutoScrollSpeedProperty, value);
    }

    /// <summary>Identifies the <see cref="SearchText"/> dependency property.</summary>
    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
        nameof(SearchText),
        typeof(string),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(string.Empty, OnSearchTextChanged)
    );

    /// <summary>Gets or sets text matched against node content, key, and description.</summary>
    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    private static readonly DependencyPropertyKey SearchResultCountPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(SearchResultCount),
            typeof(int),
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(0)
        );

    /// <summary>Identifies the read-only <see cref="SearchResultCount"/> dependency property.</summary>
    public static readonly DependencyProperty SearchResultCountProperty =
        SearchResultCountPropertyKey.DependencyProperty;

    /// <summary>Gets the number of nodes matching <see cref="SearchText"/>.</summary>
    public int SearchResultCount => (int)GetValue(SearchResultCountProperty);

    private static readonly DependencyPropertyKey CurrentSearchResultIndexPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CurrentSearchResultIndex),
            typeof(int),
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(-1)
        );

    /// <summary>Identifies the read-only <see cref="CurrentSearchResultIndex"/> property.</summary>
    public static readonly DependencyProperty CurrentSearchResultIndexProperty =
        CurrentSearchResultIndexPropertyKey.DependencyProperty;

    /// <summary>Gets the zero-based current search-result index, or -1 when no result is active.</summary>
    public int CurrentSearchResultIndex => (int)GetValue(CurrentSearchResultIndexProperty);

    private static readonly DependencyPropertyKey PrimarySelectedNodeKeyPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PrimarySelectedNodeKey),
            typeof(string),
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>Identifies the read-only <see cref="PrimarySelectedNodeKey"/> dependency property.</summary>
    public static readonly DependencyProperty PrimarySelectedNodeKeyProperty =
        PrimarySelectedNodeKeyPropertyKey.DependencyProperty;

    /// <summary>Gets the key of the focused alignment and connector-highlight target.</summary>
    public string? PrimarySelectedNodeKey => (string?)GetValue(PrimarySelectedNodeKeyProperty);

    private static readonly DependencyPropertyKey CanUndoPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanUndo),
            typeof(bool),
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="CanUndo"/> dependency property.</summary>
    public static readonly DependencyProperty CanUndoProperty = CanUndoPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether a layout-editor transaction can be undone.</summary>
    public bool CanUndo => (bool)GetValue(CanUndoProperty);

    private static readonly DependencyPropertyKey CanRedoPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanRedo),
            typeof(bool),
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="CanRedo"/> dependency property.</summary>
    public static readonly DependencyProperty CanRedoProperty = CanRedoPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether an undone layout transaction can be reapplied.</summary>
    public bool CanRedo => (bool)GetValue(CanRedoProperty);

    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Gets or sets the command executed after <see cref="NodeInvoked"/>.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Identifies the <see cref="NodeInvoked"/> routed event.</summary>
    public static readonly RoutedEvent NodeInvokedEvent = EventManager.RegisterRoutedEvent(
        nameof(NodeInvoked),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<NavigationMapView, NavigationMapNodeInvokedEventArgs>),
        typeof(NavigationMapView)
    );

    /// <summary>Identifies the <see cref="SelectionChanged"/> routed event.</summary>
    public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(SelectionChanged),
        RoutingStrategy.Bubble,
        typeof(SelectionChangedEventHandler),
        typeof(NavigationMapView)
    );

    private readonly HashSet<string> _selectedNodeKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Point> _dragOriginCenters = new(StringComparer.Ordinal);
    private readonly List<LayoutEdit> _undoHistory = [];
    private readonly List<LayoutEdit> _redoHistory = [];
    private readonly HashSet<string> _selectionAtGestureStart = new(StringComparer.Ordinal);
    private Dictionary<string, PlacementState>? _pendingDragBefore;
    private NavigationMapPanel? _editedPanel;
    private ScrollViewer? _scrollViewer;
    private Point _dragPointerOrigin;
    private Point _gesturePointerOriginInView;
    private Point _lastPointerInScrollViewer;
    private Point _panPointerOrigin;
    private ModifierKeys _gestureModifiers;
    private Vector _autoScrollVelocity;
    private double _panHorizontalOffsetOrigin;
    private double _panVerticalOffsetOrigin;
    private DateTime _lastAutoScrollFrame;
    private EditorGesture _editorGesture;
    private int _gridAlignmentRequestVersion;
    private bool _isAutoScrollRendering;
    private bool _isMiddleButtonPanning;

    static NavigationMapView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(typeof(NavigationMapView))
        );
    }

    /// <summary>Initializes a new instance of the <see cref="NavigationMapView"/> class.</summary>
    public NavigationMapView()
    {
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnItemContainerClick));
        SetCurrentValue(LayoutDefinitionProperty, new NavigationMapLayout());
    }

    /// <summary>Occurs when a generated or directly supplied map item is activated.</summary>
    public event TypedEventHandler<NavigationMapView, NavigationMapNodeInvokedEventArgs> NodeInvoked
    {
        add => AddHandler(NodeInvokedEvent, value);
        remove => RemoveHandler(NodeInvokedEvent, value);
    }

    /// <summary>Occurs after the editor or a public selection method changes selected nodes.</summary>
    public event SelectionChangedEventHandler SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>Gets a snapshot of selected node keys in source order.</summary>
    public IReadOnlyList<string> SelectedNodeKeys =>
        Items
            .Cast<object>()
            .Select(GetNodeKey)
            .Where(key => key is not null && _selectedNodeKeys.Contains(key))
            .Cast<string>()
            .ToArray();

    /// <summary>Selects a node by key and scrolls it into view.</summary>
    /// <param name="nodeKey">The stable key to select.</param>
    /// <param name="preserveSelection">Whether existing selected nodes remain selected.</param>
    /// <returns><see langword="true"/> when a generated node with that key exists.</returns>
    public bool SelectNode(string nodeKey, bool preserveSelection = false)
    {
        NavigationMapItem? container = FindContainer(nodeKey);
        if (container is null)
        {
            return false;
        }

        var selection = preserveSelection
            ? new HashSet<string>(_selectedNodeKeys, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        selection.Add(nodeKey);
        ApplySelection(selection, nodeKey);
        _ = CanEditSelection() ? Focus() : container.Focus();
        container.BringIntoView();
        return true;
    }

    /// <summary>Clears every selected editor node and connector highlight.</summary>
    public void ClearSelection() => ApplySelection([], null);

    /// <summary>Selects every generated node and retains the current primary target.</summary>
    /// <remarks>
    /// This editor command is intentionally a no-op outside active Manual editing. Read-only maps
    /// use ordinary node invocation and must not acquire an invisible bulk-selection state.
    /// </remarks>
    public void SelectAll()
    {
        if (!CanEditSelection())
        {
            return;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        string? primary = PrimarySelectedNodeKey;
        foreach (NavigationMapItem container in GetGeneratedContainers())
        {
            if (container.NodeKey is { Length: > 0 } key)
            {
                keys.Add(key);
                if (container.IsKeyboardFocusWithin)
                {
                    primary = key;
                }
            }
        }

        primary ??= keys.FirstOrDefault();
        ApplySelection(keys, primary);
    }

    /// <summary>
    /// Aligns selected nodes to the current grid, or every node when the selection is empty.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when Manual editing is active and at least one generated node was
    /// processed; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Unlike drag snapping, this command snaps each node independently. It is therefore suitable
    /// for normalizing imported layouts and the initial hierarchical fallback before group editing.
    /// </remarks>
    public bool AlignToGrid()
    {
        if (!CanEditSelection())
        {
            return false;
        }

        List<NavigationMapItem> selected = GetSelectedContainers();
        return AlignContainersToGrid(
            selected.Count == 0 ? GetGeneratedContainers().ToList() : selected,
            recordHistory: true
        );
    }

    /// <summary>
    /// Removes every saved placement and returns Manual mode to its grid-aligned hierarchical
    /// starting arrangement.
    /// </summary>
    /// <remarks>
    /// Reset is treated as a new editor document state, so undo and redo history is discarded. The
    /// generated hierarchy must first complete WPF layout before its centers can be snapped; the
    /// method therefore schedules normalization at dispatcher context-idle priority.
    /// </remarks>
    public void ResetLayout()
    {
        EnsureLayout().Clear();
        ClearLayoutHistory();
        ScheduleFullGridAlignment();
    }

    /// <summary>Aligns selected nodes to the primary selected node.</summary>
    /// <param name="alignment">The edge or center axis to share.</param>
    /// <returns><see langword="true"/> when at least two generated nodes were aligned.</returns>
    public bool AlignSelection(NavigationMapAlignment alignment)
    {
        if (!CanEditSelection() || FindContainer(PrimarySelectedNodeKey) is not { } primary)
        {
            return false;
        }

        List<NavigationMapItem> selected = GetSelectedContainers();
        if (selected.Count < 2)
        {
            return false;
        }

        string[] selectedKeys = selected
            .Select(static container => container.NodeKey)
            .Where(static key => key is not null)
            .Cast<string>()
            .ToArray();
        Dictionary<string, PlacementState> before = CapturePlacementStates(selectedKeys);
        Rect target = GetContainerBounds(primary);
        foreach (NavigationMapItem container in selected)
        {
            if (ReferenceEquals(container, primary) || container.NodeKey is not { Length: > 0 } key)
            {
                continue;
            }

            Rect bounds = GetContainerBounds(container);
            Point center = Center(bounds);
            center = alignment switch
            {
                NavigationMapAlignment.Left => new Point(target.Left + (bounds.Width / 2D), center.Y),
                NavigationMapAlignment.HorizontalCenter => new Point(Center(target).X, center.Y),
                NavigationMapAlignment.Right => new Point(target.Right - (bounds.Width / 2D), center.Y),
                NavigationMapAlignment.Top => new Point(center.X, target.Top + (bounds.Height / 2D)),
                NavigationMapAlignment.VerticalCenter => new Point(center.X, Center(target).Y),
                NavigationMapAlignment.Bottom => new Point(center.X, target.Bottom - (bounds.Height / 2D)),
                _ => throw new ArgumentOutOfRangeException(nameof(alignment)),
            };
            EnsureLayout().SetPosition(key, ClampCenter(container, center));
        }

        RecordLayoutEdit(before, CapturePlacementStates(selectedKeys));
        return true;
    }

    /// <summary>Activates the next search result, selecting and scrolling it into view.</summary>
    /// <returns><see langword="true"/> when a result exists.</returns>
    public bool FindNext() => MoveCurrentSearchResult(1);

    /// <summary>Activates the previous search result, selecting and scrolling it into view.</summary>
    /// <returns><see langword="true"/> when a result exists.</returns>
    public bool FindPrevious() => MoveCurrentSearchResult(-1);

    /// <summary>Increases <see cref="ZoomFactor"/> by <see cref="ZoomStep"/>.</summary>
    public void ZoomIn() => SetCurrentValue(ZoomFactorProperty, ZoomFactor + ZoomStep);

    /// <summary>Decreases <see cref="ZoomFactor"/> by <see cref="ZoomStep"/>.</summary>
    public void ZoomOut() => SetCurrentValue(ZoomFactorProperty, ZoomFactor - ZoomStep);

    /// <summary>Restores a one-to-one map scale.</summary>
    public void ResetZoom() => SetCurrentValue(ZoomFactorProperty, 1D);

    /// <summary>Scales the complete map to fit the current scrolling viewport.</summary>
    /// <returns><see langword="true"/> when the template and map panel are available.</returns>
    public bool ZoomToFit()
    {
        NavigationMapPanel? panel = FindVisualDescendant<NavigationMapPanel>(this);
        if (_scrollViewer is null || panel is null || panel.RenderSize.Width <= 0D || panel.RenderSize.Height <= 0D)
        {
            return false;
        }

        double horizontal = _scrollViewer.ViewportWidth / panel.RenderSize.Width;
        double vertical = _scrollViewer.ViewportHeight / panel.RenderSize.Height;
        SetCurrentValue(ZoomFactorProperty, Math.Min(horizontal, vertical));
        _scrollViewer.ScrollToHome();
        return true;
    }

    /// <summary>Reverts the most recent drag, nudge, or alignment transaction.</summary>
    /// <returns><see langword="true"/> when a transaction was reverted.</returns>
    public bool Undo()
    {
        if (_undoHistory.Count == 0)
        {
            return false;
        }

        LayoutEdit edit = _undoHistory[^1];
        _undoHistory.RemoveAt(_undoHistory.Count - 1);
        ApplyPlacementStates(edit.Before);
        _redoHistory.Add(edit);
        UpdateHistoryState();
        return true;
    }

    /// <summary>Reapplies the most recently undone layout-editor transaction.</summary>
    /// <returns><see langword="true"/> when a transaction was reapplied.</returns>
    public bool Redo()
    {
        if (_redoHistory.Count == 0)
        {
            return false;
        }

        LayoutEdit edit = _redoHistory[^1];
        _redoHistory.RemoveAt(_redoHistory.Count - 1);
        ApplyPlacementStates(edit.After);
        _undoHistory.Add(edit);
        UpdateHistoryState();
        return true;
    }

    /// <summary>
    /// Clears editor undo and redo history, for example after an application loads another file.
    /// </summary>
    public void ClearLayoutHistory()
    {
        _pendingDragBefore = null;
        _undoHistory.Clear();
        _redoHistory.Clear();
        UpdateHistoryState();
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        // A template replacement invalidates both the coordinate space used by the gesture and
        // the ScrollViewer receiving offsets. End the gesture before replacing that reference so
        // a captured mouse cannot continue driving a detached template part.
        EndMiddleButtonPanning();
        base.OnApplyTemplate();
        _scrollViewer = GetTemplateChild(ScrollViewerPartName) as ScrollViewer;
    }

    /// <summary>Raises invocation and then executes <see cref="Command"/>.</summary>
    /// <param name="item">The item owned by WPF's item generator.</param>
    /// <param name="container">The activated visual container.</param>
    protected virtual void OnNodeInvoked(object item, NavigationMapItem container)
    {
        var args = new NavigationMapNodeInvokedEventArgs(NodeInvokedEvent, this, item, container);
        RaiseEvent(args);
        if (!args.Handled && Command?.CanExecute(item) == true)
        {
            Command.Execute(item);
        }
    }

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item) => item is NavigationMapItem;

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride() => new NavigationMapItem();

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is not NavigationMapItem container)
        {
            return;
        }

        if (item is NavigationMapNode node)
        {
            BindContainer(container, NavigationMapItem.NodeKeyProperty, node, nameof(NavigationMapNode.Key));
            BindContainer(container, NavigationMapItem.ParentKeyProperty, node, nameof(NavigationMapNode.ParentKey));
            BindContainer(container, NavigationMapItem.TargetProperty, node, nameof(NavigationMapNode.Target));
            BindContainer(container, NavigationMapItem.DescriptionProperty, node, nameof(NavigationMapNode.Description));
            BindContainer(container, AutomationProperties.NameProperty, node, nameof(NavigationMapNode.Content));
        }

        UpdateContainerStates(container, item);
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is NavigationMapItem container && !ReferenceEquals(element, item))
        {
            container.SetLayoutSelected(false, false);
            container.SetSearchMatch(false, false);
            BindingOperations.ClearBinding(container, NavigationMapItem.NodeKeyProperty);
            BindingOperations.ClearBinding(container, NavigationMapItem.ParentKeyProperty);
            BindingOperations.ClearBinding(container, NavigationMapItem.TargetProperty);
            BindingOperations.ClearBinding(container, NavigationMapItem.DescriptionProperty);
            BindingOperations.ClearBinding(container, AutomationProperties.NameProperty);
            container.SetLayoutPosition(0, true, 1, 1);
        }

        base.ClearContainerForItemOverride(element, item);
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                RefreshGeneratedState();
                if (CanEditSelection() && SnapToGrid)
                {
                    // Loaded priority is already after item generation. Normalizing here avoids a
                    // second dispatcher hop that could cancel the edit-entry alignment while a
                    // template is still being established.
                    UpdateLayout();
                    _ = AlignContainersToGrid(
                        GetGeneratedContainers().ToList(),
                        recordHistory: false
                    );
                }
            })
        );
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        if (!CanEditSelection() || _isMiddleButtonPanning)
        {
            return;
        }

        // Keyboard commands belong to the editor, not to whichever card happened to be clicked
        // last. Selection and its primary node already provide the visual/item focus semantics.
        // Keeping keyboard focus here also makes a blank-surface click immediately command-ready.
        _ = Focus();

        DependencyObject? source = e.OriginalSource as DependencyObject;
        NavigationMapPanel? panel = FindVisualAncestor<NavigationMapPanel>(source);
        if (panel is null || !ReferenceEquals(FindVisualAncestor<NavigationMapView>(panel), this))
        {
            // Scroll bars and template chrome are deliberately excluded. Previously every
            // non-card source cleared selection, so clicking a thumb looked like a blank click.
            return;
        }

        NavigationMapItem? container = FindVisualAncestor<NavigationMapItem>(source);
        if (
            container is null
            || !ReferenceEquals(ItemsControlFromItemContainer(container), this)
            || container.NodeKey is not { Length: > 0 } key
        )
        {
            BeginMarqueeGesture(panel, e);
            e.Handled = true;
            return;
        }

        ModifierKeys modifiers = Keyboard.Modifiers;
        bool additive = (modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
        var selection = new HashSet<string>(_selectedNodeKeys, StringComparer.Ordinal);
        if ((modifiers & ModifierKeys.Control) != 0 && selection.Contains(key))
        {
            selection.Remove(key);
            ApplySelection(selection, selection.FirstOrDefault());
            e.Handled = true;
            return;
        }

        if (!additive && !selection.Contains(key))
        {
            selection.Clear();
        }

        selection.Add(key);
        ApplySelection(selection, key);
        BeginNodeDragGesture(panel, e);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (
            e.ChangedButton != MouseButton.Middle
            || !CanEditSelection()
            || _scrollViewer is null
        )
        {
            return;
        }

        DependencyObject? source = e.OriginalSource as DependencyObject;
        NavigationMapPanel? panel = FindVisualAncestor<NavigationMapPanel>(source);
        if (panel is null || !ReferenceEquals(FindVisualAncestor<NavigationMapView>(panel), this))
        {
            // Only the map canvas and its node descendants initiate panning. In particular, a
            // middle click on a ScrollBar must retain the standard ScrollViewer interaction.
            return;
        }

        EndEditingGesture();
        _panPointerOrigin = e.GetPosition(_scrollViewer);
        _panHorizontalOffsetOrigin = _scrollViewer.HorizontalOffset;
        _panVerticalOffsetOrigin = _scrollViewer.VerticalOffset;
        _isMiddleButtonPanning = CaptureMouse();
        if (_isMiddleButtonPanning)
        {
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (_isMiddleButtonPanning)
        {
            if (e.MiddleButton != MouseButtonState.Pressed)
            {
                // Mouse capture can survive when a release is consumed outside this visual tree.
                // The physical button state is therefore the authoritative fallback termination.
                EndMiddleButtonPanning();
                return;
            }

            if (_scrollViewer is not null)
            {
                Point panPointer = e.GetPosition(_scrollViewer);
                _scrollViewer.ScrollToHorizontalOffset(
                    _panHorizontalOffsetOrigin - (panPointer.X - _panPointerOrigin.X)
                );
                _scrollViewer.ScrollToVerticalOffset(
                    _panVerticalOffsetOrigin - (panPointer.Y - _panPointerOrigin.Y)
                );
            }

            e.Handled = true;
            return;
        }

        if (
            _editedPanel is null
            || _editorGesture == EditorGesture.None
            || e.LeftButton != MouseButtonState.Pressed
        )
        {
            return;
        }

        if (_scrollViewer is not null)
        {
            _lastPointerInScrollViewer = e.GetPosition(_scrollViewer);
        }

        Point pointer = e.GetPosition(_editedPanel);
        if (
            _editorGesture is EditorGesture.NodeDragPending or EditorGesture.MarqueePending
            && !IsDragThresholdExceeded(e.GetPosition(this))
        )
        {
            e.Handled = true;
            return;
        }

        if (_editorGesture == EditorGesture.NodeDragPending)
        {
            _editorGesture = EditorGesture.NodeDrag;
            _pendingDragBefore = CapturePlacementStates(_dragOriginCenters.Keys);
            _editedPanel.BeginExtentHold();
            StartAutoScroll();
        }
        else if (_editorGesture == EditorGesture.MarqueePending)
        {
            _editorGesture = EditorGesture.Marquee;
        }

        if (_editorGesture == EditorGesture.NodeDrag)
        {
            UpdateDraggedPositions(pointer);
        }
        else if (_editorGesture == EditorGesture.Marquee)
        {
            UpdateMarqueeSelection(pointer);
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);
        if (e.ChangedButton != MouseButton.Middle || !_isMiddleButtonPanning)
        {
            return;
        }

        EndMiddleButtonPanning();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (_editedPanel is null || _editorGesture == EditorGesture.None)
        {
            return;
        }

        if (_editorGesture == EditorGesture.Marquee)
        {
            UpdateMarqueeSelection(e.GetPosition(_editedPanel));
        }
        else if (
            _editorGesture == EditorGesture.MarqueePending
            && (_gestureModifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == 0
        )
        {
            // A click, rather than a drag, on the actual map surface is the only blank-space
            // gesture that clears selection. Template chrome never reaches this branch.
            ClearSelection();
        }

        EndEditingGesture();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndMiddleButtonPanning(releaseCapture: false);
        EndEditingGesture(releaseCapture: false);
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            base.OnPreviewMouseWheel(e);
            return;
        }

        ChangeZoomAtPointer(e.Delta > 0 ? ZoomFactor + ZoomStep : ZoomFactor - ZoomStep, e);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (
            CanEditSelection()
            && (Keyboard.Modifiers & ModifierKeys.Control) != 0
            && e.Key == Key.Z
        )
        {
            _ = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? Redo() : Undo();
            e.Handled = true;
            return;
        }

        if (
            CanEditSelection()
            && (Keyboard.Modifiers & ModifierKeys.Control) != 0
            && e.Key == Key.Y
        )
        {
            _ = Redo();
            e.Handled = true;
            return;
        }

        if (CanEditSelection() && e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            SelectAll();
            e.Handled = true;
            return;
        }

        if (CanEditSelection() && e.Key == Key.Escape)
        {
            ClearSelection();
            e.Handled = true;
            return;
        }

        if (!CanEditSelection() || _selectedNodeKeys.Count == 0)
        {
            return;
        }

        double step = SnapToGrid ? GridSize : (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10D : 1D;
        Vector delta = e.Key switch
        {
            Key.Left => new Vector(-step, 0D),
            Key.Right => new Vector(step, 0D),
            Key.Up => new Vector(0D, -step),
            Key.Down => new Vector(0D, step),
            _ => default,
        };
        if (delta == default)
        {
            return;
        }

        MoveSelection(delta);
        e.Handled = true;
    }

    private void OnItemContainerClick(object sender, RoutedEventArgs e)
    {
        if (CanEditSelection())
        {
            e.Handled = true;
            return;
        }

        if (e.OriginalSource is not NavigationMapItem container)
        {
            return;
        }

        object item = ItemContainerGenerator.ItemFromContainer(container);
        if (!ReferenceEquals(item, DependencyProperty.UnsetValue))
        {
            OnNodeInvoked(item, container);
        }
    }

    private void BeginNodeDragGesture(NavigationMapPanel panel, MouseButtonEventArgs e)
    {
        _dragOriginCenters.Clear();
        foreach (NavigationMapItem selected in GetSelectedContainers())
        {
            if (selected.NodeKey is { Length: > 0 } key)
            {
                _dragOriginCenters[key] = Center(GetContainerBounds(selected));
            }
        }

        BeginEditorGesture(panel, e, EditorGesture.NodeDragPending);
    }

    private void BeginMarqueeGesture(NavigationMapPanel panel, MouseButtonEventArgs e)
    {
        _dragOriginCenters.Clear();
        BeginEditorGesture(panel, e, EditorGesture.MarqueePending);
    }

    private void BeginEditorGesture(
        NavigationMapPanel panel,
        MouseButtonEventArgs e,
        EditorGesture gesture)
    {
        EndEditingGesture();
        _editedPanel = panel;
        _editorGesture = gesture;
        _dragPointerOrigin = e.GetPosition(panel);
        _gesturePointerOriginInView = e.GetPosition(this);
        _gestureModifiers = Keyboard.Modifiers;
        _selectionAtGestureStart.Clear();
        _selectionAtGestureStart.UnionWith(_selectedNodeKeys);
        _pendingDragBefore = null;

        _scrollViewer ??= FindVisualDescendant<ScrollViewer>(this);
        if (_scrollViewer is not null)
        {
            _lastPointerInScrollViewer = e.GetPosition(_scrollViewer);
        }

        CaptureMouse();
    }

    private bool IsDragThresholdExceeded(Point pointerInView) =>
        Math.Abs(pointerInView.X - _gesturePointerOriginInView.X) >= SystemParameters.MinimumHorizontalDragDistance
        || Math.Abs(pointerInView.Y - _gesturePointerOriginInView.Y) >= SystemParameters.MinimumVerticalDragDistance;

    private void UpdateMarqueeSelection(Point pointer)
    {
        if (_editedPanel is null)
        {
            return;
        }

        var rectangle = new Rect(_dragPointerOrigin, pointer);
        _editedPanel.SetSelectionRectangle(rectangle);
        string[] hits = GetGeneratedContainers()
            .Where(container => container.IsVisible && rectangle.IntersectsWith(GetContainerBounds(container)))
            .Select(static container => container.NodeKey)
            .Where(static key => key is not null)
            .Cast<string>()
            .ToArray();
        var selection = new HashSet<string>(_selectionAtGestureStart, StringComparer.Ordinal);
        if ((_gestureModifiers & ModifierKeys.Control) != 0)
        {
            foreach (string key in hits)
            {
                if (!selection.Add(key))
                {
                    selection.Remove(key);
                }
            }
        }
        else
        {
            if ((_gestureModifiers & ModifierKeys.Shift) == 0)
            {
                selection.Clear();
            }

            selection.UnionWith(hits);
        }

        string? primary = PrimarySelectedNodeKey;
        if (primary is null || !selection.Contains(primary))
        {
            primary = hits.FirstOrDefault(selection.Contains) ?? selection.FirstOrDefault();
        }

        ApplySelection(selection, primary);
    }

    private void UpdateDraggedPositions(Point pointer)
    {
        if (_editedPanel is null || _dragOriginCenters.Count == 0)
        {
            return;
        }

        Vector delta = pointer - _dragPointerOrigin;
        if (
            SnapToGrid
            && PrimarySelectedNodeKey is { } primaryKey
            && _dragOriginCenters.TryGetValue(primaryKey, out Point primaryOrigin)
        )
        {
            Point desired = primaryOrigin + delta;
            delta = new Vector(Snap(desired.X) - primaryOrigin.X, Snap(desired.Y) - primaryOrigin.Y);
        }

        delta = ClampGroupDelta(delta, _dragOriginCenters);
        NavigationMapLayout layout = EnsureLayout();
        foreach (KeyValuePair<string, Point> origin in _dragOriginCenters)
        {
            layout.SetPosition(origin.Key, origin.Value + delta);
        }
    }

    private void MoveSelection(Vector requestedDelta)
    {
        var origins = new Dictionary<string, Point>(StringComparer.Ordinal);
        foreach (NavigationMapItem container in GetSelectedContainers())
        {
            if (container.NodeKey is { Length: > 0 } key)
            {
                origins[key] = Center(GetContainerBounds(container));
            }
        }

        Dictionary<string, PlacementState> before = CapturePlacementStates(origins.Keys);
        Vector delta = ClampGroupDelta(requestedDelta, origins);
        NavigationMapLayout layout = EnsureLayout();
        foreach (KeyValuePair<string, Point> origin in origins)
        {
            layout.SetPosition(origin.Key, origin.Value + delta);
        }

        RecordLayoutEdit(before, CapturePlacementStates(origins.Keys));
    }

    private bool AlignContainersToGrid(
        IReadOnlyList<NavigationMapItem> containers,
        bool recordHistory)
    {
        string[] keys = containers
            .Select(static container => container.NodeKey)
            .Where(static key => key is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (keys.Length == 0)
        {
            return false;
        }

        Dictionary<string, PlacementState> before = CapturePlacementStates(keys);
        NavigationMapLayout layout = EnsureLayout();
        foreach (NavigationMapItem container in containers)
        {
            if (container.NodeKey is not { Length: > 0 } key)
            {
                continue;
            }

            Point center = Center(GetContainerBounds(container));
            layout.SetPosition(key, ClampCenter(container, new Point(Snap(center.X), Snap(center.Y))));
        }

        if (recordHistory)
        {
            RecordLayoutEdit(before, CapturePlacementStates(keys));
        }

        return true;
    }

    private void ScheduleFullGridAlignment()
    {
        int requestVersion = ++_gridAlignmentRequestVersion;
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                if (
                    requestVersion != _gridAlignmentRequestVersion
                    || !CanEditSelection()
                    || !SnapToGrid
                )
                {
                    return;
                }

                // Automatic fallback geometry can change after reset, direction, spacing, item, or
                // node-size changes. Always wait for that layout pass, then materialize every center
                // together so a later group drag cannot mix old and newly snapped coordinates.
                UpdateLayout();
                _ = AlignContainersToGrid(GetGeneratedContainers().ToList(), recordHistory: false);
            })
        );
    }

    private void OnLayoutPlacementsCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs args)
    {
        // Clear() raises Reset before an application repopulates a loaded document. Context-idle
        // coalescing lets all synchronous additions finish and then normalizes the final layout,
        // while ordinary SetPosition additions during drag do not recursively schedule alignment.
        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            ScheduleFullGridAlignment();
        }
    }

    private Vector ClampGroupDelta(Vector delta, IReadOnlyDictionary<string, Point> origins)
    {
        double minimumX = double.PositiveInfinity;
        double minimumY = double.PositiveInfinity;
        foreach (KeyValuePair<string, Point> origin in origins)
        {
            if (FindContainer(origin.Key) is not { } container)
            {
                continue;
            }

            minimumX = Math.Min(minimumX, origin.Value.X - (container.RenderSize.Width / 2D));
            minimumY = Math.Min(minimumY, origin.Value.Y - (container.RenderSize.Height / 2D));
        }

        if (!double.IsPositiveInfinity(minimumX))
        {
            delta.X = Math.Max(delta.X, -minimumX);
            delta.Y = Math.Max(delta.Y, -minimumY);
        }

        return delta;
    }

    private void EndEditingGesture(bool releaseCapture = true)
    {
        CompletePendingDrag();
        NavigationMapPanel? panel = _editedPanel;
        _editedPanel = null;
        _editorGesture = EditorGesture.None;
        _selectionAtGestureStart.Clear();
        panel?.SetSelectionRectangle(null);
        panel?.EndExtentHold();
        StopAutoScroll();
        if (releaseCapture && IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void EndMiddleButtonPanning(bool releaseCapture = true)
    {
        if (!_isMiddleButtonPanning)
        {
            return;
        }

        _isMiddleButtonPanning = false;
        if (releaseCapture && IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void CompletePendingDrag()
    {
        if (_pendingDragBefore is not { } before)
        {
            return;
        }

        _pendingDragBefore = null;
        RecordLayoutEdit(before, CapturePlacementStates(before.Keys));
    }

    private void StartAutoScroll()
    {
        if (_isAutoScrollRendering)
        {
            return;
        }

        _lastAutoScrollFrame = DateTime.UtcNow;
        _isAutoScrollRendering = true;
        CompositionTarget.Rendering += OnAutoScrollFrame;
    }

    private void StopAutoScroll()
    {
        if (_isAutoScrollRendering)
        {
            CompositionTarget.Rendering -= OnAutoScrollFrame;
            _isAutoScrollRendering = false;
        }

        _autoScrollVelocity = default;
    }

    private void OnAutoScrollFrame(object? sender, EventArgs e)
    {
        if (
            _editedPanel is null
            || _editorGesture != EditorGesture.NodeDrag
            || _scrollViewer is null
            || Mouse.LeftButton != MouseButtonState.Pressed
        )
        {
            StopAutoScroll();
            return;
        }

        DateTime now = DateTime.UtcNow;
        double elapsed = Math.Min(0.05D, Math.Max(0D, (now - _lastAutoScrollFrame).TotalSeconds));
        _lastAutoScrollFrame = now;
        Vector target = new(
            GetAutoScrollSpeed(_lastPointerInScrollViewer.X, _scrollViewer.ViewportWidth),
            GetAutoScrollSpeed(_lastPointerInScrollViewer.Y, _scrollViewer.ViewportHeight)
        );
        double smoothing = Math.Min(1D, elapsed * 10D);
        _autoScrollVelocity += (target - _autoScrollVelocity) * smoothing;
        if (_autoScrollVelocity.Length < 0.5D)
        {
            return;
        }

        double oldHorizontal = _scrollViewer.HorizontalOffset;
        double oldVertical = _scrollViewer.VerticalOffset;
        _scrollViewer.ScrollToHorizontalOffset(oldHorizontal + (_autoScrollVelocity.X * elapsed));
        _scrollViewer.ScrollToVerticalOffset(oldVertical + (_autoScrollVelocity.Y * elapsed));
        _scrollViewer.UpdateLayout();
        if (
            oldHorizontal != _scrollViewer.HorizontalOffset
            || oldVertical != _scrollViewer.VerticalOffset
        )
        {
            // Scrolling changes the pointer's map coordinate even while the physical mouse remains
            // still, so update the drag on every animation frame instead of waiting for MouseMove.
            UpdateDraggedPositions(Mouse.GetPosition(_editedPanel));
        }
    }

    private double GetAutoScrollSpeed(double coordinate, double viewportLength)
    {
        if (coordinate < AutoScrollEdgeSize)
        {
            return -MaximumAutoScrollSpeed * Math.Min(1D, (AutoScrollEdgeSize - coordinate) / AutoScrollEdgeSize);
        }

        if (coordinate > viewportLength - AutoScrollEdgeSize)
        {
            return MaximumAutoScrollSpeed
                * Math.Min(1D, (coordinate - viewportLength + AutoScrollEdgeSize) / AutoScrollEdgeSize);
        }

        return 0D;
    }

    private void ChangeZoomAtPointer(double requestedZoom, MouseWheelEventArgs e)
    {
        if (_scrollViewer is null || FindVisualDescendant<NavigationMapPanel>(this) is not { } panel)
        {
            SetCurrentValue(ZoomFactorProperty, requestedZoom);
            return;
        }

        Point mapPoint = e.GetPosition(panel);
        Point viewportPoint = e.GetPosition(_scrollViewer);
        SetCurrentValue(ZoomFactorProperty, requestedZoom);
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (_scrollViewer is null || !panel.IsDescendantOf(this))
                {
                    return;
                }

                UpdateLayout();
                Point transformed = panel.TransformToAncestor(_scrollViewer).Transform(mapPoint);
                _scrollViewer.ScrollToHorizontalOffset(
                    _scrollViewer.HorizontalOffset + transformed.X - viewportPoint.X
                );
                _scrollViewer.ScrollToVerticalOffset(
                    _scrollViewer.VerticalOffset + transformed.Y - viewportPoint.Y
                );
            })
        );
    }

    private void ApplySelection(HashSet<string> newSelection, string? primaryKey)
    {
        string[] removed = _selectedNodeKeys.Except(newSelection, StringComparer.Ordinal).ToArray();
        string[] added = newSelection.Except(_selectedNodeKeys, StringComparer.Ordinal).ToArray();
        _selectedNodeKeys.Clear();
        _selectedNodeKeys.UnionWith(newSelection);
        if (primaryKey is null || !_selectedNodeKeys.Contains(primaryKey))
        {
            primaryKey = _selectedNodeKeys.FirstOrDefault();
        }

        SetValue(PrimarySelectedNodeKeyPropertyKey, primaryKey);
        foreach (NavigationMapItem container in GetGeneratedContainers())
        {
            string? key = container.NodeKey;
            bool selected = key is not null && _selectedNodeKeys.Contains(key);
            container.SetLayoutSelected(
                selected,
                selected && StringComparer.Ordinal.Equals(key, primaryKey)
            );
        }

        if (removed.Length > 0 || added.Length > 0)
        {
            RaiseEvent(new SelectionChangedEventArgs(SelectionChangedEvent, removed, added));
        }
    }

    private void RefreshGeneratedState()
    {
        var existingKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (NavigationMapItem container in GetGeneratedContainers())
        {
            object item = ItemContainerGenerator.ItemFromContainer(container);
            if (container.NodeKey is { Length: > 0 } key)
            {
                existingKeys.Add(key);
            }

            UpdateContainerStates(container, item);
        }

        _selectedNodeKeys.IntersectWith(existingKeys);
        if (PrimarySelectedNodeKey is { } primary && !_selectedNodeKeys.Contains(primary))
        {
            SetValue(PrimarySelectedNodeKeyPropertyKey, _selectedNodeKeys.FirstOrDefault());
        }

        RefreshSearchMatches();
    }

    private void UpdateContainerStates(NavigationMapItem container, object item)
    {
        bool selected = container.NodeKey is { } key && _selectedNodeKeys.Contains(key);
        container.SetLayoutSelected(
            selected,
            selected && StringComparer.Ordinal.Equals(container.NodeKey, PrimarySelectedNodeKey)
        );
        bool match = IsSearchMatch(item, SearchText);
        container.SetSearchMatch(match, false);
    }

    private void RefreshSearchMatches()
    {
        List<NavigationMapItem> matches = [];
        foreach (NavigationMapItem container in GetGeneratedContainers())
        {
            object item = ItemContainerGenerator.ItemFromContainer(container);
            bool match = IsSearchMatch(item, SearchText);
            container.SetSearchMatch(match, false);
            if (match)
            {
                matches.Add(container);
            }
        }

        SetValue(SearchResultCountPropertyKey, matches.Count);
        SetValue(CurrentSearchResultIndexPropertyKey, -1);
    }

    private bool MoveCurrentSearchResult(int direction)
    {
        List<NavigationMapItem> matches = GetGeneratedContainers().Where(static item => item.IsSearchMatch).ToList();
        if (matches.Count == 0)
        {
            return false;
        }

        int current = CurrentSearchResultIndex;
        int next = current < 0
            ? direction > 0 ? 0 : matches.Count - 1
            : (current + direction + matches.Count) % matches.Count;
        for (int index = 0; index < matches.Count; index++)
        {
            matches[index].SetSearchMatch(true, index == next);
        }

        SetValue(CurrentSearchResultIndexPropertyKey, next);
        NavigationMapItem container = matches[next];
        if (container.NodeKey is { Length: > 0 } key)
        {
            ApplySelection(new HashSet<string>(StringComparer.Ordinal) { key }, key);
        }

        _ = CanEditSelection() ? Focus() : container.Focus();
        container.BringIntoView();
        return true;
    }

    private static bool IsSearchMatch(object item, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return false;
        }

        string query = searchText.Trim();
        return item switch
        {
            NavigationMapNode node => Contains(node.Key, query)
                || Contains(node.Content?.ToString(), query)
                || Contains(node.Description, query),
            NavigationMapItem container => Contains(container.NodeKey, query)
                || Contains(container.Content?.ToString(), query)
                || Contains(container.Description, query),
            _ => Contains(item?.ToString(), query),
        };
    }

    private static bool Contains(string? value, string query) =>
        value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

    private Dictionary<string, PlacementState> CapturePlacementStates(IEnumerable<string> keys)
    {
        var states = new Dictionary<string, PlacementState>(StringComparer.Ordinal);
        foreach (string key in keys.Distinct(StringComparer.Ordinal))
        {
            if (
                LayoutDefinition is { } layout
                && layout.TryGetPlacement(key, out NavigationMapPlacement? placement)
                && placement is not null
            )
            {
                states[key] = new PlacementState(true, placement.Position, placement.IsPinned);
            }
            else
            {
                states[key] = PlacementState.Missing;
            }
        }

        return states;
    }

    private void ApplyPlacementStates(IReadOnlyDictionary<string, PlacementState> states)
    {
        NavigationMapLayout layout = EnsureLayout();
        foreach (KeyValuePair<string, PlacementState> pair in states)
        {
            if (!pair.Value.Exists)
            {
                _ = layout.Remove(pair.Key);
                continue;
            }

            NavigationMapPlacement placement = layout.SetPosition(pair.Key, pair.Value.Position);
            placement.IsPinned = pair.Value.IsPinned;
        }
    }

    private void RecordLayoutEdit(
        Dictionary<string, PlacementState> before,
        Dictionary<string, PlacementState> after)
    {
        if (
            before.Count != after.Count
            || before.Any(pair => !after.TryGetValue(pair.Key, out PlacementState state) || state != pair.Value)
        )
        {
            const int maximumHistoryLength = 100;
            if (_undoHistory.Count == maximumHistoryLength)
            {
                _undoHistory.RemoveAt(0);
            }

            _undoHistory.Add(new LayoutEdit(before, after));
            _redoHistory.Clear();
            UpdateHistoryState();
        }
    }

    private void UpdateHistoryState()
    {
        SetValue(CanUndoPropertyKey, _undoHistory.Count > 0);
        SetValue(CanRedoPropertyKey, _redoHistory.Count > 0);
    }

    private NavigationMapLayout EnsureLayout()
    {
        if (LayoutDefinition is { } layout)
        {
            return layout;
        }

        layout = new NavigationMapLayout();
        SetCurrentValue(LayoutDefinitionProperty, layout);
        return layout;
    }

    private bool CanEditSelection() => IsEditing && LayoutMode == NavigationMapLayoutMode.Manual;

    private double Snap(double coordinate) => Math.Round(coordinate / GridSize) * GridSize;

    private static Point ClampCenter(NavigationMapItem container, Point center) =>
        new(
            Math.Max(container.RenderSize.Width / 2D, center.X),
            Math.Max(container.RenderSize.Height / 2D, center.Y)
        );

    private static Rect GetContainerBounds(NavigationMapItem container)
    {
        Vector offset = VisualTreeHelper.GetOffset(container);
        return new Rect(offset.X, offset.Y, container.RenderSize.Width, container.RenderSize.Height);
    }

    private static Point Center(Rect bounds) =>
        new(bounds.Left + (bounds.Width / 2D), bounds.Top + (bounds.Height / 2D));

    private List<NavigationMapItem> GetSelectedContainers() =>
        GetGeneratedContainers()
            .Where(container => container.NodeKey is { } key && _selectedNodeKeys.Contains(key))
            .ToList();

    private IEnumerable<NavigationMapItem> GetGeneratedContainers()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(index) is NavigationMapItem container)
            {
                yield return container;
            }
        }
    }

    private NavigationMapItem? FindContainer(string? nodeKey) =>
        nodeKey is null
            ? null
            : GetGeneratedContainers().FirstOrDefault(container =>
                StringComparer.Ordinal.Equals(container.NodeKey, nodeKey)
            );

    private static string? GetNodeKey(object item) =>
        item switch
        {
            NavigationMapNode node => node.Key,
            NavigationMapItem container => container.NodeKey,
            _ => null,
        };

    private static void BindContainer(
        DependencyObject target,
        DependencyProperty property,
        object source,
        string path)
    {
        BindingOperations.SetBinding(
            target,
            property,
            new Binding(path) { Source = source, Mode = BindingMode.OneWay }
        );
    }

    private static T? FindVisualAncestor<T>(DependencyObject? origin)
        where T : DependencyObject
    {
        DependencyObject? current = origin;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                FrameworkContentElement contentElement => contentElement.Parent,
                ContentElement content => ContentOperations.GetParent(content),
                _ => LogicalTreeHelper.GetParent(current),
            };
        }

        return null;
    }

    private static T? FindVisualDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            if (FindVisualDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static bool ValidateDirection(object? value) =>
        value is NavigationMapDirection.TopToBottom or NavigationMapDirection.LeftToRight;

    private static bool ValidateLayoutMode(object? value) =>
        value is NavigationMapLayoutMode.Hierarchical or NavigationMapLayoutMode.Manual;

    private static bool ValidatePositiveDouble(object? value) =>
        value is double number && number > 0D && !double.IsInfinity(number) && !double.IsNaN(number);

    private static bool ValidateNonNegativeDouble(object? value) =>
        value is double number && number >= 0D && !double.IsInfinity(number) && !double.IsNaN(number);

    private static void OnIsEditingChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var view = (NavigationMapView)dependencyObject;
        if ((bool)args.NewValue)
        {
            view.ScheduleFullGridAlignment();
        }
        else
        {
            view._gridAlignmentRequestVersion++;
            view.EndMiddleButtonPanning();
            view.EndEditingGesture();
            view.ClearSelection();
        }
    }

    private static void OnLayoutModeChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        dependencyObject.CoerceValue(IsEditingProperty);
        if ((NavigationMapLayoutMode)args.NewValue != NavigationMapLayoutMode.Manual)
        {
            ((NavigationMapView)dependencyObject).ClearSelection();
        }
    }

    private static void OnSearchTextChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs _)
    {
        ((NavigationMapView)dependencyObject).RefreshSearchMatches();
    }

    private static void OnLayoutDefinitionChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var view = (NavigationMapView)dependencyObject;
        if (args.OldValue is NavigationMapLayout oldLayout)
        {
            CollectionChangedEventManager.RemoveHandler(
                oldLayout.Placements,
                view.OnLayoutPlacementsCollectionChanged
            );
        }

        if (args.NewValue is NavigationMapLayout newLayout)
        {
            // The layout is commonly view-model-owned. WPF's weak collection manager prevents the
            // model from retaining a discarded view while still letting Clear trigger normalization.
            CollectionChangedEventManager.AddHandler(
                newLayout.Placements,
                view.OnLayoutPlacementsCollectionChanged
            );
        }

        // History records coordinates relative to one concrete model. Replacing that model is a
        // document boundary; retaining transactions would allow Undo to write an unrelated file's
        // coordinates into the newly loaded document.
        view.ClearLayoutHistory();
        view.ScheduleFullGridAlignment();
    }

    private static void OnAutomaticLayoutInputChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs _)
    {
        // These inputs can move hierarchical fallback nodes without touching LayoutDefinition.
        // Normalize only after WPF has completed the invalidated measure/arrange pass.
        ((NavigationMapView)dependencyObject).ScheduleFullGridAlignment();
    }

    private static void OnSnapToGridChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if ((bool)args.NewValue)
        {
            ((NavigationMapView)dependencyObject).ScheduleFullGridAlignment();
        }
    }

    private static void OnZoomLimitChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs _)
    {
        dependencyObject.CoerceValue(MaximumZoomFactorProperty);
        dependencyObject.CoerceValue(ZoomFactorProperty);
    }

    private static object CoerceIsEditing(DependencyObject dependencyObject, object? baseValue) =>
        ((NavigationMapView)dependencyObject).LayoutMode == NavigationMapLayoutMode.Manual
        && baseValue is true;

    private static object CoerceMaximumZoomFactor(DependencyObject dependencyObject, object? value) =>
        Math.Max(((NavigationMapView)dependencyObject).MinimumZoomFactor, (double)value!);

    private static object CoerceZoomFactor(DependencyObject dependencyObject, object? value)
    {
        var view = (NavigationMapView)dependencyObject;
        return Math.Max(view.MinimumZoomFactor, Math.Min(view.MaximumZoomFactor, (double)value!));
    }

    private readonly record struct PlacementState(bool Exists, Point Position, bool IsPinned)
    {
        internal static PlacementState Missing { get; } = new(false, default, false);
    }

    private sealed class LayoutEdit(
        Dictionary<string, PlacementState> before,
        Dictionary<string, PlacementState> after)
    {
        internal Dictionary<string, PlacementState> Before { get; } = before;

        internal Dictionary<string, PlacementState> After { get; } = after;
    }

    private enum EditorGesture
    {
        None,
        NodeDragPending,
        NodeDrag,
        MarqueePending,
        Marquee,
    }
}
