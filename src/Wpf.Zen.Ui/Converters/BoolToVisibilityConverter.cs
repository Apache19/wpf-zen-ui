// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// A WPF value converter that converts between <see cref="bool"/> and <see cref="Visibility"/>.
/// </summary>
/// <remarks>
/// Conversion rules:
/// - When <see cref="Invert"/> is false (default):
///   true → <see cref="Visibility.Visible"/>
///   false → <see cref="Visibility.Hidden"/> or <see cref="Visibility.Collapsed"/> (depending on <see cref="UseHidden"/>).
///
/// - When <see cref="Invert"/> is true:
///   false → <see cref="Visibility.Visible"/>
///   true → <see cref="Visibility.Hidden"/> or <see cref="Visibility.Collapsed"/>.
///
/// Hidden mode:
/// - If <see cref="UseHidden"/> is true, the converter returns <see cref="Visibility.Hidden"/>.
/// - Otherwise (default), it returns <see cref="Visibility.Collapsed"/>.
///
/// Example usage (XAML):
/// <code>
/// &lt;Window xmlns:conv="clr-namespace:VideoLibrary.Converters"&gt;
///     &lt;Window.Resources&gt;
///         &lt;conv:BoolToVisibilityConverter x:Key="BoolToVisibility" /&gt;
///         &lt;conv:BoolToVisibilityConverter x:Key="BoolToVisibility_Invert" Invert="True" /&gt;
///         &lt;conv:BoolToVisibilityConverter x:Key="BoolToVisibility_Hidden" UseHidden="True" /&gt;
///     &lt;/Window.Resources&gt;
///
///     &lt;TextBlock Text="Example"
///                Visibility="{Binding IsShown, Converter={StaticResource BoolToVisibility}}" /&gt;
///
///     &lt;TextBlock Text="Invert Example"
///                Visibility="{Binding IsShown, Converter={StaticResource BoolToVisibility_Invert}}" /&gt;
///
///     &lt;TextBlock Text="Hidden Example"
///                Visibility="{Binding IsShown, Converter={StaticResource BoolToVisibility_Hidden}}" /&gt;
/// &lt;/Window&gt;
/// </code>
/// </remarks>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Gets or sets a value indicating whether the conversion logic should be inverted.
    /// </summary>
    /// <value>
    /// - false (default): true → Visible, false → Hidden/Collapsed.
    /// - true: false → Visible, true → Hidden/Collapsed.
    /// </value>
    public bool Invert { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the hidden state should use
    /// <see cref="Visibility.Hidden"/> instead of <see cref="Visibility.Collapsed"/>.
    /// </summary>
    /// <value>
    /// - true: Uses <see cref="Visibility.Hidden"/> (element is invisible but still occupies layout space).
    /// - false (default): Uses <see cref="Visibility.Collapsed"/> (element does not occupy layout space).
    /// </value>
    public bool UseHidden { get; set; }

    /// <summary>
    /// Converts a <see cref="bool"/> value to <see cref="Visibility"/>.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            if (Invert)
            {
                b = !b;
            }

            return b ? Visibility.Visible : UseHidden ? Visibility.Hidden : Visibility.Collapsed;
        }

        return DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Converts a <see cref="Visibility"/> value back to <see cref="bool"/>.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility vis)
        {
            var isVisible = vis == Visibility.Visible;
            return Invert ? !isVisible : isVisible;
        }

        return DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Provides the converter instance for XAML markup extension usage.
    /// </summary>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}