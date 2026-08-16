// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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
/// panning, and zoom. Standard WPF routed commands own common editing, movement, and zoom actions;
/// <see cref="NavigationMapCommands"/> supplies map-specific selection, search, alignment, and
/// viewport actions. Search state remains independent of a fixed toolbar, allowing an application
/// to present its own Fluent search UI and target this control from buttons or menus. The default
/// template supplies the required scrolling and zoom host.
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
public partial class NavigationMapView : ItemsControl
{
    private const string ScrollViewerPartName = "PART_ScrollViewer";

    #region DP: LayoutMode

    /// <summary>Identifies the <see cref="LayoutMode"/> dependency property.</summary>
    public static readonly DependencyProperty LayoutModeProperty = DependencyProperty.Register(
        nameof(LayoutMode),
        typeof(NavigationMapLayoutMode),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(
            NavigationMapLayoutMode.Hierarchical,
            OnLayoutModeChanged
        ),
        validateValueCallback: static value =>
            value is NavigationMapLayoutMode.Hierarchical or NavigationMapLayoutMode.Manual
    );

    /// <summary>Gets or sets the strategy used to place navigation-map nodes.</summary>
    public NavigationMapLayoutMode LayoutMode
    {
        get => (NavigationMapLayoutMode)GetValue(LayoutModeProperty);
        set => SetValue(LayoutModeProperty, value);
    }

    #endregion

    #region DP: LayoutDefinition

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

    #endregion

    #region DP: IsEditing

    /// <summary>Identifies the <see cref="IsEditing"/> dependency property.</summary>
    public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register(
        nameof(IsEditing),
        typeof(bool),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(
            false,
            OnIsEditingChanged,
            coerceValueCallback: static (d, baseValue) =>
                ((NavigationMapView)d).LayoutMode == NavigationMapLayoutMode.Manual && baseValue is true
        )
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

    #endregion

    #region DP: Direction

    /// <summary>Identifies the <see cref="Direction"/> dependency property.</summary>
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
        nameof(Direction),
        typeof(NavigationMapDirection),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(
            NavigationMapDirection.TopToBottom,
            flags: FrameworkPropertyMetadataOptions.AffectsMeasure
                 | FrameworkPropertyMetadataOptions.AffectsArrange
                 | FrameworkPropertyMetadataOptions.AffectsRender,
            OnAutomaticLayoutInputChanged
        ),
        validateValueCallback: static value =>
            value is NavigationMapDirection.TopToBottom or NavigationMapDirection.LeftToRight
    );

