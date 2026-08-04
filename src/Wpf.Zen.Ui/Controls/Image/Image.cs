// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.IO;
using Wpf.Zen.Ui.Effects;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays an <see cref="ImageSource"/> with optional rounded clipping and GPU-accelerated
/// color adjustment.
/// </summary>
/// <remarks>
/// <para>
/// This type derives directly from the native WPF <see cref="System.Windows.Controls.Image"/>.
/// It therefore preserves the native image loading, DPI, layout, automation, URI, and
/// <see cref="System.Windows.Controls.Image.ImageFailed"/> behavior instead of reproducing those
/// framework responsibilities in a control template.
/// </para>
/// <para>
/// A zero <see cref="CornerRadius"/> and neutral color values use the native rendering path without
/// allocating a template or an effect surface. Rounded clipping uses one frozen, per-instance
/// <see cref="StreamGeometry"/> that is rebuilt only when the render size or corner radii change.
/// Color adjustment is opt-in: its internal visual and single-pass pixel shader are created only
/// while at least one color property is non-neutral.
/// </para>
/// <para>
/// All color properties are dependency properties and support WPF animation. The adjustment order
/// is hue, saturation, brightness, then contrast. The inherited <see cref="UIElement.Effect"/>
/// remains available and is applied after the internal color adjustment.
/// </para>
/// <para>
/// Use <see cref="ColorAdjustmentEffect"/> on a common container instead when every element in a
/// visual subtree needs the same adjustment. WPF then applies one effect to the composited subtree,
/// avoiding a separate effect surface for each <see cref="Image"/>.
/// </para>
/// </remarks>
public class Image : System.Windows.Controls.Image
{
    /// <summary>
    /// Identifies the <see cref="CornerRadius"/> dependency property.
    /// </summary>
    /// <remarks>
    /// The property affects rendering but not measurement. Its validation is shared with
    /// <see cref="System.Windows.Controls.Border.CornerRadius"/>, so every radius must be finite
    /// and non-negative. Oversized adjacent radii are proportionally reduced during geometry
    /// generation rather than changing the effective property value.
    /// </remarks>
    public static readonly DependencyProperty CornerRadiusProperty =
        System.Windows.Controls.Border.CornerRadiusProperty.AddOwner(
            typeof(Image),
            new FrameworkPropertyMetadata(
                new CornerRadius(0D),
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnCornerRadiusChanged
            )
        );

    /// <summary>
    /// Gets or sets the radius of each corner used to clip the rendered image.
    /// </summary>
    /// <value>The radius of each corner. The default is <c>0</c>.</value>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Brightness"/> dependency property.
    /// </summary>
    /// <remarks>
    /// Animated and local values are coerced to the supported range of <c>0</c> through <c>2</c>.
    /// Values remain render-only and never invalidate image measurement.
    /// </remarks>
    public static readonly DependencyProperty BrightnessProperty = DependencyProperty.Register(
        nameof(Brightness),
        typeof(double),
        typeof(Image),
        new FrameworkPropertyMetadata(
            1D,
            FrameworkPropertyMetadataOptions.None,
            OnColorAdjustmentChanged,
            CoerceColorScale
        ),
        IsFiniteDouble
    );

