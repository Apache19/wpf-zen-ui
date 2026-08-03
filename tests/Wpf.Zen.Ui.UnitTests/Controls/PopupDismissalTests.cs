// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

[Collection(nameof(KeyboardFocusTestCollection))]
public class PopupDismissalTests
{
    [Fact]
    public void ExcludedElementClick_TemporarilyReleasesAndRestoresAutoCloseCapture()
    {
        RunOnStaThread(() =>
        {
            var excludedElement = new Border
            {
                Width = 140D,
                Height = 40D,
                Background = Brushes.Transparent,
            };
            var window = new Window
            {
                Width = 240D,
                Height = 140D,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = excludedElement,
            };
            var popup = new Popup
            {
                AllowsTransparency = true,
                Child = new Border { Width = 160D, Height = 80D },
                PlacementTarget = excludedElement,
                StaysOpen = false,
            };

            PopupDismissal.SetExcludedElement(popup, excludedElement);

            try
            {
                window.Show();
                window.UpdateLayout();
                MoveElementUnderCursor(window, excludedElement);
                popup.IsOpen = true;

                Assert.Same(excludedElement, PopupDismissal.GetExcludedElement(popup));
                Assert.True(popup.IsOpen);
                Assert.False(popup.StaysOpen);

                popup.RaiseEvent(
                    CreateMouseButtonEvent(popup, Mouse.PreviewMouseDownOutsideCapturedElementEvent)
                );

                // Changing StaysOpen releases PopupRoot capture before WPF dispatches the original
                // click-through input to the excluded owner element.
                Assert.True(popup.StaysOpen);
                Assert.True(popup.IsOpen);

                excludedElement.RaiseEvent(CreateMouseButtonEvent(excludedElement, Mouse.MouseUpEvent));

                Assert.False(popup.StaysOpen);
                Assert.True(popup.IsOpen);

                popup.RaiseEvent(
                    CreateMouseButtonEvent(popup, Mouse.PreviewMouseDownOutsideCapturedElementEvent)
                );
                Assert.True(popup.StaysOpen);

                PopupDismissal.SetExcludedElement(popup, null);
                Assert.Null(PopupDismissal.GetExcludedElement(popup));
                Assert.False(popup.StaysOpen);

                popup.RaiseEvent(
                    CreateMouseButtonEvent(popup, Mouse.PreviewMouseDownOutsideCapturedElementEvent)
                );

                // Clearing the attached property removes the behavior and leaves WPF's configured
                // auto-close mode untouched.
                Assert.False(popup.StaysOpen);
            }
            finally
            {
                popup.IsOpen = false;
                window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                window.Close();
            }
        });
    }

    private static void MoveElementUnderCursor(Window window, FrameworkElement element)
    {
        Assert.True(NativeMethods.GetCursorPos(out NativePoint cursorPosition));
        Point topLeft = element.PointToScreen(default);
        DpiScale dpi = VisualTreeHelper.GetDpi(element);
        window.Left +=
            (cursorPosition.X - topLeft.X - (element.ActualWidth * dpi.DpiScaleX / 2D)) / dpi.DpiScaleX;
        window.Top +=
            (cursorPosition.Y - topLeft.Y - (element.ActualHeight * dpi.DpiScaleY / 2D)) / dpi.DpiScaleY;
        window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

        Assert.True(NativeMethods.GetCursorPos(out cursorPosition));
        Point localPosition = element.PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y));
        Assert.True(new Rect(element.RenderSize).Contains(localPosition));
    }

    private static MouseButtonEventArgs CreateMouseButtonEvent(UIElement source, RoutedEvent routedEvent)
    {
        return new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = routedEvent,
            Source = source,
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out NativePoint point);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
