// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;

namespace Wpf.Zen.Ui.Converters;

internal class ProgressThicknessConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        double height = 0;
        double? strokeThickness = null;

        if (values is { Length: >= 1 } && values[0] is double h)
        {
            height = h;
        }

        if (values is { Length: >= 2 } && values[1] is double st)
        {
            strokeThickness = st;
        }

        if (strokeThickness.HasValue && !double.IsNaN(strokeThickness.Value))
        {
            return strokeThickness.Value;
        }

        return height / 8;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
