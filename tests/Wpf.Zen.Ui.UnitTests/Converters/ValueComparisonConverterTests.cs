// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Windows.Data;
using Wpf.Zen.Ui.Converters;

namespace Wpf.Zen.Ui.UnitTests.Converters;

public class ValueComparisonConverterTests
{
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(2d, 1d, null, true)]
    [InlineData(2d, 1d, ">", true)]
    [InlineData(2d, 2d, ">=", true)]
    [InlineData(1d, 2d, "<", true)]
    [InlineData(2d, 2d, "<=", true)]
    [InlineData(2d, 2d, "==", true)]
    [InlineData(2d, 1d, "!=", true)]
    [InlineData(1d, 2d, ">", false)]
    public void Convert_AppliesRequestedOperator(
        double left,
        double right,
        string? comparisonOperator,
        bool expected
    )
    {
        var converter = new ValueComparisonConverter();

        object result = converter.Convert(
            [left, right],
            typeof(bool),
            comparisonOperator,
            InvariantCulture
        );

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_UsesBindingCultureForNumericText()
    {
        var converter = new ValueComparisonConverter();
        var culture = CultureInfo.GetCultureInfo("fr-FR");

        object result = converter.Convert(["1,5", "1,25"], typeof(bool), ">", culture);

        Assert.Equal(true, result);
    }

    [Theory]
    [InlineData("==", true)]
    [InlineData("!=", false)]
    [InlineData(">", false)]
    [InlineData(">=", true)]
    public void Convert_AppliesToleranceConsistently(string comparisonOperator, bool expected)
    {
        var converter = new ValueComparisonConverter { Tolerance = 0.01d };

        object result = converter.Convert(
            [1d, 1.005d],
            typeof(bool),
            comparisonOperator,
            InvariantCulture
        );

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("==", false)]
    [InlineData("!=", true)]
    [InlineData(">", false)]
    [InlineData("<", false)]
    public void Convert_NaNFollowsFloatingPointSemantics(string comparisonOperator, bool expected)
    {
        var converter = new ValueComparisonConverter();

        object result = converter.Convert(
            [double.NaN, double.NaN],
            typeof(bool),
            comparisonOperator,
            InvariantCulture
        );

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_EqualPositiveInfinitiesCompareAsEqual()
    {
        var converter = new ValueComparisonConverter { Tolerance = 0.01d };

        object result = converter.Convert(
            [double.PositiveInfinity, double.PositiveInfinity],
            typeof(bool),
            "==",
            InvariantCulture
        );

        Assert.Equal(true, result);
    }

    [Theory]
    [MemberData(nameof(GetInvalidInputs))]
    public void Convert_InvalidInputReturnsUnsetValue(object[] values, object? parameter)
    {
        var converter = new ValueComparisonConverter();

        object result = converter.Convert(values, typeof(bool), parameter, InvariantCulture);

        Assert.Same(DependencyProperty.UnsetValue, result);
    }

    [Theory]
    [InlineData(-0.01d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Tolerance_RejectsInvalidValues(double tolerance)
    {
        var converter = new ValueComparisonConverter();

        Assert.Throws<ArgumentOutOfRangeException>(() => converter.Tolerance = tolerance);
    }

    [Fact]
    public void ConvertBack_ReturnsNoSourceValues()
    {
        var converter = new ValueComparisonConverter();

        object[] result = converter.ConvertBack(
            true,
            [typeof(double), typeof(double)],
            ">",
            InvariantCulture
        );

        Assert.Empty(result);
    }

    public static TheoryData<object[], object?> GetInvalidInputs()
    {
        return new TheoryData<object[], object?>
        {
            { [], ">" },
            { [1d], ">" },
            { [1d, 2d, 3d], ">" },
            { [DependencyProperty.UnsetValue, 1d], ">" },
            { [Binding.DoNothing, 1d], ">" },
            { [null!, 1d], ">" },
            { ["not a number", 1d], ">" },
            { [1d, 2d], "approximately" },
            { [1d, 2d], 42 },
        };
    }
}
