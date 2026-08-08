// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ClippingBorderTests
{
    [Fact]
    public void MeasureAndArrange_ReserveBorderThicknessAndPadding()
    {
        RunOnStaThread(
            () =>
            {
                var child = new FixedSizeElement(new Size(100, 40));
                var border = new ClippingBorder
                {
                    BorderThickness = new Thickness(10, 20, 30, 40),
                    Padding = new Thickness(3, 4, 5, 6),
                    Child = child,
                };

                border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                border.Arrange(new Rect(0, 0, 200, 120));

                Assert.Equal(new Size(148, 110), border.DesiredSize);
                Assert.Equal(new Size(152, 50), child.ArrangedSize);
            }
        );
    }

    [Fact]
    public void Arrange_PreservesChildClip()
    {
        RunOnStaThread(
            () =>
            {
                var childClip = new RectangleGeometry(new Rect(1, 2, 10, 12));
                var child = new Border { Clip = childClip };
                var border = new ClippingBorder
                {
                    CornerRadius = new CornerRadius(12),
                    Child = child,
                };

                Arrange(border, 40, 40);

                Assert.Same(childClip, child.Clip);
#pragma warning disable xUnit2032 // Type assertions based on 'assignable from' are confusingly named
                var contentHost = Assert.IsAssignableFrom<Decorator>(
                    VisualTreeHelper.GetChild(border, 0)
                );
#pragma warning restore xUnit2032 // Type assertions based on 'assignable from' are confusingly named
                Assert.NotNull(contentHost.Clip);
            }
        );
    }

    [Fact]
    public void Render_WhenClipContentIsEnabled_ClipsChildAtRoundedCorner()
    {
        RunOnStaThread(
            () =>
            {
                var border = new ClippingBorder
                {
                    CornerRadius = new CornerRadius(16),
                    Child = new Border { Background = Brushes.Red },
                };

                Color cornerPixel = RenderPixel(border, 40, 40, 0, 0);
                Color centerPixel = RenderPixel(border, 40, 40, 20, 20);

                Assert.Equal(0, cornerPixel.A);
                Assert.Equal(Colors.Red, centerPixel);
            }
        );
    }

    [Fact]
    public void Render_WhenClipContentIsDisabled_DoesNotClipChildAtRoundedCorner()
    {
        RunOnStaThread(
            () =>
            {
                var border = new ClippingBorder
                {
                    ClipContent = false,
                    CornerRadius = new CornerRadius(16),
                    Child = new Border { Background = Brushes.Red },
                };

                Color cornerPixel = RenderPixel(border, 40, 40, 0, 0);

                Assert.Equal(Colors.Red, cornerPixel);
            }
        );
    }

    [Fact]
    public void Render_ClipsOverflowAtInnerBorderEdgeButAllowsItInPadding()
    {
        RunOnStaThread(
            () =>
            {
                var border = new ClippingBorder
                {
                    BorderThickness = new Thickness(2),
                    Padding = new Thickness(8),
                    Child = new Rectangle
                    {
                        Fill = Brushes.Red,
                        Margin = new Thickness(-8),
                    },
                };

                Color outsideInnerEdge = RenderPixel(border, 40, 40, 1, 20);
                Color paddingPixel = RenderPixel(border, 40, 40, 3, 20);

                Assert.Equal(0, outsideInnerEdge.A);
                Assert.Equal(Colors.Red, paddingPixel);
            }
        );
    }

    private static Color RenderPixel(UIElement element, int width, int height, int x, int y)
    {
        Arrange(element, width, height);

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);

        var pixels = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixels, 4, 0);

        return Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
    }

    private static void Arrange(UIElement element, int width, int height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }

    private sealed class FixedSizeElement(Size desiredSize) : FrameworkElement
    {
        public Size ArrangedSize { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            return desiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ArrangedSize = finalSize;

            return finalSize;
        }
    }
}