    /// <summary>
    /// Gets or sets the multiplicative brightness of the image.
    /// </summary>
    /// <value>
    /// A value from <c>0</c> through <c>2</c>, where <c>0</c> produces black, <c>1</c> is neutral,
    /// and values above <c>1</c> brighten the image. The default is <c>1</c>.
    /// </value>
    public double Brightness
    {
        get => (double)GetValue(BrightnessProperty);
        set => SetValue(BrightnessProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Contrast"/> dependency property.
    /// </summary>
    /// <remarks>
    /// Animated and local values are coerced to the supported range of <c>0</c> through <c>2</c>.
    /// Contrast is evaluated around the mid-gray value of <c>0.5</c>.
    /// </remarks>
    public static readonly DependencyProperty ContrastProperty = DependencyProperty.Register(
        nameof(Contrast),
        typeof(double),
        typeof(Image),
        new FrameworkPropertyMetadata(
            1D,
            FrameworkPropertyMetadataOptions.None,
            OnColorAdjustmentChanged,
            CoerceColorScale
        ),
        IsFiniteDouble
    );

    /// <summary>
    /// Gets or sets the contrast of the image around mid-gray.
    /// </summary>
    /// <value>
    /// A value from <c>0</c> through <c>2</c>, where <c>0</c> produces mid-gray, <c>1</c> is neutral,
    /// and values above <c>1</c> increase contrast. The default is <c>1</c>.
    /// </value>
    public double Contrast
    {
        get => (double)GetValue(ContrastProperty);
        set => SetValue(ContrastProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Saturation"/> dependency property.
    /// </summary>
    /// <remarks>
    /// Animated and local values are coerced to the supported range of <c>0</c> through <c>2</c>.
    /// Saturation uses Rec. 709 luminance coefficients and does not change alpha.
    /// </remarks>
    public static readonly DependencyProperty SaturationProperty = DependencyProperty.Register(
        nameof(Saturation),
        typeof(double),
        typeof(Image),
        new FrameworkPropertyMetadata(
            1D,
            FrameworkPropertyMetadataOptions.None,
            OnColorAdjustmentChanged,
            CoerceColorScale
        ),
        IsFiniteDouble
    );

    /// <summary>
    /// Gets or sets the color saturation of the image.
    /// </summary>
    /// <value>
    /// A value from <c>0</c> through <c>2</c>, where <c>0</c> produces grayscale, <c>1</c> is neutral,
    /// and values above <c>1</c> increase saturation. The default is <c>1</c>.
    /// </value>
    public double Saturation
    {
        get => (double)GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="HueAngle"/> dependency property.
    /// </summary>
    /// <remarks>
    /// The value must be finite. It is normalized modulo 360 only when the render matrix is
    /// calculated, which permits continuous animations through multiple revolutions without
    /// unexpectedly changing the dependency property's effective value.
    /// </remarks>
    public static readonly DependencyProperty HueAngleProperty = DependencyProperty.Register(
        nameof(HueAngle),
        typeof(double),
        typeof(Image),
        new FrameworkPropertyMetadata(0D, OnColorAdjustmentChanged),
        IsFiniteDouble
    );

    /// <summary>
    /// Gets or sets the clockwise hue rotation in degrees.
    /// </summary>
    /// <value>
    /// Any finite angle. Values repeat every 360 degrees and <c>0</c> is neutral. The default is
    /// <c>0</c>.
    /// </value>
    public double HueAngle
    {
        get => (double)GetValue(HueAngleProperty);
        set => SetValue(HueAngleProperty, value);
    }

    private StreamGeometry? _clipGeometryCache;
    private Size _clipGeometrySize;
    private CornerRadius _clipGeometryCornerRadius;
    private DrawingVisual? _adjustedImageVisual;
    private ColorAdjustmentEffect? _colorAdjustmentEffect;
    private bool _colorAdjustmentUnavailable;

    /// <inheritdoc/>
    protected override int VisualChildrenCount => _adjustedImageVisual is null ? 0 : 1;

    /// <inheritdoc/>
    protected override Visual GetVisualChild(int index)
    {
        if (index != 0 || _adjustedImageVisual is null)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _adjustedImageVisual;
    }

    /// <summary>
    /// Draws the source through the least expensive path that satisfies the active properties.
    /// </summary>
    /// <param name="drawingContext">The drawing context supplied by WPF for this render pass.</param>
    /// <remarks>
    /// WPF's native <see cref="System.Windows.Controls.Image.OnRender"/> is a single
    /// <see cref="DrawingContext.DrawImage(ImageSource, Rect)"/> call. The neutral path deliberately
    /// delegates to that implementation so future framework fixes remain intact. An adjusted image
    /// is drawn into the lazily-created child visual because WPF exposes only one effect slot per
    /// visual; keeping the shader on the child preserves the public <see cref="UIElement.Effect"/>
    /// slot on this control.
    /// </remarks>
    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_adjustedImageVisual is not null)
        {
            DrawAdjustedImage();
            return;
        }

        StreamGeometry? clipGeometry = GetClipGeometry();
        if (clipGeometry is null)
        {
            base.OnRender(drawingContext);
            return;
        }

        drawingContext.PushClip(clipGeometry);
        try
        {
            base.OnRender(drawingContext);
        }
        finally
        {
            drawingContext.Pop();
        }
    }

    private static void OnCornerRadiusChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs
    )
    {
        var image = (Image)dependencyObject;

        // The cache key includes CornerRadius, but clearing the reference here immediately releases
        // a potentially large geometry command stream when the property changes before the next render.
        image._clipGeometryCache = null;
    }

    private static void OnColorAdjustmentChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs
    )
    {
        ((Image)dependencyObject).UpdateColorAdjustment();
    }

    private static bool IsFiniteDouble(object? value) =>
        ColorAdjustmentEffect.IsFiniteDouble(value);

    private static object CoerceColorScale(DependencyObject dependencyObject, object? value) =>
        ColorAdjustmentEffect.CoerceColorScale(dependencyObject, value);

    private void UpdateColorAdjustment()
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        bool requiresAdjustment =
            Brightness != 1D ||
            Contrast   != 1D ||
            Saturation != 1D ||
            NormalizeHueAngle(HueAngle) != 0D;
        // ReSharper restore CompareOfFloatsByEqualityOperator

        if (!requiresAdjustment)
        {
            DisableColorAdjustment();
            return;
        }

        if (_colorAdjustmentUnavailable)
        {
            return;
        }

        if (_colorAdjustmentEffect is null)
        {
            try
            {
                _colorAdjustmentEffect = new ColorAdjustmentEffect();
                _adjustedImageVisual = new DrawingVisual { Effect = _colorAdjustmentEffect };
                AddVisualChild(_adjustedImageVisual);
                InvalidateVisual();
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or IOException
                    or NotSupportedException
                    or TypeInitializationException
                    or UriFormatException
            )
            {
                // A missing or unsupported shader must not make image loading a fatal operation.
                // Keep the native image path and avoid retrying on every animation tick.
                _colorAdjustmentEffect = null;
                _adjustedImageVisual = null;
                _colorAdjustmentUnavailable = true;
                Trace.TraceWarning(
                    "Image color adjustment was disabled because its pixel shader could not be loaded: {0}",
                    exception.Message
                );
                return;
            }
        }

        _colorAdjustmentEffect.SetAdjustments(
            Brightness,
            Contrast,
            Saturation,
            HueAngle
        );
    }

