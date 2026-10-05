// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// Portions derived from dotnet/wpf Border.cs

using System.Collections;
using System.Windows.Controls;
using System.Windows.Markup;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// A Border-like decorator that clips its content to the inner edge of the border.
/// The clip is applied to an internal host, so the child's own Clip property is preserved.
/// </summary>
[ContentProperty(nameof(Child))]
public sealed class ClippingBorder : Decorator
{
    private readonly CompressiblePaddingLayout _paddingLayout = new();
    private const string DoNotApplyLayoutRoundingToBorderThicknessSwitchName =
        "Switch.MS.Internal.DoNotApplyLayoutRoundingToMarginsAndBorderThickness";

    private static readonly bool DoNotApplyLayoutRoundingToBorderThickness =
        IsAppContextSwitchEnabled(DoNotApplyLayoutRoundingToBorderThicknessSwitchName);

    private static readonly Geometry EmptyClipGeometry = CreateEmptyGeometry();

    private readonly ContainerVisual _contentHost;
    private UIElement? _child;

    private Geometry? _borderGeometry;
    private Geometry? _backgroundGeometry;

    private bool         _renderGeometryCacheValid;
    private Size         _cachedRenderSize;
    private Thickness    _cachedRenderBorderThickness;
    private CornerRadius _cachedRenderCornerRadius;

    private bool         _contentClipCacheValid;
    private Rect         _cachedClipRect;
    private Thickness    _cachedClipBorderThickness;
    private CornerRadius _cachedClipCornerRadius;
    private Geometry?    _cachedContentClipGeometry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClippingBorder"/> class.
    /// </summary>
    public ClippingBorder()
    {
        _contentHost = new ContainerVisual();

        // ContainerVisual supplies an independent Clip without inserting another FrameworkElement into the
        // logical tree. Keeping the host purely visual is important: inherited dependency properties and
        // FrameworkElement.Parent must observe ClippingBorder as the content's immediate logical parent, just as
        // they do for a native Border. Force-inherited input properties still cross the non-input visual because
        // WPF walks to the nearest containing UIElement.
        AddVisualChild(_contentHost);
    }

    /// <summary>Gets or sets the element displayed inside the border.</summary>
    /// <remarks>
    /// A private visual-only host carries the rounded clip so that the content's own
    /// <see cref="UIElement.Clip"/> value is preserved. The host does not participate in the logical tree;
    /// consequently, this <see cref="ClippingBorder"/> remains the content's direct logical parent and inherited
    /// dependency properties are not exposed to an unnecessary intermediate framework element.
    /// </remarks>
    [DefaultValue(null)]
    public override UIElement? Child
    {
        get => _child;
        set
        {
            if (ReferenceEquals(_child, value))
            {
                return;
            }

            if (_child is not null)
            {
                _contentHost.Children.Remove(_child);
                RemoveLogicalChild(_child);
            }

            _child = value;

            if (_child is not null)
            {
                // Match Decorator.Child's ordering: establish the logical relationship before attaching the
                // visual. This gives bindings and inherited values the expected parent as soon as the child enters
                // the rendered tree.
                AddLogicalChild(_child);
                _contentHost.Children.Add(_child);
            }

            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Gets an enumerator over the public content rather than the private visual clip host.
    /// </summary>
    /// <remarks>
    /// The visual-only host is deliberately hidden from logical-tree consumers so resource lookup, inherited
    /// properties, element bindings, and <see cref="FrameworkElement.Parent"/> retain native decorator semantics.
    /// </remarks>
    protected override IEnumerator LogicalChildren =>
        _child is null
            ? Array.Empty<object>().GetEnumerator()
            : new object[] { _child }.GetEnumerator();

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index)
    {
        if (index != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _contentHost;
    }

    /// <summary>Identifies the <see cref="BorderThickness"/> dependency property.</summary>
    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(ClippingBorder),
            new FrameworkPropertyMetadata(
                default(Thickness),
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsArrange |
                FrameworkPropertyMetadataOptions.AffectsRender
            ),
            ValidateThickness
        );

    /// <summary>
    /// Gets or sets the thickness reserved for the border on each side of the control.
    /// </summary>
    /// <remarks>
    /// The default is an empty <see cref="Thickness"/>. The value participates in measure and arrange, and the
    /// rendered content clip follows the rounded inner edge produced by this thickness.
    /// </remarks>
    public Thickness BorderThickness
    {
        get => (Thickness)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>Identifies the <see cref="Padding"/> dependency property.</summary>
    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(ClippingBorder),
            new FrameworkPropertyMetadata(
                default(Thickness),
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsArrange
            ),
            ValidateThickness
        );

    /// <summary>Gets or sets the distance between the inner border edge and the child.</summary>
    /// <remarks>
    /// The default is an empty <see cref="Thickness"/>. Padding affects layout but does not shrink the clipping
    /// outline: overflowing content may occupy the padding area, but it cannot cross the inner border edge.
    /// </remarks>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsPaddingCompressible"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsPaddingCompressibleProperty =
        UiBorder.IsPaddingCompressibleProperty.AddOwner(
            typeof(ClippingBorder),
            new FrameworkPropertyMetadata(
                false, FrameworkPropertyMetadataOptions.AffectsMeasure)
        );

    /// <summary>
    /// Gets or sets whether content takes priority over padding when layout space is limited.
    /// </summary>
    /// <remarks>
    /// The default is false. Each pair of padding edges shrinks proportionally toward zero
    /// and recovers when space returns, without changing Padding or the rounded clipping outline.
    /// Content is measured before padding is reserved, which can delay text wrapping.
    /// Compression uses the constraints supplied by WPF; ancestor clipping and explicit minimum
    /// sizes may prevent the decorator from receiving the smaller visible size.
    /// </remarks>
    public bool IsPaddingCompressible
    {
        get => (bool)GetValue(IsPaddingCompressibleProperty);
        set => SetValue(IsPaddingCompressibleProperty, value);
    }

    /// <summary>Identifies the <see cref="CornerRadius"/> dependency property.</summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(ClippingBorder),
            new FrameworkPropertyMetadata(
                default(CornerRadius),
                FrameworkPropertyMetadataOptions.AffectsArrange |
                FrameworkPropertyMetadataOptions.AffectsRender
            ),
            ValidateCornerRadius
        );

