// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Zen.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Exercises content-first layout across native, render-only and clipping borders.
/// </summary>
public class CompressiblePaddingTests
{
    /// <summary>
    /// Verifies proportional compression, independent axes and recovery after resizing.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Compression_PreservesContentAndRestoresAsymmetricPadding(int mode)
    {
        WpfTestThread.Run(() =>
        {
            var child = new Border { Width = 80, Height = 20 };
            Decorator border = CreateBorder(mode, child);
            SetPadding(border, new Thickness(10, 6, 30, 6));

            Layout(border, 100, 40);
            Rect slot = LayoutInformation.GetLayoutSlot(child);
            Assert.Equal(new Rect(5, 6, 80, 28), slot);
            Assert.Equal(80, child.ActualWidth);
            Assert.Null(LayoutInformation.GetLayoutClip(child));

            Layout(border, 140, 40);
            slot = LayoutInformation.GetLayoutSlot(child);
            Assert.Equal(new Rect(10, 6, 100, 28), slot);
            DependencyProperty property = PaddingProperty(border);
            object configuredPadding = border.GetValue(property);
            Assert.Equal(new Thickness(10, 6, 30, 6), configuredPadding);
        });
    }

    /// <summary>
    /// Verifies that content receives the entire slot after optional padding is exhausted.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Compression_WhenContentExceedsSpace_RemovesAllPadding(int mode)
    {
        WpfTestThread.Run(() =>
        {
            var child = new Border { Width = 80, Height = 20 };
            Decorator border = CreateBorder(mode, child);
            SetPadding(border, new Thickness(16));
            Layout(border, 60, 10);

            Assert.Equal(new Size(60, 10), border.RenderSize);
            Rect slot = LayoutInformation.GetLayoutSlot(child);
            Assert.Equal(new Rect(0, 0, 60, 10), slot);
        });
    }

    /// <summary>
    /// Verifies unconstrained demand and layout invalidation when the feature is disabled.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Compression_UnboundedMeasureRetainsPadding_AndToggleInvalidatesMeasure(int mode)
    {
        WpfTestThread.Run(() =>
        {
            var child = new Border { Width = 80, Height = 20 };
            Decorator border = CreateBorder(mode, child);
            SetPadding(border, new Thickness(16, 6, 16, 6));
            border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(new Size(112, 32), border.DesiredSize);

            Layout(border, 100, 32);
            border.SetValue(UiBorder.IsPaddingCompressibleProperty, false);
            Assert.False(border.IsMeasureValid);
            Layout(border, 100, 32);
            Rect slot = LayoutInformation.GetLayoutSlot(child);
            Assert.Equal(16, slot.X);
            Assert.Equal(68, slot.Width);
        });
    }

    /// <summary>
    /// Verifies that padding does not force additional lines before content gets available width.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Compression_WrappingTextUsesSpaceBeforeReservingPadding(int mode)
    {
        WpfTestThread.Run(() =>
        {
            var child = new TextBlock
            {
                Text = "Padding should yield space to this wrapping text.",
                TextWrapping = TextWrapping.Wrap
            };
            child.Measure(new Size(140, double.PositiveInfinity));
            double expectedHeight = child.DesiredSize.Height;
            Decorator border = CreateBorder(mode, child);
            SetPadding(border, new Thickness(40, 0, 40, 0));
            border.Measure(new Size(140, double.PositiveInfinity));
            Assert.Equal(expectedHeight, child.DesiredSize.Height);
        });
    }

    /// <summary>
    /// Verifies native complex geometry caches, border space and the child's existing clip.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Compression_PreservesBorderRenderingAndChildClip(int mode)
    {
        WpfTestThread.Run(() =>
        {
            var clip = new RectangleGeometry(new Rect(0, 0, 80, 20));
            var child = new Border { Width = 80, Height = 20, Clip = clip };
            Decorator border = CreateBorder(mode, child);
            SetPadding(border, new Thickness(16));
            if (border is UiBorder ui)
            {
                ui.BorderThickness = new Thickness(2);
                ui.BorderBrush = Brushes.Red;
                ui.Background = Brushes.Blue;
                ui.CornerRadius = new CornerRadius(8, 4, 6, 2);
            }
            else if (border is ClippingBorder clipping)
            {
                clipping.BorderThickness = new Thickness(2);
                clipping.BorderBrush = Brushes.Red;
                clipping.Background = Brushes.Blue;
                clipping.CornerRadius = new CornerRadius(8, 4, 6, 2);
            }

            Layout(border, 100, 40);
            Rect slot = LayoutInformation.GetLayoutSlot(child);
            Assert.Equal(new Rect(10, 10, 80, 20), slot);
            Assert.Same(clip, child.Clip);
            var bitmap = new RenderTargetBitmap(100, 40, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(border);
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(50, 20, 1, 1), pixel, 4, 0);
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, pixel);
            bitmap.CopyPixels(new Int32Rect(50, 0, 1, 1), pixel, 4, 0);
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, pixel);
        });
    }

    /// <summary>
    /// Verifies an empty decorator can yield padding and later measure replacement content.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Compression_EmptyAndReplacementContent_RecomputesDemand(int mode)
    {
        WpfTestThread.Run(() =>
        {
            Decorator border = CreateBorder(mode, new Border());
            border.Child = null;
            SetPadding(border, new Thickness(20));
            Layout(border, 10, 10);
            Assert.Equal(new Size(10, 10), border.RenderSize);

            var child = new Border { Width = 80, Height = 20, Margin = new Thickness(5) };
            border.Child = child;
            Layout(border, 100, 40);
            Rect slot = LayoutInformation.GetLayoutSlot(child);
            Assert.Equal(new Rect(5, 5, 90, 30), slot);
            Assert.Null(LayoutInformation.GetLayoutClip(child));
        });
    }

    /// <summary>
    /// Verifies the opt-in default on both dependency-property owners.
    /// </summary>
    [Fact]
    public void Compression_DefaultsToDisabled()
    {
        WpfTestThread.Run(() =>
        {
            var border = new UiBorder();
            var clipping = new ClippingBorder();
            Assert.False(border.IsPaddingCompressible);
            Assert.False(clipping.IsPaddingCompressible);
        });
    }

    private static Decorator CreateBorder(int mode, UIElement child)
    {
        if (mode == 3)
        {
            return new ClippingBorder { Child = child, IsPaddingCompressible = true };
        }

        return new UiBorder
        {
            Child = child,
            BorderPlacement = (BorderPlacement)mode,
            IsPaddingCompressible = true
        };
    }

    private static DependencyProperty PaddingProperty(Decorator border)
    {
        return border is ClippingBorder ? ClippingBorder.PaddingProperty : Border.PaddingProperty;
    }

    private static void SetPadding(Decorator border, Thickness padding)
    {
        DependencyProperty property = PaddingProperty(border);
        border.SetValue(property, padding);
    }

    private static void Layout(Decorator border, double width, double height)
    {
        border.Measure(new Size(width, height));
        border.Arrange(new Rect(0, 0, width, height));
    }
}
