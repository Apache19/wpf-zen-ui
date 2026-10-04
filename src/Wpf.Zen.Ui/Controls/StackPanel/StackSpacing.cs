// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Shares axis conversion and gap rendering between the two stack panels.
/// </summary>
internal static class StackSpacing
{
    /// <summary>
    /// Accepts finite, nonnegative spacing values for dependency property validation.
    /// </summary>
    internal static bool IsValid(object value)
    {
        return value is double number && number >= 0 && !double.IsInfinity(number);
    }

    /// <summary>
    /// Reads the dimension parallel to the stacking direction.
    /// </summary>
    internal static double Along(Size size, Orientation orientation)
    {
        return orientation == Orientation.Vertical ? size.Height : size.Width;
    }

    /// <summary>
    /// Reads the dimension perpendicular to the stacking direction.
    /// </summary>
    internal static double Across(Size size, Orientation orientation)
    {
        return orientation == Orientation.Vertical ? size.Width : size.Height;
    }

    /// <summary>
    /// Converts stacking and cross-axis dimensions into a WPF size.
    /// </summary>
    internal static Size Size(double along, double across, Orientation orientation)
    {
        return orientation == Orientation.Vertical ? new Size(across, along) : new Size(along, across);
    }

    /// <summary>
    /// Converts stacking and cross-axis coordinates into a WPF layout rectangle.
    /// </summary>
    internal static Rect Rect(
        double along, double across, double length, double breadth, Orientation orientation)
    {
        return orientation == Orientation.Vertical
            ? new Rect(across, along, breadth, length)
            : new Rect(along, across, length, breadth);
    }

    /// <summary>
    /// Fills only the arranged gap slots, clipped to the panel, above its background. A single
    /// geometry gives relative brushes consistent coordinates across the complete gap pattern.
    /// </summary>
    internal static void Draw(DrawingContext context, Brush? brush, List<Rect> gaps, Size size)
    {
        if (brush is null || gaps.Count == 0)
        {
            return;
        }

        var panelBounds = new Rect(size);
        var geometry    = new StreamGeometry();

        using (var writer = geometry.Open())
        {
            foreach (var gap in gaps)
            {
                var clipped = System.Windows.Rect.Intersect(gap, panelBounds);
                if (clipped.IsEmpty || clipped.Width <= 0 || clipped.Height <= 0)
                {
                    continue;
                }

                writer.BeginFigure(clipped.TopLeft, true, true);
                writer.LineTo(clipped.TopRight, true, false);
                writer.LineTo(clipped.BottomRight, true, false);
                writer.LineTo(clipped.BottomLeft, true, false);
            }
        }

        geometry.Freeze();
        context.DrawGeometry(brush, null, geometry);
    }
}