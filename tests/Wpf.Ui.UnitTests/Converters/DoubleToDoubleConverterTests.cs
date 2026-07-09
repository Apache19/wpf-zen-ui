// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using Wpf.Ui.Converters;

namespace Wpf.Ui.UnitTests.Converters;

public class DoubleToDoubleConverterTests
{
    [Fact]
    public void Convert_DefaultConfiguration_ReturnsSourceValue()
    {
        var converter = new DoubleToDoubleConverter();

        var result = Convert(converter, 12.5d);

        Assert.Equal(12.5d, result);
    }

    [Fact]
    public void Convert_AppliesAllOperationsInOrder()
    {
        var converter = new DoubleToDoubleConverter
        {
            Add = 5d,
            Subtract = 2d,
            Multiply = 3d,
            Divide = 2d,
        };

        var result = Convert(converter, 7d);

        Assert.Equal(15d, result);
    }

    [Fact]
    public void Convert_NonDoubleValue_ReturnsUnsetValue()
    {
        var converter = new DoubleToDoubleConverter();

        var result = converter.Convert(1, typeof(double), null, CultureInfo.InvariantCulture);

        Assert.Same(DependencyProperty.UnsetValue, result);
    }

    [Fact]
    public void Convert_DivisionByZero_UsesDoubleArithmetic()
    {
        var converter = new DoubleToDoubleConverter { Divide = 0d };

        var result = Convert(converter, 1d);

        Assert.Equal(double.PositiveInfinity, result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new DoubleToDoubleConverter();

        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(1d, typeof(double), null, CultureInfo.InvariantCulture)
        );
    }

    private static double Convert(DoubleToDoubleConverter converter, object? value)
    {
        return Assert.IsType<double>(
            converter.Convert(value, typeof(double), null, CultureInfo.InvariantCulture)
        );
    }
}
