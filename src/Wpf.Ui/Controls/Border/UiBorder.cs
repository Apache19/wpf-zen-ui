// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Media;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Describes how <see cref="UiBorder"/> applies <see cref="Border.BorderThickness"/>
/// to layout and rendering.
/// </summary>
/// <remarks>
/// Native <see cref="Border"/> always treats <see cref="Border.BorderThickness"/> as both layout chrome and
/// render chrome. <see cref="UiBorder"/> keeps that behavior available, while also supporting an overlay mode
/// for Fluent-style controls whose stroke should not push content inward.
/// </remarks>
public enum BorderPlacement
{
    /// <summary>
    /// Places the border outside the background and child content, matching the native <see cref="Border"/>.
    /// </summary>
    Outside,

    /// <summary>
    /// Places the border over the background without reserving layout space for the border thickness.
    /// </summary>
    Inside,
}

/// <summary>
/// Represents a border that can either draw like the native <see cref="Border"/>, or draw its border over
/// the background without reserving layout space for <see cref="Border.BorderThickness"/>.
/// </summary>
/// <remarks>
/// The control intentionally keeps <see cref="Border.Padding"/> semantics aligned with <see cref="Border"/> in both
/// placement modes. Only <see cref="Border.BorderThickness"/> changes behavior in <see cref="BorderPlacement.Inside"/>.
/// </remarks>
public class UiBorder : Border
{
    private const double DoubleEpsilon = 2.2204460492503131e-016;

    /// <summary>Identifies the <see cref="BorderPlacement"/> dependency property.</summary>
    public static readonly DependencyProperty BorderPlacementProperty =
        DependencyProperty.Register(
            nameof(BorderPlacement),
            typeof(BorderPlacement),
            typeof(UiBorder),
            new FrameworkPropertyMetadata(
                // Inside is the default because UiBorder exists primarily for strokes that should overlay the surface.
                // Consumers can opt back into native Border layout/rendering with BorderPlacement.Outside.
                BorderPlacement.Inside,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender
            ),
            ValidateBorderPlacement
        );

