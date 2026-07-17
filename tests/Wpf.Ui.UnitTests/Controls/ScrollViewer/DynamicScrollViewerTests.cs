// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls.ScrollViewer;

public class DynamicScrollViewerTests
{
    [Fact]
    public void LogicalScroll_ShowsVerticalScrollBarForItemSizedOffsetChange()
    {
        RunOnStaThread(
            () =>
            {
                var itemsPanel = new StackPanel();

                for (int index = 0; index < 20; index++)
                {
                    itemsPanel.Children.Add(new Border { Height = 20d });
                }

                var scrollViewer = new DynamicScrollViewer
                {
                    Width = 100d,
                    Height = 100d,
                    CanContentScroll = true,
                    Content = itemsPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
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

                    var verticalScrollBar = Assert.IsType<DynamicScrollBar>(
                        scrollViewer.Template.FindName("PART_VerticalScrollBar", scrollViewer)
                    );

                    Assert.True(scrollViewer.ScrollableHeight > 0d);
                    Assert.Equal(0d, scrollViewer.VerticalOffset);
                    Assert.False(verticalScrollBar.IsInteracted);

                    scrollViewer.LineDown();
                    DrainDispatcher();

                    // StackPanel implements IScrollInfo in logical units, so one line changes the offset
                    // by one item. This deliberately remains below the pixel-oriented MinimalChange default.
                    Assert.Equal(1d, scrollViewer.VerticalOffset);
                    Assert.True(scrollViewer.IsScrollingVertically);
                    Assert.True(verticalScrollBar.IsInteracted);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false)
        );
        Dispatcher.PushFrame(frame);
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
