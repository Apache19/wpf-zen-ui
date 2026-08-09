// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Multi-value converter that creates a <see cref="Rect"/> from an element's
/// <see cref="FrameworkElement.ActualWidth"/> and <see cref="FrameworkElement.ActualHeight"/>.
/// </summary>
/// <remarks>
/// <para>
/// Intended for use with <see cref="RectangleGeometry.Rect"/> bindings where the
/// geometry must track a parent element's render size — a common scenario for
/// rounded-corner clipping via <see cref="UIElement.Clip"/>.
/// </para>
/// <para>Usage example:</para>
/// <code>
/// &lt;RectangleGeometry ...&gt;
///   &lt;RectangleGeometry.Rect&gt;
///     &lt;MultiBinding&gt;
///       &lt;Binding Path="ActualWidth" RelativeSource="{RelativeSource AncestorType=Border}"/&gt;
///       &lt;Binding Path="ActualHeight" RelativeSource="{RelativeSource AncestorType=Border}"/&gt;
///       &lt;MultiBinding.Converter&gt;
///         &lt;converters:SizeToRectConverter /&gt;
///       &lt;/MultiBinding.Converter&gt;
///     &lt;/MultiBinding&gt;
///   &lt;/RectangleGeometry.Rect&gt;
/// &lt;/RectangleGeometry&gt;
/// </code>
/// </remarks>
[ValueConversion(typeof(object[]), typeof(Rect))]
[MarkupExtensionReturnType(typeof(SizeToRectConverter))]
public sealed class SizeToRectConverter : MarkupExtension, IMultiValueConverter
{
    /// <inheritdoc />
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
        {
            return new Rect(0, 0, 0, 0);
        }

        var w = ToDouble(values[0]);
        var h = ToDouble(values[1]);

        if (double.IsNaN(w) || double.IsNaN(h) || w <= 0 || h <= 0)
        {
            return new Rect(0, 0, 0, 0);
        }

        return new Rect(0, 0, w, h);
    }

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return []; // One-way only
    }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }

    private static double ToDouble(object v)
    {
        return v is double d ? d : double.TryParse(v?.ToString(), out var x) ? x : double.NaN;
    }
}
