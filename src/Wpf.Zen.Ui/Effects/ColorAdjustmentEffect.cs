// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace Wpf.Zen.Ui.Effects;

#pragma warning disable SA1407 // Standard color-matrix formulas are clearer with conventional arithmetic precedence.

/// <summary>
/// Adjusts the brightness, contrast, saturation, and hue of a WPF visual in one GPU shader pass.
/// </summary>
/// <remarks>
/// <para>
/// Assign the effect to <see cref="UIElement.Effect"/> on either one element or a container. When
/// it is assigned to a container, WPF first composites that element's visual subtree and then runs
/// this effect over the composite surface. A group of related images can therefore share one
/// effect pass instead of allocating an effect surface for every image.
/// </para>
/// <para>
/// The adjustment order is hue, saturation, brightness, then contrast. The shader preserves source
/// alpha and applies affine color offsets using premultiplied-alpha semantics, so transparent and
/// anti-aliased edges do not acquire colored fringes.
/// </para>
/// <para>
/// Like every WPF <see cref="Effect"/>, this type occupies the target element's single
/// <see cref="UIElement.Effect"/> slot. Put this effect on a parent or an intermediate wrapper when
/// the target element must retain a different effect of its own.
/// </para>
/// </remarks>
/// <example>
/// The following example applies one color-adjustment pass to two images by assigning the effect
/// to their common container. Leave <see cref="Input"/> unset so WPF supplies the container's
/// composited visual subtree as the shader input.
/// <code lang="xml">
/// &lt;Window
///     xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
///     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
///     xmlns:effects="clr-namespace:Wpf.Zen.Ui.Effects;assembly=Wpf.Zen.Ui"&gt;
///     &lt;Grid&gt;
///         &lt;Grid.Effect&gt;
///             &lt;effects:ColorAdjustmentEffect
///                 Brightness="0.9"
///                 Contrast="1.1"
///                 Saturation="0.5"
///                 HueAngle="15" /&gt;
///         &lt;/Grid.Effect&gt;
///
///         &lt;StackPanel Orientation="Horizontal"&gt;
///             &lt;Image Source="Images/Card1.png" /&gt;
///             &lt;Image Source="Images/Card2.png" /&gt;
///         &lt;/StackPanel&gt;
///     &lt;/Grid&gt;
/// &lt;/Window&gt;
/// </code>
/// </example>
public sealed class ColorAdjustmentEffect : ShaderEffect
{
    private static readonly PixelShader SharedPixelShader = CreatePixelShader();

    /// <summary>
    /// Identifies the <see cref="Input"/> dependency property.
    /// </summary>
    /// <remarks>
    /// The default value is WPF's implicit input brush, which samples the visual subtree to which
    /// the effect is assigned. The property maps to pixel shader sampler register <c>s0</c>.
    /// </remarks>
    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty(
        nameof(Input),
        typeof(ColorAdjustmentEffect),
        0
    );

