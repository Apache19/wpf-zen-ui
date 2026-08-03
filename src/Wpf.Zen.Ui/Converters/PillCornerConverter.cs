// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// A converter that calculates a "pill / circular" corner radius based on the
/// actual width and height of a control: radius = min(width, height) * Factor.
/// - If the target property is <see cref="CornerRadius"/>, a uniform corner radius is returned.
/// - If the target property is <see cref="double"/>, the radius value is returned
///   (for example, for NineSliceShadow.CornerRadius).
/// </summary>
/// <remarks>
/// Usage example (XAML):
/// <code>
/// &lt;Grid xmlns:conv="clr-namespace:VideoLibrary.Converters"&gt;
///   &lt;Grid.Resources&gt;
///     &lt;conv:PillCornerConverter x:Key="PillCorner" Factor="0.5" /&gt;
///   &lt;/Grid.Resources&gt;
///
///   <!-- 1) Target is CornerRadius (for example, Wpf.Zen.Ui Button) -->
///   &lt;ui:Button Content="Pill Button"&gt;
///     &lt;ui:Button.CornerRadius&gt;
///       &lt;MultiBinding Converter="{StaticResource PillCorner}"&gt;
///         &lt;Binding RelativeSource="{RelativeSource Self}" Path="ActualWidth" /&gt;
///         &lt;Binding RelativeSource="{RelativeSource Self}" Path="ActualHeight" /&gt;
///       &lt;/MultiBinding&gt;
///     &lt;/ui:Button.CornerRadius&gt;
///   &lt;/ui:Button&gt;
///
///   <!-- 2) Target is double (for example, NineSliceShadow.CornerRadius) -->
///   &lt;ctrls:NineSliceShadow Slice="6" ShadowOpacity="0.35"&gt;
///     &lt;ctrls:NineSliceShadow.CornerRadius&gt;
///       &lt;MultiBinding Converter="{StaticResource PillCorner}"&gt;
///         &lt;Binding RelativeSource="{RelativeSource Self}" Path="ActualWidth" /&gt;
///         &lt;Binding RelativeSource="{RelativeSource Self}" Path="ActualHeight" /&gt;
///       &lt;/MultiBinding&gt;
///     &lt;/ctrls:NineSliceShadow.CornerRadius&gt;
///   &lt;/ctrls:NineSliceShadow&gt;
/// &lt;/Grid&gt;
/// </code>
/// Notes:
/// - The default <see cref="Factor"/> is 0.5, which represents half of the shorter side.
///   If width &gt; height, the shape becomes a horizontal pill (left and right semicircles).
///   If height &gt; width, the shape becomes a vertical pill (top and bottom semicircles).
///   If width = height, the result is a perfect circle.
/// - <see cref="MinRadius"/> and <see cref="MaxRadius"/> can be used to constrain the
///   minimum and maximum radius values. Setting <see cref="Factor"/> &lt; 0.5 reduces
///   the corner roundness, while values &gt; 0.5 make the corners more rounded.
/// </remarks>
[ValueConversion(typeof(object[]), typeof(object))]
[MarkupExtensionReturnType(typeof(PillCornerConverter))]
public sealed class PillCornerConverter : MarkupExtension, IMultiValueConverter
{
    /// <summary>Gets or sets the radius factor, default is 0.5 (i.e., half of the shorter side).</summary>
    public double Factor { get; set; } = 0.5;

    /// <summary>Gets or sets the minimum radius (optional).</summary>
    public double MinRadius { get; set; } = 0;

    /// <summary>Gets or sets the maximum radius (optional, 0 means no limit).</summary>
    public double MaxRadius { get; set; } = 0;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
        {
            return targetType == typeof(CornerRadius) ? new CornerRadius(0) : 0d;
        }

        var w = ToDouble(values[0]);
        var h = ToDouble(values[1]);

        if (double.IsNaN(w) || double.IsNaN(h) || w <= 0 || h <= 0)
        {
            return targetType == typeof(CornerRadius) ? new CornerRadius(0) : 0d;
        }

        var r = Math.Max(0, Math.Min(w, h)) * Factor;

        if (MaxRadius > 0) r = Math.Min(r, MaxRadius);
        if (MinRadius > 0) r = Math.Max(r, MinRadius);

        if (targetType == typeof(CornerRadius))
        {
            return new CornerRadius(r);
        }

        // NineSliceShadow.CornerRadius is double
        return r;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return []; // Conversion back is not supported
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }

    private static double ToDouble(object v)
    {
        return v is double d ? d : double.TryParse(v?.ToString(), out var x) ? x : double.NaN;
    }
}
