// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Wpf.Zen.Ui.Converters;

namespace Wpf.Zen.Ui.UnitTests.Converters;

public class ObjectToBoolConverterTests
{
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData("Ready")]
    public void Convert_WithoutMappings_ReturnsFalse(object? value)
    {
        var converter = new ObjectToBoolConverter();

        Assert.False(Convert(converter, value));
    }

    [Fact]
    public void Convert_WithOnlyTrueValue_ReturnsWhetherValueMatches()
    {
        var converter = new ObjectToBoolConverter { TrueValue = "Ready" };

        Assert.True(Convert(converter, "Ready"));
        Assert.False(Convert(converter, "Pending"));
    }

    [Fact]
    public void Convert_WithBothMappings_ReturnsCorrespondingBoolean()
    {
        var converter = new ObjectToBoolConverter
        {
            TrueValue = "Enabled",
            FalseValue = "Disabled",
        };

        Assert.True(Convert(converter, "Enabled"));
        Assert.False(Convert(converter, "Disabled"));
        Assert.False(Convert(converter, "Unknown"));
    }

    [Fact]
    public void Convert_CanUseTrueAsTheUnmatchedValue()
    {
        var converter = new ObjectToBoolConverter
        {
            FalseValue = "Disabled",
            UnmatchedValue = true,
        };

        Assert.False(Convert(converter, "Disabled"));
        Assert.True(Convert(converter, "Enabled"));
    }

    [Fact]
    public void Convert_ExplicitNullMapping_IsDifferentFromOmittedMapping()
    {
        var converter = new ObjectToBoolConverter { TrueValue = null };

        Assert.True(Convert(converter, null));
        Assert.False(Convert(converter, new object()));
    }

    [Fact]
    public void Convert_UsesConfiguredComparer()
    {
        var converter = new ObjectToBoolConverter
        {
            TrueValue = "READY",
            Comparer = StringComparer.OrdinalIgnoreCase,
        };

        Assert.True(Convert(converter, "ready"));
    }

    [Fact]
    public void Convert_DoesNotCoerceDifferentTypes()
    {
        var converter = new ObjectToBoolConverter { TrueValue = "1" };

        Assert.False(Convert(converter, 1));
    }

    [Theory]
    [MemberData(nameof(GetUnavailableValues))]
    public void Convert_UnavailableBindingValue_ReturnsUnsetValue(object value)
    {
        var converter = new ObjectToBoolConverter { UnmatchedValue = true };

        object? result = converter.Convert(value, typeof(bool), null, InvariantCulture);

        Assert.Same(DependencyProperty.UnsetValue, result);
    }

    [Fact]
    public void Convert_WhenBothMappingsMatch_ReturnsUnsetValue()
    {
        var converter = new ObjectToBoolConverter
        {
            TrueValue = "value",
            FalseValue = "VALUE",
            Comparer = StringComparer.OrdinalIgnoreCase,
        };

        object? result = converter.Convert("Value", typeof(bool), null, InvariantCulture);

        Assert.Same(DependencyProperty.UnsetValue, result);
    }

    [Fact]
    public void ConvertBack_ReturnsConfiguredMapping()
    {
        var converter = new ObjectToBoolConverter
        {
            TrueValue = "Enabled",
            FalseValue = "Disabled",
        };

        Assert.Equal("Enabled", ConvertBack(converter, true));
        Assert.Equal("Disabled", ConvertBack(converter, false));
    }

    [Fact]
    public void ConvertBack_CanReturnExplicitlyConfiguredNull()
    {
        var converter = new ObjectToBoolConverter { FalseValue = null };

        Assert.Null(ConvertBack(converter, false));
    }

    [Fact]
    public void ConvertBack_WithoutCorrespondingMapping_DoesNothing()
    {
        var converter = new ObjectToBoolConverter { TrueValue = "Enabled" };

        Assert.Same(Binding.DoNothing, ConvertBack(converter, false));
    }

    [Fact]
    public void ConvertBack_WithAmbiguousMappings_DoesNothing()
    {
        var converter = new ObjectToBoolConverter
        {
            TrueValue = "Enabled",
            FalseValue = "ENABLED",
            Comparer = StringComparer.OrdinalIgnoreCase,
        };

        Assert.Same(Binding.DoNothing, ConvertBack(converter, true));
        Assert.Same(Binding.DoNothing, ConvertBack(converter, false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData("true")]
    public void ConvertBack_WithNonBooleanInput_ReturnsUnsetValue(object? value)
    {
        var converter = new ObjectToBoolConverter { TrueValue = "Enabled" };

        object? result = converter.ConvertBack(value, typeof(object), null, InvariantCulture);

        Assert.Same(DependencyProperty.UnsetValue, result);
    }

    [Fact]
    public void ProvideValue_ReturnsCurrentInstance()
    {
        var converter = new ObjectToBoolConverter();

        Assert.Same(converter, converter.ProvideValue(null!));
    }

    [Fact]
    public void InlineMarkupExtension_ConfiguresBindingConverter()
    {
        WpfTestThread.Run(
            () =>
            {
                const string xaml =
                    """
                    <Border
                        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:ui="http://schemas.thezendev.com/wpf/2027"
                        Tag="{Binding State, Converter={ui:ObjectToBoolConverter TrueValue=Ready}}" />
                    """;

                var target = Assert.IsType<Border>(XamlReader.Parse(xaml));
                Binding binding = Assert.IsType<Binding>(
                    BindingOperations.GetBinding(target, FrameworkElement.TagProperty)
                );
                var converter = Assert.IsType<ObjectToBoolConverter>(binding.Converter);

                Assert.Equal("Ready", converter.TrueValue);
                Assert.True(Convert(converter, "Ready"));
                Assert.False(Convert(converter, "Pending"));
            }
        );
    }

    public static TheoryData<object> GetUnavailableValues()
    {
        return new TheoryData<object> { DependencyProperty.UnsetValue, Binding.DoNothing };
    }

    private static bool Convert(ObjectToBoolConverter converter, object? value)
    {
        return Assert.IsType<bool>(
            converter.Convert(value, typeof(bool), null, InvariantCulture)
        );
    }

    private static object? ConvertBack(ObjectToBoolConverter converter, bool value)
    {
        return converter.ConvertBack(value, typeof(object), null, InvariantCulture);
    }
}
