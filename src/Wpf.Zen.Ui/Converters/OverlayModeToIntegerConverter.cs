// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;
using Wpf.Zen.Ui.Controls;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Converts a <see cref="ScrollViewerOverlayMode"/> value to a configurable <see cref="int"/>.
/// </summary>
/// <remarks>
/// <para>
/// The converter uses two configurable <see cref="int"/> properties to define
/// the values corresponding to each mode.
/// </para>
/// <para>
/// The default mapping is:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="ScrollViewerOverlayMode.Overlay"/> → <see cref="OverlayValue"/> (default: <c>2</c>)
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="ScrollViewerOverlayMode.Traditional"/> → <see cref="TraditionalValue"/> (default: <c>1</c>)
/// </description>
/// </item>
/// </list>
/// <para>Usage examples (XAML):</para>
/// <code>
/// &lt;!-- Default: Overlay→2, Traditional→1 --&gt;
/// &lt;conv:OverlayModeToIntegerConverter x:Key="OverlayModeToSpan" /&gt;
///
/// &lt;!-- Custom mapping: Overlay→0, Traditional→4 --&gt;
/// &lt;conv:OverlayModeToIntegerConverter x:Key="OverlayModeToMargin"
///     OverlayValue="0" TraditionalValue="4" /&gt;
/// </code>
/// </remarks>
[ValueConversion(typeof(ScrollViewerOverlayMode), typeof(int))]
[MarkupExtensionReturnType(typeof(OverlayModeToIntegerConverter))]
public sealed class OverlayModeToIntegerConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Gets or sets the value returned when the mode is <see cref="ScrollViewerOverlayMode.Overlay"/>.
    /// The default value is <c>2</c>.
    /// </summary>
    public int OverlayValue { get; set; } = 2;

    /// <summary>
    /// Gets or sets the value returned when the mode is <see cref="ScrollViewerOverlayMode.Traditional"/>.
    /// The default value is <c>1</c>.
    /// </summary>
    public int TraditionalValue { get; set; } = 1;

    /// <summary>
    /// Converts a <see cref="ScrollViewerOverlayMode"/> value to an <see cref="int"/>.
    /// Returns <see cref="OverlayValue"/> if the value is not a recognized mode.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScrollViewerOverlayMode mode)
        {
            return mode == ScrollViewerOverlayMode.Traditional ? TraditionalValue : OverlayValue;
        }

        return OverlayValue;
    }

    /// <summary>
    /// Converts an <see cref="int"/> value back to a <see cref="ScrollViewerOverlayMode"/> value.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="ScrollViewerOverlayMode.Overlay"/> when the value equals <see cref="OverlayValue"/>;
    /// otherwise, returns <see cref="ScrollViewerOverlayMode.Traditional"/>.
    /// </remarks>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            return i == OverlayValue
                ? ScrollViewerOverlayMode.Overlay
                : ScrollViewerOverlayMode.Traditional;
        }

        return ScrollViewerOverlayMode.Overlay;
    }

    /// <summary>
    /// Provides the converter instance to the XAML markup extension system.
    /// </summary>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}
