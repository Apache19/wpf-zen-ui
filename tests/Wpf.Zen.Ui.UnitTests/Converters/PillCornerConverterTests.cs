// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Threading;
using Wpf.Zen.Ui.Converters;

namespace Wpf.Zen.Ui.UnitTests.Converters;

/// <summary>
/// Verifies radius policies and real WPF binding updates across layout and template lifetimes.
/// </summary>
public class PillCornerConverterTests
{
    /// <summary>
    /// Each corner mask preserves square corners, including when a positive minimum is configured.
    /// </summary>
    [Fact]
    public void CornerMasks_PreserveUnselectedCorners()
    {
        for (int mask = 0; mask <= 15; mask++)
        {
            var options = new PillCornerConverter { Corners = (CornerRadiusCorners)mask, MinRadius = 25 };
            var radius = Assert.IsType<CornerRadius>(Convert(options, 100d, 40d));
            Assert.Equal((mask & 1) != 0 ? 25 : 0, radius.TopLeft);
            Assert.Equal((mask & 2) != 0 ? 25 : 0, radius.TopRight);
            Assert.Equal((mask & 4) != 0 ? 25 : 0, radius.BottomRight);
            Assert.Equal((mask & 8) != 0 ? 25 : 0, radius.BottomLeft);
        }
    }

    /// <summary>
    /// Axis selection allows narrow segments to share their container's pill radius.
    /// </summary>
    [Theory]
    [InlineData(PillCornerDimension.ShorterSide, 10d)]
    [InlineData(PillCornerDimension.Width, 10d)]
    [InlineData(PillCornerDimension.Height, 30d)]
    public void Dimension_SelectsRadiusBasis(PillCornerDimension dimension, double expected)
    {
        Assert.Equal(new CornerRadius(expected), Convert(new() { Dimension = dimension }, 20d, 60d));
    }

    /// <summary>
    /// Scaling precedes offset and limits; zero is a meaningful upper bound.
    /// </summary>
    [Fact]
    public void RadiusOptions_ApplyInOrderAndSupportScalarTargets()
    {
        var options = new PillCornerConverter { Factor = 0.25, Offset = 3, MinRadius = 5, MaxRadius = 12 };
        Assert.Equal(new CornerRadius(8), Convert(options, 20d, 60d));
        Assert.Equal(new CornerRadius(12), Convert(options, 100d, 60d));
        options.Offset = -100;
        Assert.Equal(new CornerRadius(5), Convert(options, 20d, 60d));
        options.MinRadius = 0;
        options.MaxRadius = 0;
        Assert.Equal(new CornerRadius(0), Convert(options, 20d, 60d));
        options = new PillCornerConverter { Corners = CornerRadiusCorners.None };
        Assert.Equal(10d, Convert(options, 20d, 60d, typeof(double)));
    }

    /// <summary>
    /// Missing layout and invalid dimensions never produce nonfinite radii or apply minimums.
    /// </summary>
    [Fact]
    public void InvalidDimensions_ReturnZeroAndOverflowRemainsFinite()
    {
        var options = new PillCornerConverter { MinRadius = 5 };
        foreach (object invalid in new object[] { 0d, -1d, double.NaN, double.PositiveInfinity,
                     DependencyProperty.UnsetValue, "40" })
        {
            Assert.Equal(new CornerRadius(0), Convert(options, invalid, 40d));
            Assert.Equal(new CornerRadius(0), Convert(options, 40d, invalid));
        }

        options.Factor = double.MaxValue;
        options.Offset = double.MaxValue;
        Assert.Equal(new CornerRadius(double.MaxValue), Convert(options, 40d, 40d));
        Assert.Same(DependencyProperty.UnsetValue, Convert(options, 40d, 40d, typeof(string)));
    }

    /// <summary>
    /// Invalid settings fail before a binding is attached to the visual tree.
    /// </summary>
    [Fact]
    public void InvalidOptions_AreRejected()
    {
        PillCornerConverter[] invalid =
        [
            new() { Factor = -1 }, new() { Factor = double.NaN },
            new() { Offset = double.PositiveInfinity }, new() { MinRadius = -1 },
            new() { MinRadius = double.PositiveInfinity }, new() { MaxRadius = double.NaN },
            new() { MinRadius = 10, MaxRadius = 9 }, new() { Dimension = (PillCornerDimension)99 },
            new() { Corners = (CornerRadiusCorners)16 }, new() { ElementName = " " },
        ];
        foreach (var options in invalid)
        {
            Assert.ThrowsAny<ArgumentException>(() => options.CreateBinding());
        }

        Assert.Throws<InvalidOperationException>(() => new PillCornerConverter
        {
            ElementName = "Host", RelativeSource = RelativeSource.Self,
        }.CreateBinding());
    }

