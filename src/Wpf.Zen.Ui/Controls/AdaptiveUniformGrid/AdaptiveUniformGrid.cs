// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Extends <see cref="UniformGrid"/> with fixed spacing and rows whose remaining children share
/// the available width without pulling children out of subsequent rows.
/// </summary>
/// <remarks>
/// <para>
/// With <see cref="IsRowStretchEnabled"/> enabled, original child indices (including collapsed
/// children) determine row membership. Every logical row contains the computed column count of
/// children except for a possibly incomplete final row. Each populated row divides its width
/// equally among its non-collapsed children, after subtracting column spacing. Entirely collapsed
/// rows disappear;
/// remaining rows have equal heights. Rows with different non-collapsed counts can therefore
/// have different slot widths without changing their original child membership.
/// <see cref="Visibility.Hidden"/> still occupies a cell, as it does in WPF's UniformGrid.
/// </para>
/// <para>
/// Inherited <see cref="UniformGrid.Rows"/> and <see cref="UniformGrid.Columns"/> retain their
/// grid-sizing roles. Automatic dimensions count all children in stretch mode so changing
/// visibility does not change row membership. Entirely collapsed populated rows within the
/// computed grid reduce its height division; collapsed overflow rows do not. Otherwise explicitly
/// reserved rows and the automatic square grid remain intact. Children exceeding an explicitly
/// fixed grid capacity are arranged below that grid.
/// </para>
/// <para>
/// Stretch mode ignores <see cref="UniformGrid.FirstColumn"/> for both sizing and grouping.
/// With stretching disabled, it reserves leading cells only when smaller than the explicitly
/// specified Columns, following UniformGrid's offset rules. Neither mode changes its value or
/// binding, so a temporarily ignored offset can take effect after mode or column changes.
/// </para>
/// <para>
/// Equality applies to layout slots, not forced child dimensions. Width, Height, MinWidth,
/// MaxWidth, MinHeight, MaxHeight, Margin and alignment are handled by the child's normal WPF
/// measure/arrange pipeline. A fixed-size or non-stretched child need not fill its slot; its
/// visible edge can therefore be farther away than the configured spacing between slots.
/// </para>
/// <para>
/// This non-virtualizing panel needs no control template or named parts. When used as an
/// ItemsPanel, visibility must be applied to the item containers, which are its direct children.
/// </para>
/// <para>
/// Child templates and bindings may change visibility or grid settings during measurement.
/// The panel retries once immediately and requests a subsequent WPF layout pass if needed.
/// Layout-dependent child code must eventually stabilize; continuously toggling layout inputs
/// cannot produce a stable grid. Child collection mutations during measurement are not supported.
/// </para>
/// </remarks>
public class AdaptiveUniformGrid : UniformGrid
{
    /// <summary>
    /// Identifies the <see cref="SpacingBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SpacingBrushProperty = DependencyProperty.Register(
        nameof(SpacingBrush),
        typeof(Brush),
        typeof(AdaptiveUniformGrid),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>
    /// Gets or sets the brush used to fill row and column spacing. The default is null.
    /// </summary>
    /// <remarks>
    /// Spacing is painted above Background and below children, within the panel bounds.
    /// Cell interiors, including child margins and alignment space, remain unpainted.
    /// In stretch mode column gaps follow each populated row's actual cell widths.
    /// In regular mode gaps follow the configured grid, including reserved empty cells.
    /// A single geometry fill keeps translucent brushes uniform at gap intersections and
    /// maps relative brush coordinates to the combined spacing bounds.
    /// </remarks>
    public Brush? SpacingBrush
    {
        get => (Brush?)GetValue(SpacingBrushProperty);
        set => SetValue(SpacingBrushProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="RowSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
        nameof(RowSpacing),
        typeof(double),
        typeof(AdaptiveUniformGrid),
        new FrameworkPropertyMetadata(0D, FrameworkPropertyMetadataOptions.AffectsMeasure),
        IsValidSpacing
    );

    /// <summary>
    /// Gets or sets the distance, in device-independent units, between row slots. The default is
    /// zero.
    /// </summary>
    /// <remarks>
    /// The value must be finite and nonnegative. It affects measurement and arrangement, does not
    /// inherit, and can be animated. Validation prevents invalid sizes entering WPF layout.
    /// Spacing is retained if the available height is too small; cell heights then become zero.
    /// </remarks>
    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ColumnSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register(
        nameof(ColumnSpacing),
        typeof(double),
        typeof(AdaptiveUniformGrid),
        new FrameworkPropertyMetadata(0D, FrameworkPropertyMetadataOptions.AffectsMeasure),
        IsValidSpacing
    );

    /// <summary>
    /// Gets or sets the distance, in device-independent units, between cell slots in a row.
    /// The default is zero.
    /// </summary>
    /// <remarks>
    /// The value must be finite and nonnegative. It affects measurement and arrangement, does not
    /// inherit, and can be animated. Stretching changes cell widths, never the spacing between
    /// them.
    /// Spacing is retained if the available width is too small; cell widths then become zero.
    /// </remarks>
    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsRowStretchEnabled"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsRowStretchEnabledProperty = DependencyProperty.Register(
        nameof(IsRowStretchEnabled),
        typeof(bool),
        typeof(AdaptiveUniformGrid),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure)
    );

    /// <summary>
    /// Gets or sets a value indicating whether partial rows stretch and collapsed children preserve
    /// their original row membership. The default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// When false, non-collapsed children fill consecutive cells and the last row keeps empty
    /// trailing cells, as in UniformGrid, and FirstColumn controls the leading empty cells.
    /// When true, FirstColumn is ignored without changing its value or binding, and original
    /// child indices alone determine row membership. Spacing remains effective in either mode.
    /// This property affects measurement and arrangement, does not inherit, and supports discrete
    /// animation.
    /// </remarks>
    public bool IsRowStretchEnabled
    {
        get => (bool)GetValue(IsRowStretchEnabledProperty);
        set => SetValue(IsRowStretchEnabledProperty, value);
    }

    private readonly List<RowLayout> _populatedRows = new();

    private int _columns;
    private int _rows;
    private int _firstColumn;

    private bool _isMeasuringChildren;
    private bool _measureNeedsRetry;

    private Geometry? _spacingGeometry;

    /// <summary>
    /// Paints the background and the spacing captured by the latest arrangement.
    /// </summary>
    /// <param name="drawingContext">The drawing context supplied by WPF.</param>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (SpacingBrush is { } brush && _spacingGeometry is { } geometry)
        {
            drawingContext.DrawGeometry(brush, null, geometry);
        }
    }

    /// <summary>
    /// Retains child layout notifications that WPF would otherwise suppress during this panel's
    /// measure pass.
    /// </summary>
    /// <param name="child">The direct child whose desired size or collapsed state changed.</param>
    protected override void OnChildDesiredSizeChanged(UIElement child)
    {
        // UIElement.Measure does not notify a measuring parent for ordinary DesiredSize changes,
        // but Visibility transitions still signal it. Templates and bindings can cause these
        // transitions inside child.Measure, after this panel has already computed row membership.
        _measureNeedsRetry |= _isMeasuringChildren;
        base.OnChildDesiredSizeChanged(child);
    }

    /// <summary>
    /// Retains layout-setting changes made by child templates or bindings during measurement.
    /// </summary>
    /// <param name="e">The dependency property change reported by WPF.</param>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_isMeasuringChildren
            && (e.Property == RowsProperty
                || e.Property == ColumnsProperty
                || e.Property == FirstColumnProperty
                || e.Property == RowSpacingProperty
                || e.Property == ColumnSpacingProperty
                || e.Property == IsRowStretchEnabledProperty))
        {
            // AffectsMeasure cannot dirty an element while its Measure is in progress. Preserve
            // the notification ourselves so dimensions and child constraints use the same settings.
            _measureNeedsRetry = true;
        }
    }

    /// <summary>
    /// Measures children using their actual row's share of the constraint and reports the size
    /// required by the largest equal-height row and widest row of equal-width cells.
    /// </summary>
    /// <param name="constraint">Available size; either dimension may be infinite.</param>
    /// <returns>The desired grid size including internal spacing.</returns>
    protected override Size MeasureOverride(Size constraint)
    {
        _isMeasuringChildren = true;
        try
        {
            Size desiredSize = default;
            for (var pass = 0; pass < 2; pass++)
            {
                _measureNeedsRetry = false;
                desiredSize = MeasureChildren(constraint);
                if (!_measureNeedsRetry)
                {
                    break;
                }
            }

            // A second pass resolves one-time template/binding initialization immediately. Do not
            // loop indefinitely on child code that changes layout in response to each constraint;
            // ArrangeOverride schedules a normal WPF measure pass if the result is still unstable.
            return desiredSize;
        }
        finally
        {
            _isMeasuringChildren = false;
        }
    }

    /// <summary>
    /// Measures one grouping; callers repeat it when child initialization changes the layout
    /// inputs.
    /// </summary>
    private Size MeasureChildren(Size constraint)
    {
        UpdateDimensions();

        var cellHeight = GetCellExtent(constraint.Height, _rows, RowSpacing);
        var maxWidth   = 0D;
        var maxHeight  = 0D;

        if (!IsRowStretchEnabled)
        {
            var cellSize = new Size(GetCellExtent(constraint.Width, _columns, ColumnSpacing), cellHeight);

            foreach (UIElement child in InternalChildren)
            {
                child.Measure(cellSize);

                maxWidth  = Math.Max(maxWidth, child.DesiredSize.Width);
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }

            return new Size(
                GetTotalExtent(maxWidth, _columns, ColumnSpacing),
                GetTotalExtent(maxHeight, _rows, RowSpacing)
            );
        }

        // Arrange consumes the measured grouping. Visibility notifications raised during child
        // initialization request another pass so cached row counts cannot silently become stale.
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Measure(default);
            }
        }

        foreach (var row in _populatedRows)
        {
            var cellSize = new Size(
                GetCellExtent(constraint.Width, row.CellCount, ColumnSpacing),
                cellHeight
            );

            var rowWidth = 0D;

            for (var index = row.Start; index < row.End; index++)
            {
                var child = InternalChildren[index];
                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                // Measure at the expanded width, not the original column width: wrapping content
                // must report the height it actually needs in its expanded slot.
                child.Measure(cellSize);

                rowWidth  = Math.Max(rowWidth, child.DesiredSize.Width);
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }

            maxWidth = Math.Max(maxWidth, GetTotalExtent(rowWidth, row.CellCount, ColumnSpacing));
        }

        return new Size(maxWidth, GetTotalExtent(maxHeight, _rows, RowSpacing));
    }

    /// <summary>
    /// Distributes the final space into layout slots without changing child sizing properties.
    /// </summary>
    /// <param name="arrangeSize">Final size allocated to the panel.</param>
    /// <returns>The allocated size.</returns>
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (_measureNeedsRetry)
        {
            // UIElement clears measure dirtiness after MeasureOverride returns. Arrangement is
            // the first synchronous opportunity to request another pass without losing that request.
            InvalidateMeasure();
        }

        var cellHeight = GetCellExtent(arrangeSize.Height, _rows, RowSpacing);
        UpdateSpacingGeometry(arrangeSize, cellHeight);

        if (!IsRowStretchEnabled)
        {
            var cellWidth = GetCellExtent(arrangeSize.Width, _columns, ColumnSpacing);
            var column    = _firstColumn;
            var row       = 0;

            // Count cells instead of comparing accumulated coordinates. UniformGrid's one-DIP
            // wrap tolerance can wrap early when the entire panel or individual cells are tiny.
            foreach (UIElement child in InternalChildren)
            {
                child.Arrange(
                    new Rect(
                        GetOffset(column, cellWidth, ColumnSpacing),
                        GetOffset(row, cellHeight, RowSpacing),
                        cellWidth,
                        cellHeight
                    )
                );

                if (child.Visibility != Visibility.Collapsed && ++column == _columns)
                {
                    column = 0;
                    row++;
                }
            }

            return arrangeSize;
        }

        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(default(Rect));
            }
        }

        for (var rowIndex = 0; rowIndex < _populatedRows.Count; rowIndex++)
        {
            var row       = _populatedRows[rowIndex];
            var cellWidth = GetCellExtent(arrangeSize.Width, row.CellCount, ColumnSpacing);
            var column    = 0;

            for (var index = row.Start; index < row.End; index++)
            {
                var child = InternalChildren[index];
                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                // FrameworkElement.ArrangeCore applies Margin, explicit/min/max dimensions and
                // alignment inside this slot. Setting Width/Height or arranging the desired size
                // here would bypass that contract and break normal WPF child behavior.
                child.Arrange(
                    new Rect(
                        GetOffset(column, cellWidth, ColumnSpacing),
                        GetOffset(rowIndex, cellHeight, RowSpacing),
                        cellWidth,
                        cellHeight
                    )
                );

                column++;
            }
        }

        return arrangeSize;
    }

    /// <summary>
    /// Captures disjoint gap rectangles using the same dimensions and offsets as child layout.
    /// </summary>
    private void UpdateSpacingGeometry(Size size, double cellHeight)
    {
        _spacingGeometry = null;
        if (_rows > 0 && size.Width > 0 && size.Height > 0
            && (RowSpacing > 0 || ColumnSpacing > 0))
        {
            var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (var context = geometry.Open())
            {
                var rowCount = IsRowStretchEnabled ? Math.Max(_rows, _populatedRows.Count) : _rows;
                for (var row = 0; row < rowCount; row++)
                {
                    var top = GetOffset(row, cellHeight, RowSpacing);
                    if (top >= size.Height)
                    {
                        break;
                    }

                    if (RowSpacing > 0 && row < rowCount - 1)
                    {
                        AddSpacingRectangle(context, size, 0, top + cellHeight,
                            size.Width, RowSpacing);
                    }

                    var columns = IsRowStretchEnabled
                        ? (row < _populatedRows.Count ? _populatedRows[row].CellCount : 0)
                        : _columns;
                    if (ColumnSpacing <= 0 || cellHeight <= 0 || columns < 2)
                    {
                        continue;
                    }

                    var cellWidth = GetCellExtent(size.Width, columns, ColumnSpacing);
                    for (var column = 0; column < columns - 1; column++)
                    {
                        var left = GetOffset(column, cellWidth, ColumnSpacing) + cellWidth;
                        if (left >= size.Width)
                        {
                            break;
                        }

                        // Vertical gaps stop at the row edge; horizontal gaps own intersections.
                        AddSpacingRectangle(context, size, left, top, ColumnSpacing, cellHeight);
                    }
                }
            }

            geometry.Freeze();
            _spacingGeometry = geometry;
        }

        // Reflow can move gaps without changing RenderSize, for example after collapsing a child.
        InvalidateVisual();
    }

    /// <summary>
    /// Appends a rectangle clipped to the panel bounds, including undersized layout constraints.
    /// </summary>
    private static void AddSpacingRectangle(
        StreamGeometryContext context, Size bounds, double x, double y, double width, double height)
    {
        width = Math.Min(width, bounds.Width - x);
        height = Math.Min(height, bounds.Height - y);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        context.BeginFigure(new Point(x, y), true, true);
        context.LineTo(new Point(x + width, y), false, false);
        context.LineTo(new Point(x + width, y + height), false, false);
        context.LineTo(new Point(x, y + height), false, false);
    }

    /// <summary>
    /// Accepts finite, nonnegative spacing values for the layout calculations.
    /// </summary>
    private static bool IsValidSpacing(object? value)
    {
        return value is double spacing && spacing >= 0D && !double.IsInfinity(spacing);
    }

    /// <summary>
    /// Resolves the native grid dimensions, then removes only fully collapsed logical rows.
    /// </summary>
    private void UpdateDimensions()
    {
        _populatedRows.Clear();

        _columns = Columns;
        _rows    = Rows;

        _firstColumn = !IsRowStretchEnabled && FirstColumn < _columns ? FirstColumn : 0;

        // UniformGrid.UpdateComputedValues checks FirstColumn against the configured Columns,
        // before calculating automatic columns. Its base MeasureOverride also writes an invalid
        // offset back to the dependency property, which can remove a binding. Own the layout in
        // both spacing cases and normalize only _firstColumn to preserve the requested value.
        // Stretch mode must ignore even a valid offset: every row groups consecutive original
        // children and distributes its full width among the non-collapsed members.
        var count = InternalChildren.Count;
        if (!IsRowStretchEnabled && (_rows == 0 || _columns == 0))
        {
            count = 0;
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility != Visibility.Collapsed)
                {
                    count++;
                }
            }
        }

        count = Math.Max(1, count);
        if (_rows == 0)
        {
            if (_columns > 0)
            {
                _rows = (int)(((long)count + _firstColumn + _columns - 1) / _columns);
            }
            else
            {
                _rows = _columns = (int)Math.Ceiling(Math.Sqrt(count));
            }
        }
        else if (_columns == 0)
        {
            _columns = (int)(((long)count + _rows - 1) / _rows);
        }

        if (!IsRowStretchEnabled)
        {
            return;
        }

        var emptyRows  = 0;
        var logicalRow = 0;

        for (var start = 0; start < InternalChildren.Count;)
        {
            var end = start + Math.Min(_columns, InternalChildren.Count - start);
            var visibleCount = 0;

            for (var index = start; index < end; index++)
            {
                if (InternalChildren[index].Visibility != Visibility.Collapsed)
                {
                    visibleCount++;
                }
            }

            if (visibleCount == 0)
            {
                // Overflow rows never contributed to the configured height division. Removing
                // one must not enlarge preceding rows or push an in-grid row outside the panel.
                if (logicalRow < _rows)
                {
                    emptyRows++;
                }
            }
            else
            {
                _populatedRows.Add(new RowLayout(start, end, visibleCount));
            }

            start = end;
            logicalRow++;
        }

        // Keep one height division when only overflow children remain visible, so their rows
        // can still be arranged after compaction. An entirely collapsed panel needs no spacing.
        _rows = _populatedRows.Count == 0 ? 0 : Math.Max(1, _rows - emptyRows);
    }

    // Unbounded measure must remain unbounded. Finite constraints smaller than the requested
    // gaps yield zero-sized slots, without silently shrinking spacing or producing negative sizes.
    private static double GetCellExtent(double available, int count, double spacing)
    {
        return count == 0 ? 0D : Math.Max(0D, available - GetTotalExtent(0D, count, spacing)) / count;
    }

    // Saturation keeps even very large finite spacing/count combinations valid WPF sizes/points.
    private static double GetTotalExtent(double cell, int count, double spacing)
    {
        return count == 0 ? 0D : Math.Min(double.MaxValue, (cell * count) + (spacing * (count - 1)));
    }

    private static double GetOffset(int index, double cell, double spacing)
    {
        return Math.Min(double.MaxValue, (index * cell) + (index * spacing));
    }

    private readonly struct RowLayout(int start, int end, int cellCount)
    {
        /// <summary>
        /// Gets the inclusive index in the unfiltered child collection.
        /// </summary>
        public int Start { get; } = start;

        /// <summary>
        /// Gets the exclusive index; collapsed children remain inside this range.
        /// </summary>
        public int End { get; } = end;

        /// <summary>
        /// Gets the number of equal slots for the row's non-collapsed children.
        /// </summary>
        public int CellCount { get; } = cellCount;
    }
}
