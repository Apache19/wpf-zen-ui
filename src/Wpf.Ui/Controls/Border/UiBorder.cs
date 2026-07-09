// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
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
/// render chrome. <see cref="UiBorder"/> uses that behavior by default through
/// <see cref="BorderPlacement.Native"/>. <see cref="BorderPlacement.Outside"/> and
/// <see cref="BorderPlacement.Inside"/> are opt-in render-only placement modes for strokes whose thickness may
/// change at run time without invalidating surrounding layout.
/// </remarks>
public enum BorderPlacement
{
    /// <summary>
    /// Uses the native <see cref="Border"/> layout and rendering behavior. This is the default.
    /// </summary>
    /// <remarks>
    /// In this mode <see cref="Border.BorderThickness"/> participates in measure and arrange, and the control
    /// delegates rendering to <see cref="Border"/>. This is the correct choice when WPF's default chrome sizing
    /// and drawing behavior is desired exactly.
    /// </remarks>
    Native,

    /// <summary>
    /// Draws the border outside the background without reserving layout space for the border thickness.
    /// </summary>
    /// <remarks>
    /// The child is arranged as if the border thickness were zero. During rendering, the background is deflated
    /// by <see cref="Border.BorderThickness"/> before it is drawn, so translucent border and background brushes
    /// do not blend over the same pixels.
    /// </remarks>
    Outside,

    /// <summary>
    /// Places the border over the background without reserving layout space for the border thickness.
    /// </summary>
    Inside,
}

/// <summary>
/// Represents a border that can either draw like the native <see cref="Border"/>, or draw a render-only border
/// without reserving layout space for <see cref="Border.BorderThickness"/>.
/// </summary>
/// <remarks>
/// The control intentionally keeps <see cref="Border.Padding"/> semantics aligned with <see cref="Border"/> in all
/// placement modes. Only <see cref="Border.BorderThickness"/> changes behavior in <see cref="BorderPlacement.Outside"/>
/// and <see cref="BorderPlacement.Inside"/>: it is ignored by layout, then interpreted only by the render path.
/// </remarks>
public class UiBorder : Border
{
    private const double DoubleEpsilon = 2.2204460492503131e-016;
    private const string DoNotApplyLayoutRoundingToBorderThicknessSwitchName =
        "Switch.MS.Internal.DoNotApplyLayoutRoundingToMarginsAndBorderThickness";
    private const uint AffectsMeasureMetadataFlag = 0x00000040;

    private static readonly FieldInfo? PropertyMetadataFlagsField = typeof(PropertyMetadata).GetField(
        "_flags",
        BindingFlags.Instance | BindingFlags.NonPublic
    );
    private static readonly bool DoNotApplyLayoutRoundingToBorderThickness =
        IsAppContextSwitchEnabled(DoNotApplyLayoutRoundingToBorderThicknessSwitchName);

    private StreamGeometry? _backgroundGeometryCache;
    private StreamGeometry? _borderGeometryCache;
    private Size            _cachedRenderSize;
    private CornerRadius    _cachedCornerRadius;
    private Thickness       _cachedBorderThickness;
    private BorderPlacement _cachedBorderPlacement;
    private bool            _isGeometryCacheValid;

    static UiBorder()
    {
        var borderThicknessMetadata = new FrameworkPropertyMetadata(
            default(Thickness),
            FrameworkPropertyMetadataOptions.AffectsRender,
            OnBorderThicknessChanged
        );

        BorderThicknessProperty.OverrideMetadata(typeof(UiBorder), borderThicknessMetadata);

        // FrameworkPropertyMetadata.Merge deliberately preserves AffectsMeasure from every base owner, so the public
        // metadata API cannot express UiBorder's render-only modes. Remove that one inherited flag from UiBorder's own
        // sealed metadata; the property callback below restores native invalidation only when Native mode needs it.
        // If WPF changes its internal metadata representation, this helper leaves the flag untouched and UiBorder
        // safely falls back to the extra layout pass rather than risking invalid dependency-property metadata.
        TryRemoveInheritedAffectsMeasure(borderThicknessMetadata);
    }

    /// <summary>Identifies the <see cref="BorderPlacement"/> dependency property.</summary>
    public static readonly DependencyProperty BorderPlacementProperty =
        DependencyProperty.Register(
            nameof(BorderPlacement),
            typeof(BorderPlacement),
            typeof(UiBorder),
            // Native is the default so UiBorder preserves Border's layout and rendering behavior unless one of the
            // render-only placement modes is explicitly selected.
            new FrameworkPropertyMetadata(
                BorderPlacement.Native,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender
            ),
            ValidateBorderPlacement
        );

