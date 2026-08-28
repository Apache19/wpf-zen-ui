// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.IO;
using System.Reflection;
using System.Security;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using UiImage = Wpf.Zen.Ui.Controls.Image;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ImageTests
{
    [Fact]
    public void FileSource_DecodesAndReleasesFileBeforeReturning()
    {
        string filePath = CreateTemporaryPng(32, 24, Colors.CornflowerBlue);

        try
        {
            RunOnStaThread(
                () =>
                {
                    var image = new UiImage
                    {
                        DecodePixelWidth = 16,
                        FileSource = filePath,
                    };

                    BitmapSource source = Assert.IsAssignableFrom<BitmapSource>(image.Source);
                    Assert.True(source.IsFrozen);
                    Assert.Equal(16, source.PixelWidth);
                    Assert.Equal(12, source.PixelHeight);

                    File.Delete(filePath);

                    Assert.False(File.Exists(filePath));
                    Assert.Equal(
                        Colors.CornflowerBlue,
                        RenderPixel(image, 16, 12, 8, 6)
                    );
                }
            );
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void FileSourceXamlLiteral_DecodesAndReleasesFile()
    {
        string filePath = CreateTemporaryPng(20, 20, Colors.OrangeRed);

        try
        {
            RunOnStaThread(
                () =>
                {
                    string escapedPath = SecurityElement.Escape(filePath) ?? filePath;
                    string xaml =
                        $"""
                        <ui:Image
                            xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                            xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"
                            FileSource="{escapedPath}" />
                        """;

                    var image = Assert.IsType<UiImage>(XamlReader.Parse(xaml));
                    BitmapSource source = Assert.IsAssignableFrom<BitmapSource>(image.Source);

                    Assert.True(source.IsFrozen);
                    File.Delete(filePath);
                    Assert.False(File.Exists(filePath));
                    Assert.Equal(Colors.OrangeRed, RenderPixel(image, 20, 20, 10, 10));
                }
            );
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void FileSource_ReusesFrozenBitmapForUnchangedFileAndDecodeSize()
    {
        string filePath = CreateTemporaryPng(24, 24, Colors.MediumPurple);

        try
        {
            RunOnStaThread(
                () =>
                {
                    var first = new UiImage { FileSource = filePath };
                    var second = new UiImage { FileSource = filePath };

                    Assert.Same(first.Source, second.Source);

                    File.Delete(filePath);
                    Assert.False(File.Exists(filePath));
                }
            );
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void FileSource_StringBindingUsesNoLockDecodePath()
    {
        string filePath = CreateTemporaryPng(18, 18, Colors.SeaGreen);

        try
        {
            RunOnStaThread(
                () =>
                {
                    var image = new UiImage();
                    image.SetBinding(
                        UiImage.FileSourceProperty,
                        new Binding { Source = filePath }
                    );

                    Assert.Equal(filePath, image.FileSource);
                    Assert.IsAssignableFrom<BitmapSource>(image.Source);

                    File.Delete(filePath);
                    Assert.False(File.Exists(filePath));
                }
            );
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Defaults_UseNativeImageWithoutAdditionalVisualOrEffect()
    {
        RunOnStaThread(
            () =>
            {
                var image = new UiImage();

                Assert.IsAssignableFrom<System.Windows.Controls.Image>(image);
                Assert.Equal(new CornerRadius(0D), image.CornerRadius);
                Assert.Equal(1D, image.Brightness);
                Assert.Equal(1D, image.Contrast);
                Assert.Equal(1D, image.Saturation);
                Assert.Equal(0D, image.HueAngle);
                Assert.Equal(0, VisualTreeHelper.GetChildrenCount(image));
                Assert.Null(image.Effect);
            }
        );
    }

    [Fact]
    public void RoundedRender_ClipsIndependentCorners()
    {
        RunOnStaThread(
            () =>
            {
                var image = new UiImage
                {
                    CornerRadius = new CornerRadius(0D, 16D, 0D, 0D),
                    Source = CreateSolidBitmap(40, 40, Colors.Red),
                };

                Color squareCorner = RenderPixel(image, 40, 40, 0, 0);
                Color roundedCorner = RenderPixel(image, 40, 40, 39, 0);
                Color center = RenderPixel(image, 40, 40, 20, 20);

                Assert.Equal(Colors.Red, squareCorner);
                Assert.Equal(0, roundedCorner.A);
                Assert.Equal(Colors.Red, center);
            }
        );
    }

    [Fact]
    public void RoundedRender_ReusesFrozenGeometryUntilSizeOrRadiusChanges()
    {
        RunOnStaThread(
            () =>
            {
                var image = new UiImage
                {
                    CornerRadius = new CornerRadius(12D),
                    Source = CreateSolidBitmap(40, 40, Colors.Red),
                };

                _ = RenderPixel(image, 40, 40, 20, 20);
                StreamGeometry firstGeometry = GetCachedClipGeometry(image);

                image.Source = CreateSolidBitmap(40, 40, Colors.Blue);
                _ = RenderPixel(image, 40, 40, 20, 20);
                StreamGeometry secondGeometry = GetCachedClipGeometry(image);

                Assert.True(firstGeometry.IsFrozen);
                Assert.Same(firstGeometry, secondGeometry);

                image.CornerRadius = new CornerRadius(8D);
                _ = RenderPixel(image, 40, 40, 20, 20);

                Assert.NotSame(firstGeometry, GetCachedClipGeometry(image));
            }
        );
    }

    [Fact]
    public void ColorAdjustment_IsLazyAndPreservesInheritedEffectSlot()
    {
        RunOnStaThread(
            () =>
            {
                var outerEffect = new BlurEffect { Radius = 2D };
                var image = new UiImage { Effect = outerEffect };

                image.Saturation = 0.5D;

                Assert.Same(outerEffect, image.Effect);
                Assert.Equal(1, VisualTreeHelper.GetChildrenCount(image));

                image.Saturation = 1D;

                Assert.Same(outerEffect, image.Effect);
                Assert.Equal(0, VisualTreeHelper.GetChildrenCount(image));
            }
        );
    }

    [Fact]
    public void ColorAdjustment_RendersThroughEmbeddedPixelShader()
    {
        RunOnStaThread(
            () =>
            {
                var image = new UiImage
                {
                    Brightness = 0D,
                    Source = CreateSolidBitmap(20, 20, Colors.Red),
                };

                Color pixel = RenderPixel(image, 20, 20, 10, 10);

                Assert.Equal(255, pixel.A);
                Assert.Equal(0, pixel.R);
                Assert.Equal(0, pixel.G);
                Assert.Equal(0, pixel.B);
            }
        );
    }

    [Fact]
    public void ColorAdjustment_ContrastRendersExpectedPixel()
    {
        RunOnStaThread(
            () =>
            {
                var image = new UiImage
                {
                    Contrast = 0.5D,
                    Source = CreateSolidBitmap(20, 20, Color.FromRgb(64, 128, 192)),
                };

                Color pixel = RenderPixel(image, 20, 20, 10, 10);

                AssertColorClose(Color.FromRgb(96, 128, 160), pixel);
            }
        );
    }

    [Fact]
    public void ColorAdjustment_ZeroSaturationRendersRec709Luminance()
    {
        RunOnStaThread(
            () =>
            {
                var image = new UiImage
                {
                    Saturation = 0D,
                    Source = CreateSolidBitmap(20, 20, Color.FromRgb(64, 128, 192)),
                };

                Color pixel = RenderPixel(image, 20, 20, 10, 10);

                AssertColorClose(Color.FromRgb(120, 120, 120), pixel);
            }
        );
    }

    [Fact]
    public void ColorAdjustment_HueRotationRendersExpectedPixel()
    {
        RunOnStaThread(
            () =>
            {
                var image = new UiImage
                {
                    HueAngle = 180D,
                    Source = CreateSolidBitmap(20, 20, Colors.Red),
                };

                Color pixel = RenderPixel(image, 20, 20, 10, 10);

                AssertColorClose(Color.FromRgb(0, 108, 108), pixel);
            }
        );
    }

    [Fact]
    public void ColorAdjustment_CombinesTransformsInDocumentedOrder()
    {
        RunOnStaThread(
            () =>
            {
                var image = new UiImage
                {
                    Brightness = 0.75D,
                    Contrast = 1.25D,
                    Saturation = 0.5D,
                    HueAngle = 90D,
                    Source = CreateSolidBitmap(20, 20, Color.FromRgb(64, 128, 192)),
                };

                Color pixel = RenderPixel(image, 20, 20, 10, 10);

                // The expected value is the result of applying hue, saturation, brightness, and
                // contrast in that order. A different composition order produces different channels.
                AssertColorClose(Color.FromRgb(114, 67, 105), pixel);
            }
        );
    }

    [Fact]
    public void ColorAdjustment_AnimatedPropertyUpdatesWithoutRecreatingVisual()
    {
        RunOnStaThread(
            () =>
            {
                var image = new UiImage { Saturation = 0.5D };
                DependencyObject adjustedVisual = VisualTreeHelper.GetChild(image, 0);
                var animation = new DoubleAnimation
                {
                    From = 0.5D,
                    To = 0.5D,
                    Duration = TimeSpan.FromMinutes(1D),
                };

                image.BeginAnimation(UiImage.BrightnessProperty, animation);

                Assert.True(image.HasAnimatedProperties);
                Assert.Same(adjustedVisual, VisualTreeHelper.GetChild(image, 0));
            }
        );
    }

    [Fact]
    public void ColorScaleProperties_CoerceAnimationSafeRangeAndRejectNonFiniteValues()
    {
        RunOnStaThread(
            () =>
            {
                var image = new UiImage
                {
                    Brightness = 3D,
                    Contrast = -1D,
                    Saturation = 4D,
                };

                Assert.Equal(2D, image.Brightness);
                Assert.Equal(0D, image.Contrast);
                Assert.Equal(2D, image.Saturation);
                Assert.Throws<ArgumentException>(() => image.HueAngle = double.NaN);
            }
        );
    }

    private static BitmapSource CreateSolidBitmap(int width, int height, Color color)
    {
        int stride = width * 4;
        var pixels = new byte[stride * height];
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = color.B;
            pixels[offset + 1] = color.G;
            pixels[offset + 2] = color.R;
            pixels[offset + 3] = color.A;
        }

        BitmapSource bitmap = BitmapSource.Create(
            width,
            height,
            96D,
            96D,
            PixelFormats.Pbgra32,
            null,
            pixels,
            stride
        );
        bitmap.Freeze();
        return bitmap;
    }

    private static string CreateTemporaryPng(int width, int height, Color color)
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"WpfZenUi-{Guid.NewGuid():N}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(CreateSolidBitmap(width, height, color)));

        using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            encoder.Save(stream);
        }

        return filePath;
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

    private static void AssertColorClose(Color expected, Color actual, int tolerance = 2)
    {
        Assert.InRange(actual.A, expected.A - tolerance, expected.A + tolerance);
        Assert.InRange(actual.R, Math.Max(0, expected.R - tolerance), Math.Min(255, expected.R + tolerance));
        Assert.InRange(actual.G, Math.Max(0, expected.G - tolerance), Math.Min(255, expected.G + tolerance));
        Assert.InRange(actual.B, Math.Max(0, expected.B - tolerance), Math.Min(255, expected.B + tolerance));
    }

    private static StreamGeometry GetCachedClipGeometry(UiImage image)
    {
        FieldInfo? field = typeof(UiImage).GetField(
            "_clipGeometryCache",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        return Assert.IsType<StreamGeometry>(field?.GetValue(image));
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }
}