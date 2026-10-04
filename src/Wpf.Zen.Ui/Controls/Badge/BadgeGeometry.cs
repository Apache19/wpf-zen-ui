// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Portions adapted from dotnet/wpf Border.cs (GenerateGeometry and Radii).

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Builds Border-compatible inner fills and even-odd border rings for the badge's drawing surface.
/// </summary>
internal static class BadgeGeometry
{
    /// <summary>
    /// Creates frozen contours. Explicit radii describe the border centerline; null derives
    /// a capsule from the outer dimensions. Overlapping arcs are fitted to the available edges.
    /// </summary>
    internal static void Create(
        Size size,
        Thickness border,
        CornerRadius? radius,
        out StreamGeometry? background,
        out StreamGeometry? outline)
    {
        background = null;
        outline    = null;

        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var outer = new Rect(size);
        var inner = Deflate(outer, border);

        Radii innerRadii;
        Radii outerRadii;

        if (radius is CornerRadius explicitRadius)
        {
            innerRadii = new Radii(explicitRadius, border, false);
            outerRadii = new Radii(explicitRadius, border, true);
        }
        else
        {
            // Start at the outside contour so thick or unequal borders cannot distort the capsule.
            var capsuleRadius = Math.Min(size.Width, size.Height) / 2D;
            var outerRadius   = new CornerRadius(capsuleRadius);
            outerRadii = new Radii(outerRadius, new Thickness(), true);
            innerRadii = new Radii(outerRadius, border, false, true);
        }

        var hasInner = inner.Width > 0 && inner.Height > 0;
        if (hasInner)
        {
            background = new StreamGeometry();
            using (var context = background.Open())
            {
                GenerateGeometry(context, inner, innerRadii);
            }

            background.Freeze();
        }

        if (border == new Thickness())
        {
            return;
        }

        outline = new StreamGeometry
        {
            FillRule = FillRule.EvenOdd
        };

        using (var context = outline.Open())
        {
            GenerateGeometry(context, outer, outerRadii);
            if (hasInner)
            {
                GenerateGeometry(context, inner, innerRadii);
            }
        }

        outline.Freeze();
    }

    /// <summary>
    /// Removes border or padding widths without producing negative dimensions.
    /// </summary>
    internal static Rect Deflate(Rect rect, Thickness thickness)
    {
        var width  = Math.Max(0, rect.Width - thickness.Left - thickness.Right);
        var height = Math.Max(0, rect.Height - thickness.Top - thickness.Bottom);
        return new Rect(rect.Left + thickness.Left, rect.Top + thickness.Top, width, height);
    }

    /// <summary>
    /// Emits a closed contour using the same corner overlap rules as WPF Border.
    /// </summary>
    private static void GenerateGeometry(StreamGeometryContext context, Rect rect, Radii radii)
    {
        // --------------------------------------------------------------------------------------------------
        // This is adapted from WPF Border.GenerateGeometry. The important behavior is the overlap correction
        // below: when adjacent corner radii are too large for an edge, WPF scales both radii proportionally
        // instead of clamping one side first. That keeps independently configured corners visually balanced.
        // --------------------------------------------------------------------------------------------------

        Point topLeft     = new(radii.LeftTop, 0);
        Point topRight    = new(rect.Width - radii.RightTop, 0);
        Point rightTop    = new(rect.Width, radii.TopRight);
        Point rightBottom = new(rect.Width, rect.Height - radii.BottomRight);
        Point bottomRight = new(rect.Width - radii.RightBottom, rect.Height);
        Point bottomLeft  = new(radii.LeftBottom, rect.Height);
        Point leftBottom  = new(0, rect.Height - radii.BottomLeft);
        Point leftTop     = new(0, radii.TopLeft);

        if (topLeft.X > topRight.X)
        {
            var value = radii.LeftTop / (radii.LeftTop + radii.RightTop) * rect.Width;
            topLeft.X  = value;
            topRight.X = value;
        }

        if (rightTop.Y > rightBottom.Y)
        {
            var value = radii.TopRight / (radii.TopRight + radii.BottomRight) * rect.Height;
            rightTop.Y    = value;
            rightBottom.Y = value;
        }

        if (bottomRight.X < bottomLeft.X)
        {
            var value = radii.LeftBottom / (radii.LeftBottom + radii.RightBottom) * rect.Width;
            bottomRight.X = value;
            bottomLeft.X  = value;
        }

        if (leftBottom.Y < leftTop.Y)
        {
            var value = radii.TopLeft / (radii.TopLeft + radii.BottomLeft) * rect.Height;
            leftBottom.Y = value;
            leftTop.Y    = value;
        }

        Vector offset = new(rect.TopLeft.X, rect.TopLeft.Y);

        topLeft     += offset;
        topRight    += offset;
        rightTop    += offset;
        rightBottom += offset;
        bottomRight += offset;
        bottomLeft  += offset;
        leftBottom  += offset;
        leftTop     += offset;

        context.BeginFigure(topLeft, true, true);
        context.LineTo(topRight, true, false);

        var radiusX = rect.TopRight.X - topRight.X;
        var radiusY = rightTop.Y - rect.TopRight.Y;
        if (!IsZero(radiusX) || !IsZero(radiusY))
        {
            context.ArcTo(rightTop, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);
        }

        context.LineTo(rightBottom, true, false);

        radiusX = rect.BottomRight.X - bottomRight.X;
        radiusY = rect.BottomRight.Y - rightBottom.Y;
        if (!IsZero(radiusX) || !IsZero(radiusY))
        {
            context.ArcTo(bottomRight, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);
        }

        context.LineTo(bottomLeft, true, false);

        radiusX = bottomLeft.X - rect.BottomLeft.X;
        radiusY = rect.BottomLeft.Y - leftBottom.Y;
        if (!IsZero(radiusX) || !IsZero(radiusY))
        {
            context.ArcTo(leftBottom, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);
        }

        context.LineTo(leftTop, true, false);

        radiusX = topLeft.X - rect.TopLeft.X;
        radiusY = leftTop.Y - rect.TopLeft.Y;
        if (!IsZero(radiusX) || !IsZero(radiusY))
        {
            context.ArcTo(topLeft, new Size(radiusX, radiusY), 0, false, SweepDirection.Clockwise, true, false);
        }
    }