    /// <summary>
    /// Inline XAML tracks initial layout and subsequent independent changes to both dimensions.
    /// </summary>
    [Fact]
    public void InlineXaml_TracksBothDimensions()
    {
        WpfTestThread.Run(() =>
        {
            var border = (Border)XamlReader.Parse("""
                <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:c="clr-namespace:Wpf.Zen.Ui.Converters;assembly=Wpf.Zen.Ui"
                        CornerRadius="{c:PillCornerConverter Corners=Left}" />
                """);
            Assert.Equal(new CornerRadius(0), border.CornerRadius);
            Layout(border, 100d, 40d);
            Assert.Equal(new CornerRadius(20, 0d, 0d, 20d), border.CornerRadius);
            Layout(border, 100d, 60d);
            Assert.Equal(new CornerRadius(30, 0d, 0d, 30d), border.CornerRadius);
            Layout(border, 20d, 60d);
            Assert.Equal(new CornerRadius(10, 0d, 0d, 10d), border.CornerRadius);
        });
    }

    /// <summary>
    /// A shared style creates independent size bindings for each target element.
    /// </summary>
    [Fact]
    public void StyleSetter_TracksEachTargetIndependently()
    {
        WpfTestThread.Run(() =>
        {
            var style = (Style)XamlReader.Parse("""
                <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                       xmlns:c="clr-namespace:Wpf.Zen.Ui.Converters;assembly=Wpf.Zen.Ui"
                       TargetType="Border">
                    <Setter Property="CornerRadius" Value="{c:PillCornerConverter}" />
                </Style>
                """);
            var first = new Border { Style = style };
            var second = new Border { Style = style };
            Layout(first, 100d, 40d);
            Layout(second, 100d, 60d);
            Assert.Equal(new CornerRadius(20), first.CornerRadius);
            Assert.Equal(new CornerRadius(30), second.CornerRadius);
            Layout(first, 10d, 40d);
            Assert.Equal(new CornerRadius(5), first.CornerRadius);
            Assert.Equal(new CornerRadius(30), second.CornerRadius);
        });
    }

    /// <summary>
    /// A named common container supplies consistent radii to separate interactive segments.
    /// </summary>
    [Fact]
    public void ElementName_TracksCommonContainer()
    {
        WpfTestThread.Run(() =>
        {
            var grid = (Grid)XamlReader.Parse("""
                <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                      xmlns:c="clr-namespace:Wpf.Zen.Ui.Converters;assembly=Wpf.Zen.Ui"
                      x:Name="Host">
                    <Border Width="10"
                        CornerRadius="{c:PillCornerConverter ElementName=Host, Corners=Right}" />
                </Grid>
                """);
            var border = (Border)grid.Children[0];
            Layout(grid, 100d, 40d);
            Assert.Equal(new CornerRadius(0, 20d, 20d, 0d), border.CornerRadius);
            Layout(grid, 100d, 60d);
            Assert.Equal(new CornerRadius(0, 30d, 30d, 0d), border.CornerRadius);
        });
    }

    /// <summary>
    /// Deferred template bindings resolve their templated parent and survive template replacement.
    /// </summary>
    [Fact]
    public void Template_TracksTemplatedParentAfterReapplication()
    {
        WpfTestThread.Run(() =>
        {
            var template = (ControlTemplate)XamlReader.Parse("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:c="clr-namespace:Wpf.Zen.Ui.Converters;assembly=Wpf.Zen.Ui"
                    TargetType="Control">
                    <Border x:Name="Part" Width="10"
                        CornerRadius="{c:PillCornerConverter
                            RelativeSource={RelativeSource TemplatedParent}, Corners='TopLeft,BottomRight'}" />
                </ControlTemplate>
                """);
            var control = new Control { Template = template };
            Layout(control, 100d, 40d);
            var border = (Border)template.FindName("Part", control);
            Assert.Equal(new CornerRadius(20, 0d, 20d, 0d), border.CornerRadius);
            control.Template = null;
            control.ApplyTemplate();
            control.Template = template;
            Layout(control, 100d, 60d);
            border = (Border)template.FindName("Part", control);
            Assert.Equal(new CornerRadius(30, 0d, 30d, 0d), border.CornerRadius);
        });
    }

    /// <summary>
    /// Bindings retain their configuration even if the factory is reused and changed later.
    /// </summary>
    [Fact]
    public void CreateBinding_CapturesOptions()
    {
        var options = new PillCornerConverter();
        var binding = options.CreateBinding();
        options.Factor = 0;
        Assert.Equal(new CornerRadius(20), binding.Converter.Convert(
            [100d, 40d], typeof(CornerRadius), null!, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => binding.Converter.ConvertBack(
            new CornerRadius(20), [typeof(double), typeof(double)], null!, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Exercises conversion without a dispatcher for deterministic numeric policy tests.
    /// </summary>
    private static object Convert(PillCornerConverter options, object width, object height, Type? type = null)
    {
        return options.CreateBinding().Converter.Convert(
            [width, height], type ?? typeof(CornerRadius), null!, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Completes layout and delivers queued WPF size notifications and binding transfers.
    /// </summary>
    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Width = width;
        element.Height = height;
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0d, width, height));
        element.UpdateLayout();
        element.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
}
