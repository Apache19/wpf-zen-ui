// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Arranges a flat collection of keyboard items into weighted rows while retaining authority over
/// both axes of the final layout.
/// </summary>
/// <remarks>
/// <para>
/// Direct children select a logical row through <see cref="RowProperty"/> and a relative share of
/// that row through <see cref="WidthWeightProperty"/>. Row identifiers are grouping keys rather
/// than absolute coordinates: populated rows are ordered by identifier and gaps in the identifiers
/// do not create empty visual rows. Children within a row retain collection order.
/// </para>
/// <para>
/// All populated rows receive the same height. Each row independently divides its remaining width
/// according to child weights after subtracting <see cref="ColumnSpacing"/>. This lets a single
/// items control describe a complete keyboard without nested row containers or button margins.
/// </para>
/// <para>
/// When the available extent is smaller than the requested gaps, the effective gaps contract before
/// item slots reach zero. This is intentional: a layout panel must not arrange its final child or row
/// beyond the bounds assigned by its parent. <see cref="UIElement.ClipToBounds"/> also defaults to
/// <see langword="true"/> so a child's minimum size or custom rendering cannot escape the keyboard
/// surface when the host becomes exceptionally small.
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
    /// Gets or sets the requested distance between adjacent items in each row. The default is 4 DIP.
    /// </summary>
    /// <remarks>
    /// The value must be finite and nonnegative. Under an undersized finite constraint, the panel
    /// reduces the effective spacing just enough to keep every item slot inside its final bounds.
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

    /// <summary>Identifies the attached relative-width property.</summary>
    public static readonly DependencyProperty WidthWeightProperty = DependencyProperty.RegisterAttached(
        "WidthWeight",
        typeof(double),
        typeof(SoftKeyboardPanel),
        new FrameworkPropertyMetadata(
            1D,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure
                | FrameworkPropertyMetadataOptions.AffectsParentArrange
        ),
        static value => value is double weight && weight > 0D && !double.IsInfinity(weight)
    );

    /// <summary>Gets the positive relative width assigned to an item container.</summary>
    /// <param name="element">The direct child of a <see cref="SoftKeyboardPanel"/>.</param>
    /// <returns>The width weight. The default is one.</returns>
    public static double GetWidthWeight(DependencyObject element)
    {
        return (double)element.GetValue(WidthWeightProperty);
    }

    /// <summary>Assigns a positive, finite relative width to an item container.</summary>
    /// <param name="element">The direct child of a <see cref="SoftKeyboardPanel"/>.</param>
    /// <param name="value">The width weight.</param>
    public static void SetWidthWeight(DependencyObject element, double value)
    {
        element.SetValue(WidthWeightProperty, value);
    }

    /// <summary>
    /// Measures every populated row against the same height and each item against its weighted share
    /// of the available row width.
    /// </summary>
    /// <param name="availableSize">The size offered by the parent; either dimension may be infinite.</param>
    /// <returns>The desired size of all populated rows and internal spacing.</returns>
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
        double desiredWidth = 0D;
        double desiredRowHeight = 0D;

        foreach (RowLayout row in _rows)
        {
            double columnSpacing = GetEffectiveSpacing(availableSize.Width, row.Children.Count, ColumnSpacing);
            double usableWidth = GetUsableExtent(availableSize.Width, row.Children.Count, columnSpacing);
            WeightDistribution weights = GetWeightDistribution(row.Children);
            double desiredUnitWidth = 0D;

            foreach (UIElement child in row.Children)
            {
                double normalizedWeight = GetWidthWeight(child) / weights.Scale;
                double width = double.IsInfinity(usableWidth)
                    ? double.PositiveInfinity
                    : usableWidth * normalizedWeight / weights.Total;
                child.Measure(new Size(width, rowHeight));
                desiredUnitWidth = Math.Max(desiredUnitWidth, child.DesiredSize.Width / normalizedWeight);
                desiredRowHeight = Math.Max(desiredRowHeight, child.DesiredSize.Height);
            }

            desiredWidth = Math.Max(
                desiredWidth,
                GetTotalExtent(desiredUnitWidth * weights.Total, row.Children.Count, columnSpacing)
            );
        }

        return new Size(
            desiredWidth,
            GetTotalExtent(desiredRowHeight * _rows.Count, _rows.Count, rowSpacing)
        );
    }

    /// <summary>Arranges the measured grouping without allowing accumulated gaps to exceed the final size.</summary>
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
        double y = 0D;

        for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            RowLayout row = _rows[rowIndex];
            double columnSpacing = GetEffectiveSpacing(finalSize.Width, row.Children.Count, ColumnSpacing);
            double usableWidth = GetUsableExtent(finalSize.Width, row.Children.Count, columnSpacing);
            WeightDistribution weights = GetWeightDistribution(row.Children);
            double x = 0D;
            double remainingWidth = usableWidth;
            double remainingWeight = weights.Total;
            double height = rowIndex == _rows.Count - 1 ? Math.Max(0D, finalSize.Height - y) : rowHeight;

            for (var childIndex = 0; childIndex < row.Children.Count; childIndex++)
            {
                UIElement child = row.Children[childIndex];
                double weight = GetWidthWeight(child) / weights.Scale;
                // Give the final item the exact remainder. This prevents floating-point accumulation
                // from placing its right edge a fraction beyond the panel's authoritative boundary.
                double width = childIndex == row.Children.Count - 1
                    ? remainingWidth
                    : remainingWidth * weight / remainingWeight;
                child.Arrange(new Rect(x, y, Math.Max(0D, width), height));
                x += width + columnSpacing;
                remainingWidth -= width;
                remainingWeight -= weight;
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

    private static double GetTotalExtent(double contentExtent, int count, double spacing)
    {
        return count == 0
            ? 0D
            : Math.Min(double.MaxValue, contentExtent + (spacing * (count - 1)));
    }

    // Relative weights are scale invariant. Normalizing by the largest value keeps the sum finite
    // even when a custom layout legitimately combines several values near double.MaxValue.
    private static WeightDistribution GetWeightDistribution(IReadOnlyList<UIElement> children)
    {
        double scale = children.Max(GetWidthWeight);
        double total = children.Sum(child => GetWidthWeight(child) / scale);
        return new WeightDistribution(scale, total);
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

    private readonly record struct WeightDistribution(double Scale, double Total);
}
