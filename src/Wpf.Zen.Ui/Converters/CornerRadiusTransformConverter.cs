// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Defines a single corner of a <see cref="CornerRadius" />.
/// </summary>
public enum CornerRadiusCorner
{
    /// <summary>No corner.</summary>
    None = 0,

    /// <summary>The top-left corner.</summary>
    TopLeft,

    /// <summary>The top-right corner.</summary>
    TopRight,

    /// <summary>The bottom-right corner.</summary>
    BottomRight,

    /// <summary>The bottom-left corner.</summary>
    BottomLeft,
}

/// <summary>
/// Defines a set of <see cref="CornerRadius" /> corners.
/// </summary>
[Flags]
public enum CornerRadiusCorners
{
    /// <summary>No corners.</summary>
    None = 0,

    /// <summary>The top-left corner.</summary>
    TopLeft = 1,

    /// <summary>The top-right corner.</summary>
    TopRight = 1 << 1,

    /// <summary>The bottom-right corner.</summary>
    BottomRight = 1 << 2,

    /// <summary>The bottom-left corner.</summary>
    BottomLeft = 1 << 3,

    /// <summary>The top-left and top-right corners.</summary>
    Top = TopLeft | TopRight,

    /// <summary>The top-right and bottom-right corners.</summary>
    Right = TopRight | BottomRight,

    /// <summary>The bottom-right and bottom-left corners.</summary>
    Bottom = BottomRight | BottomLeft,

    /// <summary>The top-left and bottom-left corners.</summary>
    Left = TopLeft | BottomLeft,

    /// <summary>All corners.</summary>
    All = TopLeft | TopRight | BottomRight | BottomLeft,
}

