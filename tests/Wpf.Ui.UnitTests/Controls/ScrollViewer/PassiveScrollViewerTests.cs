// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls.ScrollViewer;

public class PassiveScrollViewerTests
{
    [Fact]
    public void ScrollSpillModeEnabled_DoesNotHandleMouseWheelAtScrollBoundary()
    {
        RunOnStaThread(
            () =>
            {
                WithScrollableViewer(
                    ScrollSpillMode.Enabled,
                    scrollViewer =>
                    {
                        MouseWheelEventArgs args = CreateMouseWheelEvent(delta: -120);

                        scrollViewer.InvokeOnMouseWheel(args);

                        Assert.False(args.Handled);
                    }
                );
            }
        );
    }

    [Fact]
    public void ScrollSpillModeDisabled_HandlesMouseWheelAtScrollBoundary()
    {
        RunOnStaThread(
            () =>
            {
                WithScrollableViewer(
                    ScrollSpillMode.Disabled,
                    scrollViewer =>
                    {
                        MouseWheelEventArgs args = CreateMouseWheelEvent(delta: -120);

                        scrollViewer.InvokeOnMouseWheel(args);

                        Assert.True(args.Handled);
                    }
                );
            }
        );
    }

    [Fact]
    public void ScrollSpillModeNative_UsesNativeMouseWheelHandling()
    {
        RunOnStaThread(
            () =>
            {
                WithScrollableViewer(
                    ScrollSpillMode.Native,
                    scrollViewer =>
                    {
                        MouseWheelEventArgs args = CreateMouseWheelEvent(delta: -120);

                        scrollViewer.InvokeOnMouseWheel(args);

                        Assert.True(args.Handled);
                    }
                );
            }
        );
    }

    [Fact]
    public void ScrollSpillMode_DefaultsToEnabled()
    {
        RunOnStaThread(
            () =>
            {
                var scrollViewer = new TestPassiveScrollViewer();

                Assert.Equal(ScrollSpillMode.Enabled, scrollViewer.ScrollSpillMode);
            }
        );
    }

    private static MouseWheelEventArgs CreateMouseWheelEvent(int delta)
    {
        return new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
        };
    }

    private static void WithScrollableViewer(ScrollSpillMode mode, Action<TestPassiveScrollViewer> action)
    {
        var scrollViewer = new TestPassiveScrollViewer
        {
            Width = 100d,
            Height = 100d,
            Content = new Border { Width = 100d, Height = 400d },
            ScrollSpillMode = mode,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };

        var window = new Window
        {
            Content = scrollViewer,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Width = 120d,
            Height = 120d,
        };

        try
        {
            window.Show();
            scrollViewer.UpdateLayout();
            scrollViewer.ScrollToBottom();
            scrollViewer.UpdateLayout();

            Assert.True(scrollViewer.ScrollableHeight > 0d);
            Assert.Equal(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset);

            action(scrollViewer);
        }
        finally
        {
            window.Close();
        }
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

    private sealed class TestPassiveScrollViewer : PassiveScrollViewer
    {
        public void InvokeOnMouseWheel(MouseWheelEventArgs e)
        {
            OnMouseWheel(e);
        }
    }
}