    /// <summary>Gets or sets the radius applied to each outer border corner.</summary>
    /// <remarks>
    /// The default is an empty <see cref="CornerRadius"/>. WPF-style half-thickness adjustments derive the inner
    /// corner radii used by both the background and content clip.
    /// </remarks>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Identifies the <see cref="BorderBrush"/> dependency property.</summary>
    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Brush),
            typeof(ClippingBorder),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.SubPropertiesDoNotAffectRender
            )
        );

    /// <summary>Gets or sets the brush used to fill the border region.</summary>
    public Brush? BorderBrush
    {
        get => (Brush?)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>Identifies the <see cref="Background"/> dependency property.</summary>
    public static readonly DependencyProperty BackgroundProperty =
        Panel.BackgroundProperty.AddOwner(
            typeof(ClippingBorder),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.SubPropertiesDoNotAffectRender
            )
        );

    /// <summary>Gets or sets the brush used to fill the area inside the border.</summary>
    public Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Identifies the <see cref="ClipContent"/> dependency property.</summary>
    public static readonly DependencyProperty ClipContentProperty =
        DependencyProperty.Register(
            nameof(ClipContent),
            typeof(bool),
            typeof(ClippingBorder),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsArrange
            )
        );

    /// <summary>
    /// Gets or sets a value indicating whether content is clipped to the border's inner outline.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true"/>. Set this property to <see langword="false"/> when descendants are
    /// intentionally allowed to render over the padding and border areas.
    /// </remarks>
    public bool ClipContent
    {
        get => (bool)GetValue(ClipContentProperty);
        set => SetValue(ClipContentProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size constraint)
    {
        if (IsPaddingCompressible)
        {
            var layoutBorder = GetLayoutBorderThickness();
            return _paddingLayout.Measure(_child, constraint, layoutBorder, Padding);
        }

        var border      = GetLayoutBorderThickness();
        var borderSize  = CollapseThickness(border);
        var paddingSize = CollapseThickness(Padding);

        var decorationSize = new Size(
            borderSize.Width + paddingSize.Width,
            borderSize.Height + paddingSize.Height
        );

        var childConstraint = new Size(
            Math.Max(0.0, constraint.Width - decorationSize.Width),
            Math.Max(0.0, constraint.Height - decorationSize.Height)
        );

        _child?.Measure(childConstraint);

        return new Size(
            (_child?.DesiredSize.Width ?? 0.0) + decorationSize.Width,
            (_child?.DesiredSize.Height ?? 0.0) + decorationSize.Height
        );
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var border       = GetLayoutBorderThickness();
        var cornerRadius = CornerRadius;
        var outerRect    = new Rect(finalSize);
        var innerRect    = DeflateRect(outerRect, border);

        var padding = IsPaddingCompressible
            ? _paddingLayout.GetPadding(innerRect.Size, Padding)
            : Padding;

        var contentRect = DeflateRect(innerRect, padding);

        // ContainerVisual has no layout participation and shares this control's coordinate system. Arrange the
        // public child in the actual content rectangle so its own layout rounding, margin, and alignment logic see
        // the same layout slot they would receive from a native Border.
        _child?.Arrange(contentRect);

        if (!_renderGeometryCacheValid ||
            !_cachedRenderSize.Equals(finalSize) ||
            !_cachedRenderBorderThickness.Equals(border) ||
            !_cachedRenderCornerRadius.Equals(cornerRadius))
        {
            BuildRenderGeometries(outerRect, innerRect, border, cornerRadius);

            _cachedRenderSize            = finalSize;
            _cachedRenderBorderThickness = border;
            _cachedRenderCornerRadius    = cornerRadius;
            _renderGeometryCacheValid    = true;
        }

        UpdateContentClip(innerRect, border, cornerRadius);

        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var borderThickness = GetLayoutBorderThickness();
        var cornerRadius     = CornerRadius;

        // WPF Border deliberately avoids the two-geometry code path for the overwhelmingly common case of a
        // uniform border, uniform corners, and an origin-independent border brush. A filled border ring and the
        // inner background otherwise rasterize their shared edge independently; their anti-aliased coverage is not
        // guaranteed to be complementary, which can leave a visible hairline along straight edges as well as arcs.
        // Drawing the border with a centered pen follows Border.OnRender and gives MILCore one continuous stroke.
        // Keep the geometry path for non-uniform values and non-solid brushes, where a single pen cannot preserve
        // the requested thickness or brush mapping.
        if (CanUseSimpleRenderPath(borderThickness, cornerRadius, BorderBrush))
        {
            RenderUniformBorder(drawingContext, borderThickness, cornerRadius);
            return;
        }

        if (_borderGeometry is not null && BorderBrush is not null)
        {
            drawingContext.DrawGeometry(BorderBrush, null, _borderGeometry);
        }

        if (_backgroundGeometry is not null && Background is not null)
        {
            drawingContext.DrawGeometry(Background, null, _backgroundGeometry);
        }
    }

    private void RenderUniformBorder(
        DrawingContext drawingContext,
        Thickness borderThickness,
        CornerRadius cornerRadius)
    {
        var borderBrush = BorderBrush;
        var thickness   = borderThickness.Left;
        var rounded     = cornerRadius.TopLeft > 0.0;

        if (thickness > 0.0 && borderBrush is not null)
        {
            var halfThickness = thickness * 0.5;
            var borderRect = new Rect(
                halfThickness,
                halfThickness,
                Math.Max(0.0, RenderSize.Width - thickness),
                Math.Max(0.0, RenderSize.Height - thickness)
            );

            if (borderRect.Width > 0.0 && borderRect.Height > 0.0)
            {
                var pen = new Pen(borderBrush, thickness);

                if (borderBrush.IsFrozen)
                {
                    pen.Freeze();
                }

                if (rounded)
                {
                    drawingContext.DrawRoundedRectangle(
                        null,
                        pen,
                        borderRect,
                        cornerRadius.TopLeft,
                        cornerRadius.TopLeft
                    );
                }
                else
                {
                    drawingContext.DrawRectangle(null, pen, borderRect);
                }
            }
        }

        var background = Background;

        if (background is null)
        {
            return;
        }

        var backgroundRect = DeflateRect(new Rect(RenderSize), borderThickness);

        if (backgroundRect.Width <= 0.0 || backgroundRect.Height <= 0.0)
        {
            return;
        }

        if (rounded)
        {
            var innerRadius = new Radii(cornerRadius, borderThickness, outer: false).TopLeft;
            drawingContext.DrawRoundedRectangle(
                background,
                null,
                backgroundRect,
                innerRadius,
                innerRadius
            );
        }
        else
        {
            drawingContext.DrawRectangle(background, null, backgroundRect);
        }
    }

    private static bool CanUseSimpleRenderPath(
        Thickness borderThickness,
        CornerRadius cornerRadius,
        Brush? borderBrush)
    {
        return IsUniform(borderThickness) &&
               IsUniform(cornerRadius) &&
               (borderBrush is null || borderBrush is SolidColorBrush);
    }

    private void BuildRenderGeometries(
        Rect outerRect,
        Rect innerRect,
        Thickness borderThickness,
        CornerRadius cornerRadius)
    {
        _backgroundGeometry = CreateGeometry(
            innerRect,
            new Radii(cornerRadius, borderThickness, outer: false)
        );

        if (!HasAnyThickness(borderThickness) ||
            outerRect.Width <= 0.0 || outerRect.Height <= 0.0)
        {
            _borderGeometry = null;
            return;
        }

        var borderGeometry = new StreamGeometry()
        {
            FillRule = FillRule.EvenOdd
        };

        using (var context = borderGeometry.Open())
        {
            GenerateGeometry(
                context,
                outerRect,
                new Radii(cornerRadius, borderThickness, outer: true)
            );

            if (innerRect.Width > 0.0 && innerRect.Height > 0.0)
            {
                GenerateGeometry(
                    context,
                    innerRect,
                    new Radii(cornerRadius, borderThickness, outer: false)
                );
            }
        }

        borderGeometry.Freeze();
        _borderGeometry = borderGeometry;
    }

    private void UpdateContentClip(
        Rect innerRect,
        Thickness borderThickness,
        CornerRadius cornerRadius)
    {
        if (!ClipContent)
        {
            if (_contentHost.Clip is not null)
            {
                _contentHost.Clip = null;
            }

            return;
        }

        if (innerRect.Width <= 0.0 || innerRect.Height <= 0.0)
        {
            if (!ReferenceEquals(_contentHost.Clip, EmptyClipGeometry))
            {
                _contentHost.Clip = EmptyClipGeometry;
            }

            return;
        }

        // The visual-only host shares ClippingBorder's coordinate system. Clipping to the inner rectangle permits
        // overflowing content to occupy padding while still stopping it at the inner edge of the border.
        var clipRect = innerRect;

        if (!_contentClipCacheValid ||
            !_cachedClipRect.Equals(clipRect) ||
            !_cachedClipBorderThickness.Equals(borderThickness) ||
            !_cachedClipCornerRadius.Equals(cornerRadius))
        {
            _cachedContentClipGeometry = CreateGeometry(
                clipRect,
                new Radii(cornerRadius, borderThickness, outer: false)
            );

            _cachedClipRect            = clipRect;
            _cachedClipBorderThickness = borderThickness;
            _cachedClipCornerRadius    = cornerRadius;
            _contentClipCacheValid     = true;
        }

        if (!ReferenceEquals(_contentHost.Clip, _cachedContentClipGeometry))
        {
            _contentHost.Clip = _cachedContentClipGeometry;
        }
    }

    private static Geometry CreateEmptyGeometry()
    {
        var geometry = new StreamGeometry();
        geometry.Freeze();
        return geometry;
    }

    private Geometry? CreateGeometry(Rect rect, Radii radii)
    {
        if (rect.Width <= 0.0 || rect.Height <= 0.0)
        {
            return null;
        }

        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            GenerateGeometry(context, rect, radii);
        }

        geometry.Freeze();
        return geometry;
    }

    private Thickness GetLayoutBorderThickness()
    {
        var thickness = BorderThickness;

        // Border uses this framework compatibility switch before rounding its layout thickness. Mirroring that
        // behavior prevents ClippingBorder and a neighboring native Border from disagreeing about their chrome size.
        if (!UseLayoutRounding || DoNotApplyLayoutRoundingToBorderThickness)
        {
            return thickness;
        }

        var dpi = VisualTreeHelper.GetDpi(this);

        return new Thickness(
            RoundLayoutValue(thickness.Left,   dpi.DpiScaleX),
            RoundLayoutValue(thickness.Top,    dpi.DpiScaleY),
            RoundLayoutValue(thickness.Right,  dpi.DpiScaleX),
            RoundLayoutValue(thickness.Bottom, dpi.DpiScaleY)
        );
    }

    private static double RoundLayoutValue(double value, double dpiScale)
    {
        if (dpiScale == 0.0)
        {
            return value;
        }

        var roundedValue = Math.Round(value * dpiScale) / dpiScale;

        // This follows UIElement.RoundLayoutValue's failure path. DPI is supplied by WPF and normally valid, but
        // retaining the original layout value is safer than allowing an invalid value to poison the layout pass.
        return double.IsNaN(roundedValue) || double.IsInfinity(roundedValue)
            ? value
            : roundedValue;
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

    private static bool HasAnyThickness(Thickness thickness)
    {
        return thickness.Left > 0.0 || thickness.Top > 0.0 ||
               thickness.Right > 0.0 || thickness.Bottom > 0.0;
    }

    private static bool IsUniform(Thickness thickness)
    {
        return thickness.Left.Equals(thickness.Top) &&
               thickness.Left.Equals(thickness.Right) &&
               thickness.Left.Equals(thickness.Bottom);
    }

    private static bool IsUniform(CornerRadius cornerRadius)
    {
        return cornerRadius.TopLeft.Equals(cornerRadius.TopRight) &&
               cornerRadius.TopLeft.Equals(cornerRadius.BottomRight) &&
               cornerRadius.TopLeft.Equals(cornerRadius.BottomLeft);
    }

    private static bool ValidateThickness(object? value)
    {
        if (value is not Thickness thickness)
        {
            return false;
        }

        return IsFiniteAndNonNegative(thickness.Left) &&
               IsFiniteAndNonNegative(thickness.Top) &&
               IsFiniteAndNonNegative(thickness.Right) &&
               IsFiniteAndNonNegative(thickness.Bottom);
    }

    private static bool ValidateCornerRadius(object? value)
    {
        if (value is not CornerRadius radius)
        {
            return false;
        }

        return IsFiniteAndNonNegative(radius.TopLeft) &&
               IsFiniteAndNonNegative(radius.TopRight) &&
               IsFiniteAndNonNegative(radius.BottomRight) &&
               IsFiniteAndNonNegative(radius.BottomLeft);
    }

    private static bool IsFiniteAndNonNegative(double value)
    {
        return value >= 0.0 && !double.IsInfinity(value) && !double.IsNaN(value);
    }

    private static bool IsAppContextSwitchEnabled(string switchName)
    {
        return AppContext.TryGetSwitch(switchName, out var isEnabled) && isEnabled;
    }

    private static void GenerateGeometry(StreamGeometryContext context, Rect rect, Radii radii)
    {
        var topLeft     = new Point(radii.LeftTop,                  0.0);
        var topRight    = new Point(rect.Width - radii.RightTop,    0.0);
        var rightTop    = new Point(rect.Width,                     radii.TopRight);
        var rightBottom = new Point(rect.Width,                     rect.Height - radii.BottomRight);
        var bottomRight = new Point(rect.Width - radii.RightBottom, rect.Height);
        var bottomLeft  = new Point(radii.LeftBottom,               rect.Height);
        var leftBottom  = new Point(0.0,                            rect.Height - radii.BottomLeft);
        var leftTop     = new Point(0.0,                            radii.TopLeft);

        // When adjacent radii overlap, proportionally scale their meeting point.
        if (topLeft.X > topRight.X)
        {
            var x = radii.LeftTop / (radii.LeftTop + radii.RightTop) * rect.Width;
            topLeft.X  = x;
            topRight.X = x;
        }

        if (rightTop.Y > rightBottom.Y)
        {
            var y = radii.TopRight / (radii.TopRight + radii.BottomRight) * rect.Height;
            rightTop.Y    = y;
            rightBottom.Y = y;
        }

        if (bottomRight.X < bottomLeft.X)
        {
            var x = radii.LeftBottom / (radii.LeftBottom + radii.RightBottom) * rect.Width;
            bottomRight.X = x;
            bottomLeft.X  = x;
        }

        if (leftBottom.Y < leftTop.Y)
        {
            var y = radii.TopLeft / (radii.TopLeft + radii.BottomLeft) * rect.Height;
            leftBottom.Y = y;
            leftTop.Y    = y;
        }

        var offset = (Vector)rect.TopLeft;
        topLeft     += offset;
        topRight    += offset;
        rightTop    += offset;
        rightBottom += offset;
        bottomRight += offset;
        bottomLeft  += offset;
        leftBottom  += offset;
        leftTop     += offset;

        context.BeginFigure(topLeft, isFilled: true, isClosed: true);
        context.LineTo(topRight, isStroked: true, isSmoothJoin: false);

        AddArc(
            context,
            rightTop,
            rect.TopRight.X - topRight.X,
            rightTop.Y - rect.TopRight.Y
        );

        context.LineTo(rightBottom, isStroked: true, isSmoothJoin: false);

        AddArc(
            context,
            bottomRight,
            rect.BottomRight.X - bottomRight.X,
            rect.BottomRight.Y - rightBottom.Y
        );

        context.LineTo(bottomLeft, isStroked: true, isSmoothJoin: false);

        AddArc(
            context,
            leftBottom,
            bottomLeft.X - rect.BottomLeft.X,
            rect.BottomLeft.Y - leftBottom.Y
        );

        context.LineTo(leftTop, isStroked: true, isSmoothJoin: false);

        AddArc(
            context,
            topLeft,
            topLeft.X - rect.TopLeft.X,
            leftTop.Y - rect.TopLeft.Y
        );
    }

    private static void AddArc(StreamGeometryContext context, Point endPoint, double radiusX, double radiusY)
    {
        if (radiusX <= 0.0 && radiusY <= 0.0)
        {
            return;
        }

        context.ArcTo(
            endPoint,
            new Size(Math.Max(0.0, radiusX), Math.Max(0.0, radiusY)),
            rotationAngle: 0.0,
            isLargeArc: false,
            sweepDirection: SweepDirection.Clockwise,
            isStroked: true,
            isSmoothJoin: false
        );
    }

    private struct Radii
    {
        public Radii(CornerRadius radius, Thickness borderThickness, bool outer)
        {
            this = default;

            var left   = borderThickness.Left * 0.5;
            var top    = borderThickness.Top * 0.5;
            var right  = borderThickness.Right * 0.5;
            var bottom = borderThickness.Bottom * 0.5;

            if (outer)
            {
                SetOuterRadii(radius, left, top, right, bottom);
            }
            else
            {
                LeftTop     = Math.Max(0.0, radius.TopLeft - left);
                TopLeft     = Math.Max(0.0, radius.TopLeft - top);
                TopRight    = Math.Max(0.0, radius.TopRight - top);
                RightTop    = Math.Max(0.0, radius.TopRight - right);
                RightBottom = Math.Max(0.0, radius.BottomRight - right);
                BottomRight = Math.Max(0.0, radius.BottomRight - bottom);
                BottomLeft  = Math.Max(0.0, radius.BottomLeft - bottom);
                LeftBottom  = Math.Max(0.0, radius.BottomLeft - left);
            }
        }

        public double LeftTop;
        public double TopLeft;
        public double TopRight;
        public double RightTop;
        public double RightBottom;
        public double BottomRight;
        public double BottomLeft;
        public double LeftBottom;

        private void SetOuterRadii(CornerRadius radius, double left, double top, double right, double bottom)
        {
            LeftTop = TopLeft = radius.TopLeft == 0.0
                ? 0.0
                : radius.TopLeft;

            TopRight = RightTop = radius.TopRight == 0.0
                ? 0.0
                : radius.TopRight;

            RightBottom = BottomRight = radius.BottomRight == 0.0
                ? 0.0
                : radius.BottomRight;

            BottomLeft = LeftBottom = radius.BottomLeft == 0.0
                ? 0.0
                : radius.BottomLeft;

            if (radius.TopLeft != 0.0)
            {
                LeftTop += left;
                TopLeft += top;
            }

            if (radius.TopRight != 0.0)
            {
                TopRight += top;
                RightTop += right;
            }

            if (radius.BottomRight != 0.0)
            {
                RightBottom += right;
                BottomRight += bottom;
            }

            if (radius.BottomLeft != 0.0)
            {
                BottomLeft += bottom;
                LeftBottom += left;
            }
        }
    }
}
