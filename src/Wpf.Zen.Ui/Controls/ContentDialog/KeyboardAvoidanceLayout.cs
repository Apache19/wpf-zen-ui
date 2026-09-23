// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Chooses an unoccluded, host-local layout rectangle independently of native keyboard state.
/// </summary>
internal static class KeyboardAvoidanceLayout
{
    /// <summary>
    /// Excludes presentation padding from keyboard occlusion while keeping the complete outer
    /// rectangle inside the host. Placement is calculated for the inner content, then expanded
    /// back into the child's ordinary layout slot.
    /// </summary>
    internal static Rect Place(
        Rect host,
        Rect preferred,
        Rect keyboard,
        Thickness occlusionPadding,
        Size minimum = default)
    {
        var horizontal = occlusionPadding.Left + occlusionPadding.Right;
        var vertical   = occlusionPadding.Top + occlusionPadding.Bottom;

        if (preferred.Width <= horizontal || preferred.Height <= vertical)
        {
            return preferred;
        }

        var innerHost = new Rect(
            host.Left + occlusionPadding.Left,
            host.Top + occlusionPadding.Top,
            host.Width - horizontal,
            host.Height - vertical
        );

        var innerPreferred = new Rect(
            preferred.Left + occlusionPadding.Left,
            preferred.Top + occlusionPadding.Top,
            preferred.Width - horizontal,
            preferred.Height - vertical
        );

        var innerMinimum = new Size(
            Math.Max(0, minimum.Width - horizontal),
            Math.Max(0, minimum.Height - vertical)
        );

        var innerPlacement = Place(innerHost, innerPreferred, keyboard, innerMinimum);
        if (innerPlacement == innerPreferred)
        {
            return preferred;
        }

        return new Rect(
            innerPlacement.Left - occlusionPadding.Left,
            innerPlacement.Top - occlusionPadding.Top,
            innerPlacement.Width + horizontal,
            innerPlacement.Height + vertical
        );
    }

    /// <summary>
    /// Preserves the preferred rectangle when possible, otherwise chooses the smallest translation.
    /// If no direction fits, prefers slots satisfying minimum size, then maximizes retained area.
    /// Displacement breaks area ties. Remaining ties prefer
    /// above, below, left, then right, so identical input always produces identical placement.
    /// </summary>
    /// <remarks>
    /// All rectangles use the same DIP coordinate space and describe content requiring avoidance.
    /// A completely covered host retains its layout because no usable region exists; explicit
    /// child minimums remain the responsibility of WPF's layout and clipping.
    /// </remarks>
    internal static Rect Place(Rect host, Rect preferred, Rect keyboard, Size minimum = default)
    {
        var obstruction = Rect.Intersect(host, keyboard);
        var overlap     = Rect.Intersect(preferred, obstruction);

        if (overlap.IsEmpty || overlap.Width <= 0 || overlap.Height <= 0)
        {
            return preferred;
        }

        Rect[] regions =
        [
            new(host.Left, host.Top, host.Width, obstruction.Top - host.Top),
            new(host.Left, obstruction.Bottom, host.Width, host.Bottom - obstruction.Bottom),
            new(host.Left, host.Top, obstruction.Left - host.Left, host.Height),
            new(obstruction.Right, host.Top, host.Right - obstruction.Right, host.Height),
        ];

        var best         = preferred;
        var bestArea     = -1d;
        var bestDistance = double.PositiveInfinity;
        var bestMeetsMinimum = false;

        foreach (var region in regions)
        {
            if (region.Width <= 0 || region.Height <= 0)
            {
                continue;
            }

            var width  = Math.Min(preferred.Width, region.Width);
            var height = Math.Min(preferred.Height, region.Height);

            // Clamp the original center, rather than a previously displaced rectangle. This also
            // keeps the unaffected axis stationary when only one dimension needs to shrink.
            var x = Clamp(preferred.Left + (preferred.Width - width) / 2, region.Left, region.Right - width);
            var y = Clamp(preferred.Top + (preferred.Height - height) / 2, region.Top, region.Bottom - height);

            var dx = x + width / 2 - (preferred.Left + preferred.Width / 2);
            var dy = y + height / 2 - (preferred.Top + preferred.Height / 2);

            var area     = width * height;
            var distance = dx * dx + dy * dy;
            var meetsMinimum = width >= minimum.Width && height >= minimum.Height;

            if ((meetsMinimum && !bestMeetsMinimum)
                || (meetsMinimum == bestMeetsMinimum
                    && (area > bestArea || (area == bestArea && distance < bestDistance))))
            {
                best         = new Rect(x, y, width, height);
                bestArea     = area;
                bestDistance = distance;
                bestMeetsMinimum = meetsMinimum;
            }
        }

        return best;
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Max(minimum, Math.Min(value, maximum));
    }
}
