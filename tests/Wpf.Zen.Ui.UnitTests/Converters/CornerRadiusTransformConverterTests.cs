// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using Wpf.Zen.Ui.Converters;

namespace Wpf.Zen.Ui.UnitTests.Converters;

public class CornerRadiusTransformConverterTests
{
    [Fact]
    public void Convert_DefaultConfiguration_ReturnsSourceCornerRadius()
    {
        var converter = new CornerRadiusTransformConverter();
        var source = new CornerRadius(1, 2, 3, 4);

        var result = Convert(converter, source);

        Assert.Equal(source, result);
    }

    [Fact]
    public void Convert_UsesBaseCornerRadius_WhenSourceIsNotCornerRadius()
    {
        var converter = new CornerRadiusTransformConverter { BaseCornerRadius = new CornerRadius(4, 3, 2, 1) };

        var result = Convert(converter, "not a corner radius");

        Assert.Equal(new CornerRadius(4, 3, 2, 1), result);
    }

    [Fact]
    public void Convert_CopiesSelectedCornersOverBaseCornerRadius()
    {
        var converter = new CornerRadiusTransformConverter
        {
            BaseCornerRadius = new CornerRadius(10),
            CopyCorners = CornerRadiusCorners.TopLeft | CornerRadiusCorners.BottomLeft,
        };

        var result = Convert(converter, new CornerRadius(1, 2, 3, 4));

        Assert.Equal(new CornerRadius(1, 10, 10, 4), result);
    }

    [Fact]
    public void Convert_MapsSourceCornersAfterCopying()
    {
        var converter = new CornerRadiusTransformConverter
        {
            CopyCorners = CornerRadiusCorners.None,
            MapTopLeftTo = CornerRadiusCorner.BottomRight,
            MapBottomRightTo = CornerRadiusCorner.BottomLeft,
        };

        var result = Convert(converter, new CornerRadius(1, 2, 3, 4));

        Assert.Equal(new CornerRadius(0, 0, 1, 3), result);
    }

    [Fact]
    public void Convert_AppliesOffsetAfterCopyingAndMapping()
    {
        var converter = new CornerRadiusTransformConverter
        {
            CopyCorners = CornerRadiusCorners.TopLeft,
            MapBottomLeftTo = CornerRadiusCorner.TopRight,
            Offset = new CornerRadius(10, 20, 30, 40),
        };

        var result = Convert(converter, new CornerRadius(1, 2, 3, 4));

        Assert.Equal(new CornerRadius(11, 24, 30, 40), result);
    }

    [Fact]
    public void Convert_PreserveZeroCorners_SkipsOffsetForZeroTargetCorners()
    {
        var converter = new CornerRadiusTransformConverter
        {
            Offset = new CornerRadius(2),
            PreserveZeroCorners = true,
        };

        var result = Convert(converter, new CornerRadius(4, 4, 0, 0));

        Assert.Equal(new CornerRadius(6, 6, 0, 0), result);
    }

    [Fact]
    public void Convert_PreserveZeroCorners_UsesValueAfterMultiplier()
    {
        var converter = new CornerRadiusTransformConverter
        {
            BaseCornerRadius = new CornerRadius(3, 0, 3, 0),
            CopyCorners = CornerRadiusCorners.Top,
            Multiplier = 0,
            Offset = new CornerRadius(2),
            PreserveZeroCorners = true,
        };

        var result = Convert(converter, new CornerRadius(4, 0, 8, 8));

        Assert.Equal(new CornerRadius(0), result);
    }

    [Fact]
    public void Convert_PreserveZeroCorners_AppliesOffsetToNonZeroBaseCorners()
    {
        var converter = new CornerRadiusTransformConverter
        {
            BaseCornerRadius = new CornerRadius(3, 0, 3, 0),
            CopyCorners = CornerRadiusCorners.None,
            Offset = new CornerRadius(2),
            PreserveZeroCorners = true,
        };

        var result = Convert(converter, new CornerRadius(0));

        Assert.Equal(new CornerRadius(5, 0, 5, 0), result);
    }

    [Fact]
    public void Convert_PreserveZeroCorners_PreservesCopiedZeroOverNonZeroBaseCorner()
    {
        var converter = new CornerRadiusTransformConverter
        {
            BaseCornerRadius = new CornerRadius(3),
            CopyCorners = CornerRadiusCorners.TopLeft,
            Offset = new CornerRadius(2),
            PreserveZeroCorners = true,
        };

        var result = Convert(converter, new CornerRadius(0));

        Assert.Equal(new CornerRadius(0, 5, 5, 5), result);
    }

    [Fact]
    public void Convert_AppliesMultiplierAndClamp()
    {
        var converter = new CornerRadiusTransformConverter
        {
            Multiplier = 2,
            Offset = new CornerRadius(-5, 0, 5, 10),
            Minimum = new CornerRadius(0),
            Maximum = new CornerRadius(10),
        };

        var result = Convert(converter, new CornerRadius(1, 3, 6, 8));

        Assert.Equal(new CornerRadius(0, 6, 10, 10), result);
    }

    [Fact]
    public void Convert_ParameterOverridesCopyCorners()
    {
        var converter = new CornerRadiusTransformConverter
        {
            BaseCornerRadius = new CornerRadius(9),
            CopyCorners = CornerRadiusCorners.None,
        };

        var result = Convert(converter, new CornerRadius(1, 2, 3, 4), "TopLeft, BottomRight");

        Assert.Equal(new CornerRadius(1, 9, 3, 9), result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new CornerRadiusTransformConverter();

        Assert.Throws<NotSupportedException>(
            () => converter.ConvertBack(new CornerRadius(0), typeof(CornerRadius), null, CultureInfo.InvariantCulture)
        );
    }

    private static CornerRadius Convert(CornerRadiusTransformConverter converter, object? value, object? parameter = null)
    {
        return Assert.IsType<CornerRadius>(
            converter.Convert(value, typeof(CornerRadius), parameter, CultureInfo.InvariantCulture)
        );
    }
}
