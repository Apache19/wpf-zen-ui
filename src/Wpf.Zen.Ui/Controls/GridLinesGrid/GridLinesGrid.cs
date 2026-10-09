// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// A Grid with independently configurable horizontal and vertical grid-line pens.
/// </summary>
/// <remarks>
/// Enable the inherited <see cref="Grid.ShowGridLines"/> property to display internal row and
/// column boundaries above children. Lines cross spanning cells and do not reserve layout space
/// or draw an outer border. Grid sizing, shared sizes, layout rounding and flow direction retain
/// their normal WPF behavior. Each pen supports brushes, thickness, dash patterns and line caps.
/// </remarks>
public class GridLinesGrid : Grid
{
    /// <summary>
    /// Identifies the <see cref="HorizontalGridLinePen"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HorizontalGridLinePenProperty
        = DependencyProperty.Register(
            nameof(HorizontalGridLinePen),
            typeof(Pen),
            typeof(GridLinesGrid),
            new FrameworkPropertyMetadata(
                CreateDefaultPen(),
                FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>
    /// Gets or sets the pen for internal row boundaries. The default is a gray, one-DIP solid pen.
    /// A null pen, null brush or zero thickness hides horizontal lines. Mutable pen and brush
    /// changes refresh the drawing; bindings and animations are supported.
    /// </summary>
    public Pen? HorizontalGridLinePen
    {
        get => (Pen?)GetValue(HorizontalGridLinePenProperty);
        set => SetValue(HorizontalGridLinePenProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="VerticalGridLinePen"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VerticalGridLinePenProperty
        = DependencyProperty.Register(
            nameof(VerticalGridLinePen),
            typeof(Pen),
            typeof(GridLinesGrid),
            new FrameworkPropertyMetadata(
                CreateDefaultPen(),
                FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>
    /// Gets or sets the pen for internal column boundaries. The default is a gray, one-DIP solid
    /// pen. A null pen, null brush or zero thickness hides vertical lines. Mutable pen and brush
    /// changes refresh the drawing; bindings and animations are supported.
    /// </summary>
    public Pen? VerticalGridLinePen
    {
        get => (Pen?)GetValue(VerticalGridLinePenProperty);
        set => SetValue(VerticalGridLinePenProperty, value);
    }

    /// <summary>
    /// Arranges children using Grid and replaces its diagnostic drawing at the final boundaries.
    /// </summary>
    /// <param name="arrangeSize">The available layout size, in device-independent units.</param>
    /// <returns>The size arranged by Grid.</returns>
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var arrangedSize = base.ArrangeOverride(arrangeSize);
        UpdateGridLines(arrangedSize);
        return arrangedSize;
    }

    /// <summary>
    /// Paints the panel background and refreshes grid lines after pen or brush changes.
    /// </summary>
    /// <param name="drawingContext">The drawing context for the panel background.</param>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        UpdateGridLines(RenderSize);
    }

    /// <summary>
    /// Creates an immutable metadata default that can safely be shared by control instances.
    /// </summary>
    private static Pen CreateDefaultPen()
    {
        var pen = new Pen(Brushes.Gray, 1);
        pen.Freeze();
        return pen;
    }

    /// <summary>
    /// Replaces the retained drawing on Grid's final visual child with the configured pens.
    /// </summary>
    private void UpdateGridLines(Size size)
    {
        if (!ShowGridLines || base.VisualChildrenCount == 0)
        {
            return;
        }

        // WPF Grid.GetVisualChild exposes its GridLinesRenderer after all UIElement children.
        // Grid.ArrangeOverride recreates its hard-coded drawing on every arrange; overwrite it
        // after base arrangement and on render invalidation. A trivial Grid may have no renderer.
        // This relies on WPF's renderer ordering/type, covered by the visual regression tests.
        var lastIndex  = base.VisualChildrenCount - 1;
        var lastVisual = base.GetVisualChild(lastIndex);
        if (lastVisual is not DrawingVisual renderer)
        {
            return;
        }

        using var context = renderer.RenderOpen();

        var verticalPen = VerticalGridLinePen;
        if (verticalPen is { Brush: not null, Thickness: > 0 })
        {
            for (var index = 1; index < ColumnDefinitions.Count; index++)
            {
                var offset = ColumnDefinitions[index].Offset;

                var start = new Point(offset, 0);
                var end   = new Point(offset, size.Height);

                context.DrawLine(verticalPen, start, end);
            }
        }

        var horizontalPen = HorizontalGridLinePen;
        if (horizontalPen is { Brush: not null, Thickness: > 0 })
        {
            for (var index = 1; index < RowDefinitions.Count; index++)
            {
                var offset = RowDefinitions[index].Offset;

                var start = new Point(0, offset);
                var end   = new Point(size.Width, offset);

                context.DrawLine(horizontalPen, start, end);
            }
        }
    }
}