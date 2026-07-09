// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media;
using WpfButton = Wpf.Ui.Controls.Button;

namespace Wpf.Ui.UnitTests.Controls.ButtonControl;

public class ButtonTests
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
                var button = new TestButton
                {
                    Background = background,
                    BorderBrush = borderBrush,
                    Foreground = foreground,
                };

                Assert.Same(background, button.VisualBackground);
                Assert.Same(borderBrush, button.VisualBorderBrush);
                Assert.Same(foreground, button.VisualForeground);
            }
        );
    }

    [Fact]
    public void PressedBrushes_UpdateVisualBrushesWithoutChangingStandardBrushes()
    {
        RunOnStaThread(
            () =>
            {
                var background = new SolidColorBrush(Colors.Red);
                var borderBrush = new SolidColorBrush(Colors.Green);
                var foreground = new SolidColorBrush(Colors.Blue);
                var pressedBackground = new SolidColorBrush(Colors.Orange);
                var pressedBorderBrush = new SolidColorBrush(Colors.Purple);
                var pressedForeground = new SolidColorBrush(Colors.White);
                var button = new TestButton
                {
                    Background = background,
                    BorderBrush = borderBrush,
                    Foreground = foreground,
                    PressedBackground = pressedBackground,
                    PressedBorderBrush = pressedBorderBrush,
                    PressedForeground = pressedForeground,
                };

                button.SetPressed(true);

                Assert.Same(pressedBackground, button.VisualBackground);
                Assert.Same(pressedBorderBrush, button.VisualBorderBrush);
                Assert.Same(pressedForeground, button.VisualForeground);
                Assert.Same(background, button.Background);
                Assert.Same(borderBrush, button.BorderBrush);
                Assert.Same(foreground, button.Foreground);

                button.SetPressed(false);

                Assert.Same(background, button.VisualBackground);
                Assert.Same(borderBrush, button.VisualBorderBrush);
                Assert.Same(foreground, button.VisualForeground);
            }
        );
    }

    [Fact]
    public void ActiveStateBrushChange_ImmediatelyUpdatesVisualBrush()
    {
        RunOnStaThread(
            () =>
            {
                var firstPressedBackground = new SolidColorBrush(Colors.Orange);
                var secondPressedBackground = new SolidColorBrush(Colors.Purple);
                var button = new TestButton
                {
                    Background = new SolidColorBrush(Colors.Red),
                    PressedBackground = firstPressedBackground,
                };

                button.SetPressed(true);
                Assert.Same(firstPressedBackground, button.VisualBackground);

                button.PressedBackground = secondPressedBackground;

                Assert.Same(secondPressedBackground, button.VisualBackground);
            }
        );
    }

    [Fact]
    public void NullPressedBrush_FallsBackToStandardBrush()
    {
        RunOnStaThread(
            () =>
            {
                var background = new SolidColorBrush(Colors.Red);
                var button = new TestButton
                {
                    Background = background,
                    PressedBackground = null,
                };

                button.SetPressed(true);

                Assert.Same(background, button.VisualBackground);
            }
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

    private sealed class TestButton : WpfButton
    {
        public void SetPressed(bool value)
        {
            IsPressed = value;
        }
    }
}