    /// <summary>Gets or sets the direction in which hierarchy levels advance.</summary>
    public NavigationMapDirection Direction
    {
        get => (NavigationMapDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    #endregion

    #region DP: HorizontalSpacing

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

    #endregion

    #region DP: VerticalSpacing

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

    #endregion

    #region DP: ConnectorBrush

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

    #endregion

    #region DP: ConnectorThickness

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

    #endregion

    #region DP: HighlightedConnectorBrush

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

    #endregion

    #region DP: HighlightedConnectorThickness

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

    #endregion

    #region DP: RoutingClearance

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

    #endregion

    #region DP: ShowRouteKey

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

    #endregion

    #region DP: SnapToGrid

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

    #endregion

    #region DP: ShowGrid

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

    #endregion

    #region DP: GridSize

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

    #endregion

    #region DP: ZoomFactor

    /// <summary>Identifies the <see cref="ZoomFactor"/> dependency property.</summary>
    public static readonly DependencyProperty ZoomFactorProperty = DependencyProperty.Register(
        nameof(ZoomFactor),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(
            1D,
            OnZoomFactorChanged,
            coerceValueCallback: static (d, value) =>
            {
                var view = (NavigationMapView)d;
                return Math.Max(
                    view.MinimumZoomFactor,
                    Math.Min(view.MaximumZoomFactor, (double)value)
                );
            }
        ),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the visual scale applied to the complete map and its scrolling extent.</summary>
    public double ZoomFactor
    {
        get => (double)GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    #endregion

    #region DP: MinimumZoomFactor

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

    #endregion

    #region DP: MaximumZoomFactor

    /// <summary>Identifies the <see cref="MaximumZoomFactor"/> dependency property.</summary>
    public static readonly DependencyProperty MaximumZoomFactorProperty = DependencyProperty.Register(
        nameof(MaximumZoomFactor),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(
            3D,
            OnZoomLimitChanged,
            coerceValueCallback: (d, value) => Math.Max(
                ((NavigationMapView)d).MinimumZoomFactor,
                (double)value
            )
        ),
        ValidatePositiveDouble
    );

    /// <summary>Gets or sets the largest permitted <see cref="ZoomFactor"/>.</summary>
    public double MaximumZoomFactor
    {
        get => (double)GetValue(MaximumZoomFactorProperty);
        set => SetValue(MaximumZoomFactorProperty, value);
    }

    #endregion

    #region DP: ZoomStep

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

    #endregion

    #region DP: AutoScrollEdgeSize

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

    #endregion

    #region DP: MaximumAutoScrollSpeed

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

    #endregion

    #region DP: SearchText

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

    #endregion

    #region Read-only DP: SearchResultCount

    private static readonly DependencyPropertyKey SearchResultCountPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(SearchResultCount),
            typeof(int),
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(0)
        );

    /// <summary>Identifies the <see cref="SearchResultCount"/> dependency property.</summary>
    public static readonly DependencyProperty SearchResultCountProperty =
        SearchResultCountPropertyKey.DependencyProperty;

    /// <summary>Gets the number of nodes matching <see cref="SearchText"/>.</summary>
    public int SearchResultCount => (int)GetValue(SearchResultCountProperty);

    #endregion

    #region Read-only DP: CurrentSearchResultIndex

    private static readonly DependencyPropertyKey CurrentSearchResultIndexPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CurrentSearchResultIndex),
            typeof(int),
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(-1)
        );

    /// <summary>Identifies the <see cref="CurrentSearchResultIndex"/> dependency property.</summary>
    public static readonly DependencyProperty CurrentSearchResultIndexProperty =
        CurrentSearchResultIndexPropertyKey.DependencyProperty;

    /// <summary>Gets the zero-based current search-result index, or -1 when no result is active.</summary>
    public int CurrentSearchResultIndex => (int)GetValue(CurrentSearchResultIndexProperty);

    #endregion

    #region Read-only DP: PrimarySelectedNodeKey

    private static readonly DependencyPropertyKey PrimarySelectedNodeKeyPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PrimarySelectedNodeKey),
            typeof(string),
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>Identifies the <see cref="PrimarySelectedNodeKey"/> dependency property.</summary>
    public static readonly DependencyProperty PrimarySelectedNodeKeyProperty =
        PrimarySelectedNodeKeyPropertyKey.DependencyProperty;

    /// <summary>Gets the key of the focused alignment and connector-highlight target.</summary>
    public string? PrimarySelectedNodeKey => (string?)GetValue(PrimarySelectedNodeKeyProperty);

    #endregion

    #region Read-only DP: CanUndo

