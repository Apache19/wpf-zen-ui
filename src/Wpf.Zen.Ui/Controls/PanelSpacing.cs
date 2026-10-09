// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Validates and paints layout gaps shared by spacing-aware panels.
/// </summary>
internal static class PanelSpacing
{
    /// <summary>
    /// Accepts finite, nonnegative spacing values for dependency property validation.
    /// </summary>
    internal static bool IsValid(object value)
    {
        return value is double number && number >= 0 && !double.IsInfinity(number);
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