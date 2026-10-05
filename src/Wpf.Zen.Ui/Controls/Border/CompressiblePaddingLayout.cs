// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Allocates space to content before reserving optional padding for border decorators.
/// </summary>
internal sealed class CompressiblePaddingLayout
{
    private Size _contentDemand;

    /// <summary>
    /// Measures content inside the border, then reserves as much padding as fits.
    /// </summary>
    /// <remarks>
    /// Demand is captured before the final measure so that arranging in a larger slot can restore
    /// padding. Finite constraints preserve wrapping and scrolling behavior. Greedy content that
    /// requests the entire constraint leaves no room for padding on that axis.
    /// </remarks>
    internal Size Measure(UIElement? child, Size constraint, Thickness border, Thickness padding)
    {
        var borderWidth  = border.Left + border.Right;
        var borderHeight = border.Top + border.Bottom;

        var available = new Size(
            Math.Max(0.0, constraint.Width - borderWidth),
            Math.Max(0.0, constraint.Height - borderHeight)
        );

        child?.Measure(available);

        _contentDemand = child?.DesiredSize ?? default;

        var effectivePadding = GetPadding(available, padding);
        var paddingWidth     = effectivePadding.Left + effectivePadding.Right;
        var paddingHeight    = effectivePadding.Top + effectivePadding.Bottom;

        var contentConstraint = new Size(
            Math.Max(0.0, available.Width - paddingWidth),
            Math.Max(0.0, available.Height - paddingHeight)
        );

        // The final constraint must also reach panels whose layout depends on available space,
        // even when their reported desired size did not consume that space.
        child?.Measure(contentConstraint);

        var desired = child?.DesiredSize ?? default;
        var width   = desired.Width + paddingWidth + borderWidth;
        var height  = desired.Height + paddingHeight + borderHeight;

        // Avoid recording an oversized unclipped desired size on the decorator itself when
        // even its fixed border exceeds the constraint. Child minimum sizes still apply normally.
        return new Size(Math.Min(constraint.Width, width), Math.Min(constraint.Height, height));
    }

    /// <summary>
    /// Distributes remaining space independently on each axis, preserving each pair's ratio.
    /// </summary>
    internal Thickness GetPadding(Size available, Thickness padding)
    {
        var horizontal = GetScale(available.Width, _contentDemand.Width, padding.Left, padding.Right);
        var vertical   = GetScale(available.Height, _contentDemand.Height, padding.Top, padding.Bottom);

        return new Thickness(
            padding.Left * horizontal,
            padding.Top * vertical,
            padding.Right * horizontal,
            padding.Bottom * vertical
        );
    }

    /// <summary>
    /// Computes the retained fraction without imposing a constraint on an unbounded axis.
    /// </summary>
    private static double GetScale(double available, double demand, double first, double second)
    {
        var total = first + second;
        if (total == 0.0 || double.IsPositiveInfinity(available))
        {
            return 1.0;
        }

        var remaining = Math.Max(0.0, available - demand);
        var fraction  = remaining / total;

        return Math.Min(1.0, fraction);
    }
}