// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Arranges keyboard items on a shared, uniformly sized logical grid.
/// </summary>
/// <remarks>
/// <para>
/// Direct children select a logical row through <see cref="RowProperty"/> and a horizontal slot
/// through <see cref="ColumnProperty"/> and <see cref="ColumnSpanProperty"/>. All rows use the same
/// <see cref="ColumnCount"/>, so an unoccupied column remains real space instead of being distributed
/// among the populated keys. This is what permits naturally staggered keyboard rows.
/// </para>
/// <para>
/// Row identifiers are grouping keys rather than absolute coordinates: populated rows are ordered by
/// identifier and gaps in the identifiers do not create empty visual rows. All populated rows receive
/// the same height.
/// </para>
/// <para>
/// <see cref="ColumnSpacing"/> is a grid gutter. A key that spans multiple columns also spans the
/// gutters between those columns, matching WPF <see cref="Grid"/> span geometry and CSS grid tracks.
/// Under an undersized finite constraint, gutters contract before tracks reach zero.
/// </para>
/// <para>
/// The panel is non-virtualizing. When used as an <see cref="ItemsControl.ItemsPanel"/>, set the
/// attached properties on the generated item container rather than on the data template root.
/// </para>
/// </remarks>
public sealed class SoftKeyboardPanel : Panel
{
    private readonly List<RowLayout> _rows = [];

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

