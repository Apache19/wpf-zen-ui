// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Ui.Converters;

/// <summary>
/// Converts a <see cref="double" /> value by applying configurable arithmetic operations.
/// </summary>
/// <remarks>
/// Operations are applied in the following order:
/// addition, subtraction, multiplication, and division.
/// </remarks>
[ValueConversion(typeof(double), typeof(double))]
[MarkupExtensionReturnType(typeof(DoubleToDoubleConverter))]
public sealed class DoubleToDoubleConverter : MarkupExtension, IValueConverter
{
    /// <summary>
    /// Gets or sets the value to add. The default is <c>0</c>.
    /// </summary>
    public double Add { get; set; }

    /// <summary>
    /// Gets or sets the value to subtract. The default is <c>0</c>.
    /// </summary>
    public double Subtract { get; set; }

    /// <summary>
    /// Gets or sets the value to multiply by. The default is <c>1</c>.
    /// </summary>
    public double Multiply { get; set; } = 1d;

    /// <summary>
    /// Gets or sets the value to divide by. The default is <c>1</c>.
    /// </summary>
    public double Divide { get; set; } = 1d;

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double source)
        {
            return DependencyProperty.UnsetValue;
        }

        return ((source + Add - Subtract) * Multiply) / Divide;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}