    private static readonly DependencyPropertyKey CanUndoPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanUndo),
            typeof(bool),
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="CanUndo"/> dependency property.</summary>
    public static readonly DependencyProperty CanUndoProperty = CanUndoPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether a layout-editor transaction can be undone.</summary>
    public bool CanUndo => (bool)GetValue(CanUndoProperty);

    #endregion

    #region Read-only DP: CanRedo

    private static readonly DependencyPropertyKey CanRedoPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanRedo),
            typeof(bool),
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="CanRedo"/> dependency property.</summary>
    public static readonly DependencyProperty CanRedoProperty = CanRedoPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether an undone layout transaction can be reapplied.</summary>
    public bool CanRedo => (bool)GetValue(CanRedoProperty);

    #endregion

    #region DP: Command

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

    #endregion

    #region Event: NodeInvoked

    /// <summary>Identifies the <see cref="NodeInvoked"/> routed event.</summary>
    public static readonly RoutedEvent NodeInvokedEvent = EventManager.RegisterRoutedEvent(
        nameof(NodeInvoked),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<NavigationMapView, NavigationMapNodeInvokedEventArgs>),
        typeof(NavigationMapView)
    );

    /// <summary>Occurs when a generated or directly supplied map item is activated.</summary>
    public event TypedEventHandler<NavigationMapView, NavigationMapNodeInvokedEventArgs> NodeInvoked
    {
        add => AddHandler(NodeInvokedEvent, value);
        remove => RemoveHandler(NodeInvokedEvent, value);
    }

    #endregion

    #region Event: SelectionChanged

    /// <summary>Identifies the <see cref="SelectionChanged"/> routed event.</summary>
    public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(SelectionChanged),
        RoutingStrategy.Bubble,
        typeof(SelectionChangedEventHandler),
        typeof(NavigationMapView)
    );

    /// <summary>Occurs after the editor or a public selection method changes selected nodes.</summary>
    public event SelectionChangedEventHandler SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    #endregion

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

        RegisterCommandBindings();
    }

    /// <summary>Initializes a new instance of the <see cref="NavigationMapView"/> class.</summary>
    public NavigationMapView()
    {
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnItemContainerClick));
        SetCurrentValue(LayoutDefinitionProperty, new NavigationMapLayout());
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
    /// <seealso cref="NavigationMapCommands.SelectNode"/>
    public bool SelectNode(string nodeKey, bool preserveSelection = false)
    {
        var container = FindContainer(nodeKey);
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
    /// <seealso cref="NavigationMapCommands.ClearSelection"/>
    public void ClearSelection() => ApplySelection([], null);

    /// <summary>Selects every generated node and retains the current primary target.</summary>
    /// <remarks>
    /// This editor command is intentionally a no-op outside active Manual editing. Read-only maps
    /// use ordinary node invocation and must not acquire an invisible bulk-selection state.
    /// </remarks>
    /// <seealso cref="ApplicationCommands.SelectAll"/>
    public void SelectAll()
    {
        if (!CanEditSelection())
        {
            return;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var primary = PrimarySelectedNodeKey;

        foreach (var container in GetGeneratedContainers())
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
    /// <seealso cref="NavigationMapCommands.AlignToGrid"/>
    public bool AlignToGrid()
    {
        if (!CanEditSelection())
        {
            return false;
        }

        var selected = GetSelectedContainers();
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
    /// <seealso cref="NavigationMapCommands.ResetLayout"/>
    public void ResetLayout()
    {
        EnsureLayout().Clear();
        ClearLayoutHistory();
        ScheduleFullGridAlignment();
    }

    /// <summary>Aligns selected nodes to the primary selected node.</summary>
    /// <param name="alignment">The edge or center axis to share.</param>
    /// <returns><see langword="true"/> when at least two generated nodes were aligned.</returns>
    /// <seealso cref="NavigationMapCommands.AlignSelection"/>
    public bool AlignSelection(NavigationMapAlignment alignment)
    {
        if (!CanEditSelection() || FindContainer(PrimarySelectedNodeKey) is not { } primary)
        {
            return false;
        }

        var selected = GetSelectedContainers();
        if (selected.Count < 2)
        {
            return false;
        }

        var selectedKeys = selected
            .Select(static container => container.NodeKey)
            .Where(static key => key is not null)
            .Cast<string>()
            .ToArray();

        var before = CapturePlacementStates(selectedKeys);
        var target = GetContainerBounds(primary);

        foreach (var container in selected)
        {
            if (ReferenceEquals(container, primary) || container.NodeKey is not { Length: > 0 } key)
            {
                continue;
            }

            var bounds = GetContainerBounds(container);
            var center = Center(bounds);

            center = alignment switch
            {
                // ReSharper disable UseWithExpressionToCopyStruct
                NavigationMapAlignment.Left => new Point(target.Left + (bounds.Width / 2D), center.Y),
                NavigationMapAlignment.HorizontalCenter => new Point(Center(target).X, center.Y),
                NavigationMapAlignment.Right => new Point(target.Right - (bounds.Width / 2D), center.Y),
                NavigationMapAlignment.Top => new Point(center.X, target.Top + (bounds.Height / 2D)),
                NavigationMapAlignment.VerticalCenter => new Point(center.X, Center(target).Y),
                NavigationMapAlignment.Bottom => new Point(center.X, target.Bottom - (bounds.Height / 2D)),
                // ReSharper restore UseWithExpressionToCopyStruct
                _ => throw new ArgumentOutOfRangeException(nameof(alignment)),
            };

            EnsureLayout().SetPosition(key, ClampCenter(container, center));
        }

        RecordLayoutEdit(before, CapturePlacementStates(selectedKeys));
        return true;
    }

    /// <summary>Activates the next search result, selecting and scrolling it into view.</summary>
    /// <returns><see langword="true"/> when a result exists.</returns>
    /// <seealso cref="NavigationMapCommands.FindNext"/>
    public bool FindNext() => MoveCurrentSearchResult(1);

    /// <summary>Activates the previous search result, selecting and scrolling it into view.</summary>
    /// <returns><see langword="true"/> when a result exists.</returns>
    /// <seealso cref="NavigationMapCommands.FindPrevious"/>
    public bool FindPrevious() => MoveCurrentSearchResult(-1);

    /// <summary>Increases <see cref="ZoomFactor"/> by <see cref="ZoomStep"/>.</summary>
    /// <seealso cref="NavigationCommands.IncreaseZoom"/>
    public void ZoomIn() => SetCurrentValue(ZoomFactorProperty, ZoomFactor + ZoomStep);

    /// <summary>Decreases <see cref="ZoomFactor"/> by <see cref="ZoomStep"/>.</summary>
    /// <seealso cref="NavigationCommands.DecreaseZoom"/>
    public void ZoomOut() => SetCurrentValue(ZoomFactorProperty, ZoomFactor - ZoomStep);

    /// <summary>Restores a one-to-one map scale.</summary>
    /// <seealso cref="NavigationMapCommands.ResetZoom"/>
    public void ResetZoom() => SetCurrentValue(ZoomFactorProperty, 1D);

    /// <summary>Scales the complete map to fit the current scrolling viewport.</summary>
    /// <returns><see langword="true"/> when the template and map panel are available.</returns>
    /// <seealso cref="NavigationMapCommands.ZoomToFit"/>
    public bool ZoomToFit()
    {
        var panel = FindVisualDescendant<NavigationMapPanel>(this);
        if (_scrollViewer is null || panel is null || panel.RenderSize.Width <= 0D || panel.RenderSize.Height <= 0D)
        {
            return false;
        }

        var horizontal = _scrollViewer.ViewportWidth / panel.RenderSize.Width;
        var vertical   = _scrollViewer.ViewportHeight / panel.RenderSize.Height;

        SetCurrentValue(ZoomFactorProperty, Math.Min(horizontal, vertical));
        _scrollViewer.ScrollToHome();

        return true;
    }

    /// <summary>Reverts the most recent drag, nudge, or alignment transaction.</summary>
    /// <returns><see langword="true"/> when a transaction was reverted.</returns>
    /// <seealso cref="ApplicationCommands.Undo"/>
    public bool Undo()
    {
        if (_undoHistory.Count == 0)
        {
            return false;
        }

        var edit = _undoHistory[^1];

        _undoHistory.RemoveAt(_undoHistory.Count - 1);

        ApplyPlacementStates(edit.Before);
        _redoHistory.Add(edit);

        UpdateHistoryState();

        return true;
    }

    /// <summary>Reapplies the most recently undone layout-editor transaction.</summary>
    /// <returns><see langword="true"/> when a transaction was reapplied.</returns>
    /// <seealso cref="ApplicationCommands.Redo"/>
    public bool Redo()
    {
        if (_redoHistory.Count == 0)
        {
            return false;
        }

        var edit = _redoHistory[^1];

        _redoHistory.RemoveAt(_redoHistory.Count - 1);

        ApplyPlacementStates(edit.After);
        _undoHistory.Add(edit);

        UpdateHistoryState();

        return true;
    }

    /// <summary>
    /// Clears editor undo and redo history, for example after an application loads another file.
    /// </summary>
    /// <remarks>
    /// This is a document-lifecycle operation rather than a user edit and intentionally has no
    /// routed command. Applications should call it after replacing or externally loading a layout.
    /// </remarks>
    public void ClearLayoutHistory()
    {
        _pendingDragBefore = null;

        _undoHistory.Clear();
        _redoHistory.Clear();

        UpdateHistoryState();
    }

    private static bool ValidatePositiveDouble(object? value)
    {
        return value is double number and > 0D
            && !double.IsInfinity(number)
            && !double.IsNaN(number);
    }

    private static bool ValidateNonNegativeDouble(object? value)
    {
        return value is double number and >= 0D
            && !double.IsInfinity(number)
            && !double.IsNaN(number);
    }

    private static void OnIsEditingChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        var view = (NavigationMapView)d;

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

        CommandManager.InvalidateRequerySuggested();
    }

    private static void OnLayoutModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        d.CoerceValue(IsEditingProperty);

        if ((NavigationMapLayoutMode)args.NewValue != NavigationMapLayoutMode.Manual)
        {
            ((NavigationMapView)d).ClearSelection();
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
    {
        ((NavigationMapView)d).RefreshSearchMatches();
    }

    private static void OnLayoutDefinitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        var view = (NavigationMapView)d;

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

    private static void OnAutomaticLayoutInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
    {
        // These inputs can move hierarchical fallback nodes without touching LayoutDefinition.
        // Normalize only after WPF has completed the invalidated measure/arrange pass.
        ((NavigationMapView)d).ScheduleFullGridAlignment();
    }

    private static void OnSnapToGridChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        if ((bool)args.NewValue)
        {
            ((NavigationMapView)d).ScheduleFullGridAlignment();
        }
    }

    private static void OnZoomLimitChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
    {
        d.CoerceValue(MaximumZoomFactorProperty);
        d.CoerceValue(ZoomFactorProperty);
    }

    private static void OnZoomFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
