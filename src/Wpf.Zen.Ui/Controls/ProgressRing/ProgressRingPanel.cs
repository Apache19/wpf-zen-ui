// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Shapes;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Arranges the visual layers of a <see cref="ProgressRing"/> in a centered square.
/// </summary>
/// <remarks>
/// This panel is an internal template implementation detail. A normal <see cref="Grid"/> stretches
/// <see cref="Arc"/> to rectangular bounds and therefore makes its geometry elliptical. Performing
/// the constraint in arrange also avoids relying on size bindings, whose updates can occur after an
/// Arc has already generated geometry for the current layout pass.
/// </remarks>
internal sealed class ProgressRingPanel : Panel
{
    /// <summary>
    /// Identifies the <see cref="StrokeThickness"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(ProgressRingPanel),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsArrange)
    );

    /// <summary>
    /// Gets or sets the explicit ring stroke, or <see cref="double.NaN"/> to derive it from the
    /// arranged diameter.
    /// </summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var diameter = GetConstrainedDiameter(availableSize);
        var childConstraint = double.IsInfinity(diameter)
            ? availableSize
            : new Size(diameter, diameter);
        var desiredSize = default(Size);

        foreach (UIElement child in InternalChildren)
        {
            child.Measure(childConstraint);
            desiredSize.Width = Math.Max(desiredSize.Width, child.DesiredSize.Width);
            desiredSize.Height = Math.Max(desiredSize.Height, child.DesiredSize.Height);
        }

        return desiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var diameter = GetConstrainedDiameter(finalSize);
        var offsetX = (finalSize.Width - diameter) / 2;
        var offsetY = (finalSize.Height - diameter) / 2;
        var childBounds = new Rect(offsetX, offsetY, diameter, diameter);
        var effectiveStrokeThickness = double.IsNaN(StrokeThickness)
            ? diameter / 8
            : Math.Max(0.0d, StrokeThickness);

        foreach (UIElement child in InternalChildren)
        {
            // The panel owns these template layers, so applying the computed value here keeps
            // geometry and stroke synchronized in the same arrange pass during size animation.
            if (child is Arc arc)
            {
                arc.SetCurrentValue(Shape.StrokeThicknessProperty, effectiveStrokeThickness);
            }

            child.Arrange(childBounds);
        }

        return finalSize;
    }

    /// <summary>
    /// Returns the non-negative shorter side while preserving an unconstrained measure pass.
    /// </summary>
    private static double GetConstrainedDiameter(Size size)
    {
        if (double.IsInfinity(size.Width) && double.IsInfinity(size.Height))
        {
            return double.PositiveInfinity;
        }

        if (double.IsInfinity(size.Width))
        {
            return Math.Max(0.0d, size.Height);
        }

        if (double.IsInfinity(size.Height))
        {
            return Math.Max(0.0d, size.Width);
        }

        return Math.Max(0.0d, Math.Min(size.Width, size.Height));
    }
}
