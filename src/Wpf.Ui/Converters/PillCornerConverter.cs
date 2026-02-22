// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Ui.Converters;

/// <summary>
/// 根据控件实际宽高计算“胶囊/圆形”圆角的转换器：radius = min(width, height) * Factor。
/// - 目标属性若是 <see cref="CornerRadius"/>，则返回统一圆角；
/// - 目标属性若是 <see cref="double"/>，则返回半径值（适用于 NineSliceShadow.CornerRadius）。
/// </summary>
/// <remarks>
/// 用法示例（XAML）：
/// <code>
/// &lt;Grid xmlns:conv="clr-namespace:VideoLibrary.Converters"&gt;
///   &lt;Grid.Resources&gt;
///     &lt;conv:PillCornerConverter x:Key="PillCorner" Factor="0.5" /&gt;
///   &lt;/Grid.Resources&gt;
///
///   <!-- 1) 目标为 CornerRadius（例如 Wpf.Ui 的 Button） -->
///   &lt;ui:Button Content="胶囊按钮"&gt;
///     &lt;ui:Button.CornerRadius&gt;
///       &lt;MultiBinding Converter="{StaticResource PillCorner}"&gt;
///         &lt;Binding RelativeSource="{RelativeSource Self}" Path="ActualWidth" /&gt;
///         &lt;Binding RelativeSource="{RelativeSource Self}" Path="ActualHeight" /&gt;
///       &lt;/MultiBinding&gt;
///     &lt;/ui:Button.CornerRadius&gt;
///   &lt;/ui:Button&gt;
///
///   <!-- 2) 目标为 double（例如 NineSliceShadow 的 CornerRadius） -->
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
/// 说明：
/// - Factor 默认 0.5，表示半个短边，宽&gt;高 时为左右半圆， 高&gt;宽 时为上下半圆，宽=高 时为正圆。
/// - 可通过 MinRadius/MaxRadius 约束半径的下/上限；设置 Factor&lt;0.5 可减小圆角，&gt;0.5 可更圆。
/// </remarks>
[ValueConversion(typeof(object[]), typeof(object))]
public sealed class PillCornerConverter : MarkupExtension, IMultiValueConverter
{
    /// <summary>半径系数，默认 0.5（即半个短边）。</summary>
    public double Factor { get; set; } = 0.5;

    /// <summary>最小半径（可选）。</summary>
    public double MinRadius { get; set; } = 0;

    /// <summary>最大半径（可选，0 表示不限制）。</summary>
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

        // NineSliceShadow.CornerRadius 是 double
        return r;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return []; // 不支持反向转换
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
