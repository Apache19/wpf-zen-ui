// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
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
    public void OwnerClick_TemporarilyReleasesAndRestoresAutoCloseCapture()
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

                excludedElement.RaiseEvent(CreateMouseButtonEvent(excludedElement, Mouse.MouseUpEvent));
                Assert.False(popup.StaysOpen);

                popup.RaiseEvent(
                    CreateMouseButtonEvent(popup, Mouse.PreviewMouseDownOutsideCapturedElementEvent)
                );

                // No per-control configuration is required. Clearing the explicit override falls
                // back to Popup.PlacementTarget, which is the owner in the common case.
                Assert.True(popup.StaysOpen);
            }
            finally
            {
                popup.IsOpen = false;
                window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    public void ContextMenuOwnerGesture_ArmsOneShotShieldAfterNativeMouseDownDismissal(
        MouseButton button
    )
    {
        RunOnStaThread(() =>
        {
            // This test otherwise uses only native WPF types, which does not force the referenced
            // Wpf.Zen.Ui assembly to load and run its module initializer in isolation.
            PopupDismissal.Initialize();

            var owner = new Border
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
                Content = owner,
            };
            var contextMenu = new ContextMenu
            {
                PlacementTarget = owner,
                Items = { new MenuItem { Header = "Item" } },
            };
            bool opened = false;
            contextMenu.Opened += (_, _) => opened = true;

            try
            {
                window.Show();
                window.UpdateLayout();
                MoveElementUnderCursor(window, owner);
                contextMenu.IsOpen = true;
                window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(contextMenu.IsOpen);
                Assert.True(opened);
                Assert.Same(owner, contextMenu.PlacementTarget);
                // ContextMenu inherits Popup.StaysOpen's true default metadata, but MenuBase owns a
                // separate capture and still performs native click-through dismissal.
                Assert.True(contextMenu.StaysOpen);

                Assert.True(NativeMethods.GetCursorPos(out NativePoint cursorPosition));
                Assert.True(
                    new Rect(owner.RenderSize).Contains(
                        owner.PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y))
                    )
                );
                var mouseDown = CreateMouseButtonEvent(
                    contextMenu,
                    Mouse.PreviewMouseDownOutsideCapturedElementEvent,
                    button
                );
                contextMenu.RaiseEvent(mouseDown);
                Assert.True(NativeMethods.GetCursorPos(out cursorPosition));
                Assert.True(
                    new Rect(owner.RenderSize).Contains(
                        owner.PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y))
                    )
                );

                // MenuBase keeps its native mouse-down dismissal. PopupDismissal arms the placement
                // target in the same outside-capture route, before the raw input is retargeted.
                Assert.False(contextMenu.IsOpen);

                var ownerMouseDown = CreateMouseButtonEvent(
                    owner,
                    Mouse.PreviewMouseDownEvent,
                    button
                );
                owner.RaiseEvent(ownerMouseDown);
                Assert.True(ownerMouseDown.Handled);

                var ownerMouseUp = CreateMouseButtonEvent(
                    owner,
                    Mouse.PreviewMouseUpEvent,
                    button
                );
                owner.RaiseEvent(ownerMouseUp);

                Assert.True(ownerMouseUp.Handled);
                Assert.False(contextMenu.IsOpen);
            }
            finally
            {
                contextMenu.IsOpen = false;
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

    private static MouseButtonEventArgs CreateMouseButtonEvent(
        UIElement source,
        RoutedEvent routedEvent,
        MouseButton button = MouseButton.Left
    )
    {
        return new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
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
        WpfTestThread.Run(action);
    }
}
