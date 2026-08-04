// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Zen.Ui.Effects;

namespace Wpf.Zen.Ui.UnitTests.Effects;

public class ColorAdjustmentEffectTests
{
    [Fact]
    public void Defaults_AreNeutralAndPropertiesValidatePublicRanges()
    {
        RunOnStaThread(
            () =>
            {
                var effect = new ColorAdjustmentEffect();

                Assert.NotNull(effect.Input);
                Assert.Equal(1D, effect.Brightness);
                Assert.Equal(1D, effect.Contrast);
                Assert.Equal(1D, effect.Saturation);
                Assert.Equal(0D, effect.HueAngle);

                effect.Brightness = 3D;
                effect.Contrast = -1D;
                effect.Saturation = 4D;

                Assert.Equal(2D, effect.Brightness);
                Assert.Equal(0D, effect.Contrast);
                Assert.Equal(2D, effect.Saturation);
                Assert.Throws<ArgumentException>(() => effect.HueAngle = double.NaN);
            }
        );
    }

    [Fact]
    public void Clone_PreservesPublicAdjustmentValues()
    {
        RunOnStaThread(
            () =>
            {
                var effect = new ColorAdjustmentEffect
                {
                    Brightness = 0.75D,
                    Contrast = 1.25D,
                    Saturation = 0.5D,
                    HueAngle = 90D,
                };

                ColorAdjustmentEffect clone = Assert.IsType<ColorAdjustmentEffect>(effect.Clone());

                Assert.Equal(effect.Brightness, clone.Brightness);
                Assert.Equal(effect.Contrast, clone.Contrast);
                Assert.Equal(effect.Saturation, clone.Saturation);
                Assert.Equal(effect.HueAngle, clone.HueAngle);
            }
        );
    }

    [Fact]
    public void ContainerEffect_AdjustsTheCompositedVisualSubtreeInOnePass()
    {
        RunOnStaThread(
            () =>
            {
                var leftCard = new Border
                {
                    Width = 10D,
                    Background = Brushes.Red,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                var rightCard = new Border
                {
                    Width = 10D,
                    Background = Brushes.Blue,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };
                var container = new Grid
                {
                    Width = 20D,
                    Height = 10D,
                    Effect = new ColorAdjustmentEffect { Brightness = 0D },
                };
                container.Children.Add(leftCard);
                container.Children.Add(rightCard);

                Color leftPixel = RenderPixel(container, 20, 10, 5, 5);
                Color rightPixel = RenderPixel(container, 20, 10, 15, 5);

                Assert.Null(leftCard.Effect);
                Assert.Null(rightCard.Effect);
                Assert.Equal(Colors.Black, leftPixel);
                Assert.Equal(Colors.Black, rightPixel);
            }
        );
    }

    [Fact]
    public void ColorTransform_NeutralValuesProduceIdentityMatrix()
    {
        ColorTransform transform = ColorTransform.Create(1D, 1D, 1D, 0D);

        Assert.Equal(1D, transform.M11, 12);
        Assert.Equal(0D, transform.M12, 12);
        Assert.Equal(0D, transform.M13, 12);
        Assert.Equal(0D, transform.OffsetR, 12);
        Assert.Equal(0D, transform.M21, 12);
        Assert.Equal(1D, transform.M22, 12);
        Assert.Equal(0D, transform.M23, 12);
        Assert.Equal(0D, transform.OffsetG, 12);
        Assert.Equal(0D, transform.M31, 12);
        Assert.Equal(0D, transform.M32, 12);
        Assert.Equal(1D, transform.M33, 12);
        Assert.Equal(0D, transform.OffsetB, 12);
    }

    private static Color RenderPixel(UIElement element, int width, int height, int x, int y)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0D, 0D, width, height));
        element.UpdateLayout();

        var bitmap = new RenderTargetBitmap(width, height, 96D, 96D, PixelFormats.Pbgra32);
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
}
