// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Converts a reference value (or <see cref="string"/>) to a <see cref="Visibility"/> value
/// based on whether the input is <c>null</c>.
/// </summary>
/// <remarks>
/// <para>
/// The converter uses two configurable <see cref="Visibility"/> properties to define
/// the values corresponding to null and non-null inputs.
/// </para>
/// <para>
/// The default mapping is:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <c>null</c> → <see cref="NullValue"/> (default: <see cref="Visibility.Collapsed"/>)
/// </description>
/// </item>
/// <item>
/// <description>
/// non-null → <see cref="NotNullValue"/> (default: <see cref="Visibility.Visible"/>)
/// </description>
/// </item>
/// </list>
/// <para>
/// When <see cref="TreatEmptyStringAsNull"/> is <c>true</c>, empty strings
/// (<see cref="string.Empty"/> or <c>""</c>) are also treated as null and
/// mapped to <see cref="NullValue"/>.
/// </para>
/// <para>
/// Any null-to-Visibility mapping can be configured by setting
/// <see cref="NullValue"/> and <see cref="NotNullValue"/>.
/// </para>
/// <para>
/// Reverse conversion is not supported. Because <see cref="Visibility"/> cannot
/// encode the original non-null object, <see cref="ConvertBack"/> always returns
/// <see cref="DependencyProperty.UnsetValue"/>.
/// </para>
/// <para>Usage examples (XAML):</para>
/// <code>
/// &lt;!-- Default: null→Collapsed, non-null→Visible --&gt;
/// &lt;conv:NullToVisibilityConverter x:Key="NullToVisibility" /&gt;
///
/// &lt;!-- Inverted: null→Visible, non-null→Collapsed --&gt;
/// &lt;conv:NullToVisibilityConverter x:Key="NullToVisibilityInvert"
///     NullValue="Visible" NotNullValue="Collapsed" /&gt;
///
/// &lt;!-- Hidden mode: null→Hidden, non-null→Visible (preserves layout space) --&gt;
/// &lt;conv:NullToVisibilityConverter x:Key="NullToHidden"
///     NullValue="Hidden" /&gt;
///
/// &lt;!-- Treat empty strings as null --&gt;
/// &lt;conv:NullToVisibilityConverter x:Key="NullOrEmptyToVisibility"
///     TreatEmptyStringAsNull="True" /&gt;
/// </code>
/// </remarks>
[ValueConversion(typeof(object), typeof(Visibility))]
[MarkupExtensionReturnType(typeof(NullToVisibilityConverter))]
public sealed class NullToVisibilityConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Gets or sets the <see cref="Visibility"/> value returned when the input is <c>null</c>.
    /// The default value is <see cref="Visibility.Collapsed"/>.
    /// </summary>
    /// <remarks>
    /// When <see cref="TreatEmptyStringAsNull"/> is <c>true</c>, this value is also
    /// returned for empty strings.
    /// </remarks>
    public Visibility NullValue { get; set; } = Visibility.Collapsed;

    /// <summary>
    /// Gets or sets the <see cref="Visibility"/> value returned when the input is not <c>null</c>.
    /// The default value is <see cref="Visibility.Visible"/>.
    /// </summary>
    public Visibility NotNullValue { get; set; } = Visibility.Visible;

    /// <summary>
    /// Gets or sets a value indicating whether empty strings should be treated as <c>null</c>.
    /// </summary>
    /// <value>
    /// <c>true</c> to treat <see cref="string.Empty"/> (or <c>""</c>) as null;
    /// <c>false</c> (default) to treat empty strings as non-null values.
    /// </value>
    /// <remarks>
    /// When enabled, the input is considered null if it is either <c>null</c> (no object)
    /// or a <see cref="string"/> with zero length.
    /// </remarks>
    public bool TreatEmptyStringAsNull { get; set; }

    /// <summary>
    /// Converts a reference value to a <see cref="Visibility"/> based on whether it is <c>null</c>.
    /// </summary>
    /// <param name="value">The input value to check for null.</param>
    /// <param name="targetType">The type of the binding target property.</param>
    /// <param name="parameter">An optional converter parameter (not used).</param>
    /// <param name="culture">The culture to use in the converter.</param>
    /// <returns>
    /// <see cref="NullValue"/> if the input is null (or an empty string when
    /// <see cref="TreatEmptyStringAsNull"/> is <c>true</c>); otherwise, <see cref="NotNullValue"/>.
    /// Returns <see cref="DependencyProperty.UnsetValue"/> if the value is not a recognized type.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNull = value is null;

        if (!isNull && TreatEmptyStringAsNull && value is string s)
        {
            isNull = s.Length == 0;
        }

        return isNull ? NullValue : NotNullValue;
    }

    /// <summary>
    /// Reverse conversion is not supported. Always returns <see cref="DependencyProperty.UnsetValue"/>.
    /// </summary>
    /// <returns><see cref="DependencyProperty.UnsetValue"/> in all cases.</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Provides the converter instance to the XAML markup extension system,
    /// enabling direct use without explicit resource key declaration.
    /// </summary>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}
