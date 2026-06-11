// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;

namespace Wpf.Ui.Converters;

/// <summary>
/// Multi-value converter that creates a clipped <see cref="RectangleGeometry"/> for scroll-reveal animations.
/// </summary>
/// <remarks>
/// Replaced by <see cref="Wpf.Ui.Controls.ClipExtensions"/> which uses attached properties,
/// properly freezes the generated geometry, and removes the need for a multi-binding converter.
/// </remarks>
[Obsolete("Use Wpf.Ui.Controls.ClipExtensions.ClipPercentage attached property instead.", error: false)]
public class ClipConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 3)
        {
            return null;
        }

        if (
            values[0] is not double width
            || values[1] is not double height
            || values[2] is not double percentage
        )
        {
            return null;
        }

        double clippedWidth = width * percentage;

        var geometry = new RectangleGeometry(new Rect(0, 0, clippedWidth, height));
        geometry.Freeze();
        return geometry;
    }

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