    /// <summary>Identifies the <see cref="ColumnCount"/> dependency property.</summary>
    public static readonly DependencyProperty ColumnCountProperty = DependencyProperty.Register(
        nameof(ColumnCount),
        typeof(int),
        typeof(SoftKeyboardPanel),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure),
        static value => value is int count && count > 0
    );

    /// <summary>
    /// Gets or sets the number of equal-width logical columns shared by every row. The default is one.
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

    /// <summary>Identifies the <see cref="RowSpacing"/> dependency property.</summary>
    public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
        nameof(RowSpacing),
        typeof(double),
        typeof(SoftKeyboardPanel),
        new FrameworkPropertyMetadata(4D, FrameworkPropertyMetadataOptions.AffectsMeasure),
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

    /// <summary>Identifies the <see cref="ColumnSpacing"/> dependency property.</summary>
    public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register(
        nameof(ColumnSpacing),
        typeof(double),
        typeof(SoftKeyboardPanel),
        new FrameworkPropertyMetadata(4D, FrameworkPropertyMetadataOptions.AffectsMeasure),
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

    /// <summary>Identifies the attached row-group property.</summary>
    public static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached(
        "Row",
        typeof(int),
        typeof(SoftKeyboardPanel),
        new FrameworkPropertyMetadata(
            0,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure
                | FrameworkPropertyMetadataOptions.AffectsParentArrange
        ),
        static value => value is int row && row >= 0
    );

    /// <summary>Gets the nonnegative logical row identifier assigned to an item container.</summary>
    /// <param name="element">The direct child of a <see cref="SoftKeyboardPanel"/>.</param>
    /// <returns>The logical row identifier. The default is zero.</returns>
    public static int GetRow(DependencyObject element)
    {
        return (int)element.GetValue(RowProperty);
    }

    /// <summary>Assigns a nonnegative logical row identifier to an item container.</summary>
    /// <param name="element">The direct child of a <see cref="SoftKeyboardPanel"/>.</param>
    /// <param name="value">The logical row identifier.</param>
    public static void SetRow(DependencyObject element, int value)
    {
        element.SetValue(RowProperty, value);
    }

    /// <summary>Identifies the attached zero-based logical column property.</summary>
    public static readonly DependencyProperty ColumnProperty = DependencyProperty.RegisterAttached(
        "Column",
        typeof(int),
        typeof(SoftKeyboardPanel),
        new FrameworkPropertyMetadata(
            0,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure
                | FrameworkPropertyMetadataOptions.AffectsParentArrange
        ),
        static value => value is int column && column >= 0
    );

    /// <summary>Gets the zero-based logical column assigned to an item container.</summary>
    /// <param name="element">The direct child of a <see cref="SoftKeyboardPanel"/>.</param>
    /// <returns>The logical column. The default is zero.</returns>
    public static int GetColumn(DependencyObject element)
    {
        return (int)element.GetValue(ColumnProperty);
    }

    /// <summary>Assigns a zero-based logical column to an item container.</summary>
    /// <param name="element">The direct child of a <see cref="SoftKeyboardPanel"/>.</param>
    /// <param name="value">The logical column.</param>
    public static void SetColumn(DependencyObject element, int value)
    {
        element.SetValue(ColumnProperty, value);
    }

    /// <summary>Identifies the attached logical column-span property.</summary>
    public static readonly DependencyProperty ColumnSpanProperty = DependencyProperty.RegisterAttached(
        "ColumnSpan",
        typeof(int),
        typeof(SoftKeyboardPanel),
        new FrameworkPropertyMetadata(
            1,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure
                | FrameworkPropertyMetadataOptions.AffectsParentArrange
        ),
        static value => value is int span && span > 0
    );

    /// <summary>Gets the positive number of logical columns occupied by an item container.</summary>
    /// <param name="element">The direct child of a <see cref="SoftKeyboardPanel"/>.</param>
    /// <returns>The logical column span. The default is one.</returns>
    public static int GetColumnSpan(DependencyObject element)
    {
        return (int)element.GetValue(ColumnSpanProperty);
    }

    /// <summary>Assigns a positive logical column span to an item container.</summary>
    /// <param name="element">The direct child of a <see cref="SoftKeyboardPanel"/>.</param>
    /// <param name="value">The logical column span.</param>
    public static void SetColumnSpan(DependencyObject element, int value)
    {
        element.SetValue(ColumnSpanProperty, value);
    }

    /// <summary>
    /// Measures children against their shared grid slots and derives the smallest uniform track that
    /// satisfies every child's desired width.
    /// </summary>
    /// <param name="availableSize">The size offered by the parent; either dimension may be infinite.</param>
    /// <returns>The desired size of the complete logical grid and populated rows.</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        BuildRows();
        MeasureCollapsedChildren();
        if (_rows.Count == 0)
        {
            return default;
        }

        double rowSpacing = GetEffectiveSpacing(availableSize.Height, _rows.Count, RowSpacing);
        double rowHeight = GetSharedExtent(availableSize.Height, _rows.Count, rowSpacing);
        double columnSpacing = GetEffectiveSpacing(availableSize.Width, ColumnCount, ColumnSpacing);
        double trackWidth = GetSharedExtent(availableSize.Width, ColumnCount, columnSpacing);
        double desiredTrackWidth = 0D;
        double desiredRowHeight = 0D;

        foreach (RowLayout row in _rows)
        {
            foreach (UIElement child in row.Children)
            {
                GridPlacement placement = GetPlacement(child);
                double width = GetSpannedExtent(trackWidth, placement.ColumnSpan, columnSpacing);
                child.Measure(new Size(width, rowHeight));
                desiredTrackWidth = Math.Max(
                    desiredTrackWidth,
                    GetRequiredTrackExtent(child.DesiredSize.Width, placement.ColumnSpan, columnSpacing)
                );
                desiredRowHeight = Math.Max(desiredRowHeight, child.DesiredSize.Height);
            }
        }

        return new Size(
            GetTotalExtent(desiredTrackWidth * ColumnCount, ColumnCount, columnSpacing),
            GetTotalExtent(desiredRowHeight * _rows.Count, _rows.Count, rowSpacing)
        );
    }

    /// <summary>Arranges every child in the slot addressed by its row, column, and span.</summary>
    /// <param name="finalSize">The size allocated by the parent.</param>
    /// <returns>The allocated size.</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // Attached placement properties can change between measure and arrange. Rebuilding is cheap
        // for keyboard-sized collections and guarantees that arrangement never consumes stale groups.
        BuildRows();
        ArrangeCollapsedChildren();
        if (_rows.Count == 0)
        {
            return finalSize;
        }

        double rowSpacing = GetEffectiveSpacing(finalSize.Height, _rows.Count, RowSpacing);
        double rowHeight = GetSharedExtent(finalSize.Height, _rows.Count, rowSpacing);
        double columnSpacing = GetEffectiveSpacing(finalSize.Width, ColumnCount, ColumnSpacing);
        double trackWidth = GetSharedExtent(finalSize.Width, ColumnCount, columnSpacing);
        double columnPitch = trackWidth + columnSpacing;
        double y = 0D;

        for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            RowLayout row = _rows[rowIndex];
            double height = rowIndex == _rows.Count - 1 ? Math.Max(0D, finalSize.Height - y) : rowHeight;

            foreach (UIElement child in row.Children)
            {
                GridPlacement placement = GetPlacement(child);
                double x = placement.Column * columnPitch;
                double width = Math.Min(
                    GetSpannedExtent(trackWidth, placement.ColumnSpan, columnSpacing),
                    Math.Max(0D, finalSize.Width - x)
                );
                child.Arrange(new Rect(x, y, width, height));
            }

            y += rowHeight + rowSpacing;
        }

        return finalSize;
    }

    private static bool IsValidSpacing(object? value) =>
        value is double spacing && spacing >= 0D && !double.IsInfinity(spacing);

    private static double GetEffectiveSpacing(double available, int count, double requested)
    {
        if (count <= 1 || double.IsInfinity(available))
        {
            return requested;
        }

        return Math.Min(requested, Math.Max(0D, available) / (count - 1));
    }

    private static double GetSharedExtent(double available, int count, double spacing)
    {
        if (double.IsInfinity(available))
        {
            return double.PositiveInfinity;
        }

        return count == 0 ? 0D : GetUsableExtent(available, count, spacing) / count;
    }

    private static double GetUsableExtent(double available, int count, double spacing)
    {
        return double.IsInfinity(available)
            ? double.PositiveInfinity
            : Math.Max(0D, available - (spacing * Math.Max(0, count - 1)));
    }

    private static double GetSpannedExtent(double trackWidth, int columnSpan, double spacing)
    {
        return double.IsInfinity(trackWidth)
            ? double.PositiveInfinity
            : Math.Max(0D, (trackWidth * columnSpan) + (spacing * (columnSpan - 1)));
    }

    private static double GetRequiredTrackExtent(double desiredWidth, int columnSpan, double spacing)
    {
        return Math.Max(0D, desiredWidth - (spacing * (columnSpan - 1))) / columnSpan;
    }

    private static double GetTotalExtent(double contentExtent, int count, double spacing)
    {
        return count == 0
            ? 0D
            : Math.Min(double.MaxValue, contentExtent + (spacing * (count - 1)));
    }

    private GridPlacement GetPlacement(UIElement child)
    {
        // WPF Grid clamps an out-of-range index to its final definition and then clamps the span to
        // the remaining definitions. Mirroring that behavior keeps direct panel use resilient while
        // SoftKeyboardLayout performs strict validation for the normal data-driven path.
        int column = Math.Min(GetColumn(child), ColumnCount - 1);
        int columnSpan = Math.Min(GetColumnSpan(child), ColumnCount - column);
        return new GridPlacement(column, columnSpan);
    }

    private void BuildRows()
    {
        _rows.Clear();

        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            int rowId = GetRow(child);
            RowLayout? row = _rows.FirstOrDefault(candidate => candidate.Id == rowId);
            if (row is null)
            {
                row = new RowLayout(rowId);
                _rows.Add(row);
            }

            row.Children.Add(child);
        }

        _rows.Sort(static (left, right) => left.Id.CompareTo(right.Id));
    }

    private void MeasureCollapsedChildren()
    {
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Measure(default);
            }
        }
    }

    private void ArrangeCollapsedChildren()
    {
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(default(Rect));
            }
        }
    }

    private sealed class RowLayout(int id)
    {
        public int Id { get; } = id;

        public List<UIElement> Children { get; } = [];
    }

    private readonly record struct GridPlacement(int Column, int ColumnSpan);
}
