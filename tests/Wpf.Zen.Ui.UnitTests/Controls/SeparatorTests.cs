// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Data;
using UiSeparator = Wpf.Zen.Ui.Controls.Separator;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies divider layout and caller-owned spacing with and without the default style.
/// </summary>
public class SeparatorTests
{
    /// <summary>
    /// Unstyled controls must measure safely even when neither available dimension is bounded.
    /// </summary>
    [Fact]
    public void WithoutStyle_MeasuresWithInfiniteAvailableSize()
    {
        WpfTestThread.Run(() =>
        {
            var separator = new UiSeparator { OverridesDefaultStyle = true };
            Assert.Equal(new Thickness(0), separator.Margin);
            AssertFiniteMeasurement(separator);

            separator.Margin = new Thickness(3);
            separator.ClearValue(FrameworkElement.MarginProperty);
            Assert.Equal(new Thickness(0), separator.Margin);
            AssertFiniteMeasurement(separator);
        });
    }

    /// <summary>
    /// Orientation changes the rendered line dimensions while leaving spacing at zero.
    /// </summary>
    [Fact]
    public void DefaultStyle_ChangesLineOrientationWithoutAddingMargin()
    {
        WpfTestThread.Run(() =>
        {
            var separator = CreateStyledSeparator();
            var border = Assert.IsType<Border>(separator.Template.FindName("SeparatorBorder", separator));
            Assert.Equal(1, border.Height);
            Assert.True(double.IsNaN(border.Width));
            Assert.Equal(HorizontalAlignment.Stretch, border.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Center, border.VerticalAlignment);
            Assert.Equal(new Thickness(0), separator.Margin);
            AssertFiniteMeasurement(separator);

            separator.Orientation = Orientation.Vertical;
            Assert.Equal(1, border.Width);
            Assert.True(double.IsNaN(border.Height));
            Assert.Equal(HorizontalAlignment.Center, border.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Stretch, border.VerticalAlignment);
            Assert.Equal(new Thickness(0), separator.Margin);
            AssertFiniteMeasurement(separator);

            separator.Orientation = Orientation.Horizontal;
            Assert.Equal(1, border.Height);
            Assert.True(double.IsNaN(border.Width));
            Assert.Equal(new Thickness(0), separator.Margin);
            var panel = new StackPanel();
            panel.Children.Add(separator);
            panel.Measure(new Size(180, double.PositiveInfinity));
            Assert.True(double.IsFinite(panel.DesiredSize.Height));
        });
    }

    /// <summary>
    /// Local spacing survives orientation changes and clearing it restores zero margin.
    /// </summary>
    [Fact]
    public void LocalMargin_SurvivesDirectionChangesAndClearsToZero()
    {
        WpfTestThread.Run(() =>
        {
            var separator = CreateStyledSeparator();
            separator.Margin = new Thickness(7);
            separator.Orientation = Orientation.Vertical;
            Assert.Equal(new Thickness(7), separator.Margin);
            separator.Orientation = Orientation.Horizontal;
            Assert.Equal(new Thickness(7), separator.Margin);

            separator.ClearValue(FrameworkElement.MarginProperty);
            Assert.Equal(new Thickness(0), separator.Margin);
        });
    }

    /// <summary>
    /// A caller's margin binding remains active across orientation changes.
    /// </summary>
    [Fact]
    public void LocalBinding_RemainsActiveUntilCleared()
    {
        WpfTestThread.Run(() =>
        {
            var separator = CreateStyledSeparator();
            var source = new Border { Margin = new Thickness(5) };
            var binding = new Binding(nameof(Border.Margin)) { Source = source };
            BindingOperations.SetBinding(separator, FrameworkElement.MarginProperty, binding);
            separator.Orientation = Orientation.Vertical;
            Assert.Equal(new Thickness(5), separator.Margin);
            source.Margin = new Thickness(6);
            Assert.Equal(new Thickness(6), separator.Margin);

            BindingOperations.ClearBinding(separator, FrameworkElement.MarginProperty);
            Assert.Equal(new Thickness(0), separator.Margin);
        });
    }

    /// <summary>
    /// Derived style setters retain their spacing in both orientations.
    /// </summary>
    [Fact]
    public void DerivedStyleMargin_IsPreservedInBothOrientations()
    {
        WpfTestThread.Run(() =>
        {
            var separator = CreateStyledSeparator();
            var style = new Style(typeof(UiSeparator), separator.Style);
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(10)));
            separator.Style = style;
            Assert.Equal(new Thickness(10), separator.Margin);
            separator.Orientation = Orientation.Vertical;
            Assert.Equal(new Thickness(10), separator.Margin);
            separator.Orientation = Orientation.Horizontal;
            Assert.Equal(new Thickness(10), separator.Margin);
        });
    }

    /// <summary>
    /// Loads the compiled dictionary to exercise the actual control template and triggers.
    /// </summary>
    private static UiSeparator CreateStyledSeparator()
    {
        var resources = new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/Wpf.Zen.Ui;component/Controls/Separator/Separator.xaml"
            ),
        };
        var separator = new UiSeparator { Style = (Style)resources[typeof(UiSeparator)] };
        separator.ApplyTemplate();
        return separator;
    }

    /// <summary>
    /// Measures without constraints and requires finite desired dimensions.
    /// </summary>
    private static void AssertFiniteMeasurement(UiSeparator separator)
    {
        separator.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.True(double.IsFinite(separator.DesiredSize.Width));
        Assert.True(double.IsFinite(separator.DesiredSize.Height));
    }
}