    private static bool IsZero(double value)
    {
        return Math.Abs(value) < 2.2204460492503131e-15;
    }

    /// <summary>
    /// Stores horizontal and vertical radii for the four corners of an offset border contour.
    /// </summary>
    private struct Radii
    {
        /// <summary>
        /// Offsets centerline radii by half a border, or outer radii by a full border for the
        /// capsule's inner contour. The outerRadius flag is used only for inward offsets.
        /// </summary>
        internal Radii(CornerRadius radii, Thickness borders, bool outer, bool outerRadius = false)
        {
            var scale  = outerRadius ? 1.0 : 0.5;
            var left   = scale * borders.Left;
            var top    = scale * borders.Top;
            var right  = scale * borders.Right;
            var bottom = scale * borders.Bottom;

            if (outer)
            {
                if (IsZero(radii.TopLeft))
                {
                    LeftTop = TopLeft = 0.0;
                }
                else
                {
                    LeftTop = radii.TopLeft + left;
                    TopLeft = radii.TopLeft + top;
                }

                if (IsZero(radii.TopRight))
                {
                    TopRight = RightTop = 0.0;
                }
                else
                {
                    TopRight = radii.TopRight + top;
                    RightTop = radii.TopRight + right;
                }

                if (IsZero(radii.BottomRight))
                {
                    RightBottom = BottomRight = 0.0;
                }
                else
                {
                    RightBottom = radii.BottomRight + right;
                    BottomRight = radii.BottomRight + bottom;
                }

                if (IsZero(radii.BottomLeft))
                {
                    BottomLeft = LeftBottom = 0.0;
                }
                else
                {
                    BottomLeft = radii.BottomLeft + bottom;
                    LeftBottom = radii.BottomLeft + left;
                }
            }
            else
            {
                LeftTop     = Math.Max(0.0, radii.TopLeft - left);
                TopLeft     = Math.Max(0.0, radii.TopLeft - top);
                TopRight    = Math.Max(0.0, radii.TopRight - top);
                RightTop    = Math.Max(0.0, radii.TopRight - right);
                RightBottom = Math.Max(0.0, radii.BottomRight - right);
                BottomRight = Math.Max(0.0, radii.BottomRight - bottom);
                BottomLeft  = Math.Max(0.0, radii.BottomLeft - bottom);
                LeftBottom  = Math.Max(0.0, radii.BottomLeft - left);
            }
        }

        internal double LeftTop;
        internal double TopLeft;
        internal double TopRight;
        internal double RightTop;
        internal double RightBottom;
        internal double BottomRight;
        internal double BottomLeft;
        internal double LeftBottom;
    }
}