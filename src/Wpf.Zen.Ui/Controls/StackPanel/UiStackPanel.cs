// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Stacks children horizontally or vertically with optional painted spacing between layout slots.
/// </summary>
/// <remarks>
/// Spacing defaults to zero and is additional to child margins. Collapsed children have no slot
/// or spacing; Hidden children retain both. No spacing is added before the first or after the last
/// participating child. Children retain their normal sizing, alignment, margins and bindings.
/// This panel measures all children. A surrounding ScrollViewer scrolls it physically; use
/// <see cref="UiVirtualizingStackPanel"/> for item scrolling or virtualized ItemsControl content.
/// </remarks>
public class UiStackPanel : Panel
{
    private readonly List<Rect> _gaps = [];

    /// <summary>
    /// Identifies the <see cref="Orientation"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty OrientationProperty
        = StackPanel.OrientationProperty.AddOwner(
            typeof(UiStackPanel),
            new FrameworkPropertyMetadata(
                Orientation.Vertical,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            )
        );

    /// <summary>
    /// Gets or sets the stacking direction. The default is Vertical.
    /// </summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Spacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SpacingProperty
        = DependencyProperty.Register(
            nameof(Spacing),
            typeof(double),
            typeof(UiStackPanel),
            new FrameworkPropertyMetadata(
                0D,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            ),
            PanelSpacing.IsValid
        );

    /// <summary>
    /// Gets or sets the finite, nonnegative distance between slots in device-independent units.
    /// The default is zero. The distance participates in both measurement and arrangement.
    /// </summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="SpacingBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SpacingBrushProperty
        = DependencyProperty.Register(
            nameof(SpacingBrush),
            typeof(Brush),
            typeof(UiStackPanel),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>
    /// Gets or sets the brush filling spacing across the panel's cross-axis size. Default null.
    /// Gaps are painted above Background and below children, clipped to the panel bounds.
    /// Child margins and alignment space are not part of the painted gaps.
    /// </summary>
    public Brush? SpacingBrush
    {
        get => (Brush?)GetValue(SpacingBrushProperty);
        set => SetValue(SpacingBrushProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var length      = 0D;
        var breadth     = 0D;
        var hasPrevious = false;

        var availableBreadth = StackSpacing.Across(availableSize, Orientation);
        var constraint       = StackSpacing.Size(double.PositiveInfinity, availableBreadth, Orientation);

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(constraint);
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var leadingSpacing = hasPrevious ? Spacing : 0;

            var childLength  = StackSpacing.Along(child.DesiredSize, Orientation);
            var childBreadth = StackSpacing.Across(child.DesiredSize, Orientation);

            length      += leadingSpacing + childLength;
            breadth     =  Math.Max(breadth, childBreadth);
            hasPrevious =  true;
        }

        return StackSpacing.Size(length, breadth, Orientation);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _gaps.Clear();

        var position    = 0D;
        var breadth     = StackSpacing.Across(finalSize, Orientation);
        var hasPrevious = false;

        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(new Rect());
                continue;
            }

            if (hasPrevious && Spacing > 0)
            {
                var gap = StackSpacing.Rect(position, 0, Spacing, breadth, Orientation);
                _gaps.Add(gap);
                position += Spacing;
            }

            var length = StackSpacing.Along(child.DesiredSize, Orientation);

            var desiredBreadth = StackSpacing.Across(child.DesiredSize, Orientation);
            var childBreadth   = Math.Max(breadth, desiredBreadth);
            var bounds         = StackSpacing.Rect(position, 0, length, childBreadth, Orientation);

            child.Arrange(bounds);

            position    += length;
            hasPrevious =  true;
        }

        // Render data depends on child layout even when the panel's own size has not changed.
        InvalidateVisual();
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        PanelSpacing.Draw(drawingContext, SpacingBrush, _gaps, RenderSize);
    }
}