    private void DisableColorAdjustment()
    {
        if (_adjustedImageVisual is null)
        {
            return;
        }

        RemoveVisualChild(_adjustedImageVisual);
        _adjustedImageVisual = null;
        _colorAdjustmentEffect = null;

        // OnRender must run once to replace the now-empty parent drawing content with the native image.
        InvalidateVisual();
    }

    private void DrawAdjustedImage()
    {
        DrawingVisual? adjustedImageVisual = _adjustedImageVisual;
        Debug.Assert(
            adjustedImageVisual is not null,
            "The adjusted render path requires its lazily-created child visual."
        );
        if (adjustedImageVisual is null)
        {
            return;
        }

        // Clip belongs to the child rather than the parent drawing context on the adjusted path. This
        // keeps the image and its shader in the same retained-mode visual and avoids an opacity mask.
        StreamGeometry? clipGeometry = GetClipGeometry();
        if (!ReferenceEquals(adjustedImageVisual.Clip, clipGeometry))
        {
            adjustedImageVisual.Clip = clipGeometry;
        }

        using DrawingContext drawingContext = adjustedImageVisual.RenderOpen();
        if (Source is not null)
        {
            drawingContext.DrawImage(Source, new Rect(new Point(0D, 0D), RenderSize));
        }
    }

    private StreamGeometry? GetClipGeometry()
    {
        CornerRadius cornerRadius = CornerRadius;
        if (
            RenderSize.Width <= 0D
            || RenderSize.Height <= 0D
            || (
                IsZero(cornerRadius.TopLeft)
                && IsZero(cornerRadius.TopRight)
                && IsZero(cornerRadius.BottomRight)
                && IsZero(cornerRadius.BottomLeft)
            )
        )
        {
            return null;
        }

        if (
            _clipGeometryCache is not null
            && _clipGeometrySize == RenderSize
            && _clipGeometryCornerRadius == cornerRadius
        )
        {
            return _clipGeometryCache;
        }

        _clipGeometryCache = CreateRoundedRectangleGeometry(RenderSize, cornerRadius);
        _clipGeometrySize = RenderSize;
        _clipGeometryCornerRadius = cornerRadius;
        return _clipGeometryCache;
    }

