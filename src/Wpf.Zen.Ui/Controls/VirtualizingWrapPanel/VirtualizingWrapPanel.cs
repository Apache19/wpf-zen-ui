// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Describes the item-width policy selected by <see cref="VirtualizingWrapPanel"/> for its most
/// recent layout pass.
/// </summary>
public enum VirtualizingWrapPanelItemSizingMode
{
    /// <summary>
    /// Item widths come from their desired sizes. The panel does not impose a uniform width and
    /// cannot virtualize rows whose geometry depends on unrealized content.
    /// </summary>
    Natural,

    /// <summary>
    /// Every item uses either an explicit <see cref="VirtualizingWrapPanel.ItemWidth"/> or the width
    /// derived from item height and aspect ratio. Viewport changes affect only the column count.
    /// </summary>
    Fixed,

    /// <summary>
    /// The panel selects a column count and calculates a uniform width from the current viewport,
    /// horizontal spacing, item count, and the configured width constraints.
    /// </summary>
    Responsive,
}

/// <summary>
/// Describes how a <see cref="VirtualizingWrapPanel"/> positions its horizontal item layout when it
/// does not consume the full available width.
/// </summary>
public enum VirtualizingWrapPanelItemsAlignment
{
    /// <summary>
    /// Places the item layout at the beginning of the available horizontal space. This is the
    /// default.
    /// </summary>
    Start,

    /// <summary>
    /// Centers the item layout in the available horizontal space.
    /// </summary>
    Center,

    /// <summary>
    /// Places the item layout at the end of the available horizontal space.
    /// </summary>
    End,
}

