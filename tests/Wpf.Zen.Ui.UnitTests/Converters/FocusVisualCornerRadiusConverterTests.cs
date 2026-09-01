// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Windows.Controls;
using Wpf.Zen.Ui.Converters;
using UiButton = Wpf.Zen.Ui.Controls.Button;
using UiToggleSwitch = Wpf.Zen.Ui.Controls.ToggleSwitch;

namespace Wpf.Zen.Ui.UnitTests.Converters;

public class FocusVisualCornerRadiusConverterTests
{
    [Fact]
    public void Convert_ControlWithoutCornerRadius_ReturnsFallbackValue()
    {
        Run(() =>
        {
            var converter = new FocusVisualCornerRadiusConverter();
            var toggleSwitch = new UiToggleSwitch();

            var result = Convert(converter, toggleSwitch, DependencyProperty.UnsetValue, default(CornerRadius));

            Assert.Equal(new CornerRadius(4), result);
        });
    }

    [Fact]
    public void Convert_UnconfiguredConventionalCornerRadius_ReturnsFallbackValue()
    {
        Run(() =>
        {
            var converter = new FocusVisualCornerRadiusConverter();
            var button = new UiButton();

            var result = Convert(converter, button, button.CornerRadius, button.GetValue(Border.CornerRadiusProperty));

            Assert.Equal(new CornerRadius(4), result);
        });
    }

    [Fact]
    public void Convert_ConfiguredConventionalCornerRadius_ReturnsControlValue()
    {
        Run(() =>
        {
            var converter = new FocusVisualCornerRadiusConverter();
            var button = new UiButton { CornerRadius = new CornerRadius(8, 7, 6, 5) };

            var result = Convert(converter, button, button.CornerRadius, button.GetValue(Border.CornerRadiusProperty));

            Assert.Equal(button.CornerRadius, result);
        });
    }

    [Fact]
    public void Convert_ExplicitZeroConventionalCornerRadius_RemainsSquare()
    {
        Run(() =>
        {
            var converter = new FocusVisualCornerRadiusConverter();
            var button = new UiButton { CornerRadius = new CornerRadius(0) };

            var result = Convert(converter, button, button.CornerRadius, button.GetValue(Border.CornerRadiusProperty));

            Assert.Equal(new CornerRadius(0), result);
        });
    }

    [Fact]
    public void Convert_ConfiguredAttachedCornerRadius_ReturnsAttachedValue()
    {
        Run(() =>
        {
            var converter = new FocusVisualCornerRadiusConverter();
            var toggleSwitch = new UiToggleSwitch();
            toggleSwitch.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));

            var result = Convert(
                converter,
                toggleSwitch,
                DependencyProperty.UnsetValue,
                toggleSwitch.GetValue(Border.CornerRadiusProperty)
            );

            Assert.Equal(new CornerRadius(6), result);
        });
    }

    [Fact]
    public void Convert_UnconfiguredConventionalCornerRadius_UsesConfiguredAttachedValue()
    {
        Run(() =>
        {
            var converter = new FocusVisualCornerRadiusConverter();
            var control = new SeparateCornerRadiusControl();
            control.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));

            var result = Convert(
                converter,
                control,
                control.CornerRadius,
                control.GetValue(Border.CornerRadiusProperty)
            );

            Assert.Equal(new CornerRadius(6), result);
        });
    }

    [Fact]
    public void Convert_ExplicitZeroAttachedCornerRadius_RemainsSquare()
    {
        Run(() =>
        {
            var converter = new FocusVisualCornerRadiusConverter();
            var toggleSwitch = new UiToggleSwitch();
            toggleSwitch.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));

            var result = Convert(
                converter,
                toggleSwitch,
                DependencyProperty.UnsetValue,
                toggleSwitch.GetValue(Border.CornerRadiusProperty)
            );

            Assert.Equal(new CornerRadius(0), result);
        });
    }

    [Fact]
    public void Convert_ConventionalCornerRadius_TakesPrecedenceOverAttachedValue()
    {
        Run(() =>
        {
            var converter = new FocusVisualCornerRadiusConverter();
            var control = new SeparateCornerRadiusControl { CornerRadius = new CornerRadius(7) };
            control.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));

            var result = Convert(
                converter,
                control,
                control.CornerRadius,
                control.GetValue(Border.CornerRadiusProperty)
            );

            Assert.Equal(new CornerRadius(7), result);
        });
    }

    [Fact]
    public void Convert_CustomFallbackValue_ReturnsConfiguredFallback()
    {
        Run(() =>
        {
            var converter = new FocusVisualCornerRadiusConverter { FallbackValue = new CornerRadius(9) };

            var result = Convert(converter, new UiToggleSwitch(), DependencyProperty.UnsetValue, default(CornerRadius));

            Assert.Equal(new CornerRadius(9), result);
        });
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new FocusVisualCornerRadiusConverter();

        Assert.Throws<NotSupportedException>(
            () =>
                converter.ConvertBack(
                    new CornerRadius(0),
                    [typeof(DependencyObject), typeof(CornerRadius), typeof(CornerRadius)],
                    null,
                    CultureInfo.InvariantCulture
                )
        );
    }

    private static CornerRadius Convert(
        FocusVisualCornerRadiusConverter converter,
        object adornedElement,
        object conventionalCornerRadius,
        object attachedCornerRadius
    )
    {
        return Assert.IsType<CornerRadius>(
            converter.Convert(
                [adornedElement, conventionalCornerRadius, attachedCornerRadius],
                typeof(CornerRadius),
                null,
                CultureInfo.InvariantCulture
            )
        );
    }

    private static void Run(Action action)
    {
        WpfTestThread.Run(action);
    }

    private sealed class SeparateCornerRadiusControl : Control
    {
        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(SeparateCornerRadiusControl),
            new FrameworkPropertyMetadata(default(CornerRadius))
        );

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }
    }
}
