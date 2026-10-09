// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Shares axis conversion between the two stack panels.
/// </summary>
internal static class StackSpacing
{
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
}
