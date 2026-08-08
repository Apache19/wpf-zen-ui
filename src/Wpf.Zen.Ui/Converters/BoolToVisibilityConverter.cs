// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Converts a Boolean value to a <see cref="Visibility"/> value and supports reverse conversion.
/// </summary>
/// <remarks>
/// <para>
/// The converter uses two configurable <see cref="Visibility"/> properties to define
/// the values corresponding to <c>true</c> and <c>false</c>.
/// </para>
/// <para>
/// The default mapping is:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <c>true</c> → <see cref="TrueValue"/> (default: <see cref="Visibility.Visible"/>)
/// </description>
/// </item>
/// <item>
/// <description>
/// <c>false</c> → <see cref="FalseValue"/> (default: <see cref="Visibility.Collapsed"/>)
/// </description>
/// </item>
/// </list>
/// <para>
/// Any Boolean-to-Visibility mapping can be configured by setting
/// <see cref="TrueValue"/> and <see cref="FalseValue"/>.
/// </para>
/// <para>Usage examples (XAML):</para>
/// <code>
/// &lt;!-- Default: true→Visible, false→Collapsed --&gt;
/// &lt;conv:BoolToVisibilityConverter x:Key="BoolToVisibility" /&gt;
///
/// &lt;!-- Hidden mode: true→Visible, false→Hidden (preserves layout space) --&gt;
/// &lt;conv:BoolToVisibilityConverter x:Key="BoolToVisibilityHidden"
///     FalseValue="Hidden" /&gt;
///
/// &lt;!-- Inverted mapping: true→Collapsed, false→Visible --&gt;
/// &lt;conv:BoolToVisibilityConverter x:Key="BoolToVisibilityInvert"
///     TrueValue="Collapsed" FalseValue="Visible" /&gt;
/// </code>
/// </remarks>
[ValueConversion(typeof(bool), typeof(Visibility))]
[MarkupExtensionReturnType(typeof(BoolToVisibilityConverter))]
public sealed class BoolToVisibilityConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Gets or sets the value returned when the Boolean value is <c>true</c>.
    /// The default value is <see cref="Visibility.Visible"/>.
    /// </summary>
    public Visibility TrueValue { get; set; } = Visibility.Visible;

    /// <summary>
    /// Gets or sets the value returned when the Boolean value is <c>false</c>.
    /// The default value is <see cref="Visibility.Collapsed"/>.
    /// </summary>
    public Visibility FalseValue { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// Converts a Boolean value to a <see cref="Visibility"/>.
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
    /// Converts a <see cref="Visibility"/> value back to a Boolean value.
    /// </summary>
    /// <remarks>
    /// Returns <c>true</c> when the value equals <see cref="TrueValue"/>;
    /// otherwise, returns <c>false</c>.
    /// </remarks>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility vis)
        {
            return vis == TrueValue;
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
