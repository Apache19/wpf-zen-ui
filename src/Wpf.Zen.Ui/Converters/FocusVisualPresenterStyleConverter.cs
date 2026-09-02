// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Selects the presenter style used by the global keyboard-focus visual.
/// </summary>
/// <remarks>
/// WPF creates a new focus adorner whenever keyboard focus moves. The converter therefore resolves
/// <see cref="FocusVisual.PresenterStyleProperty"/> at the same point in the lifecycle where WPF resolves
/// <see cref="SystemParameters.FocusVisualStyleKey"/> itself. It contains no control-type or geometry policy: a
/// control opts into specialized rendering solely by setting <see cref="FocusVisual.PresenterStyleProperty"/>.
/// </remarks>
[MarkupExtensionReturnType(typeof(FocusVisualPresenterStyleConverter))]
public sealed class FocusVisualPresenterStyleConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Returns the adorned element's configured presenter style, or <paramref name="parameter"/> when no custom
    /// style has been configured.
    /// </summary>
    /// <param name="value">The <see cref="DependencyObject"/> being adorned.</param>
    /// <param name="targetType">The binding target type. The converter expects <see cref="Style"/>.</param>
    /// <param name="parameter">The default presenter <see cref="Style"/> supplied by the global focus template.</param>
    /// <param name="culture">The binding culture; style selection is culture-independent.</param>
    /// <returns>
    /// The custom or default presenter style. If no adorned element and no default style are available,
    /// <see cref="DependencyProperty.UnsetValue"/> is returned so WPF can apply normal binding fallback behavior.
    /// </returns>
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DependencyObject adornedElement && FocusVisual.GetPresenterStyle(adornedElement) is { } style)
        {
            return style;
        }

        return parameter is Style fallbackStyle ? fallbackStyle : DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Reverse conversion is unsupported because a focus presenter must never write styling back to the adorned
    /// element.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Returns this stateless converter for direct use as a XAML markup extension.
    /// </summary>
    /// <param name="serviceProvider">The XAML service provider; no services are required.</param>
    /// <returns>The current converter instance.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}