/// <summary>
/// Arranges item containers in responsive, uniformly sized rows and realizes only the rows visible
/// in a vertically scrolling viewport when their geometry can be predicted.
/// </summary>
/// <remarks>
/// <para>
/// The panel has three deliberately distinct sizing behaviors. Natural sizing is used when neither
/// a hard width policy nor a complete height-and-ratio pair exists. A finite
/// <see cref="ItemWidth"/> selects fixed-width wrapping. When no hard width policy exists, the
/// combination of <see cref="ItemHeight"/> and <see cref="ItemAspectRatio"/> derives the same kind
/// of fixed width. Neither fixed form is stretched to fill the viewport. Responsive sizing is
/// selected only when <see cref="ItemWidth"/> is automatic and at least one non-default
/// <see cref="MinimumItemWidth"/> or <see cref="MaximumItemWidth"/> constraint is present. The panel
/// then chooses a column count first and gives every column the same width, so a resize never leaves
/// a clipped partial column. The minimum determines when another column can fit without making text
/// or other content too narrow; the maximum prevents an existing column set from growing without
/// bound. When both are present, widths remain inside that interval and any space which cannot yet
/// accommodate another minimum-width item remains available for <see cref="ItemsAlignment"/>.
/// Responsive columns are also capped by the collection count, so a collection which already fits
/// on one row stops adding empty columns after its items reach the maximum. No preferred width is
/// needed: the two constraints have independent, observable responsibilities.
/// </para>
/// <para>
/// A predictable cell width and height are required to map an item index to a scroll row without
/// realizing the item. Consequently virtualization is active only in fixed or responsive sizing
/// when <see cref="ItemAspectRatio"/> or <see cref="ItemHeight"/> supplies the row height and the
/// panel can resolve a finite layout width. Height plus aspect ratio is sufficient because it also
/// derives a fixed width. If the geometry cannot be predicted, the panel measures every item and
/// behaves as a natural wrap panel. This fallback preserves correct layout for arbitrary content
/// instead of estimating unrealized sizes and publishing incorrect scrollbar information.
/// </para>
/// <para>
/// Use this panel as an <see cref="ItemsControl.ItemsPanel"/> and keep
/// <see cref="ScrollViewer.CanContentScrollProperty"/> enabled to use its virtualization and
/// scrolling. The standard <see cref="VirtualizingPanel.ScrollUnitProperty"/>,
/// <see cref="VirtualizingPanel.CacheLengthProperty"/>,
/// <see cref="VirtualizingPanel.CacheLengthUnitProperty"/>,
/// <see cref="VirtualizingPanel.IsVirtualizingWhenGroupingProperty"/>, and
/// <see cref="VirtualizingPanel.VirtualizationModeProperty"/> settings are honored. Item scrolling
/// uses one complete row as its logical unit. Intermediate offsets are row-aligned; only the terminal
/// offset may be fractional when that is required to place the content end flush with a viewport
/// whose height is not a whole-row multiple. Pixel scrolling is available without changing this
/// panel's API. Natural layout has no stable row map until every item has been measured, so it always
/// uses pixels. Recycling mode reuses item containers as they leave the viewport.
/// </para>
/// <para>
/// <see cref="IsVirtualizationActive"/> reports the effective result after layout. This is
/// intentionally separate from <see cref="VirtualizingPanel.IsVirtualizingProperty"/>, which is
/// only a request on the owning <see cref="ItemsControl"/>. Consumers can bind to the read-only
/// property when work must follow activation or fallback without duplicating the panel's geometry,
/// grouping, or viewport checks.
/// </para>
/// <para>
/// <see cref="ActualScrollUnit"/> similarly reports whether the current layout is using logical
/// rows or pixels. It can differ from the owning control's requested
/// <see cref="VirtualizingPanel.ScrollUnitProperty"/> when natural layout must fall back to pixels.
/// </para>
/// <para>
/// <see cref="ItemsAlignment"/> controls where horizontal space remaining after item sizing and
/// spacing is placed. Alignment never resizes items, changes column breakpoints, or affects
/// virtualization. Fixed and responsive layouts align one shared column grid, so a partial final
/// row remains aligned with the rows above it. Natural layout has no shared columns and therefore
/// aligns each variable-width row independently.
/// </para>
/// </remarks>
/// <example>
/// <code><![CDATA[
/// <ListBox
///     ScrollViewer.CanContentScroll="True"
///     VirtualizingPanel.IsVirtualizing="True"
///     VirtualizingPanel.VirtualizationMode="Recycling">
///     <ListBox.ItemsPanel>
///         <ItemsPanelTemplate>
///             <ui:VirtualizingWrapPanel
///                 HorizontalSpacing="12"
///                 VerticalSpacing="16"
///                 ItemsAlignment="Center"
///                 ItemAspectRatio="1.7777778"
///                 MinimumItemWidth="220"
///                 MaximumItemWidth="320" />
///         </ItemsPanelTemplate>
///     </ListBox.ItemsPanel>
/// </ListBox>
/// ]]></code>
/// </example>
public partial class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    private const double NaturalScrollLine = 16D;
    private const double HorizontalMouseWheelLines = 3D;

    private readonly Dictionary<UIElement, Rect> _naturalLayout = [];
    private UniformLayout _uniformLayout;
    private bool _usesManagedItemWidth;
    private bool _usesUniformLayout;
    private bool _usesAutomaticItemSizing;
    private bool _usesPixelBasedScrolling;
    private bool _isVirtualizing;
    private int _itemCount;
    private double _pixelViewportHeight;
    private ScrollData _scrollData;

    #region DP: HorizontalSpacing

    /// <summary>Identifies the <see cref="HorizontalSpacing"/> dependency property.</summary>
    public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
        nameof(HorizontalSpacing),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(
            0D,
            FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsArrange
        ),
        ValidateNonNegativeFiniteDouble
    );

    /// <summary>
    /// Gets or sets the horizontal gap between adjacent item cells, in device-independent pixels.
    /// The default is <c>0</c>. The spacing participates in column-breakpoint calculations.
    /// </summary>
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
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(
            0D,
            FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsArrange
        ),
        ValidateNonNegativeFiniteDouble
    );

    /// <summary>
    /// Gets or sets the vertical gap between adjacent item rows, in device-independent pixels.
    /// The default is <c>0</c>. The trailing row has no additional spacing.
    /// </summary>
    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    #endregion

    #region DP: ItemsAlignment

    /// <summary>Identifies the <see cref="ItemsAlignment"/> dependency property.</summary>
    public static readonly DependencyProperty ItemsAlignmentProperty = DependencyProperty.Register(
        nameof(ItemsAlignment),
        typeof(VirtualizingWrapPanelItemsAlignment),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(
            VirtualizingWrapPanelItemsAlignment.Start,
            FrameworkPropertyMetadataOptions.AffectsArrange
        ),
        ValidateItemsAlignment
    );

    /// <summary>
    /// Gets or sets how the horizontal item layout is positioned when it is narrower than the
    /// panel's available layout width. The default is
    /// <see cref="VirtualizingWrapPanelItemsAlignment.Start"/>.
    /// </summary>
    /// <remarks>
    /// The property applies to fixed, responsive, and natural layouts. In fixed and responsive
    /// layouts, all rows use the same grid origin; a partial final row occupies the leading columns
    /// of that grid instead of being centered or end-aligned separately. If the entire collection
    /// is shorter than one row, only its populated columns participate in alignment. Natural layout
    /// cannot establish shared columns because item widths vary, so each natural row is aligned by
    /// its own occupied width. The property changes only horizontal origins: item dimensions,
    /// <see cref="HorizontalSpacing"/>, wrapping breakpoints, extent calculation, and realized
    /// ranges remain unchanged. It affects arrange but does not require a new measure pass.
    /// </remarks>
    public VirtualizingWrapPanelItemsAlignment ItemsAlignment
    {
        get => (VirtualizingWrapPanelItemsAlignment)GetValue(ItemsAlignmentProperty);
        set => SetValue(ItemsAlignmentProperty, value);
    }

    #endregion

    #region DP: ItemWidth

    /// <summary>Identifies the <see cref="ItemWidth"/> dependency property.</summary>
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(
            double.NaN,
            FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsArrange
        ),
        ValidateAutoOrPositiveFiniteDouble
    );

    /// <summary>
    /// Gets or sets the fixed width assigned to every item, or <see cref="double.NaN"/> to leave the
    /// width automatic. The default is <see cref="double.NaN"/>.
    /// </summary>
    /// <remarks>
    /// A finite value always takes precedence over <see cref="MinimumItemWidth"/> and
    /// <see cref="MaximumItemWidth"/>. The panel wraps fixed-width cells but never expands or
    /// contracts them to consume remaining viewport space. Combine this property with
    /// <see cref="ItemHeight"/> or <see cref="ItemAspectRatio"/> to provide the predictable cell
    /// geometry required for row virtualization.
    /// </remarks>
    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    #endregion

    #region DP: ItemAspectRatio

    /// <summary>Identifies the <see cref="ItemAspectRatio"/> dependency property.</summary>
    public static readonly DependencyProperty ItemAspectRatioProperty = DependencyProperty.Register(
        nameof(ItemAspectRatio),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(
            double.NaN,
            FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsArrange
        ),
        ValidateAutoOrPositiveFiniteDouble
    );

    /// <summary>
    /// Gets or sets the item width divided by its height, or <see cref="double.NaN"/> to disable
    /// aspect-ratio inference. The default is <see cref="double.NaN"/>.
    /// </summary>
    /// <remarks>
    /// A finite positive value derives whichever dimension remains unresolved. It determines height
    /// from an explicit or responsive width; when <see cref="ItemHeight"/> is explicit and no hard
    /// width policy exists, it instead derives a fixed width. The derived fixed width is not stretched
    /// or contracted as the viewport changes. For example, an item height of <c>90</c> and a ratio of
    /// <c>1.7777778</c> produce a fixed width of <c>160</c>. When neither dimension is known,
    /// containers retain their natural widths and the ratio is applied after measurement without
    /// enabling virtualization.
    /// </remarks>
    public double ItemAspectRatio
    {
        get => (double)GetValue(ItemAspectRatioProperty);
        set => SetValue(ItemAspectRatioProperty, value);
    }

    #endregion

    #region DP: MinimumItemWidth

    /// <summary>Identifies the <see cref="MinimumItemWidth"/> dependency property.</summary>
    public static readonly DependencyProperty MinimumItemWidthProperty =
        DependencyProperty.Register(
            nameof(MinimumItemWidth),
            typeof(double),
            typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(
                0D,
                FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange
            ),
            ValidateMinimumItemWidth
        );

    /// <summary>
    /// Gets or sets the hard lower bound for a responsive item cell's width. A positive value opts
    /// into automatic responsive width calculation when <see cref="ItemWidth"/> is automatic. The
    /// default is <c>0</c>, which supplies no lower bound.
    /// </summary>
    /// <remarks>
    /// The panel removes a column before filling the available width would make its cells narrower
    /// than this value. If the viewport itself is narrower than one minimum-width cell, one column
    /// is retained at the minimum and the content overflows horizontally; silently shrinking below
    /// the requested constraint would reintroduce the text wrapping or clipping this property is
    /// intended to prevent. When this value exceeds <see cref="MaximumItemWidth"/>, the minimum wins
    /// for layout, matching WPF's general minimum-size precedence. A finite <see cref="ItemWidth"/>
    /// takes precedence over both responsive constraints. This property affects measure and arrange.
    /// </remarks>
    public double MinimumItemWidth
    {
        get => (double)GetValue(MinimumItemWidthProperty);
        set => SetValue(MinimumItemWidthProperty, value);
    }

    #endregion

    #region DP: MaximumItemWidth

    /// <summary>Identifies the <see cref="MaximumItemWidth"/> dependency property.</summary>
    public static readonly DependencyProperty MaximumItemWidthProperty =
        DependencyProperty.Register(
            nameof(MaximumItemWidth),
            typeof(double),
            typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(
                double.PositiveInfinity,
                FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange
            ),
            ValidateMaximumItemWidth
        );

    /// <summary>
    /// Gets or sets the upper bound for a responsive item cell's width. A finite value opts into
    /// automatic responsive width calculation when <see cref="ItemWidth"/> is automatic. The
    /// default is <see cref="double.PositiveInfinity"/>, which supplies no upper bound.
    /// </summary>
    /// <remarks>
    /// When the available width would make cells wider than this value, the panel increases the
    /// column count only if the resulting cells also satisfy <see cref="MinimumItemWidth"/>. Until
    /// then, the cells stay at this upper bound and the remaining space is positioned according to
    /// <see cref="ItemsAlignment"/>. The effective maximum is raised to the minimum if the configured
    /// bounds cross, so minimum content width has deterministic precedence. A finite
    /// <see cref="ItemWidth"/> takes precedence and disables responsive behavior. This property
    /// affects measure and arrange.
    /// </remarks>
    public double MaximumItemWidth
    {
        get => (double)GetValue(MaximumItemWidthProperty);
        set => SetValue(MaximumItemWidthProperty, value);
    }

    #endregion

    #region DP: ItemHeight

    /// <summary>Identifies the <see cref="ItemHeight"/> dependency property.</summary>
    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(
            double.NaN,
            FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsArrange
        ),
        ValidateAutoOrPositiveFiniteDouble
    );

    /// <summary>
    /// Gets or sets a uniform item height, or <see cref="double.NaN"/> to use either
    /// <see cref="ItemAspectRatio"/> or the content's desired height. The default is
    /// <see cref="double.NaN"/>.
    /// </summary>
    /// <remarks>
    /// An explicit height enables predictable row virtualization when a width policy is also
    /// available. If no hard width policy exists and <see cref="ItemAspectRatio"/> is finite, the
    /// panel derives a fixed width from their product; resizing the viewport changes only the column
    /// count. If a width is already explicit or responsive, this height takes precedence and the
    /// aspect ratio does not override two independently resolved dimensions.
    /// </remarks>
    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    #endregion

    #region Read-Only DP: ActualItemWidth

    private static readonly DependencyPropertyKey ActualItemWidthPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActualItemWidth),
            typeof(double),
            typeof(VirtualizingWrapPanel),
            new PropertyMetadata(0D)
        );

    /// <summary>Identifies the read-only <see cref="ActualItemWidth"/> dependency property.</summary>
    public static readonly DependencyProperty ActualItemWidthProperty =
        ActualItemWidthPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the width currently assigned to every cell in fixed or responsive layout, or <c>0</c>
    /// while natural content sizing is active or before the first finite-width measure pass.
    /// </summary>
    public double ActualItemWidth => (double)GetValue(ActualItemWidthProperty);

    #endregion

    #region Read-Only DP: ActualItemHeight

    private static readonly DependencyPropertyKey ActualItemHeightPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActualItemHeight),
            typeof(double),
            typeof(VirtualizingWrapPanel),
            new PropertyMetadata(0D)
        );

    /// <summary>Identifies the read-only <see cref="ActualItemHeight"/> dependency property.</summary>
    public static readonly DependencyProperty ActualItemHeightProperty =
        ActualItemHeightPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the height currently assigned to every cell in uniform layout, or <c>0</c> while natural
    /// content sizing is active or before the first finite-width measure pass.
    /// </summary>
    public double ActualItemHeight => (double)GetValue(ActualItemHeightProperty);

    #endregion

    #region Read-Only DP: Columns

    private static readonly DependencyPropertyKey ColumnsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Columns),
            typeof(int),
            typeof(VirtualizingWrapPanel),
            new PropertyMetadata(0)
        );

    /// <summary>Identifies the read-only <see cref="Columns"/> dependency property.</summary>
    public static readonly DependencyProperty ColumnsProperty = ColumnsPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the number of fixed or responsive item columns, or <c>0</c> while natural content sizing
    /// is active. A variable-height layout still reports its predictable column count even though
    /// it cannot virtualize rows.
    /// </summary>
    public int Columns => (int)GetValue(ColumnsProperty);

    #endregion

    #region Read-Only DP: IsVirtualizationActive

    private static readonly DependencyPropertyKey IsVirtualizationActivePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsVirtualizationActive),
            typeof(bool),
            typeof(VirtualizingWrapPanel),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the read-only <see cref="IsVirtualizationActive"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsVirtualizationActiveProperty =
        IsVirtualizationActivePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the most recent layout pass selected the row-virtualizing
    /// container-generation path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This value reports effective behavior rather than configuration intent. It becomes
    /// <see langword="true"/> only when the owning <see cref="ItemsControl"/> requests
    /// virtualization, grouping permits it, the viewport is finite, and the item geometry is
    /// predictable. Otherwise the panel realizes all items and reports <see langword="false"/>.
    /// </para>
    /// <para>
    /// A value of <see langword="true"/> means the virtualization algorithm is active; it does not
    /// promise that some containers are currently unrealized. A small collection, a large viewport,
    /// or a configured cache can legitimately make every item realized during an active pass.
    /// This is a read-only dependency property, so bindings and dependency-property observers are
    /// notified whenever a later measure pass changes the effective state.
    /// </para>
    /// </remarks>
    public bool IsVirtualizationActive => (bool)GetValue(IsVirtualizationActiveProperty);

    #endregion

    #region Read-Only DP: ActualScrollUnit

    private static readonly DependencyPropertyKey ActualScrollUnitPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActualScrollUnit),
            typeof(ScrollUnit),
            typeof(VirtualizingWrapPanel),
            new PropertyMetadata(ScrollUnit.Pixel)
        );

    /// <summary>
    /// Identifies the read-only <see cref="ActualScrollUnit"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActualScrollUnitProperty =
        ActualScrollUnitPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the scroll unit used by the most recent layout pass.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ScrollUnit.Item"/> means that one logical unit is one complete wrap row. Line,
    /// wheel, scroll-bar, and programmatic offsets are aligned to row boundaries except for the
    /// terminal offset needed to place the content end flush with a fractional-row viewport.
    /// <see cref="ScrollUnit.Pixel"/> means that offsets and vertical scroll information are in
    /// device-independent pixels.
    /// </para>
    /// <para>
    /// This value reports effective behavior rather than configuration intent. A predictable
    /// uniform layout honors the owning <see cref="ItemsControl"/>'s requested
    /// <see cref="VirtualizingPanel.ScrollUnitProperty"/>. Natural layout reports
    /// <see cref="ScrollUnit.Pixel"/> because rows cannot be addressed before their content has
    /// been measured. Bindings and dependency-property observers are notified after a later
    /// measure pass changes the effective unit.
    /// </para>
    /// </remarks>
    public ScrollUnit ActualScrollUnit => (ScrollUnit)GetValue(ActualScrollUnitProperty);

    #endregion

    #region Read-Only DP: ActualItemSizingMode

    private static readonly DependencyPropertyKey ActualItemSizingModePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActualItemSizingMode),
            typeof(VirtualizingWrapPanelItemSizingMode),
            typeof(VirtualizingWrapPanel),
            new PropertyMetadata(VirtualizingWrapPanelItemSizingMode.Natural)
        );

    /// <summary>
    /// Identifies the read-only <see cref="ActualItemSizingMode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActualItemSizingModeProperty =
        ActualItemSizingModePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the effective width policy selected during the most recent layout pass. This read-only
    /// value makes property-precedence decisions observable without introducing a second, competing
    /// sizing-mode input API.
    /// </summary>
    public VirtualizingWrapPanelItemSizingMode ActualItemSizingMode =>
        (VirtualizingWrapPanelItemSizingMode)GetValue(ActualItemSizingModeProperty);

    #endregion

    /// <inheritdoc />
    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        // VirtualizingPanel deliberately leaves add/remove/move handling to derived panels. Once the
        // generator has incorporated a mutation, disconnect every realized visual and let the next
        // measure reattach the surviving (or recycled) containers at their current generator
        // positions. This costs only the visible set during virtualization and avoids retaining a
        // removed natural-layout container whose generator index has become -1.
        if (InternalChildren.Count > 0)
        {
            RemoveInternalChildRange(0, InternalChildren.Count);
        }

        _naturalLayout.Clear();
        _naturalRowWidths.Clear();
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override void OnClearChildren()
    {
        _naturalLayout.Clear();
        _naturalRowWidths.Clear();
        _itemCount = 0;
        SetVerticalOffset(0D);
    }

    private void PublishLayoutState(
        bool usesManagedItemWidth,
        bool usesUniformLayout,
        bool isVirtualizing,
        bool usesAutomaticItemSizing
    )
    {
        SetValue(
            ActualItemWidthPropertyKey,
            usesManagedItemWidth ? _uniformLayout.ItemWidth : 0D
        );

        SetValue(
            ActualItemHeightPropertyKey,
            usesUniformLayout ? _uniformLayout.ItemHeight : 0D
        );

        SetValue(ColumnsPropertyKey, usesManagedItemWidth ? _uniformLayout.Columns : 0);
        SetValue(IsVirtualizationActivePropertyKey, isVirtualizing);
        SetValue(
            ActualScrollUnitPropertyKey,
            usesUniformLayout && !_usesPixelBasedScrolling
                ? ScrollUnit.Item
                : ScrollUnit.Pixel
        );

        SetValue(
            ActualItemSizingModePropertyKey,
            usesAutomaticItemSizing
                ? VirtualizingWrapPanelItemSizingMode.Responsive
                : usesManagedItemWidth
                    ? VirtualizingWrapPanelItemSizingMode.Fixed
                    : VirtualizingWrapPanelItemSizingMode.Natural
        );
    }

    private static bool ValidateNonNegativeFiniteDouble(object? value)
    {
        return value is double number and >= 0D
               && !double.IsInfinity(number)
               && !double.IsNaN(number);
    }

    private static bool ValidateAutoOrPositiveFiniteDouble(object? value)
    {
        return value is double number
            && (double.IsNaN(number) || (number > 0D && !double.IsInfinity(number)));
    }

    private static bool ValidateMaximumItemWidth(object? value)
    {
        return value is double number and > 0D && !double.IsNaN(number);
    }

    private static bool ValidateMinimumItemWidth(object? value)
    {
        return value is double number and >= 0D
               && !double.IsInfinity(number)
               && !double.IsNaN(number);
    }

    private static bool ValidateItemsAlignment(object? value)
    {
        return value is VirtualizingWrapPanelItemsAlignment.Start
            or VirtualizingWrapPanelItemsAlignment.Center
            or VirtualizingWrapPanelItemsAlignment.End;
    }

    private readonly record struct UniformLayout(
        int Columns,
        double ItemWidth,
        double ItemHeight,
        double RowStride
    );

    private readonly record struct ScrollData(
        double ExtentWidth,
        double ExtentHeight,
        double ViewportWidth,
        double ViewportHeight,
        double HorizontalOffset,
        double VerticalOffset
    );
}
