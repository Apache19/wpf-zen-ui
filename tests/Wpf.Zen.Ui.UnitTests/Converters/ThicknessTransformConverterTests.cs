// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using Wpf.Zen.Ui.Converters;

namespace Wpf.Zen.Ui.UnitTests.Converters;

public class ThicknessTransformConverterTests
{
    [Fact]
    public void Convert_DefaultConfiguration_ReturnsSourceThickness()
    {
        var converter = new ThicknessTransformConverter();
        var source = new Thickness(1, 2, 3, 4);

        var result = Convert(converter, source);

        Assert.Equal(source, result);
    }

    [Fact]
    public void Convert_UsesBaseThickness_WhenSourceIsNotThickness()
    {
        var converter = new ThicknessTransformConverter { BaseThickness = new Thickness(4, 3, 2, 1) };

        var result = Convert(converter, "not a thickness");

        Assert.Equal(new Thickness(4, 3, 2, 1), result);
    }

    [Fact]
    public void Convert_CopiesSelectedSidesOverBaseThickness()
    {
        var converter = new ThicknessTransformConverter
        {
            BaseThickness = new Thickness(10),
            CopySides = ThicknessSides.Left | ThicknessSides.Bottom,
        };

        var result = Convert(converter, new Thickness(1, 2, 3, 4));

        Assert.Equal(new Thickness(1, 10, 10, 4), result);
    }

    [Fact]
    public void Convert_MapsSourceSidesAfterCopying()
    {
        var converter = new ThicknessTransformConverter
        {
            CopySides = ThicknessSides.None,
            MapLeftTo = ThicknessSide.Right,
            MapRightTo = ThicknessSide.Bottom,
        };

        var result = Convert(converter, new Thickness(1, 2, 3, 4));

        Assert.Equal(new Thickness(0, 0, 1, 3), result);
    }

    [Fact]
    public void Convert_AppliesOffsetAfterCopyingAndMapping()
    {
        var converter = new ThicknessTransformConverter
        {
            CopySides = ThicknessSides.Left,
            MapBottomTo = ThicknessSide.Top,
            Offset = new Thickness(10, 20, 30, 40),
        };

        var result = Convert(converter, new Thickness(1, 2, 3, 4));

        Assert.Equal(new Thickness(11, 24, 30, 40), result);
    }

    [Fact]
    public void Convert_AppliesMultiplierAndClamp()
    {
        var converter = new ThicknessTransformConverter
        {
            Multiplier = 2,
            Offset = new Thickness(-5, 0, 5, 10),
            Minimum = new Thickness(0),
            Maximum = new Thickness(10),
        };

        var result = Convert(converter, new Thickness(1, 3, 6, 8));

        Assert.Equal(new Thickness(0, 6, 10, 10), result);
    }

    [Fact]
    public void Convert_ParameterOverridesCopySides()
    {
        var converter = new ThicknessTransformConverter
        {
            BaseThickness = new Thickness(9),
            CopySides = ThicknessSides.None,
        };

        var result = Convert(converter, new Thickness(1, 2, 3, 4), "Left, Right");

        Assert.Equal(new Thickness(1, 9, 3, 9), result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new ThicknessTransformConverter();

        Assert.Throws<NotSupportedException>(
            () => converter.ConvertBack(new Thickness(0), typeof(Thickness), null, CultureInfo.InvariantCulture)
        );
    }

    private static Thickness Convert(ThicknessTransformConverter converter, object? value, object? parameter = null)
    {
        return Assert.IsType<Thickness>(
            converter.Convert(value, typeof(Thickness), parameter, CultureInfo.InvariantCulture)
        );
    }
}