    private static StreamGeometry CreateRoundedRectangleGeometry(
        Size renderSize,
        CornerRadius cornerRadius
    )
    {
        double width = renderSize.Width;
        double height = renderSize.Height;

        Point topLeft = new(cornerRadius.TopLeft, 0D);
        Point topRight = new(width - cornerRadius.TopRight, 0D);
        Point rightTop = new(width, cornerRadius.TopRight);
        Point rightBottom = new(width, height - cornerRadius.BottomRight);
        Point bottomRight = new(width - cornerRadius.BottomRight, height);
        Point bottomLeft = new(cornerRadius.BottomLeft, height);
        Point leftBottom = new(0D, height - cornerRadius.BottomLeft);
        Point leftTop = new(0D, cornerRadius.TopLeft);

        // This is the same proportional overlap resolution used by WPF Border.GenerateGeometry.
        // Resolving each edge independently retains the requested ratio between adjacent corners
        // and naturally turns a too-large circular corner into the required elliptical arc.
        if (topLeft.X > topRight.X)
        {
            double coordinate =
                cornerRadius.TopLeft
                / (cornerRadius.TopLeft + cornerRadius.TopRight)
                * width;
            topLeft.X = coordinate;
            topRight.X = coordinate;
        }

        if (rightTop.Y > rightBottom.Y)
        {
            double coordinate =
                cornerRadius.TopRight
                / (cornerRadius.TopRight + cornerRadius.BottomRight)
                * height;
            rightTop.Y = coordinate;
            rightBottom.Y = coordinate;
        }

        if (bottomRight.X < bottomLeft.X)
        {
            double coordinate =
                cornerRadius.BottomLeft
                / (cornerRadius.BottomLeft + cornerRadius.BottomRight)
                * width;
            bottomRight.X = coordinate;
            bottomLeft.X = coordinate;
        }

        if (leftBottom.Y < leftTop.Y)
        {
            double coordinate =
                cornerRadius.TopLeft
                / (cornerRadius.TopLeft + cornerRadius.BottomLeft)
                * height;
            leftBottom.Y = coordinate;
            leftTop.Y = coordinate;
        }

        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(topLeft, true, true);
            context.LineTo(topRight, true, false);
            AddArc(context, rightTop, width - topRight.X, rightTop.Y);
            context.LineTo(rightBottom, true, false);
            AddArc(
                context,
                bottomRight,
                width - bottomRight.X,
                height - rightBottom.Y
            );
            context.LineTo(bottomLeft, true, false);
            AddArc(context, leftBottom, bottomLeft.X, height - leftBottom.Y);
            context.LineTo(leftTop, true, false);
            AddArc(context, topLeft, topLeft.X, leftTop.Y);
        }

        // Frozen Freezables avoid change-notification bookkeeping and can be marshalled directly to
        // the composition channel. The cache is replaced rather than mutated on a later size change.
        geometry.Freeze();
        return geometry;
    }

    private static void AddArc(
        StreamGeometryContext context,
        Point endPoint,
        double radiusX,
        double radiusY
    )
    {
        if (IsZero(radiusX) && IsZero(radiusY))
        {
            return;
        }

        context.ArcTo(
            endPoint,
            new Size(radiusX, radiusY),
            0D,
            false,
            SweepDirection.Clockwise,
            true,
            false
        );
    }

    private static bool IsZero(double value)
    {
        return Math.Abs(value) < 2.2204460492503131E-15;
    }

    private static double NormalizeHueAngle(double angle)
    {
        double normalized = angle % 360D;

        // A negative multiple of 360 produces IEEE 754 negative zero.
        // Canonicalize both signed-zero representations to positive zero.
        return normalized == 0D ? 0D : normalized;
    }
}