    /// <summary>
    /// Gets or sets how the border thickness participates in layout and how the border is drawn relative to the background.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="BorderPlacement.Native"/>, which preserves the behavior of <see cref="Border"/>.
    /// Set this property to <see cref="BorderPlacement.Outside"/> or <see cref="BorderPlacement.Inside"/> to opt in
    /// to render-only border behavior that does not reserve layout space for <see cref="Border.BorderThickness"/>.
    /// </remarks>
    public BorderPlacement BorderPlacement
    {
        get => (BorderPlacement)GetValue(BorderPlacementProperty);
        set => SetValue(BorderPlacementProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        // Native mode is deliberately delegated to Border instead of reimplementing it. Native Border has several
        // framework-specific details here, including layout rounding of BorderThickness.
        if (BorderPlacement == BorderPlacement.Native)
        {
            return base.MeasureOverride(constraint);
        }

        // Render-only placement modes remove only BorderThickness from layout. Padding still participates in measure
        // exactly as it does on Border, so existing content spacing behavior remains predictable when stroke thickness
        // is animated or otherwise changed at run time.
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
        // Keep native behavior intact for Native mode. This also preserves any future framework fixes in Border.
        if (BorderPlacement == BorderPlacement.Native)
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
        // Native mode intentionally uses Border.OnRender so its background shrink, half-thickness corner radii,
        // pen caches, and simple/complex render path selection stay identical to WPF.
        if (BorderPlacement == BorderPlacement.Native)
        {
            base.OnRender(drawingContext);
            return;
        }

        EnsureGeometryCache();

        if (Background is { } background && _backgroundGeometryCache is { } backgroundGeometry)
        {
            drawingContext.DrawGeometry(background, null, backgroundGeometry);
        }

        if (BorderBrush is not { } borderBrush || _borderGeometryCache is not { } borderGeometry)
        {
            return;
        }

        drawingContext.DrawGeometry(borderBrush, null, borderGeometry);
    }

    private static void OnBorderThicknessChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var border = (UiBorder)dependencyObject;

        // The custom modes consume BorderThickness only while drawing. Native mode delegates measure and arrange to
        // Border, so it must restore the invalidation that was intentionally removed from UiBorder's metadata.
        if (border.BorderPlacement == BorderPlacement.Native)
        {
            border.InvalidateMeasure();
        }
    }

