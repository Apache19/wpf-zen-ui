// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using Wpf.Ui.Controls;

namespace Wpf.Ui.Converters;

/// <summary>
/// Creates an independent <see cref="IconElement"/> from a source icon so the same logical
/// icon definition can appear in multiple visual-tree locations simultaneously.
/// </summary>
/// <remarks>
/// <para>
/// A WPF <see cref="IconElement"/> is a <see cref="FrameworkElement"/> — it can have only one
/// visual parent. Binding the same instance to two different icon slots therefore breaks one
/// of them. This converter inspects the source and creates a separate element:
/// </para>
/// <list type="bullet">
/// <item><see cref="IconSourceElement"/> → calls <see cref="IconSourceElement.CreateIconElement"/></item>
/// <item><see cref="SymbolIcon"/> → copies <see cref="SymbolIcon.Symbol"/> and <see cref="SymbolIcon.Filled"/></item>
/// <item><see cref="FontIcon"/> → copies <see cref="FontIcon.Glyph"/> and <see cref="FontIcon.FontFamily"/></item>
/// <item><see cref="ImageIcon"/> → copies <see cref="ImageIcon.Source"/></item>
/// <item>Any other value, including <see langword="null"/>, is returned as-is</item>
/// </list>
/// </remarks>
public class IconElementCloneConverter : IValueConverter
{
    /// <summary>
    /// Converts a source icon value to an independent <see cref="IconElement"/> suitable for a
    /// different visual parent.
    /// </summary>
    /// <param name="value">The source icon to clone.</param>
    /// <param name="targetType">The type of the binding target property.</param>
    /// <param name="parameter">The converter parameter (unused).</param>
    /// <param name="culture">The culture to use in the converter (unused).</param>
    /// <returns>
    /// A new <see cref="IconElement"/> instance cloned from the source, or the original value
    /// if it cannot be cloned.
    /// </returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            IconSourceElement iconSourceElement => iconSourceElement.CreateIconElement(),

            SymbolIcon symbol => new SymbolIcon
            {
                Symbol   = symbol.Symbol,
                Filled   = symbol.Filled,
            },

            FontIcon font => new FontIcon
            {
                Glyph      = font.Glyph,
                FontFamily = font.FontFamily,
            },

            ImageIcon image => new ImageIcon { Source = image.Source },

            _ => value,
        };
    }

    /// <summary>
    /// Converting back from an icon element to its source definition is not supported.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException(
            $"{nameof(IconElementCloneConverter)} only converts icon sources for presentation."
        );
    }
}
