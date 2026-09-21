// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Controls;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Arranges keyboard items on a shared, uniformly sized logical grid.
/// </summary>
/// <remarks>
/// <para>
/// Direct children select a logical row through <see cref="RowProperty"/> and a horizontal slot
/// through <see cref="ColumnProperty"/> and <see cref="ColumnSpanProperty"/>. All rows use the same
/// <see cref="ColumnCount"/>, so an unoccupied column reserves space instead of distributing it
/// among populated keys. This permits naturally staggered keyboard rows.
/// </para>
/// <para>
/// Row identifiers are grouping keys: populated rows are ordered by identifier and gaps in the
/// identifiers do not create empty visual rows. All populated rows receive the same height.
/// </para>
/// <para>
/// <see cref="ColumnSpacing"/> is a grid gutter. A key that spans multiple columns also spans the
/// gutters between those columns, matching WPF <see cref="Grid"/> span geometry.
/// Under an undersized finite constraint, gutters contract before tracks reach zero.
/// </para>
/// <para>
/// The panel is non-virtualizing. When used as an <see cref="ItemsControl.ItemsPanel"/>, set the
/// attached properties on the generated item container rather than on the data template root.
/// </para>
/// </remarks>
public sealed class SoftKeyboardPanel : Panel
{
    private readonly List<SoftKeyboardPanelRow> _rows = [];
    private readonly List<(UIElement Child, int Row, bool Collapsed)> _rowAssignments = [];
    private readonly Dictionary<int, SoftKeyboardPanelRow> _rowLookup = [];

    static SoftKeyboardPanel()
    {
        // FrameworkElement can arrange a child at its unclipped desired size when its slot is smaller.
        // The child's layout clip normally protects its slot, while this panel-level clip provides the
        // final containment boundary for custom UIElement implementations and transformed content.
        ClipToBoundsProperty.OverrideMetadata(
            typeof(SoftKeyboardPanel),
            new FrameworkPropertyMetadata(true)
        );
    }

    #region DP: ColumnCount

    /// <summary>
    /// Identifies the <see cref="ColumnCount"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ColumnCountProperty
        = DependencyProperty.Register(
            nameof(ColumnCount),
            typeof(int),
            typeof(SoftKeyboardPanel),
            new FrameworkPropertyMetadata(
                1,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            ),
            static value => value is > 0
        );

    /// <summary>
    /// Gets or sets the number of equal-width columns shared by every row. The default is one.
    /// </summary>
    /// <remarks>
    /// Empty leading and trailing columns deliberately reserve space. Child columns and spans that
    /// exceed this count are clamped to the last available columns, following WPF Grid's defensive
    /// treatment of placement values when its definition count is smaller than requested.
    /// </remarks>
    public int ColumnCount
    {
        get => (int)GetValue(ColumnCountProperty);
        set => SetValue(ColumnCountProperty, value);
    }

    #endregion

    #region DP: RowSpacing

    /// <summary>
    /// Identifies the <see cref="RowSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty RowSpacingProperty
        = DependencyProperty.Register(
            nameof(RowSpacing),
            typeof(double),
            typeof(SoftKeyboardPanel),
            new FrameworkPropertyMetadata(
                4D,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            ),
            IsValidSpacing
        );

    /// <summary>
    /// Gets or sets the requested distance between adjacent populated rows. The default is 4 DIP.
    /// </summary>
    /// <remarks>
    /// The value must be finite and nonnegative. Under an undersized finite constraint, the panel
    /// reduces the effective spacing just enough to keep every row inside its final bounds.
    /// </remarks>
    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    #endregion

    #region DP: ColumnSpacing

    /// <summary>
    /// Identifies the <see cref="ColumnSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ColumnSpacingProperty
        = DependencyProperty.Register(
            nameof(ColumnSpacing),
            typeof(double),
            typeof(SoftKeyboardPanel),
            new FrameworkPropertyMetadata(
                4D,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            ),
            IsValidSpacing
        );

    /// <summary>
    /// Gets or sets the requested gutter between adjacent logical columns. The default is 4 DIP.
    /// </summary>
    /// <remarks>
    /// The value must be finite and nonnegative. Under an undersized finite constraint, the panel
    /// reduces the effective gutter just enough to keep every track inside its final bounds.
    /// </remarks>
    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    #endregion

    #region Attached-DP: Row