    private static void TryRemoveInheritedAffectsMeasure(FrameworkPropertyMetadata metadata)
    {
        try
        {
            if (
                PropertyMetadataFlagsField?.FieldType is not { IsEnum: true } flagsType
                || PropertyMetadataFlagsField.GetValue(metadata) is not { } flags
            )
            {
                return;
            }

            uint value = Convert.ToUInt32(flags);
            PropertyMetadataFlagsField.SetValue(metadata, Enum.ToObject(flagsType, value & ~AffectsMeasureMetadataFlag));

            // Treat a changed internal bit layout as a failed reflection attempt. The fallback remains WPF's original,
            // conservative AffectsMeasure behavior and therefore cannot leave layout in an invalid state.
            if (metadata.AffectsMeasure)
            {
                PropertyMetadataFlagsField.SetValue(metadata, flags);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or FieldAccessException
                or InvalidOperationException
                or NotSupportedException
                or OverflowException
        )
        {
            // WPF metadata internals are version-specific. Failure only costs an unnecessary measure pass, which is a
            // safer degradation than failing type initialization or suppressing required native layout invalidation.
        }
    }

    private static bool ValidateBorderPlacement(object? value)
    {
        return value is BorderPlacement.Native or BorderPlacement.Outside or BorderPlacement.Inside;
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
        if (!UseLayoutRounding || DoNotApplyLayoutRoundingToBorderThickness)
        {
            return borderThickness;
        }

        DpiScale dpi = VisualTreeHelper.GetDpi(this);

        // UIElement.RoundLayoutValue is internal. The arithmetic below mirrors it without reflection, while the
        // AppContext switch above preserves Border's compatibility behavior for applications that explicitly opt out
        // of rounding margins and border thicknesses.
        return new Thickness(
            RoundLayoutValue(borderThickness.Left,   dpi.DpiScaleX),
            RoundLayoutValue(borderThickness.Top,    dpi.DpiScaleY),
            RoundLayoutValue(borderThickness.Right,  dpi.DpiScaleX),
            RoundLayoutValue(borderThickness.Bottom, dpi.DpiScaleY)
        );
    }

    private void EnsureGeometryCache()
    {
        Size renderSize = RenderSize;
        CornerRadius cornerRadius = CornerRadius;
        Thickness borderThickness = GetRoundedBorderThickness();
        BorderPlacement borderPlacement = BorderPlacement;

        if (
            _isGeometryCacheValid
            && _cachedRenderSize == renderSize
            && _cachedCornerRadius == cornerRadius
            && _cachedBorderThickness == borderThickness
            && _cachedBorderPlacement == borderPlacement
        )
        {
            return;
        }

        _cachedRenderSize = renderSize;
        _cachedCornerRadius = cornerRadius;
        _cachedBorderThickness = borderThickness;
        _cachedBorderPlacement = borderPlacement;
        _isGeometryCacheValid = true;

        Rect bounds = new(renderSize);
        if (!IsRenderable(bounds))
        {
            _backgroundGeometryCache = null;
            _borderGeometryCache = null;
            return;
        }

        // Geometry depends only on the values captured above, not on the brushes. Keeping the frozen instances across
        // brush-only render invalidations avoids rebuilding paths while still allowing Freezable brushes to update the
        // retained drawing commands independently.
        _backgroundGeometryCache = CreateBackgroundGeometry(bounds, cornerRadius, borderThickness);
        _borderGeometryCache = IsZero(borderThickness)
            ? null
            : CreateBorderGeometry(bounds, cornerRadius, borderThickness);
    }

    private static StreamGeometry CreateBorderGeometry(Rect bounds, CornerRadius cornerRadius, Thickness borderThickness)
    {
        StreamGeometry geometry = new()
        {
            FillRule = FillRule.EvenOdd
        };

        using (StreamGeometryContext context = geometry.Open())
        {
            // Native Border treats CornerRadius as the centerline radius and shrinks its background by half of
            // BorderThickness. The render-only modes treat CornerRadius as the outer edge, then cut a full-thickness
            // inner edge from it. Inside draws the background below this ring; Outside draws the background only inside
            // the same inner edge.
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

    private StreamGeometry? CreateBackgroundGeometry(Rect bounds, CornerRadius cornerRadius, Thickness borderThickness)
    {
        if (BorderPlacement == BorderPlacement.Outside)
        {
            Rect backgroundBounds = DeflateRect(bounds, borderThickness);
            if (!IsRenderable(backgroundBounds))
            {
                return null;
            }

            // Outside placement uses the same inner edge for both the border geometry and the background geometry.
            // This keeps semi-transparent brushes from being painted over each other while still allowing the stroke
            // thickness to change without affecting the control's measured or arranged size.
            return CreateRoundedRectangleGeometry(backgroundBounds, new CornerRadii(cornerRadius, borderThickness));
        }

        // Inside placement intentionally lets the background own the full rounded rectangle, then draws the border
        // over that same surface. Some Fluent states rely on this overlay behavior for subtle highlight strokes.
        return CreateRoundedRectangleGeometry(bounds, new CornerRadii(cornerRadius));
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
        // WPF performs this equality check first so equal infinities are handled before epsilon arithmetic.
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (value1 == value2)
        {
            return true;
        }

        double epsilon = (Math.Abs(value1) + Math.Abs(value2) + 10.0) * DoubleEpsilon;
        double delta   = value1 - value2;

        return -epsilon < delta && epsilon > delta;
    }

    private static bool IsAppContextSwitchEnabled(string switchName)
    {
        return AppContext.TryGetSwitch(switchName, out bool isEnabled) && isEnabled;
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
        /// Initializes a new instance of the <see cref="CornerRadii"/> struct for the inner edge of a render-only stroke.
        /// </summary>
        /// <remarks>
        /// Native Border subtracts half the border thickness because its CornerRadius is interpreted as the stroke
        /// centerline. Render-only placement subtracts the whole thickness because the requested CornerRadius belongs
        /// to the outer edge, and the background may either overlap that edge (<see cref="BorderPlacement.Inside"/>)
        /// or start at the resulting inner edge (<see cref="BorderPlacement.Outside"/>).
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
