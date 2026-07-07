// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Ui.Converters;

/// <summary>
/// Defines a single side of a <see cref="Thickness" />.
/// </summary>
public enum ThicknessSide
{
    /// <summary>No side.</summary>
    None = 0,

    /// <summary>The left side.</summary>
    Left,

    /// <summary>The top side.</summary>
    Top,

    /// <summary>The right side.</summary>
    Right,

    /// <summary>The bottom side.</summary>
    Bottom,
}

/// <summary>
/// Defines a set of <see cref="Thickness" /> sides.
/// </summary>
[Flags]
public enum ThicknessSides
{
    /// <summary>No sides.</summary>
    None = 0,

    /// <summary>The left side.</summary>
    Left = 1,

    /// <summary>The top side.</summary>
    Top = 1 << 1,

    /// <summary>The right side.</summary>
    Right = 1 << 2,

    /// <summary>The bottom side.</summary>
    Bottom = 1 << 3,

    /// <summary>The left and right sides.</summary>
    Horizontal = Left | Right,

    /// <summary>The top and bottom sides.</summary>
    Vertical = Top | Bottom,

    /// <summary>All sides.</summary>
    All = Left | Top | Right | Bottom,
}

/// <summary>
/// Converts a source <see cref="Thickness" /> into another <see cref="Thickness" /> by copying,
/// mapping, scaling, offsetting, and constraining individual sides.
/// </summary>
/// <remarks>
/// The converter starts with <see cref="BaseThickness" />, copies the same-named source sides
/// selected by <see cref="CopySides" />, applies source-to-target side mappings, multiplies the
/// result by <see cref="Multiplier" />, adds <see cref="Offset" />, and finally clamps the result
/// between <see cref="Minimum" /> and <see cref="Maximum" />.
/// </remarks>
[ValueConversion(typeof(Thickness), typeof(Thickness))]
[MarkupExtensionReturnType(typeof(ThicknessTransformConverter))]
public sealed class ThicknessTransformConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Gets or sets the initial target value. The default is <c>0,0,0,0</c>.
    /// </summary>
    public Thickness BaseThickness { get; set; } = new(0);

    /// <summary>
    /// Gets or sets the source sides copied to the same target sides. The default is <see cref="ThicknessSides.All" />.
    /// </summary>
    public ThicknessSides CopySides { get; set; } = ThicknessSides.All;

    /// <summary>
    /// Gets or sets the target side that receives the source left value. The default is <see cref="ThicknessSide.None" />.
    /// </summary>
    public ThicknessSide MapLeftTo { get; set; } = ThicknessSide.None;

    /// <summary>
    /// Gets or sets the target side that receives the source top value. The default is <see cref="ThicknessSide.None" />.
    /// </summary>
    public ThicknessSide MapTopTo { get; set; } = ThicknessSide.None;

    /// <summary>
    /// Gets or sets the target side that receives the source right value. The default is <see cref="ThicknessSide.None" />.
    /// </summary>
    public ThicknessSide MapRightTo { get; set; } = ThicknessSide.None;

    /// <summary>
    /// Gets or sets the target side that receives the source bottom value. The default is <see cref="ThicknessSide.None" />.
    /// </summary>
    public ThicknessSide MapBottomTo { get; set; } = ThicknessSide.None;

    /// <summary>
    /// Gets or sets the offset added after copying, mapping, and scaling. The default is <c>0,0,0,0</c>.
    /// </summary>
    public Thickness Offset { get; set; } = new(0);

    /// <summary>
    /// Gets or sets the multiplier applied before <see cref="Offset" />. The default is <c>1</c>.
    /// </summary>
    public double Multiplier { get; set; } = 1d;

    /// <summary>
    /// Gets or sets the minimum value allowed for each target side.
    /// </summary>
    public Thickness Minimum { get; set; } = new(double.NegativeInfinity);

    /// <summary>
    /// Gets or sets the maximum value allowed for each target side.
    /// </summary>
    public Thickness Maximum { get; set; } = new(double.PositiveInfinity);

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = BaseThickness;

        if (value is Thickness source)
        {
            var copySides = GetCopySides(parameter);
            result = Copy(source, result, copySides);
            result = Map(source, result);
        }

        result = Multiply(result, Multiplier);
        result = Add(result, Offset);

        return Clamp(result, Minimum, Maximum);
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }

    private ThicknessSides GetCopySides(object? parameter)
    {
        if (parameter is ThicknessSides sides)
        {
            return sides;
        }

        if (parameter is ThicknessSide side)
        {
            return ToSides(side);
        }

        if (parameter is string text && Enum.TryParse(text, true, out ThicknessSides parsedSides))
        {
            return parsedSides;
        }

        return CopySides;
    }

    private static Thickness Copy(Thickness source, Thickness target, ThicknessSides sides)
    {
        return new Thickness(
            sides.HasFlag(ThicknessSides.Left) ? source.Left : target.Left,
            sides.HasFlag(ThicknessSides.Top) ? source.Top : target.Top,
            sides.HasFlag(ThicknessSides.Right) ? source.Right : target.Right,
            sides.HasFlag(ThicknessSides.Bottom) ? source.Bottom : target.Bottom
        );
    }

    private Thickness Map(Thickness source, Thickness target)
    {
        var left   = target.Left;
        var top    = target.Top;
        var right  = target.Right;
        var bottom = target.Bottom;

        SetValue(MapLeftTo,   source.Left,   ref left, ref top, ref right, ref bottom);
        SetValue(MapTopTo,    source.Top,    ref left, ref top, ref right, ref bottom);
        SetValue(MapRightTo,  source.Right,  ref left, ref top, ref right, ref bottom);
        SetValue(MapBottomTo, source.Bottom, ref left, ref top, ref right, ref bottom);

        return new Thickness(left, top, right, bottom);
    }

    private static Thickness Add(Thickness thickness, Thickness offset)
    {
        return new Thickness(
            thickness.Left + offset.Left,
            thickness.Top + offset.Top,
            thickness.Right + offset.Right,
            thickness.Bottom + offset.Bottom
        );
    }

    private static Thickness Multiply(Thickness thickness, double multiplier)
    {
        return new Thickness(
            thickness.Left * multiplier,
            thickness.Top * multiplier,
            thickness.Right * multiplier,
            thickness.Bottom * multiplier
        );
    }

    private static Thickness Clamp(Thickness thickness, Thickness minimum, Thickness maximum)
    {
        return new Thickness(
            Clamp(thickness.Left,   minimum.Left,   maximum.Left),
            Clamp(thickness.Top,    minimum.Top,    maximum.Top),
            Clamp(thickness.Right,  minimum.Right,  maximum.Right),
            Clamp(thickness.Bottom, minimum.Bottom, maximum.Bottom)
        );
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        if (minimum > maximum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        return value < minimum
            ? minimum
            : value > maximum
                ? maximum
                : value;
    }

    private static void SetValue(
        ThicknessSide side,
        double value,
        ref double left,
        ref double top,
        ref double right,
        ref double bottom
    )
    {
        switch (side)
        {
            case ThicknessSide.Left:
                left = value;
                break;

            case ThicknessSide.Top:
                top = value;
                break;

            case ThicknessSide.Right:
                right = value;
                break;

            case ThicknessSide.Bottom:
                bottom = value;
                break;
        }
    }

    private static ThicknessSides ToSides(ThicknessSide side)
    {
        return side switch
        {
            ThicknessSide.Left   => ThicknessSides.Left,
            ThicknessSide.Top    => ThicknessSides.Top,
            ThicknessSide.Right  => ThicknessSides.Right,
            ThicknessSide.Bottom => ThicknessSides.Bottom,
            _                    => ThicknessSides.None,
        };
    }
}