// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Zen.Ui.AutomationPeers;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.ScrollViewer;

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

    [Fact]
    public void ScrollSpillMode_CanBeSetOnTemplateOwner()
    {
        RunOnStaThread(
            () =>
            {
                var templateOwner = new System.Windows.Controls.ListBox();

                PassiveScrollViewer.SetScrollSpillMode(
                    templateOwner,
                    ScrollSpillMode.Disabled
                );

                Assert.Equal(
                    ScrollSpillMode.Disabled,
                    PassiveScrollViewer.GetScrollSpillMode(templateOwner)
                );
            }
        );
    }

    [Fact]
    public void DefaultListBoxTemplate_ForwardsAttachedScrollSpillMode()
    {
        RunOnStaThread(
            () =>
            {
                RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
                var resources = new ResourceDictionary
                {
                    Source = System.IO.Packaging.PackUriHelper.Create(
                        new Uri("application:///", UriKind.Absolute),
                        new Uri(
                            "/Wpf.Zen.Ui;component/Controls/ListBox/DefaultListBox.xaml",
                            UriKind.Relative
                        )
                    ),
                };
                var listBox = new System.Windows.Controls.ListBox
                {
                    Style = Assert.IsType<Style>(resources["DefaultListBoxStyle"]),
                };
                PassiveScrollViewer.SetScrollSpillMode(listBox, ScrollSpillMode.Enabled);

                Assert.True(listBox.ApplyTemplate());

                var scrollViewer = Assert.IsType<PassiveScrollViewer>(
                    FindVisualDescendant<PassiveScrollViewer>(listBox)
                );
                Assert.Equal(ScrollSpillMode.Enabled, scrollViewer.ScrollSpillMode);
            }
        );
    }

    [Fact]
    public void HideButtons_SetOnScrollViewer_CollapsesButtonsAndReclaimsTheirSpace()
    {
        RunOnStaThread(
            () =>
            {
                var scrollViewer = new PassiveScrollViewer
                {
                    Width = 100d,
                    Height = 100d,
                    Content = new Border { Width = 400d, Height = 400d },
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                };
                ScrollViewerAssist.SetHideButtons(scrollViewer, true);

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

                    var verticalScrollBar = Assert.IsType<UiScrollBar>(
                        scrollViewer.Template.FindName("PART_VerticalScrollBar", scrollViewer)
                    );
                    var horizontalScrollBar = Assert.IsType<UiScrollBar>(
                        scrollViewer.Template.FindName("PART_HorizontalScrollBar", scrollViewer)
                    );

                    AssertScrollBarButtonsCollapsed(verticalScrollBar, "UpButton", "DownButton");
                    AssertScrollBarButtonsCollapsed(horizontalScrollBar, "LeftButton", "RightButton");
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void DefaultListBoxTemplate_ForwardsAssistPropertiesOnlyToItsScrollViewer()
    {
        RunOnStaThread(
            () =>
            {
                RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
                var resources = new ResourceDictionary
                {
                    Source = System.IO.Packaging.PackUriHelper.Create(
                        new Uri("application:///", UriKind.Absolute),
                        new Uri(
                            "/Wpf.Zen.Ui;component/Controls/ListBox/DefaultListBox.xaml",
                            UriKind.Relative
                        )
                    ),
                };
                var nestedScrollViewer = new PassiveScrollViewer();
                var listBox = new System.Windows.Controls.ListBox
                {
                    ItemsSource = new[] { nestedScrollViewer },
                    Style = Assert.IsType<Style>(resources["DefaultListBoxStyle"]),
                };
                ScrollViewerAssist.SetHideButtons(listBox, true);
                ScrollViewerAssist.SetOverlayMode(listBox, ScrollViewerOverlayMode.Traditional);

                var window = new Window
                {
                    Content = listBox,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Width = 120d,
                    Height = 120d,
                };

                try
                {
                    window.Show();
                    listBox.UpdateLayout();

                    var templateScrollViewer = Assert.IsType<PassiveScrollViewer>(
                        FindVisualDescendant<PassiveScrollViewer>(listBox)
                    );

                    Assert.True(ScrollViewerAssist.GetHideButtons(templateScrollViewer));
                    Assert.Equal(
                        ScrollViewerOverlayMode.Traditional,
                        ScrollViewerAssist.GetOverlayMode(templateScrollViewer)
                    );
                    Assert.False(ScrollViewerAssist.GetHideButtons(nestedScrollViewer));
                    Assert.Equal(
                        ScrollViewerOverlayMode.Overlay,
                        ScrollViewerAssist.GetOverlayMode(nestedScrollViewer)
                    );

                    var scrollContentPresenter = Assert.IsType<ScrollContentPresenter>(
                        templateScrollViewer.Template.FindName(
                            "ScrollContent",
                            templateScrollViewer
                        )
                    );
                    Assert.Equal(1, Grid.GetRowSpan(scrollContentPresenter));
                    Assert.Equal(1, Grid.GetColumnSpan(scrollContentPresenter));

                    var verticalScrollBar = Assert.IsType<UiScrollBar>(
                        templateScrollViewer.Template.FindName(
                            "PART_VerticalScrollBar",
                            templateScrollViewer
                        )
                    );
                    AssertScrollBarButtonsCollapsed(verticalScrollBar, "UpButton", "DownButton");

                    ScrollViewerAssist.SetOverlayMode(listBox, ScrollViewerOverlayMode.Overlay);

                    Assert.Equal(
                        ScrollViewerOverlayMode.Overlay,
                        ScrollViewerAssist.GetOverlayMode(templateScrollViewer)
                    );
                    Assert.Equal(2, Grid.GetRowSpan(scrollContentPresenter));
                    Assert.Equal(2, Grid.GetColumnSpan(scrollContentPresenter));
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void ContentStopsOverflowing_ClearsRetainedVerticalOffset()
    {
        RunOnStaThread(
            () =>
            {
                var content = new Border { Width = 100d, Height = 400d };
                var scrollViewer = new TestPassiveScrollViewer
                {
                    Width = 100d,
                    Height = 100d,
                    Content = content,
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
                    scrollViewer.ScrollToVerticalOffset(120d);
                    scrollViewer.UpdateLayout();

                    Assert.Equal(120d, scrollViewer.VerticalOffset);

                    content.Height = 50d;
                    scrollViewer.UpdateLayout();

                    Assert.Equal(0d, scrollViewer.ScrollableHeight);
                    Assert.Equal(0d, scrollViewer.VerticalOffset);

                    content.Height = 400d;
                    scrollViewer.UpdateLayout();

                    Assert.True(scrollViewer.ScrollableHeight > 0d);
                    Assert.Equal(0d, scrollViewer.VerticalOffset);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void ContentShrinksPastOffset_CommitsCoercedVerticalOffset()
    {
        RunOnStaThread(
            () =>
            {
                var content = new Border { Width = 100d, Height = 500d };
                var scrollViewer = new TestPassiveScrollViewer
                {
                    Width = 100d,
                    Height = 100d,
                    Content = content,
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
                    scrollViewer.ScrollToVerticalOffset(350d);
                    scrollViewer.UpdateLayout();

                    content.Height = 250d;
                    scrollViewer.UpdateLayout();

                    double coercedOffset = scrollViewer.ScrollableHeight;
                    Assert.True(coercedOffset > 0d);
                    Assert.Equal(coercedOffset, scrollViewer.VerticalOffset);

                    content.Height = 500d;
                    scrollViewer.UpdateLayout();

                    Assert.Equal(coercedOffset, scrollViewer.VerticalOffset);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void AutomationPeer_AppliesHorizontalOverflowThresholdSemantics()
    {
        RunOnStaThread(
            () =>
            {
                var scrollViewer = new PassiveScrollViewer
                {
                    Content = new Border { Height = 50d, Width = 100.5d },
                    Height = 80d,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibilityThreshold = 1d,
                    Width = 100d,
                };
                var window = new Window
                {
                    Content = scrollViewer,
                    Height = 100d,
                    ShowInTaskbar = false,
                    Width = 120d,
                    WindowStyle = WindowStyle.None,
                };

                try
                {
                    window.Show();
                    scrollViewer.UpdateLayout();

                    Assert.InRange(scrollViewer.ScrollableWidth, 0.01d, 1d);
                    var peer = new PassiveScrollViewerAutomationPeer(scrollViewer);
                    var provider = Assert.IsAssignableFrom<IScrollProvider>(
                        peer.GetPattern(PatternInterface.Scroll)
                    );

                    Assert.False(provider.HorizontallyScrollable);
                    Assert.Equal(ScrollPatternIdentifiers.NoScroll, provider.HorizontalScrollPercent);
                    Assert.Equal(100d, provider.HorizontalViewSize);
                    Assert.Throws<InvalidOperationException>(() =>
                        provider.Scroll(ScrollAmount.SmallIncrement, ScrollAmount.NoAmount)
                    );

                    scrollViewer.HorizontalScrollBarVisibilityThreshold = 0d;

                    Assert.True(provider.HorizontallyScrollable);
                    Assert.Equal(0d, provider.HorizontalScrollPercent);
                    Assert.True(provider.HorizontalViewSize < 100d);
                }
                finally
                {
                    window.Close();
                }
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

    private static void AssertScrollBarButtonsCollapsed(
        UiScrollBar scrollBar,
        string decrementButtonName,
        string incrementButtonName
    )
    {
        Assert.True(ScrollViewerAssist.GetHideButtons(scrollBar));
        _ = scrollBar.ApplyTemplate();

        var decrementButton = Assert.IsType<RepeatButton>(
            scrollBar.Template.FindName(decrementButtonName, scrollBar)
        );
        var incrementButton = Assert.IsType<RepeatButton>(
            scrollBar.Template.FindName(incrementButtonName, scrollBar)
        );

        Assert.Equal(Visibility.Collapsed, decrementButton.Visibility);
        Assert.Equal(Visibility.Collapsed, incrementButton.Visibility);
        Assert.Equal(0d, decrementButton.ActualWidth);
        Assert.Equal(0d, decrementButton.ActualHeight);
        Assert.Equal(0d, incrementButton.ActualWidth);
        Assert.Equal(0d, incrementButton.ActualHeight);
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
        WpfTestThread.Run(action);
    }

    private static T? FindVisualDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            if (FindVisualDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private sealed class TestPassiveScrollViewer : PassiveScrollViewer
    {
        public void InvokeOnMouseWheel(MouseWheelEventArgs e)
        {
            OnMouseWheel(e);
        }
    }
}