    /// <summary>
    /// Gets or sets the brush sampled by the effect.
    /// </summary>
    /// <value>
    /// WPF's implicit input brush by default. When the effect is assigned to a visual, the default
    /// samples that visual and all descendants composited beneath it.
    /// </value>
    public Brush Input
    {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Brightness"/> dependency property.
    /// </summary>
    /// <remarks>
    /// Local and animated values are coerced to <c>0</c> through <c>2</c>. A change updates shader
    /// constants only and does not invalidate layout.
    /// </remarks>
    public static readonly DependencyProperty BrightnessProperty = DependencyProperty.Register(
        nameof(Brightness),
        typeof(double),
        typeof(ColorAdjustmentEffect),
        new UIPropertyMetadata(1D, OnAdjustmentChanged, CoerceColorScale),
        IsFiniteDouble
    );

    /// <summary>
    /// Gets or sets the multiplicative brightness applied to the input.
    /// </summary>
    /// <value>
    /// A value from <c>0</c> through <c>2</c>, where <c>0</c> produces black, <c>1</c> is neutral,
    /// and values above <c>1</c> brighten the input. The default is <c>1</c>.
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
    /// Local and animated values are coerced to <c>0</c> through <c>2</c>. Contrast is evaluated
    /// around the mid-gray value <c>0.5</c> and does not invalidate layout.
    /// </remarks>
    public static readonly DependencyProperty ContrastProperty = DependencyProperty.Register(
        nameof(Contrast),
        typeof(double),
        typeof(ColorAdjustmentEffect),
        new UIPropertyMetadata(1D, OnAdjustmentChanged, CoerceColorScale),
        IsFiniteDouble
    );

    /// <summary>
    /// Gets or sets the contrast applied around mid-gray.
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
    /// Local and animated values are coerced to <c>0</c> through <c>2</c>. The calculation uses
    /// Rec. 709 luminance coefficients, preserves alpha, and does not invalidate layout.
    /// </remarks>
    public static readonly DependencyProperty SaturationProperty = DependencyProperty.Register(
        nameof(Saturation),
        typeof(double),
        typeof(ColorAdjustmentEffect),
        new UIPropertyMetadata(1D, OnAdjustmentChanged, CoerceColorScale),
        IsFiniteDouble
    );

    /// <summary>
    /// Gets or sets the color saturation applied to the input.
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
    /// The value must be finite. It is normalized modulo 360 only when shader constants are
    /// calculated, so an animation can continue through multiple revolutions without changing the
    /// dependency property's effective value.
    /// </remarks>
    public static readonly DependencyProperty HueAngleProperty = DependencyProperty.Register(
        nameof(HueAngle),
        typeof(double),
        typeof(ColorAdjustmentEffect),
        new UIPropertyMetadata(0D, OnAdjustmentChanged),
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

    private static readonly DependencyProperty RedTransformProperty = DependencyProperty.Register(
        "RedTransform",
        typeof(Point4D),
        typeof(ColorAdjustmentEffect),
        new UIPropertyMetadata(
            new Point4D(1D, 0D, 0D, 0D),
            PixelShaderConstantCallback(0)
        )
    );

    private static readonly DependencyProperty GreenTransformProperty =
        DependencyProperty.Register(
            "GreenTransform",
            typeof(Point4D),
            typeof(ColorAdjustmentEffect),
            new UIPropertyMetadata(
                new Point4D(0D, 1D, 0D, 0D),
                PixelShaderConstantCallback(1)
            )
        );

    private static readonly DependencyProperty BlueTransformProperty =
        DependencyProperty.Register(
            "BlueTransform",
            typeof(Point4D),
            typeof(ColorAdjustmentEffect),
            new UIPropertyMetadata(
                new Point4D(0D, 0D, 1D, 0D),
                PixelShaderConstantCallback(2)
            )
        );

    private bool _isSynchronizingAdjustments;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorAdjustmentEffect"/> class with neutral
    /// adjustment values.
    /// </summary>
    public ColorAdjustmentEffect()
    {
        PixelShader = SharedPixelShader;
        UpdateShaderValue(InputProperty);
        UpdateShaderValue(RedTransformProperty);
        UpdateShaderValue(GreenTransformProperty);
        UpdateShaderValue(BlueTransformProperty);
    }

    internal void SetAdjustments(
        double brightness,
        double contrast,
        double saturation,
        double hueAngle
    )
    {
        // Image mirrors these public properties onto its internal effect. Suppressing intermediate
        // matrix updates keeps one Image property change to one matrix calculation even when the
        // effect is being initialized with several non-neutral values.
        _isSynchronizingAdjustments = true;
        try
        {
            SetCurrentValue(BrightnessProperty, brightness);
            SetCurrentValue(ContrastProperty, contrast);
            SetCurrentValue(SaturationProperty, saturation);
            SetCurrentValue(HueAngleProperty, hueAngle);
        }
        finally
        {
            _isSynchronizingAdjustments = false;
        }

        UpdateTransform();
    }

    private static void OnAdjustmentChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs
    )
    {
        var effect = (ColorAdjustmentEffect)dependencyObject;
        if (!effect._isSynchronizingAdjustments)
        {
            effect.UpdateTransform();
        }
    }

    internal static bool IsFiniteDouble(object? value)
    {
        double number = (double)value!;
        return !double.IsNaN(number) && !double.IsInfinity(number);
    }

    internal static object CoerceColorScale(DependencyObject dependencyObject, object? value)
    {
        return Math.Max(0D, Math.Min(2D, (double)value!));
    }

    private void UpdateTransform()
    {
        ColorTransform transform = ColorTransform.Create(
            Brightness,
            Contrast,
            Saturation,
            HueAngle
        );
        SetCurrentValue(
            RedTransformProperty,
            new Point4D(transform.M11, transform.M12, transform.M13, transform.OffsetR)
        );
        SetCurrentValue(
            GreenTransformProperty,
            new Point4D(transform.M21, transform.M22, transform.M23, transform.OffsetG)
        );
        SetCurrentValue(
            BlueTransformProperty,
            new Point4D(transform.M31, transform.M32, transform.M33, transform.OffsetB)
        );
    }

    private static PixelShader CreatePixelShader()
    {
        var shader = new PixelShader
        {
            UriSource = new Uri(
                "/Wpf.Zen.Ui;component/Effects/ColorAdjustment.ps",
                UriKind.Relative
            ),
        };

        // PixelShader is immutable after loading and is safe to share across all effect instances.
        // Freezing also removes per-instance Freezable change tracking on the composition channel.
        shader.Freeze();
        return shader;
    }
}

/// <summary>
/// Represents the three affine rows needed to transform premultiplied RGB values.
/// </summary>
internal readonly struct ColorTransform
{
    private const double LuminanceR = 0.2126D;
    private const double LuminanceG = 0.7152D;
    private const double LuminanceB = 0.0722D;

    private ColorTransform(
        double m11,
        double m12,
        double m13,
        double offsetR,
        double m21,
        double m22,
        double m23,
        double offsetG,
        double m31,
        double m32,
        double m33,
        double offsetB
    )
    {
        M11 = m11;
        M12 = m12;
        M13 = m13;
        OffsetR = offsetR;
        M21 = m21;
        M22 = m22;
        M23 = m23;
        OffsetG = offsetG;
        M31 = m31;
        M32 = m32;
        M33 = m33;
        OffsetB = offsetB;
    }

    internal double M11 { get; }

    internal double M12 { get; }

    internal double M13 { get; }

    internal double OffsetR { get; }

    internal double M21 { get; }

    internal double M22 { get; }

    internal double M23 { get; }

    internal double OffsetG { get; }

    internal double M31 { get; }

    internal double M32 { get; }

    internal double M33 { get; }

    internal double OffsetB { get; }

    internal static ColorTransform Create(
        double brightness,
        double contrast,
        double saturation,
        double hueAngle
    )
    {
        double radians = hueAngle % 360D * Math.PI / 180D;
        double cosine = Math.Cos(radians);
        double sine = Math.Sin(radians);

        // These coefficients are the standard luminance-preserving hue rotation used by SVG/CSS
        // color matrices. Computing trigonometry once per property update is substantially cheaper
        // than evaluating it for every pixel in the shader.
        var hue = new ColorTransform(
            LuminanceR + cosine * (1D - LuminanceR) - sine * LuminanceR,
            LuminanceG - cosine * LuminanceG - sine * LuminanceG,
            LuminanceB - cosine * LuminanceB + sine * (1D - LuminanceB),
            0D,
            LuminanceR - cosine * LuminanceR + sine * 0.143D,
            LuminanceG + cosine * (1D - LuminanceG) + sine * 0.140D,
            LuminanceB - cosine * LuminanceB - sine * 0.283D,
            0D,
            LuminanceR - cosine * LuminanceR - sine * (1D - LuminanceR),
            LuminanceG - cosine * LuminanceG + sine * LuminanceG,
            LuminanceB + cosine * (1D - LuminanceB) + sine * LuminanceB,
            0D
        );

        double inverseSaturation = 1D - saturation;
        var saturationTransform = new ColorTransform(
            LuminanceR * inverseSaturation + saturation,
            LuminanceG * inverseSaturation,
            LuminanceB * inverseSaturation,
            0D,
            LuminanceR * inverseSaturation,
            LuminanceG * inverseSaturation + saturation,
            LuminanceB * inverseSaturation,
            0D,
            LuminanceR * inverseSaturation,
            LuminanceG * inverseSaturation,
            LuminanceB * inverseSaturation + saturation,
            0D
        );

        var brightnessTransform = new ColorTransform(
            brightness,
            0D,
            0D,
            0D,
            0D,
            brightness,
            0D,
            0D,
            0D,
            0D,
            brightness,
            0D
        );

        double contrastOffset = 0.5D * (1D - contrast);
        var contrastTransform = new ColorTransform(
            contrast,
            0D,
            0D,
            contrastOffset,
            0D,
            contrast,
            0D,
            contrastOffset,
            0D,
            0D,
            contrast,
            contrastOffset
        );

        return Multiply(
            contrastTransform,
            Multiply(brightnessTransform, Multiply(saturationTransform, hue))
        );
    }

    private static ColorTransform Multiply(ColorTransform left, ColorTransform right)
    {
        // Composition is left(right(rgb)). Keeping the affine offsets in the fourth component lets
        // the shader apply them as offset * alpha and retain correct premultiplied-alpha semantics.
        return new ColorTransform(
            left.M11 * right.M11 + left.M12 * right.M21 + left.M13 * right.M31,
            left.M11 * right.M12 + left.M12 * right.M22 + left.M13 * right.M32,
            left.M11 * right.M13 + left.M12 * right.M23 + left.M13 * right.M33,
            left.M11 * right.OffsetR
                + left.M12 * right.OffsetG
                + left.M13 * right.OffsetB
                + left.OffsetR,
            left.M21 * right.M11 + left.M22 * right.M21 + left.M23 * right.M31,
            left.M21 * right.M12 + left.M22 * right.M22 + left.M23 * right.M32,
            left.M21 * right.M13 + left.M22 * right.M23 + left.M23 * right.M33,
            left.M21 * right.OffsetR
                + left.M22 * right.OffsetG
                + left.M23 * right.OffsetB
                + left.OffsetG,
            left.M31 * right.M11 + left.M32 * right.M21 + left.M33 * right.M31,
            left.M31 * right.M12 + left.M32 * right.M22 + left.M33 * right.M32,
            left.M31 * right.M13 + left.M32 * right.M23 + left.M33 * right.M33,
            left.M31 * right.OffsetR
                + left.M32 * right.OffsetG
                + left.M33 * right.OffsetB
                + left.OffsetB
        );
    }
}

#pragma warning restore SA1407