    /// <summary>
    /// Identifies the attached row-group property.
    /// </summary>
    public static readonly DependencyProperty RowProperty
        = DependencyProperty.RegisterAttached(
            "Row",
            typeof(int),
            typeof(SoftKeyboardPanel),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.AffectsParentMeasure
                | FrameworkPropertyMetadataOptions.AffectsParentArrange
            ),
            static value => value is >= 0
        );

    /// <summary>
    /// Gets the nonnegative logical row identifier assigned to an item container.
    /// </summary>
    /// <param name="element">
    /// The direct child of a <see cref="SoftKeyboardPanel"/>.
    /// </param>
    /// <returns>The logical row identifier. The default is zero.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static int GetRow(DependencyObject element)
    {
        return (int)element.GetValue(RowProperty);
    }

    /// <summary>
    /// Assigns a nonnegative logical row identifier to an item container.
    /// </summary>
    /// <param name="element">
    /// The direct child of a <see cref="SoftKeyboardPanel"/>.
    /// </param>
    /// <param name="value">
    /// The logical row identifier.
    /// </param>
    public static void SetRow(DependencyObject element, int value)
    {
        element.SetValue(RowProperty, value);
    }

    #endregion

    #region Attached-DP: Column

    /// <summary>
    /// Identifies the attached zero-based logical column property.
    /// </summary>
    public static readonly DependencyProperty ColumnProperty
        = DependencyProperty.RegisterAttached(
            "Column",
            typeof(int),
            typeof(SoftKeyboardPanel),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.AffectsParentMeasure
                | FrameworkPropertyMetadataOptions.AffectsParentArrange
            ),
            static value => value is >= 0
        );

    /// <summary>
    /// Gets the zero-based logical column assigned to an item container.
    /// </summary>
    /// <param name="element">
    /// The direct child of a <see cref="SoftKeyboardPanel"/>.
    /// </param>
    /// <returns>
    /// The logical column. The default is zero.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static int GetColumn(DependencyObject element)
    {
        return (int)element.GetValue(ColumnProperty);
    }

    /// <summary>
    /// Assigns a zero-based logical column to an item container.
    /// </summary>
    /// <param name="element">
    /// The direct child of a <see cref="SoftKeyboardPanel"/>.
    /// </param>
    /// <param name="value">
    /// The logical column.
    /// </param>
    public static void SetColumn(DependencyObject element, int value)
    {
        element.SetValue(ColumnProperty, value);
    }

    #endregion

    #region Attached-DP: ColumnSpan

    /// <summary>
    /// Identifies the attached logical column-span property.
    /// </summary>
    public static readonly DependencyProperty ColumnSpanProperty
        = DependencyProperty.RegisterAttached(
            "ColumnSpan",
            typeof(int),
            typeof(SoftKeyboardPanel),
            new FrameworkPropertyMetadata(
                1,
                FrameworkPropertyMetadataOptions.AffectsParentMeasure
                | FrameworkPropertyMetadataOptions.AffectsParentArrange
            ),
            static value => value is > 0
        );

    /// <summary>
    /// Gets the positive number of logical columns occupied by an item container.
    /// </summary>
    /// <param name="element">
    /// The direct child of a <see cref="SoftKeyboardPanel"/>.
    /// </param>
    /// <returns>
    /// The logical column span. The default is one.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static int GetColumnSpan(DependencyObject element)
    {
        return (int)element.GetValue(ColumnSpanProperty);
    }

    /// <summary>
    /// Assigns a positive logical column span to an item container.
    /// </summary>
    /// <param name="element">
    /// The direct child of a <see cref="SoftKeyboardPanel"/>.
    /// </param>
    /// <param name="value"> The logical column span.</param>
    public static void SetColumnSpan(DependencyObject element, int value)
    {
        element.SetValue(ColumnSpanProperty, value);
    }

    #endregion

    /// <summary>
    /// Measures children against their shared grid slots and derives the smallest uniform track
    /// that satisfies every child's desired width.
    /// </summary>
    /// <param name="availableSize">
    /// The size offered by the parent; either dimension may be infinite.
    /// </param>
    /// <returns>
    /// The desired size of the complete logical grid and populated rows.
    /// </returns>
    protected override Size MeasureOverride(Size availableSize)
    {
#if DEBUG
        var started = Stopwatch.GetTimestamp();
#endif
        // InternalChildren generates and prepares item containers on first access. Keep that work
        // separate from row indexing and child measurement when diagnosing a slow initial layout.
        var children = InternalChildren;
#if DEBUG
        var generated = Stopwatch.GetTimestamp();
#endif
        EnsureRows(children);
#if DEBUG
        var indexed = Stopwatch.GetTimestamp();
#endif
        MeasureCollapsedChildren(children);

        if (_rows.Count == 0)
        {
            return default;
        }

        var rowSpacing    = GetEffectiveSpacing(availableSize.Height, _rows.Count, RowSpacing);
        var rowHeight     = GetSharedExtent(availableSize.Height, _rows.Count, rowSpacing);
        var columnSpacing = GetEffectiveSpacing(availableSize.Width, ColumnCount, ColumnSpacing);
        var trackWidth    = GetSharedExtent(availableSize.Width, ColumnCount, columnSpacing);

        var desiredTrackWidth = 0D;
        var desiredRowHeight  = 0D;

        foreach (var row in _rows)
        {
            foreach (var child in row.Children)
            {
                var placement = GetPlacement(child);
                var width     = GetSpannedExtent(trackWidth, placement.ColumnSpan, columnSpacing);

                child.Measure(new Size(width, rowHeight));

                desiredTrackWidth = Math.Max(
                    desiredTrackWidth,
                    GetRequiredTrackExtent(child.DesiredSize.Width, placement.ColumnSpan, columnSpacing)
                );

                desiredRowHeight = Math.Max(desiredRowHeight, child.DesiredSize.Height);
            }
        }

        var desiredSize = new Size(
            GetTotalExtent(desiredTrackWidth * ColumnCount, ColumnCount, columnSpacing),
            GetTotalExtent(desiredRowHeight * _rows.Count, _rows.Count, rowSpacing)
        );
#if DEBUG
        var finished = Stopwatch.GetTimestamp();
        var millisecondsPerTick = 1000D / Stopwatch.Frequency;

        if ((finished - started) * millisecondsPerTick >= 16D)
        {
            Debug.WriteLine(
                $"[SoftKeyboard.Layout] Slow MEASURE completed; children={children.Count}, "
                + $"rows={_rows.Count}, columns={ColumnCount}, available={availableSize}, "
                + $"generationMs={(generated - started) * millisecondsPerTick:F3}, "
                + $"indexMs={(indexed - generated) * millisecondsPerTick:F3}, "
                + $"childrenMs={(finished - indexed) * millisecondsPerTick:F3}, "
                + $"totalMs={(finished - started) * millisecondsPerTick:F3}."
            );
        }
#endif
        return desiredSize;
    }

    /// <summary>
    /// Arranges every child in the slot addressed by its row, column, and span.
    /// </summary>
    /// <param name="finalSize">The size allocated by the parent.</param>
    /// <returns>The allocated size.</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // Placement and visibility can change between passes. Validate the cached grouping without
        // allocating row lists or delegates when only the available size or key content changed.
        var children = InternalChildren;

        EnsureRows(children);
        ArrangeCollapsedChildren(children);

        if (_rows.Count == 0)
        {
            return finalSize;
        }

        var rowSpacing    = GetEffectiveSpacing(finalSize.Height, _rows.Count, RowSpacing);
        var rowHeight     = GetSharedExtent(finalSize.Height, _rows.Count, rowSpacing);
        var columnSpacing = GetEffectiveSpacing(finalSize.Width, ColumnCount, ColumnSpacing);
        var trackWidth    = GetSharedExtent(finalSize.Width, ColumnCount, columnSpacing);
        var columnPitch   = trackWidth + columnSpacing;
        var y             = 0D;

        for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            var row    = _rows[rowIndex];
            var height = rowIndex == _rows.Count - 1 ? Math.Max(0D, finalSize.Height - y) : rowHeight;

            foreach (var child in row.Children)
            {
                var placement = GetPlacement(child);
                var x         = placement.Column * columnPitch;

                var width = Math.Min(
                    GetSpannedExtent(trackWidth, placement.ColumnSpan, columnSpacing),
                    Math.Max(0D, finalSize.Width - x)
                );

                child.Arrange(new Rect(x, y, width, height));
            }

            y += rowHeight + rowSpacing;
        }

        return finalSize;
    }

    /// <summary>
    /// Restricts gutters to finite nonnegative distances accepted by WPF layout.
    /// </summary>
    private static bool IsValidSpacing(object? value)
    {
        return value is double spacing and >= 0D && !double.IsInfinity(spacing);
    }

    /// <summary>
    /// Contracts gutters when they alone would exceed the finite space offered by the parent.
    /// </summary>
    private static double GetEffectiveSpacing(double available, int count, double requested)
    {
        if (count <= 1 || double.IsInfinity(available))
        {
            return requested;
        }

        return Math.Min(requested, Math.Max(0D, available) / (count - 1));
    }

    /// <summary>
    /// Divides usable space uniformly while preserving an unconstrained measurement axis.
    /// </summary>
    private static double GetSharedExtent(double available, int count, double spacing)
    {
        if (double.IsInfinity(available))
        {
            return double.PositiveInfinity;
        }

        return count == 0 ? 0D : GetUsableExtent(available, count, spacing) / count;
    }

    /// <summary>
    /// Removes the intervening gutters to obtain the space available to tracks.
    /// </summary>
    private static double GetUsableExtent(double available, int count, double spacing)
    {
        return double.IsInfinity(available)
            ? double.PositiveInfinity
            : Math.Max(0D, available - (spacing * Math.Max(0, count - 1)));
    }

    /// <summary>
    /// Includes internal gutters in the slot of a key spanning multiple tracks.
    /// </summary>
    private static double GetSpannedExtent(double trackWidth, int columnSpan, double spacing)
    {
        return double.IsInfinity(trackWidth)
            ? double.PositiveInfinity
            : Math.Max(0D, (trackWidth * columnSpan) + (spacing * (columnSpan - 1)));
    }

    /// <summary>
    /// Converts a spanned key's desired width into its minimum uniform track requirement.
    /// </summary>
    private static double GetRequiredTrackExtent(double desiredWidth, int columnSpan, double spacing)
    {
        return Math.Max(0D, desiredWidth - (spacing * (columnSpan - 1))) / columnSpan;
    }

    /// <summary>
    /// Adds inter-track spacing and keeps the panel's desired extent finite.
    /// </summary>
    private static double GetTotalExtent(double contentExtent, int count, double spacing)
    {
        return count == 0
            ? 0D
            : Math.Min(double.MaxValue, contentExtent + (spacing * (count - 1)));
    }

    /// <summary>
    /// Reads the current column range, clamped to the panel's declared logical grid.
    /// </summary>
    private SoftKeyboardGridPlacement GetPlacement(UIElement child)
    {
        // WPF Grid clamps an out-of-range index to its final definition and then clamps the span to
        // the remaining definitions. Mirroring that behavior keeps direct panel use resilient while
        // SoftKeyboardLayout performs strict validation for the normal data-driven path.
        var column     = Math.Min(GetColumn(child), ColumnCount - 1);
        var columnSpan = Math.Min(GetColumnSpan(child), ColumnCount - column);

        return new SoftKeyboardGridPlacement(column, columnSpan);
    }

    /// <summary>
    /// Reuses row groups until child identity, row assignment, or collapsed state changes.
    /// </summary>
    /// <remarks>
    /// Inspecting direct children also handles visibility changes without per-child subscriptions.
    /// Hidden children still reserve a slot. Column and span changes are read during each pass and
    /// do not require regrouping. The item generator must supply children before this call.
    /// </remarks>
    private void EnsureRows(UIElementCollection children)
    {
        var unchanged = _rowAssignments.Count == children.Count;
        if (unchanged)
        {
            for (var index = 0; index < children.Count; index++)
            {
                var child = children[index];
                var cached = _rowAssignments[index];
                if (!ReferenceEquals(cached.Child, child)
                    || cached.Row != GetRow(child)
                    || cached.Collapsed != (child.Visibility == Visibility.Collapsed))
                {
                    unchanged = false;
                    break;
                }
            }
        }

        if (unchanged)
        {
            return;
        }

        _rows.Clear();
        _rowLookup.Clear();
        _rowAssignments.Clear();

        for (var index = 0; index < children.Count; index++)
        {
            var child     = children[index];
            var rowId     = GetRow(child);
            var collapsed = child.Visibility == Visibility.Collapsed;

            _rowAssignments.Add((child, rowId, collapsed));

            if (collapsed)
            {
                continue;
            }

            if (!_rowLookup.TryGetValue(rowId, out var row))
            {
                row = new SoftKeyboardPanelRow(rowId);

                _rowLookup.Add(rowId, row);
                _rows.Add(row);
            }

            row.Children.Add(child);
        }

        _rows.Sort(static (left, right) => left.Id.CompareTo(right.Id));
    }

    /// <summary>
    /// Lets WPF acknowledge collapsed children without including them in the occupied row count.
    /// </summary>
    private static void MeasureCollapsedChildren(UIElementCollection children)
    {
        foreach (UIElement child in children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Measure(default);
            }
        }
    }

    /// <summary>
    /// Clears the previous layout slot of a child removed from visible row grouping.
    /// </summary>
    private static void ArrangeCollapsedChildren(UIElementCollection children)
    {
        foreach (UIElement child in children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(default);
            }
        }
    }
}
