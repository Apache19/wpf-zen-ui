// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Converts a Boolean value to a <see cref="double"/> value and supports reverse conversion.
/// </summary>
/// <remarks>
/// <para>
/// The converter uses two configurable <see cref="double"/> properties to define
/// the values corresponding to <c>true</c> and <c>false</c>.
/// </para>
/// <para>
/// The default mapping is:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <c>true</c> → <see cref="TrueValue"/> (default: <c>1.0</c>)
/// </description>
/// </item>
/// <item>
/// <description>
/// <c>false</c> → <see cref="FalseValue"/> (default: <c>0.0</c>)
/// </description>
/// </item>
/// </list>
/// <para>
/// Any Boolean-to-double mapping can be configured by setting
/// <see cref="TrueValue"/> and <see cref="FalseValue"/>.
/// </para>
/// <para>Usage examples (XAML):</para>
/// <code>
/// &lt;!-- Default: true→1.0, false→0.0 --&gt;
/// &lt;conv:BoolToDoubleConverter x:Key="BoolToDouble" /&gt;
///
/// &lt;!-- Custom opacity mapping: true→1.0, false→0.4 --&gt;
/// &lt;conv:BoolToDoubleConverter x:Key="BoolToOpacity"
///     TrueValue="1.0" FalseValue="0.4" /&gt;
///
/// &lt;!-- Inverted mapping: true→0.0, false→1.0 --&gt;
/// &lt;conv:BoolToDoubleConverter x:Key="BoolToDoubleInvert"
///     TrueValue="0.0" FalseValue="1.0" /&gt;
/// </code>
/// </remarks>
[ValueConversion(typeof(bool), typeof(double))]
[MarkupExtensionReturnType(typeof(BoolToDoubleConverter))]
public sealed class BoolToDoubleConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Gets or sets the value returned when the Boolean value is <c>true</c>.
    /// The default value is <c>1.0</c>.
    /// </summary>
    public double TrueValue { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the value returned when the Boolean value is <c>false</c>.
    /// The default value is <c>0.0</c>.
    /// </summary>
    public double FalseValue { get; set; }

    /// <summary>
    /// Converts a Boolean value to a <see cref="double"/>.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? TrueValue : FalseValue;
        }

        return DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Converts a <see cref="double"/> value back to a Boolean value.
    /// </summary>
    /// <remarks>
    /// Returns <c>true</c> when the value equals <see cref="TrueValue"/>;
    /// otherwise, returns <c>false</c>.
    /// </remarks>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return d.Equals(TrueValue);
        }

        return DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Provides the converter instance to the XAML markup extension system.
    /// </summary>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}
