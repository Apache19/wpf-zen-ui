// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using UiComboBox = Wpf.Zen.Ui.Controls.ComboBox;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ComboBoxTests
{
    [Fact]
    public void VisualBrushes_FallBackToStandardBrushes()
    {
        RunOnStaThread(
            () =>
            {
                var background = new SolidColorBrush(Colors.Red);
                var borderBrush = new SolidColorBrush(Colors.Green);
                var foreground = new SolidColorBrush(Colors.Blue);
                var comboBox = new UiComboBox
                {
                    Background = background,
                    BorderBrush = borderBrush,
                    Foreground = foreground,
                };

                Assert.Same(background, comboBox.VisualBackground);
                Assert.Same(borderBrush, comboBox.VisualBorderBrush);
                Assert.Same(foreground, comboBox.VisualForeground);
            }
        );
    }

    [Fact]
    public void DisabledBrushes_UpdateVisualBrushesWithoutChangingStandardBrushes()
    {
        RunOnStaThread(
            () =>
            {
                var background = new SolidColorBrush(Colors.Red);
                var borderBrush = new SolidColorBrush(Colors.Green);
                var foreground = new SolidColorBrush(Colors.Blue);
                var disabledBackground = new SolidColorBrush(Colors.Gray);
                var disabledBorderBrush = new SolidColorBrush(Colors.DarkGray);
                var disabledForeground = new SolidColorBrush(Colors.LightGray);
                var comboBox = new UiComboBox
                {
                    Background = background,
                    BorderBrush = borderBrush,
                    Foreground = foreground,
                    DisabledBackground = disabledBackground,
                    DisabledBorderBrush = disabledBorderBrush,
                    DisabledForeground = disabledForeground,
                    IsEnabled = false,
                };

                Assert.Same(disabledBackground, comboBox.VisualBackground);
                Assert.Same(disabledBorderBrush, comboBox.VisualBorderBrush);
                Assert.Same(disabledForeground, comboBox.VisualForeground);
                Assert.Same(background, comboBox.Background);
                Assert.Same(borderBrush, comboBox.BorderBrush);
                Assert.Same(foreground, comboBox.Foreground);

                comboBox.IsEnabled = true;

                Assert.Same(background, comboBox.VisualBackground);
                Assert.Same(borderBrush, comboBox.VisualBorderBrush);
                Assert.Same(foreground, comboBox.VisualForeground);
            }
        );
    }

    [Fact]
    public void GenericTheme_AppliesResolvedBrushesAndCornerRadius()
    {
        RunOnStaThread(
            () =>
            {
                var background = new SolidColorBrush(Colors.Red);
                var borderBrush = new SolidColorBrush(Colors.Green);
                var foreground = new SolidColorBrush(Colors.Blue);
                var comboBox = new UiComboBox
                {
                    Background = background,
                    BorderBrush = borderBrush,
                    Foreground = foreground,
                };
                var root = new Grid();
                root.Resources.MergedDictionaries.Add(
                    new ResourceDictionary { Source = CreateThemeUri() }
                );
                root.Children.Add(comboBox);

                Assert.True(comboBox.ApplyTemplate());

                var contentBorder = Assert.IsType<Border>(
                    comboBox.Template.FindName("ContentBorder", comboBox)
                );

                Assert.Same(background, contentBorder.Background);
                Assert.Same(borderBrush, contentBorder.BorderBrush);
                Assert.Equal(foreground, TextElement.GetForeground(contentBorder));
            }
        );
    }

    [Fact]
    public void GenericTheme_DoesNotProvideNativeComboBoxStyle()
    {
        RunOnStaThread(
            () =>
            {
                var resources = new ResourceDictionary { Source = CreateThemeUri() };

                Assert.Null(resources[typeof(System.Windows.Controls.ComboBox)]);
            }
        );
    }

    [Fact]
    public void NativeControlStyles_AppliesNativeComboBoxAppearanceProperties()
    {
        RunOnStaThread(
            () =>
            {
                var background = new SolidColorBrush(Colors.Red);
                var borderBrush = new SolidColorBrush(Colors.Green);
                var cornerRadius = new CornerRadius(7);
                var comboBox = new System.Windows.Controls.ComboBox
                {
                    Background = background,
                    BorderBrush = borderBrush,
                };
                comboBox.SetValue(Border.CornerRadiusProperty, cornerRadius);
                var root = new Grid();
                root.Resources.MergedDictionaries.Add(
                    new ResourceDictionary { Source = CreateThemeUri() }
                );
                root.Resources.MergedDictionaries.Add(
                    new ResourceDictionary
                    {
                        Source = new Uri(
                            "pack://application:,,,/Wpf.Zen.Ui;component/Resources/NativeControlStyles.xaml",
                            UriKind.Absolute
                        ),
                    }
                );
                root.Children.Add(comboBox);

                Assert.True(comboBox.ApplyTemplate());

                var contentBorder = Assert.IsType<Border>(
                    comboBox.Template.FindName("ContentBorder", comboBox)
                );

                Assert.Same(background, contentBorder.Background);
                Assert.Same(borderBrush, contentBorder.BorderBrush);
                Assert.Equal(cornerRadius, contentBorder.CornerRadius);
            }
        );
    }

    private static Uri CreateThemeUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
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
