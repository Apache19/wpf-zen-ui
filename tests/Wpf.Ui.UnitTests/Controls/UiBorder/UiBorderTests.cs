// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfUiBorder = Wpf.Ui.Controls.UiBorder;
using WpfUiBorderPlacement = Wpf.Ui.Controls.BorderPlacement;

namespace Wpf.Ui.UnitTests.Controls.UiBorder;

public class UiBorderTests
{
    [Fact]
    public void BorderPlacement_DefaultsToNative()
    {
        RunOnStaThread(
            () =>
            {
                var border = new WpfUiBorder();

                Assert.Equal(WpfUiBorderPlacement.Native, border.BorderPlacement);
            }
        );
    }

    [Fact]
    public void Measure_WhenBorderPlacementIsInside_DoesNotReserveSpaceForBorderThickness()
    {
        RunOnStaThread(
            () =>
            {
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Inside,
                    BorderThickness = new Thickness(10, 20, 30, 40),
                    Padding = new Thickness(3, 4, 5, 6),
                    Child = new FixedSizeElement(new Size(100, 40)),
                };

                border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Assert.Equal(new Size(108, 50), border.DesiredSize);
            }
        );
    }

    [Fact]
    public void Arrange_WhenBorderPlacementIsInside_DoesNotReserveSpaceForBorderThickness()
    {
        RunOnStaThread(
            () =>
            {
                var child = new FixedSizeElement(new Size(100, 40));
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Inside,
                    BorderThickness = new Thickness(10, 20, 30, 40),
                    Padding = new Thickness(3, 4, 5, 6),
                    Child = child,
                };

                border.Measure(new Size(200, 100));
                border.Arrange(new Rect(0, 0, 200, 100));

                Assert.Equal(new Size(192, 90), child.ArrangedSize);
            }
        );
    }

    [Fact]
    public void Measure_WhenBorderPlacementIsOutside_DoesNotReserveSpaceForBorderThickness()
    {
        RunOnStaThread(
            () =>
            {
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Outside,
                    BorderThickness = new Thickness(10, 20, 30, 40),
                    Padding = new Thickness(3, 4, 5, 6),
                    Child = new FixedSizeElement(new Size(100, 40)),
                };

                border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Assert.Equal(new Size(108, 50), border.DesiredSize);
            }
        );
    }

    [Fact]
    public void Arrange_WhenBorderPlacementIsOutside_DoesNotReserveSpaceForBorderThickness()
    {
        RunOnStaThread(
            () =>
            {
                var child = new FixedSizeElement(new Size(100, 40));
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Outside,
                    BorderThickness = new Thickness(10, 20, 30, 40),
                    Padding = new Thickness(3, 4, 5, 6),
                    Child = child,
                };

                border.Measure(new Size(200, 120));
                border.Arrange(new Rect(0, 0, 200, 120));

                Assert.Equal(new Size(192, 110), child.ArrangedSize);
            }
        );
    }

    [Fact]
    public void Measure_WhenBorderPlacementIsNative_ReservesSpaceForBorderThickness()
    {
        RunOnStaThread(
            () =>
            {
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Native,
                    BorderThickness = new Thickness(10, 20, 30, 40),
                    Padding = new Thickness(3, 4, 5, 6),
                    Child = new FixedSizeElement(new Size(100, 40)),
                };

                border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Assert.Equal(new Size(148, 110), border.DesiredSize);
            }
        );
    }

    [Fact]
    public void Arrange_WhenBorderPlacementIsNative_ReservesSpaceForBorderThickness()
    {
        RunOnStaThread(
            () =>
            {
                var child = new FixedSizeElement(new Size(100, 40));
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Native,
                    BorderThickness = new Thickness(10, 20, 30, 40),
                    Padding = new Thickness(3, 4, 5, 6),
                    Child = child,
                };

                border.Measure(new Size(200, 120));
                border.Arrange(new Rect(0, 0, 200, 120));

                Assert.Equal(new Size(152, 50), child.ArrangedSize);
            }
        );
    }

    [Theory]
    [InlineData(WpfUiBorderPlacement.Inside)]
    [InlineData(WpfUiBorderPlacement.Outside)]
    public void BorderThicknessChange_WhenPlacementIsRenderOnly_DoesNotInvalidateMeasure(
        WpfUiBorderPlacement borderPlacement
    )
    {
        RunOnStaThread(
            () =>
            {
                var border = new WpfUiBorder
                {
                    BorderPlacement = borderPlacement,
                    BorderThickness = new Thickness(1),
                };

                border.Measure(new Size(100, 40));
                border.Arrange(new Rect(0, 0, 100, 40));
                Assert.True(border.IsMeasureValid);

                border.BorderThickness = new Thickness(2);

                Assert.True(border.IsMeasureValid);
            }
        );
    }

    [Fact]
    public void BorderThicknessChange_WhenPlacementIsNative_InvalidatesMeasure()
    {
        RunOnStaThread(
            () =>
            {
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Native,
                    BorderThickness = new Thickness(1),
                };

                border.Measure(new Size(100, 40));
                border.Arrange(new Rect(0, 0, 100, 40));
                Assert.True(border.IsMeasureValid);

                border.BorderThickness = new Thickness(2);

                Assert.False(border.IsMeasureValid);
            }
        );
    }

    [Fact]
    public void Render_WhenBorderPlacementIsInside_DoesNotDeflateBackgroundByBorderThickness()
    {
        RunOnStaThread(
            () =>
            {
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Inside,
                    BorderThickness = new Thickness(10),
                    Background = Brushes.Red,
                };

                Color pixel = RenderPixel(border, 40, 20, 5, 10);

                Assert.Equal(Colors.Red, pixel);
            }
        );
    }

    [Fact]
    public void Render_WhenBorderPlacementIsOutside_DeflatesBackgroundByBorderThickness()
    {
        RunOnStaThread(
            () =>
            {
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Outside,
                    BorderThickness = new Thickness(10),
                    Background = Brushes.Red,
                };

                Color pixel = RenderPixel(border, 40, 20, 5, 10);

                Assert.Equal(0, pixel.A);
            }
        );
    }

    [Fact]
    public void Render_WhenBorderPlacementIsOutside_DoesNotBlendBorderWithBackground()
    {
        RunOnStaThread(
            () =>
            {
                var borderBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));
                var backgroundBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Outside,
                    BorderThickness = new Thickness(10),
                    BorderBrush = borderBrush,
                    Background = backgroundBrush,
                };

                Color borderPixel = RenderPixel(border, 40, 40, 5, 20);

                Assert.Equal(Color.FromArgb(128, 0, 0, 128), borderPixel);
            }
        );
    }

    [Fact]
    public void Render_WhenBorderPlacementIsInside_DrawsBorderOverBackground()
    {
        RunOnStaThread(
            () =>
            {
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Inside,
                    BorderThickness = new Thickness(10),
                    BorderBrush = Brushes.Blue,
                    Background = Brushes.Red,
                };

                Color borderPixel = RenderPixel(border, 40, 40, 5, 20);
                Color backgroundPixel = RenderPixel(border, 40, 40, 15, 20);

                Assert.Equal(Colors.Blue, borderPixel);
                Assert.Equal(Colors.Red, backgroundPixel);
            }
        );
    }

    [Fact]
    public void Render_WhenBorderPlacementIsInside_PreservesIndependentCornerRadii()
    {
        RunOnStaThread(
            () =>
            {
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Inside,
                    Background = Brushes.Red,
                    CornerRadius = new CornerRadius(0, 16, 0, 0),
                };

                Color squareCornerPixel = RenderPixel(border, 40, 40, 1, 1);
                Color roundedCornerPixel = RenderPixel(border, 40, 40, 39, 0);

                Assert.Equal(Colors.Red, squareCornerPixel);
                Assert.Equal(0, roundedCornerPixel.A);
            }
        );
    }

    [Fact]
    public void Render_WhenOnlyBrushChanges_ReusesCachedGeometry()
    {
        RunOnStaThread(
            () =>
            {
                var border = new WpfUiBorder
                {
                    BorderPlacement = WpfUiBorderPlacement.Inside,
                    BorderThickness = new Thickness(2),
                    BorderBrush = Brushes.Blue,
                    Background = Brushes.Red,
                };

                RenderPixel(border, 40, 40, 20, 20);
                StreamGeometry firstGeometry = GetCachedBackgroundGeometry(border);

                border.Background = Brushes.Green;
                RenderPixel(border, 40, 40, 20, 20);
                StreamGeometry secondGeometry = GetCachedBackgroundGeometry(border);

                Assert.Same(firstGeometry, secondGeometry);
            }
        );
    }

    private static StreamGeometry GetCachedBackgroundGeometry(WpfUiBorder border)
    {
        FieldInfo? field = typeof(WpfUiBorder).GetField(
            "_backgroundGeometryCache",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        return Assert.IsType<StreamGeometry>(field?.GetValue(border));
    }

    private static Color RenderPixel(UIElement element, int width, int height, int x, int y)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);

        var pixels = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixels, 4, 0);

        return Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception caughtException)
                {
                    exception = caughtException;
                }
            }
        );

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class FixedSizeElement : FrameworkElement
    {
        private readonly Size _desiredSize;

        public FixedSizeElement(Size desiredSize)
        {
            _desiredSize = desiredSize;
        }

        public Size ArrangedSize { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            return _desiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ArrangedSize = finalSize;

            return finalSize;
        }
    }
}