    /// <summary>
    /// Gets or sets whether the border should behave like the native <see cref="Border"/> or overlap the background.
    /// </summary>
    public BorderPlacement BorderPlacement
    {
        get => (BorderPlacement)GetValue(BorderPlacementProperty);
        set => SetValue(BorderPlacementProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        // Outside mode is deliberately delegated to Border instead of reimplementing it. Native Border has several
        // framework-specific details here, including layout rounding of BorderThickness.
        if (BorderPlacement == BorderPlacement.Outside)
        {
            return base.MeasureOverride(constraint);
        }

        // Inside mode removes only BorderThickness from layout. Padding still participates in measure exactly as
        // it does on Border, so existing content spacing behavior remains predictable.
        Size padding = CollapseThickness(Padding);

        if (Child is not UIElement child)
        {
            return padding;
        }

        var childConstraint = new Size(
            Math.Max(0.0, constraint.Width - padding.Width),
            Math.Max(0.0, constraint.Height - padding.Height)
        );

        child.Measure(childConstraint);

        return new Size(
            child.DesiredSize.Width + padding.Width,
            child.DesiredSize.Height + padding.Height
        );
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // Keep native behavior intact for Outside mode. This also preserves any future framework fixes in Border.
        if (BorderPlacement == BorderPlacement.Outside)
        {
            return base.ArrangeOverride(finalSize);
        }

        // The child is arranged inside Padding only. The stroke is painted later over the same outer surface as
        // the background, so BorderThickness must not be subtracted here.
        Child?.Arrange(DeflateRect(new Rect(finalSize), Padding));

        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        // Outside mode intentionally uses Border.OnRender so its background shrink, half-thickness corner radii,
        // pen caches, and simple/complex render path selection stay identical to WPF.
        if (BorderPlacement == BorderPlacement.Outside)
        {
            base.OnRender(drawingContext);
            return;
        }

        Rect bounds = new(RenderSize);
        if (!IsRenderable(bounds))
        {
            return;
        }

        CornerRadius cornerRadius = CornerRadius;
        if (Background is { } background)
        {
            // In Inside mode the background owns the full rounded rectangle. It is not deflated by
            // BorderThickness, because the border is an overlay rather than layout chrome.
            StreamGeometry backgroundGeometry = CreateRoundedRectangleGeometry(bounds, new CornerRadii(cornerRadius));
            drawingContext.DrawGeometry(background, null, backgroundGeometry);
        }

        Thickness borderThickness = GetRoundedBorderThickness();
        if (IsZero(borderThickness) || BorderBrush is not { } borderBrush)
        {
            return;
        }

        StreamGeometry borderGeometry = CreateBorderGeometry(bounds, cornerRadius, borderThickness);
        drawingContext.DrawGeometry(borderBrush, null, borderGeometry);
    }

    private static bool ValidateBorderPlacement(object? value)
    {
        return value is BorderPlacement.Outside or BorderPlacement.Inside;
    }

    private static Size CollapseThickness(Thickness thickness)
    {
        return new Size(
            thickness.Left + thickness.Right,
            thickness.Top + thickness.Bottom
        );
    }

    private static Rect DeflateRect(Rect rect, Thickness thickness)
    {
        return new Rect(
            rect.Left + thickness.Left,
            rect.Top + thickness.Top,
            Math.Max(0.0, rect.Width - thickness.Left - thickness.Right),
            Math.Max(0.0, rect.Height - thickness.Top - thickness.Bottom)
        );
    }

    private Thickness GetRoundedBorderThickness()
    {
        Thickness borderThickness = BorderThickness;
        if (!UseLayoutRounding)
        {
            return borderThickness;
        }

        DpiScale dpi = VisualTreeHelper.GetDpi(this);

        // UIElement.RoundLayoutValue is internal. This mirrors the public behavior used by Border closely enough
        // for the custom Inside render path without reflecting into WPF internals for a small arithmetic helper.
        return new Thickness(
            RoundLayoutValue(borderThickness.Left,   dpi.DpiScaleX),
            RoundLayoutValue(borderThickness.Top,    dpi.DpiScaleY),
            RoundLayoutValue(borderThickness.Right,  dpi.DpiScaleX),
            RoundLayoutValue(borderThickness.Bottom, dpi.DpiScaleY)
        );
    }

    private static StreamGeometry CreateBorderGeometry(Rect bounds, CornerRadius cornerRadius, Thickness borderThickness)
    {
        StreamGeometry geometry = new()
        {
            FillRule = FillRule.EvenOdd
        };

        using (StreamGeometryContext context = geometry.Open())
        {
            // Native Border treats CornerRadius as the centerline radius and shrinks the background
            // by half of BorderThickness. Inside placement instead treats CornerRadius as the shared
            // outer edge, then cuts a full-thickness inner edge from it so the border overlays the
            // background without changing layout.
            GenerateGeometry(context, bounds, new CornerRadii(cornerRadius));

            Rect innerBounds = DeflateRect(bounds, borderThickness);
            if (IsRenderable(innerBounds))
            {
                GenerateGeometry(context, innerBounds, new CornerRadii(cornerRadius, borderThickness));
            }
        }

        geometry.Freeze();

        return geometry;
    }

    private static StreamGeometry CreateRoundedRectangleGeometry(Rect bounds, CornerRadii radii)
    {
        StreamGeometry geometry = new()
        {
            FillRule = FillRule.EvenOdd
        };

        using (StreamGeometryContext context = geometry.Open())
        {
            GenerateGeometry(context, bounds, radii);
        }

        geometry.Freeze();

        return geometry;
    }

    private static void GenerateGeometry(StreamGeometryContext context, Rect rect, CornerRadii radii)
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
            double value = radii.LeftTop / (radii.LeftTop + radii.RightTop) * rect.Width;
            topLeft.X  = value;
            topRight.X = value;
        }

