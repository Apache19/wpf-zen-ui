// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using Wpf.Ui.Theming;
using UiTextBlock = Wpf.Ui.Controls.TextBlock;

namespace Wpf.Ui.UnitTests.Controls;

public class TextBlockTests
{
    [Fact]
    public void Foreground_UsesInheritedValue_WhenFirstAttachedToParent()
    {
        RunOnStaThread(
            () =>
            {
                var inheritedForeground = new SolidColorBrush(Colors.Red);
                var defaultForeground = new SolidColorBrush(Colors.Blue);
                var parent = new Border();
                var textBlock = new UiTextBlock();

                parent.SetValue(TextElement.ForegroundProperty, inheritedForeground);
                UiTextBlock.SetDefaultForeground(textBlock, defaultForeground);
                parent.Child = textBlock;

                Assert.Same(inheritedForeground, textBlock.Foreground);
            }
        );
    }

    [Fact]
    public void Foreground_UsesInheritedBlack_WhenFirstAttachedToParent()
    {
        RunOnStaThread(
            () =>
            {
                var defaultForeground = new SolidColorBrush(Colors.Blue);
                var parent = new Border();
                var textBlock = new UiTextBlock();

                parent.SetValue(TextElement.ForegroundProperty, Brushes.Black);
                UiTextBlock.SetDefaultForeground(textBlock, defaultForeground);
                parent.Child = textBlock;

                Assert.Same(Brushes.Black, textBlock.Foreground);
            }
        );
    }

    [Fact]
    public void FontSize_UsesTypographyPreset_WhenFontSizeIsAnimated()
    {
        RunOnStaThread(
            () =>
            {
                var textBlock = CreateTextBlockWithTypographyPreset();
                var window = ShowInTestWindow(textBlock);
                var animation = new DoubleAnimation
                {
                    From = 32d,
                    To = 32d,
                    Duration = TimeSpan.FromMinutes(1),
                };

                try
                {
                    textBlock.BeginAnimation(TextElement.FontSizeProperty, animation);
                    PumpDispatcher();

                    Assert.Equal(20d, textBlock.FontSize);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void FontWeight_UsesTypographyPreset_WhenFontWeightIsAnimated()
    {
        RunOnStaThread(
            () =>
            {
                var textBlock = CreateTextBlockWithTypographyPreset();
                var window = ShowInTestWindow(textBlock);
                var animation = new ObjectAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMinutes(1),
                };
                animation.KeyFrames.Add(
                    new DiscreteObjectKeyFrame(FontWeights.Black, KeyTime.FromTimeSpan(TimeSpan.Zero))
                );

                try
                {
                    textBlock.BeginAnimation(TextElement.FontWeightProperty, animation);
                    PumpDispatcher();

                    Assert.Equal(FontWeights.SemiBold, textBlock.FontWeight);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void FontTypographyInheritedFromParent_OverridesLocalFontProperties()
    {
        RunOnStaThread(
            () =>
            {
                var parent = new Border();
                var textBlock = new UiTextBlock
                {
                    FontSize = 32d,
                    FontWeight = FontWeights.Black,
                };
                parent.Resources[FontTypography.Body.ToResourceKey()] = new FontTypographyPreset
                {
                    FontSize = 20d,
                    FontWeight = FontWeights.SemiBold,
                };

                UiTextBlock.SetFontTypography(parent, FontTypography.Body);
                parent.Child = textBlock;

                Assert.Equal(20d, textBlock.FontSize);
                Assert.Equal(FontWeights.SemiBold, textBlock.FontWeight);
            }
        );
    }

    [Fact]
    public void AppearanceInheritedFromParent_OverridesLocalForeground()
    {
        RunOnStaThread(
            () =>
            {
                var appearanceForeground = new SolidColorBrush(Colors.Blue);
                var parent = new Border();
                var textBlock = new UiTextBlock { Foreground = Brushes.Red };
                parent.Resources[TextColor.Primary.ToResourceKey()] = appearanceForeground;

                UiTextBlock.SetAppearance(parent, TextColor.Primary);
                parent.Child = textBlock;

                Assert.Same(appearanceForeground, textBlock.Foreground);
            }
        );
    }

    [Fact]
    public void NullFontTypography_BlocksInheritedTypography()
    {
        RunOnStaThread(
            () =>
            {
                var parent = new Border();
                var textBlock = new UiTextBlock
                {
                    FontSize = 32d,
                    FontWeight = FontWeights.Black,
                };
                parent.Resources[FontTypography.Body.ToResourceKey()] = new FontTypographyPreset
                {
                    FontSize = 20d,
                    FontWeight = FontWeights.SemiBold,
                };

                UiTextBlock.SetFontTypography(parent, FontTypography.Body);
                UiTextBlock.SetFontTypography(textBlock, null);
                parent.Child = textBlock;

                Assert.Equal(32d, textBlock.FontSize);
                Assert.Equal(FontWeights.Black, textBlock.FontWeight);
            }
        );
    }

    [Fact]
    public void NullAppearance_BlocksInheritedAppearance()
    {
        RunOnStaThread(
            () =>
            {
                var parent = new Border();
                var textBlock = new UiTextBlock { Foreground = Brushes.Red };
                parent.Resources[TextColor.Primary.ToResourceKey()] = Brushes.Blue;

                UiTextBlock.SetAppearance(parent, TextColor.Primary);
                UiTextBlock.SetAppearance(textBlock, null);
                parent.Child = textBlock;

                Assert.Same(Brushes.Red, textBlock.Foreground);
            }
        );
    }

    [Fact]
    public void Foreground_UsesAppearance_WhenForegroundIsAnimated()
    {
        RunOnStaThread(
            () =>
            {
                var appearanceForeground = new SolidColorBrush(Colors.Blue);
                var textBlock = new UiTextBlock();
                textBlock.Resources[TextColor.Primary.ToResourceKey()] = appearanceForeground;
                textBlock.Appearance = TextColor.Primary;
                var window = ShowInTestWindow(textBlock);
                var animation = new ObjectAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMinutes(1),
                };
                animation.KeyFrames.Add(
                    new DiscreteObjectKeyFrame(Brushes.Red, KeyTime.FromTimeSpan(TimeSpan.Zero))
                );

                try
                {
                    textBlock.BeginAnimation(TextElement.ForegroundProperty, animation);
                    PumpDispatcher();

                    Assert.Same(appearanceForeground, textBlock.Foreground);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    private static UiTextBlock CreateTextBlockWithTypographyPreset()
    {
        var textBlock = new UiTextBlock();
        textBlock.Resources[FontTypography.Body.ToResourceKey()] = new FontTypographyPreset
        {
            FontSize = 20d,
            FontWeight = FontWeights.SemiBold,
        };
        textBlock.FontTypography = FontTypography.Body;
        return textBlock;
    }

    private static Window ShowInTestWindow(UiTextBlock textBlock)
    {
        var window = new Window
        {
            Content = textBlock,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Width = 1d,
            Height = 1d,
        };
        window.Show();
        return window;
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(50),
            DispatcherPriority.ApplicationIdle,
            (_, _) => frame.Continue = false,
            Dispatcher.CurrentDispatcher
        );

        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
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