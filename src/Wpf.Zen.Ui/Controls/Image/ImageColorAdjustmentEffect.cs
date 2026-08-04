// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

#pragma warning disable SA1407 // Standard color-matrix formulas are clearer with conventional arithmetic precedence.

/// <summary>
/// Applies the affine RGB transform calculated by <see cref="ColorTransform"/> in one shader pass.
/// </summary>
/// <remarks>
/// The effect is internal because it is an implementation detail of <see cref="Image"/> rather than
/// a general-purpose effect API. Its shader handles premultiplied alpha by multiplying each affine
/// offset by source alpha; transparent edge pixels therefore remain transparent and do not develop
/// bright fringes after clipping.
/// </remarks>
internal sealed class ImageColorAdjustmentEffect : ShaderEffect
{
    private static readonly PixelShader SharedPixelShader = CreatePixelShader();

    private static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty(
        "Input",
        typeof(ImageColorAdjustmentEffect),
        0
    );

    private static readonly DependencyProperty RedTransformProperty = DependencyProperty.Register(
        "RedTransform",
        typeof(Point4D),
        typeof(ImageColorAdjustmentEffect),
        new UIPropertyMetadata(
            new Point4D(1D, 0D, 0D, 0D),
            PixelShaderConstantCallback(0)
        )
    );

    private static readonly DependencyProperty GreenTransformProperty =
        DependencyProperty.Register(
            "GreenTransform",
            typeof(Point4D),
            typeof(ImageColorAdjustmentEffect),
            new UIPropertyMetadata(
                new Point4D(0D, 1D, 0D, 0D),
                PixelShaderConstantCallback(1)
            )
        );

    private static readonly DependencyProperty BlueTransformProperty = DependencyProperty.Register(
        "BlueTransform",
        typeof(Point4D),
        typeof(ImageColorAdjustmentEffect),
        new UIPropertyMetadata(
            new Point4D(0D, 0D, 1D, 0D),
            PixelShaderConstantCallback(2)
        )
    );

    internal ImageColorAdjustmentEffect()
    {
        PixelShader = SharedPixelShader;
        UpdateShaderValue(InputProperty);
        UpdateShaderValue(RedTransformProperty);
        UpdateShaderValue(GreenTransformProperty);
        UpdateShaderValue(BlueTransformProperty);
    }

    internal void SetTransform(ColorTransform transform)
    {
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
                "/Wpf.Zen.Ui;component/Controls/Image/ImageColorAdjustment.ps",
                UriKind.Relative
            ),
        };

        // PixelShader is immutable after loading and is safe to share across every adjusted image.
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