        if (rightTop.Y > rightBottom.Y)
        {
            double value = radii.TopRight / (radii.TopRight + radii.BottomRight) * rect.Height;
            rightTop.Y    = value;
            rightBottom.Y = value;
        }

        if (bottomRight.X < bottomLeft.X)
        {
            double value = radii.LeftBottom / (radii.LeftBottom + radii.RightBottom) * rect.Width;
            bottomRight.X = value;
            bottomLeft.X  = value;
        }

        if (leftBottom.Y < leftTop.Y)
        {
            double value = radii.TopLeft / (radii.TopLeft + radii.BottomLeft) * rect.Height;
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

        double radiusX = rect.TopRight.X - topRight.X;
        double radiusY = rightTop.Y - rect.TopRight.Y;
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

    private static bool IsRenderable(Rect rect)
    {
        return GreaterThanZero(rect.Width) && GreaterThanZero(rect.Height);
    }

    private static bool IsZero(Thickness thickness)
    {
        return IsZero(thickness.Left) && IsZero(thickness.Top) && IsZero(thickness.Right) && IsZero(thickness.Bottom);
    }

    private static bool IsZero(double value)
    {
        return Math.Abs(value) < 10.0 * DoubleEpsilon;
    }

    private static bool GreaterThanZero(double value)
    {
        return value >= 10.0 * DoubleEpsilon;
    }

    private static bool AreClose(double value1, double value2)
    {
        if (Math.Abs(value1 - value2) < 0.00001)
        {
            return true;
        }

        double epsilon = (Math.Abs(value1) + Math.Abs(value2) + 10.0) * DoubleEpsilon;
        double delta   = value1 - value2;

        return -epsilon < delta && epsilon > delta;
    }

    private static double RoundLayoutValue(double value, double dpiScale)
    {
        double roundedValue = AreClose(dpiScale, 1.0) ? Math.Round(value) : Math.Round(value * dpiScale) / dpiScale;

        if (double.IsNaN(roundedValue) || double.IsInfinity(roundedValue) || AreClose(roundedValue, double.MaxValue))
        {
            return value;
        }

        return roundedValue;
    }

    private readonly struct CornerRadii
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CornerRadii"/> struct for the full outer surface
        /// used by the Inside background.
        /// </summary>
        public CornerRadii(CornerRadius radii)
        {
            LeftTop     = TopLeft     = radii.TopLeft;
            TopRight    = RightTop    = radii.TopRight;
            RightBottom = BottomRight = radii.BottomRight;
            BottomLeft  = LeftBottom  = radii.BottomLeft;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CornerRadii"/> struct for the inner edge of an inside stroke.
        /// </summary>
        /// <remarks>
        /// Native Border subtracts half the border thickness because its CornerRadius is interpreted as the stroke
        /// centerline. Inside placement subtracts the whole thickness because the outer edge and background already
        /// share the requested CornerRadius.
        /// </remarks>
        public CornerRadii(CornerRadius radii, Thickness borders)
        {
            LeftTop     = Math.Max(0.0, radii.TopLeft - borders.Left);
            TopLeft     = Math.Max(0.0, radii.TopLeft - borders.Top);
            TopRight    = Math.Max(0.0, radii.TopRight - borders.Top);
            RightTop    = Math.Max(0.0, radii.TopRight - borders.Right);
            RightBottom = Math.Max(0.0, radii.BottomRight - borders.Right);
            BottomRight = Math.Max(0.0, radii.BottomRight - borders.Bottom);
            BottomLeft  = Math.Max(0.0, radii.BottomLeft - borders.Bottom);
            LeftBottom  = Math.Max(0.0, radii.BottomLeft - borders.Left);
        }

        public double LeftTop { get; }

        public double TopLeft { get; }

        public double TopRight { get; }

        public double RightTop { get; }

        public double RightBottom { get; }

        public double BottomRight { get; }

        public double BottomLeft { get; }

        public double LeftBottom { get; }
    }
}