/// <summary>
/// Converts a source <see cref="CornerRadius" /> into another <see cref="CornerRadius" /> by copying,
/// mapping, scaling, offsetting, and constraining individual corners.
/// </summary>
/// <remarks>
/// The converter starts with <see cref="BaseCornerRadius" />, copies the same-named source corners
/// selected by <see cref="CopyCorners" />, applies source-to-target corner mappings, multiplies the
/// result by <see cref="Multiplier" />, adds <see cref="Offset" />, and finally clamps the result
/// between <see cref="Minimum" /> and <see cref="Maximum" />.
/// </remarks>
[ValueConversion(typeof(CornerRadius), typeof(CornerRadius))]
[MarkupExtensionReturnType(typeof(CornerRadiusTransformConverter))]
public sealed class CornerRadiusTransformConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Gets or sets the initial target value. The default is <c>0,0,0,0</c>.
    /// </summary>
    public CornerRadius BaseCornerRadius { get; set; } = new(0);

    /// <summary>
    /// Gets or sets the source corners copied to the same target corners. The default is <see cref="CornerRadiusCorners.All" />.
    /// </summary>
    public CornerRadiusCorners CopyCorners { get; set; } = CornerRadiusCorners.All;

    /// <summary>
    /// Gets or sets the target corner that receives the source top-left value. The default is <see cref="CornerRadiusCorner.None" />.
    /// </summary>
    public CornerRadiusCorner MapTopLeftTo { get; set; } = CornerRadiusCorner.None;

    /// <summary>
    /// Gets or sets the target corner that receives the source top-right value. The default is <see cref="CornerRadiusCorner.None" />.
    /// </summary>
    public CornerRadiusCorner MapTopRightTo { get; set; } = CornerRadiusCorner.None;

    /// <summary>
    /// Gets or sets the target corner that receives the source bottom-right value. The default is <see cref="CornerRadiusCorner.None" />.
    /// </summary>
    public CornerRadiusCorner MapBottomRightTo { get; set; } = CornerRadiusCorner.None;

    /// <summary>
    /// Gets or sets the target corner that receives the source bottom-left value. The default is <see cref="CornerRadiusCorner.None" />.
    /// </summary>
    public CornerRadiusCorner MapBottomLeftTo { get; set; } = CornerRadiusCorner.None;

    /// <summary>
    /// Gets or sets the offset added after copying, mapping, and scaling. The default is <c>0,0,0,0</c>.
    /// </summary>
    public CornerRadius Offset { get; set; } = new(0);

    /// <summary>
    /// Gets or sets the multiplier applied before <see cref="Offset" />. The default is <c>1</c>.
    /// </summary>
    public double Multiplier { get; set; } = 1d;

    /// <summary>
    /// Gets or sets the minimum value allowed for each target corner.
    /// </summary>
    public CornerRadius Minimum { get; set; } = new(double.NegativeInfinity);

    /// <summary>
    /// Gets or sets the maximum value allowed for each target corner.
    /// </summary>
    public CornerRadius Maximum { get; set; } = new(double.PositiveInfinity);

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = BaseCornerRadius;

        if (value is CornerRadius source)
        {
            var copyCorners = GetCopyCorners(parameter);
            result = Copy(source, result, copyCorners);
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

    private CornerRadiusCorners GetCopyCorners(object? parameter)
    {
        if (parameter is CornerRadiusCorners corners)
        {
            return corners;
        }

        if (parameter is CornerRadiusCorner corner)
        {
            return ToCorners(corner);
        }

        if (parameter is string text && Enum.TryParse(text, true, out CornerRadiusCorners parsedCorners))
        {
            return parsedCorners;
        }

        return CopyCorners;
    }

    private static CornerRadius Copy(CornerRadius source, CornerRadius target, CornerRadiusCorners corners)
    {
        return new CornerRadius(
            corners.HasFlag(CornerRadiusCorners.TopLeft) ? source.TopLeft : target.TopLeft,
            corners.HasFlag(CornerRadiusCorners.TopRight) ? source.TopRight : target.TopRight,
            corners.HasFlag(CornerRadiusCorners.BottomRight) ? source.BottomRight : target.BottomRight,
            corners.HasFlag(CornerRadiusCorners.BottomLeft) ? source.BottomLeft : target.BottomLeft
        );
    }

    private CornerRadius Map(CornerRadius source, CornerRadius target)
    {
        var topLeft     = target.TopLeft;
        var topRight    = target.TopRight;
        var bottomRight = target.BottomRight;
        var bottomLeft  = target.BottomLeft;

        SetValue(MapTopLeftTo,     source.TopLeft,     ref topLeft, ref topRight, ref bottomRight, ref bottomLeft);
        SetValue(MapTopRightTo,    source.TopRight,    ref topLeft, ref topRight, ref bottomRight, ref bottomLeft);
        SetValue(MapBottomRightTo, source.BottomRight, ref topLeft, ref topRight, ref bottomRight, ref bottomLeft);
        SetValue(MapBottomLeftTo,  source.BottomLeft,  ref topLeft, ref topRight, ref bottomRight, ref bottomLeft);

        return new CornerRadius(topLeft, topRight, bottomRight, bottomLeft);
    }

    private static CornerRadius Add(CornerRadius cornerRadius, CornerRadius offset)
    {
        return new CornerRadius(
            cornerRadius.TopLeft + offset.TopLeft,
            cornerRadius.TopRight + offset.TopRight,
            cornerRadius.BottomRight + offset.BottomRight,
            cornerRadius.BottomLeft + offset.BottomLeft
        );
    }

    private static CornerRadius Multiply(CornerRadius cornerRadius, double multiplier)
    {
        return new CornerRadius(
            cornerRadius.TopLeft * multiplier,
            cornerRadius.TopRight * multiplier,
            cornerRadius.BottomRight * multiplier,
            cornerRadius.BottomLeft * multiplier
        );
    }

    private static CornerRadius Clamp(CornerRadius cornerRadius, CornerRadius minimum, CornerRadius maximum)
    {
        return new CornerRadius(
            Clamp(cornerRadius.TopLeft,     minimum.TopLeft,     maximum.TopLeft),
            Clamp(cornerRadius.TopRight,    minimum.TopRight,    maximum.TopRight),
            Clamp(cornerRadius.BottomRight, minimum.BottomRight, maximum.BottomRight),
            Clamp(cornerRadius.BottomLeft,  minimum.BottomLeft,  maximum.BottomLeft)
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
        CornerRadiusCorner corner,
        double value,
        ref double topLeft,
        ref double topRight,
        ref double bottomRight,
        ref double bottomLeft
    )
    {
        switch (corner)
        {
            case CornerRadiusCorner.TopLeft:
                topLeft = value;
                break;

            case CornerRadiusCorner.TopRight:
                topRight = value;
                break;

            case CornerRadiusCorner.BottomRight:
                bottomRight = value;
                break;

            case CornerRadiusCorner.BottomLeft:
                bottomLeft = value;
                break;
        }
    }

    private static CornerRadiusCorners ToCorners(CornerRadiusCorner corner)
    {
        return corner switch
        {
            CornerRadiusCorner.TopLeft     => CornerRadiusCorners.TopLeft,
            CornerRadiusCorner.TopRight    => CornerRadiusCorners.TopRight,
            CornerRadiusCorner.BottomRight => CornerRadiusCorners.BottomRight,
            CornerRadiusCorner.BottomLeft  => CornerRadiusCorners.BottomLeft,
            _                              => CornerRadiusCorners.None,
        };
    }
}
