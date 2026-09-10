// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.ScrollViewer;

public class SmoothScrollingTests
{
    private static readonly TimeSpan Response = TimeSpan.FromMilliseconds(240);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HorizontalWheel_UsesSelectedAxisAndHonorsSmoothSetting(bool smooth)
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            ConfigureHorizontalWheel(viewer);
            viewer.IsSmoothScrollingEnabled = smooth;
            Assert.True(viewer.Wheel(-120).Handled);
            Assert.Equal(smooth, viewer.IsSmoothScrolling);
            if (smooth)
            {
                Assert.Equal(0, viewer.HorizontalOffset);
                Advance(viewer, 16);
                double first = viewer.HorizontalOffset;
                Assert.True(first > 0);
                viewer.Wheel(-120);
                Advance(viewer, 1000);
                double step = PassiveScrollViewer.GetSmoothWheelDistance(
                    -120,
                    SystemParameters.WheelScrollLines,
                    viewer.ViewportWidth);
                Assert.Equal(Math.Min(step * 2, viewer.ScrollableWidth), viewer.HorizontalOffset, 6);
            }
            else
            {
                viewer.UpdateLayout();
                Assert.True(viewer.HorizontalOffset > 0);
            }

            double beforeReversal = viewer.HorizontalOffset;
            Assert.True(viewer.Wheel(120).Handled);
            Advance(viewer, 1000);
            Assert.True(viewer.HorizontalOffset < beforeReversal);
            Assert.Equal(0, viewer.VerticalOffset);
        }));
    }

    [Theory]
    [InlineData(ScrollSpillMode.Enabled, false)]
    [InlineData(ScrollSpillMode.Disabled, true)]
    public void HorizontalWheel_PendingBoundaryUsesSpillPolicy(ScrollSpillMode mode, bool handled)
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            ConfigureHorizontalWheel(viewer);
            viewer.ScrollSpillMode = mode;
            Assert.True(viewer.Wheel(-120000).Handled);
            Assert.Equal(0, viewer.HorizontalOffset);
            Assert.Equal(handled, viewer.Wheel(-120).Handled);
            Assert.True(viewer.IsSmoothScrolling);
            Advance(viewer, 1000);
            Assert.Equal(viewer.ScrollableWidth, viewer.HorizontalOffset);
            Assert.Equal(handled, viewer.Wheel(-120).Handled);
        }));
    }

    [Fact]
    public void HorizontalWheel_DefaultNativeAndSuppressedRangesDoNotRedirect()
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            Assert.False(viewer.IsHorizontalMouseWheelEnabled);
            ConfigureHorizontalWheel(viewer);
            viewer.IsHorizontalMouseWheelEnabled = false;
            viewer.Wheel(-120);
            Advance(viewer, 1000);
            Assert.Equal(0, viewer.HorizontalOffset);

            viewer.IsHorizontalMouseWheelEnabled = true;
            viewer.ScrollSpillMode = ScrollSpillMode.Native;
            Assert.True(viewer.Wheel(-120).Handled);
            Advance(viewer, 1000);
            Assert.Equal(0, viewer.HorizontalOffset);

            viewer.ScrollSpillMode = ScrollSpillMode.Enabled;
            viewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            viewer.HorizontalScrollBarVisibilityThreshold = 10000;
            viewer.UpdateLayout();
            Assert.False(viewer.Wheel(-120).Handled);
            Assert.False(viewer.IsSmoothScrolling);
            Assert.Equal(0, viewer.HorizontalOffset);
        }));
    }

    [Fact]
    public void HorizontalWheel_VerticalRangeRetainsPriorityAtBoundary()
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            ConfigureHorizontalWheel(viewer);
            viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            ((Border)viewer.Content).Height = 2000;
            viewer.UpdateLayout();
            viewer.Wheel(-120);
            Advance(viewer, 1000);
            Assert.True(viewer.VerticalOffset > 0);
            Assert.Equal(0, viewer.HorizontalOffset);
            viewer.ScrollToBottom();
            viewer.UpdateLayout();
            Assert.False(viewer.Wheel(-120).Handled);
            Advance(viewer, 1000);
            Assert.Equal(0, viewer.HorizontalOffset);

            ((Border)viewer.Content).Height = 50;
            viewer.UpdateLayout();
            Assert.True(viewer.Wheel(-120).Handled);
            Advance(viewer, 16);
            Assert.True(viewer.HorizontalOffset > 0);
            viewer.IsHorizontalMouseWheelEnabled = false;
            Assert.False(viewer.IsSmoothScrolling);
        }));
    }

    [Theory]
    [InlineData(ScrollUnit.Pixel, true)]
    [InlineData(ScrollUnit.Item, false)]
    public void HorizontalWheel_VirtualizedItemsRespectProviderUnitsAndOwnerSetting(ScrollUnit unit, bool smooth)
    {
        WpfTestThread.Run(() =>
        {
            var viewerFactory = new FrameworkElementFactory(typeof(TestViewer));
            viewerFactory.SetValue(System.Windows.Controls.ScrollViewer.CanContentScrollProperty, true);
            viewerFactory.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
            var panelFactory = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
            panelFactory.SetValue(VirtualizingStackPanel.OrientationProperty, Orientation.Horizontal);
            var items = new System.Windows.Controls.ListBox
            {
                Template = new ControlTemplate(typeof(System.Windows.Controls.ListBox)) { VisualTree = viewerFactory },
                ItemsPanel = new ItemsPanelTemplate(panelFactory),
                ItemsSource = Enumerable.Range(0, 2000).ToArray(),
                ItemContainerStyle = new Style(typeof(System.Windows.Controls.ListBoxItem))
                {
                    Setters = { new Setter(FrameworkElement.WidthProperty, 40d) },
                },
            };
            System.Windows.Controls.ScrollViewer.SetVerticalScrollBarVisibility(items, ScrollBarVisibility.Disabled);
            System.Windows.Controls.ScrollViewer.SetHorizontalScrollBarVisibility(items, ScrollBarVisibility.Hidden);
            PassiveScrollViewer.SetIsHorizontalMouseWheelEnabled(items, true);
            VirtualizingPanel.SetScrollUnit(items, unit);
            WithWindow(items, () =>
            {
                var viewer = FindDescendant<TestViewer>(items)!;
                Assert.True(viewer.IsHorizontalMouseWheelEnabled);
                Assert.True(viewer.Wheel(-120).Handled);
                Assert.Equal(smooth, viewer.IsSmoothScrolling);
                Advance(viewer, 1000);
                Assert.True(viewer.HorizontalOffset > 0);
                Assert.Equal(0, viewer.VerticalOffset);
                Assert.InRange(FindDescendant<VirtualizingStackPanel>(items)!.Children.Count, 1, 100);
                PassiveScrollViewer.SetIsHorizontalMouseWheelEnabled(items, false);
                Assert.False(viewer.IsHorizontalMouseWheelEnabled);
            });
        });
    }

    private static void ConfigureHorizontalWheel(TestViewer viewer)
    {
        viewer.IsHorizontalMouseWheelEnabled = true;
        viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        viewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        viewer.Content = new Border { Width = 2000, Height = 50 };
        viewer.UpdateLayout();
    }

    [Theory]
    [InlineData(ScrollSpillMode.Enabled, true)]
    [InlineData(ScrollSpillMode.Disabled, false)]
    public void HorizontalWheel_BubblesToVerticalParentAtPendingBoundary(ScrollSpillMode mode, bool parentMoves)
    {
        WpfTestThread.Run(() =>
        {
            var child = new TestViewer { Height = 100, ScrollSpillMode = mode };
            ConfigureHorizontalWheel(child);
            var content = new StackPanel();
            content.Children.Add(child);
            content.Children.Add(new Border { Height = 2000 });
            var parent = new TestViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            };
            WithWindow(parent, () =>
            {
                child.Wheel(-120000);
                Assert.True(child.IsSmoothScrolling);
                Assert.Equal(0, child.HorizontalOffset);
                ((Border)child.Content).RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                });
                Assert.Equal(parentMoves, parent.IsSmoothScrolling);
                Advance(parent, 1000);
                Assert.Equal(parentMoves, parent.VerticalOffset > 0);
            });
        });
    }

    [Fact]
    public void Motion_PreservesVelocityAcrossDetentsAndClearsItOnReversal()
    {
        var motion = new SmoothScrollMotion(100);
        Assert.True(motion.MoveBy(48, 1000));
        Assert.True(motion.Advance(TimeSpan.FromMilliseconds(40), Response));
        double position = motion.Position;
        double velocity = motion.Velocity;

        Assert.True(motion.MoveBy(48, 1000));
        Assert.Equal(position, motion.Position);
        Assert.Equal(velocity, motion.Velocity);
        Assert.Equal(196, motion.Target);
        motion.Advance(TimeSpan.FromMilliseconds(10), Response);
        Assert.True(motion.Velocity > velocity);

        position = motion.Position;
        Assert.True(motion.MoveBy(-24, 1000));
        Assert.Equal(position - 24, motion.Target);
        Assert.Equal(0, motion.Velocity);
        motion.Advance(TimeSpan.FromMilliseconds(16), Response);
        Assert.True(motion.Position < position);
    }

    [Fact]
    public void Motion_UsesElapsedTimeAndSettlesWithoutOvershootAfterStall()
    {
        var oneFrame = new SmoothScrollMotion(0);
        var manyFrames = new SmoothScrollMotion(0);
        oneFrame.MoveBy(500, 1000);
        manyFrames.MoveBy(500, 1000);
        oneFrame.Advance(TimeSpan.FromMilliseconds(100), Response);
        for (int frame = 0; frame < 10; frame++)
        {
            manyFrames.Advance(TimeSpan.FromMilliseconds(10), Response);
        }

        Assert.Equal(oneFrame.Position, manyFrames.Position, 9);
        Assert.Equal(oneFrame.Velocity, manyFrames.Velocity, 9);
        Assert.False(manyFrames.Advance(TimeSpan.FromHours(1), Response));
        Assert.Equal(500, manyFrames.Position);
        Assert.Equal(0, manyFrames.Velocity);
    }

    [Fact]
    public void Motion_ClampsPendingDestinationAndDiscardsRemovedRange()
    {
        var motion = new SmoothScrollMotion(100);
        Assert.True(motion.MoveBy(1000, 200));
        Assert.False(motion.MoveBy(48, 200));
        motion.Advance(TimeSpan.FromMilliseconds(16), Response);
        motion.ClampToRange(100);
        Assert.False(motion.Advance(TimeSpan.FromMilliseconds(16), Response));
        Assert.Equal(100, motion.Target);
        motion.ClampToRange(1000);
        Assert.Equal(100, motion.Target);
    }

    [Theory]
    [InlineData(-120, 3, 200, 48)]
    [InlineData(-30, 3, 200, 12)]
    [InlineData(240, 3, 200, -96)]
    [InlineData(-120, -1, 200, 200)]
    [InlineData(-120, 0, 200, 0)]
    public void WheelDistance_RespectsDeltaMagnitudeAndWindowsPreference(int delta, int lines, double viewport, double expected)
    {
        Assert.Equal(expected, PassiveScrollViewer.GetSmoothWheelDistance(delta, lines, viewport));
    }

    [Fact]
    public void Viewer_PresentationClockIgnoresDuplicateFramesAndResetsAfterCancellation()
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            viewer.Wheel(-120);
            viewer.AdvanceSmoothScrollingTo(TimeSpan.FromSeconds(10));
            Assert.Equal(0, viewer.VerticalOffset);
            viewer.AdvanceSmoothScrollingTo(TimeSpan.FromSeconds(10) + TimeSpan.FromMilliseconds(16));
            viewer.UpdateLayout();
            double firstOffset = viewer.VerticalOffset;
            Assert.True(firstOffset > 0);

            viewer.AdvanceSmoothScrollingTo(TimeSpan.FromSeconds(10) + TimeSpan.FromMilliseconds(16));
            viewer.AdvanceSmoothScrollingTo(TimeSpan.FromSeconds(9));
            viewer.UpdateLayout();
            Assert.Equal(firstOffset, viewer.VerticalOffset);

            viewer.InvokePreviewMouseDown();
            viewer.Wheel(-120);
            viewer.AdvanceSmoothScrollingTo(TimeSpan.FromSeconds(20));
            viewer.UpdateLayout();
            Assert.Equal(firstOffset, viewer.VerticalOffset);
            viewer.AdvanceSmoothScrollingTo(TimeSpan.FromSeconds(20) + TimeSpan.FromMilliseconds(16));
            viewer.UpdateLayout();
            Assert.Equal(firstOffset * 2, viewer.VerticalOffset, 8);
        }));
    }

    [Theory]
    [InlineData(2500d)]
    [InlineData(1500d)]
    public void Viewer_SizeChangeDoesNotUndoBatchedProgrammaticScroll(double height)
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            viewer.ScrollToVerticalOffset(100);
            viewer.UpdateLayout();
            ((Border)viewer.Content).Height = height;
            viewer.ScrollToVerticalOffset(300);
            viewer.UpdateLayout();
            Assert.Equal(300, viewer.VerticalOffset);
        }));
    }

    [Fact]
    public void Viewer_MovesRealOffsetOverFramesAndAccumulatesFractionalInput()
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            double step = PassiveScrollViewer.GetSmoothWheelDistance(-120, SystemParameters.WheelScrollLines, viewer.ViewportHeight);
            viewer.Wheel(-30);
            viewer.Wheel(-30);
            viewer.Wheel(-30);
            viewer.Wheel(-30);
            Assert.True(viewer.IsSmoothScrolling);
            Assert.Equal(0, viewer.VerticalOffset);

            Advance(viewer, 16);
            Assert.InRange(viewer.VerticalOffset, 0.001, step - 0.001);
            viewer.Wheel(-240);
            Advance(viewer, 1000);
            Assert.Equal(step * 3, viewer.VerticalOffset, 6);
            Assert.False(viewer.IsSmoothScrolling);
        }));
    }

    [Fact]
    public void Viewer_RenderingLoopPublishesIntermediateOffsetsAndCompletes()
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            var frame = new DispatcherFrame();
            var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            int changes = 0;
            bool timedOut = false;
            timeout.Tick += (_, _) =>
            {
                timedOut = true;
                frame.Continue = false;
            };
            viewer.ScrollChanged += (_, e) =>
            {
                if (e.VerticalChange != 0)
                {
                    changes++;
                    if (!viewer.IsSmoothScrolling)
                    {
                        frame.Continue = false;
                    }
                }
            };

            viewer.Wheel(-120);
            timeout.Start();
            try
            {
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                timeout.Stop();
            }

            Assert.False(timedOut);
            Assert.True(changes > 1);
            Assert.False(viewer.IsSmoothScrolling);
            Assert.Equal(
                PassiveScrollViewer.GetSmoothWheelDistance(-120, SystemParameters.WheelScrollLines, viewer.ViewportHeight),
                viewer.VerticalOffset,
                6
            );
        }));
    }

    [Theory]
    [InlineData(Orientation.Vertical, false, 1)]
    [InlineData(Orientation.Vertical, false, 4)]
    [InlineData(Orientation.Vertical, true, 1)]
    [InlineData(Orientation.Vertical, true, 3)]
    [InlineData(Orientation.Horizontal, false, 1)]
    [InlineData(Orientation.Horizontal, false, 4)]
    [InlineData(Orientation.Horizontal, true, 1)]
    [InlineData(Orientation.Horizontal, true, 3)]
    public void Viewer_TemplateScrollBarLineAndPageRepeatsAccumulateSmoothMotion(
        Orientation orientation,
        bool page,
        int repetitions
    )
    {
        WpfTestThread.Run(() =>
        {
            var viewer = new TestViewer
            {
                Content = new Border { Width = 2000, Height = 2000 },
                HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            };
            WithWindow(viewer, () =>
            {
                string partName = orientation == Orientation.Vertical
                    ? "PART_VerticalScrollBar"
                    : "PART_HorizontalScrollBar";
                var scrollBar = Assert.IsAssignableFrom<ScrollBar>(
                    viewer.Template.FindName(partName, viewer)
                );
                scrollBar.ApplyTemplate();

                RepeatButton source;
                RoutedCommand command;
                if (page)
                {
                    var track = Assert.IsType<Track>(scrollBar.Template.FindName("PART_Track", scrollBar));
                    source = Assert.IsType<RepeatButton>(track.IncreaseRepeatButton);
                    command = orientation == Orientation.Vertical
                        ? ScrollBar.PageDownCommand
                        : ScrollBar.PageRightCommand;
                }
                else
                {
                    string buttonName = orientation == Orientation.Vertical ? "DownButton" : "RightButton";
                    source = Assert.IsType<RepeatButton>(scrollBar.Template.FindName(buttonName, scrollBar));
                    command = orientation == Orientation.Vertical
                        ? ScrollBar.LineDownCommand
                        : ScrollBar.LineRightCommand;
                }

                double viewport = orientation == Orientation.Vertical
                    ? viewer.ViewportHeight
                    : viewer.ViewportWidth;
                for (int repeat = 0; repeat < repetitions; repeat++)
                {
                    command.Execute(null, source);
                }

                Assert.True(viewer.IsSmoothScrolling);
                Assert.Equal(0, orientation == Orientation.Vertical
                    ? viewer.VerticalOffset
                    : viewer.HorizontalOffset);

                Advance(viewer, 1000);
                double expected = (page ? viewport : 16d) * repetitions;
                Assert.Equal(
                    expected,
                    orientation == Orientation.Vertical
                        ? viewer.VerticalOffset
                        : viewer.HorizontalOffset,
                    6
                );
                Assert.False(viewer.IsSmoothScrolling);
            });
        });
    }

    [Fact]
    public void Viewer_KeyboardScrollBarCommandUsesSmoothMotion()
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            ScrollBar.LineDownCommand.Execute(null, viewer);

            Assert.True(viewer.IsSmoothScrolling);
            Assert.Equal(0, viewer.VerticalOffset);

            Advance(viewer, 1000);
            Assert.False(viewer.IsSmoothScrolling);
            Assert.Equal(16, viewer.VerticalOffset, 6);
        }));
    }

    [Theory]
    [InlineData(Key.Down, Orientation.Vertical, 16d, 1)]
    [InlineData(Key.Down, Orientation.Vertical, 64d, 4)]
    [InlineData(Key.Right, Orientation.Horizontal, 16d, 1)]
    [InlineData(Key.Right, Orientation.Horizontal, 64d, 4)]
    [InlineData(Key.PageDown, Orientation.Vertical, -1d, 1)]
    [InlineData(Key.PageDown, Orientation.Vertical, -1d, 3)]
    public void Viewer_KeyboardScrollRepeatsAccumulateSmoothMotion(
        Key key,
        Orientation orientation,
        double expectedDistance,
        int repetitions
    )
    {
        WpfTestThread.Run(() =>
        {
            var viewer = new TestViewer
            {
                Content = new Border { Width = 2000, Height = 2000 },
                HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            };
            WithWindow(viewer, () =>
            {
                double viewport = orientation == Orientation.Vertical
                    ? viewer.ViewportHeight
                    : viewer.ViewportWidth;
                for (int repeat = 0; repeat < repetitions; repeat++)
                {
                    Assert.True(viewer.Key(key).Handled);
                }

                Assert.True(viewer.IsSmoothScrolling);
                Assert.Equal(0, orientation == Orientation.Vertical
                    ? viewer.VerticalOffset
                    : viewer.HorizontalOffset);

                Advance(viewer, 1000);
                double expected = expectedDistance < 0
                    ? viewport * repetitions
                    : expectedDistance;
                Assert.Equal(
                    expected,
                    orientation == Orientation.Vertical
                        ? viewer.VerticalOffset
                        : viewer.HorizontalOffset,
                    6
                );
                Assert.False(viewer.IsSmoothScrolling);
            });
        });
    }

    [Theory]
    [InlineData(Key.PageUp)]
    [InlineData(Key.PageDown)]
    [InlineData(Key.Home)]
    [InlineData(Key.End)]
    public void Viewer_DescendantNavigationKeysRemainAvailable(Key key)
    {
        WpfTestThread.Run(() =>
        {
            var child = new System.Windows.Controls.TextBox { Width = 2000, Height = 2000 };
            var viewer = new TestViewer
            {
                Content = child,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
            };
            WithWindow(viewer, () =>
            {
                viewer.ScrollToVerticalOffset(300);
                viewer.ScrollToHorizontalOffset(300);
                viewer.UpdateLayout();
                bool reachedChild = false;
                child.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, e) =>
                {
                    reachedChild = true;
                    e.Handled = true;
                }), true);

                var args = new KeyEventArgs(Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(child), 0, key)
                { RoutedEvent = UIElement.PreviewKeyDownEvent };
                child.RaiseEvent(args);
                Assert.False(args.Handled);
                args.RoutedEvent = UIElement.KeyDownEvent;
                child.RaiseEvent(args);

                Assert.True(reachedChild);
                Assert.False(viewer.IsSmoothScrolling);
                Assert.Equal(300, viewer.VerticalOffset);
                Assert.Equal(300, viewer.HorizontalOffset);
            });
        });
    }

    [Fact]
    public void Viewer_NestedViewerOwnsPageKey()
    {
        WpfTestThread.Run(() =>
        {
            var inner = new TestViewer
            {
                Content = new Border { Height = 2000 },
                Height = 500,
            };
            var outer = new TestViewer { Content = inner };
            WithWindow(outer, () =>
            {
                Assert.True(inner.Key(Key.PageDown).Handled);
                Assert.True(inner.IsSmoothScrolling);
                Assert.False(outer.IsSmoothScrolling);
                Advance(inner, 1000);
                Assert.Equal(inner.ViewportHeight, inner.VerticalOffset, 6);
                Assert.Equal(0, outer.VerticalOffset);
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Viewer_ContentElementCommandsRetainNativeRouting(bool scrollingCommand)
    {
        WpfTestThread.Run(() =>
        {
            var link = new Hyperlink(new Run("link"));
            var text = new System.Windows.Controls.TextBlock { Height = 2000 };
            text.Inlines.Add(link);
            var viewer = new TestViewer { Content = text };
            WithWindow(viewer, () =>
            {
                RoutedCommand command = scrollingCommand ? ScrollBar.PageDownCommand : new RoutedCommand();
                bool executed = false;
                link.CommandBindings.Add(new CommandBinding(command, (_, e) =>
                {
                    executed = true;
                    e.Handled = true;
                }));

                command.Execute(null, link);

                Assert.True(executed);
                Assert.False(viewer.IsSmoothScrolling);
                Assert.Equal(0, viewer.VerticalOffset);
            });
        });
    }

    [Theory]
    [InlineData(ScrollSpillMode.Enabled, false)]
    [InlineData(ScrollSpillMode.Disabled, true)]
    public void Viewer_UsesPendingBoundaryForSpillAndAcceptsReverseInput(ScrollSpillMode mode, bool handled)
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            viewer.ScrollSpillMode = mode;
            Assert.True(viewer.Wheel(-120000).Handled);
            Assert.Equal(0, viewer.VerticalOffset);
            Assert.Equal(handled, viewer.Wheel(-120).Handled);
            Advance(viewer, 16);
            double position = viewer.VerticalOffset;
            Assert.True(viewer.Wheel(120).Handled);
            Advance(viewer, 16);
            Assert.True(viewer.VerticalOffset < position);
        }));
    }

    [Theory]
    [InlineData("setting")]
    [InlineData("system")]
    [InlineData("native")]
    public void Viewer_NativeFallbackKeepsImmediateScrolling(string reason)
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            viewer.IsSmoothScrollingEnabled = reason != "setting";
            viewer.AllowAnimations = reason != "system";
            viewer.ScrollSpillMode = reason == "native" ? ScrollSpillMode.Native : ScrollSpillMode.Enabled;
            viewer.Wheel(-120);
            viewer.UpdateLayout();
            Assert.False(viewer.IsSmoothScrolling);
            Assert.True(viewer.VerticalOffset > 0);
        }));
    }

    [Fact]
    public void Viewer_HandledAndZeroDeltaInputDoNotStartMotion()
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            Assert.True(viewer.Wheel(-120, handled: true).Handled);
            Assert.False(viewer.Wheel(0).Handled);
            Assert.False(viewer.IsSmoothScrolling);
            Assert.Equal(0, viewer.VerticalOffset);
        }));
    }

    [Theory]
    [InlineData("programmatic")]
    [InlineData("mouse")]
    [InlineData("key")]
    [InlineData("bringIntoView")]
    [InlineData("disable")]
    [InlineData("duration")]
    [InlineData("hide")]
    [InlineData("unload")]
    public void Viewer_CancelsMotionWhenAnotherOperationTakesOwnership(string operation)
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            viewer.Wheel(-120);
            Advance(viewer, 16);
            Assert.True(viewer.IsSmoothScrolling);

            switch (operation)
            {
                case "programmatic":
                    viewer.ScrollToVerticalOffset(300);
                    break;
                case "mouse":
                    viewer.InvokePreviewMouseDown();
                    break;
                case "key":
                    viewer.Key(Key.A);
                    break;
                case "bringIntoView":
                    ((FrameworkElement)viewer.Content).BringIntoView(new Rect(0, 300, 10, 10));
                    break;
                case "disable":
                    viewer.IsSmoothScrollingEnabled = false;
                    break;
                case "duration":
                    viewer.SmoothScrollDuration = TimeSpan.FromMilliseconds(100);
                    break;
                case "hide":
                    viewer.Visibility = Visibility.Collapsed;
                    break;
                case "unload":
                    Window.GetWindow(viewer).Content = null;
                    viewer.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                    break;
            }

            viewer.UpdateLayout();
            Assert.False(viewer.IsSmoothScrolling);
            double position = viewer.VerticalOffset;
            Advance(viewer, 1000);
            Assert.Equal(position, viewer.VerticalOffset);
            if (operation == "programmatic")
            {
                Assert.Equal(300, position);
            }
        }));
    }

    [Fact]
    public void Viewer_FinalFrameSurvivesSimultaneousExtentChange()
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            viewer.Wheel(-120);
            Advance(viewer, 16);
            ((Border)viewer.Content).Height = 3000;
            Advance(viewer, 1000);
            Assert.False(viewer.IsSmoothScrolling);
            Assert.Equal(
                PassiveScrollViewer.GetSmoothWheelDistance(-120, SystemParameters.WheelScrollLines, viewer.ViewportHeight),
                viewer.VerticalOffset,
                6
            );
        }));
    }

    [Fact]
    public void Viewer_ProgrammaticScrollTakesOwnershipDuringExtentChange()
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            viewer.Wheel(-120);
            Advance(viewer, 16);
            ((Border)viewer.Content).Height = 3000;
            viewer.ScrollToVerticalOffset(300);
            viewer.UpdateLayout();
            Assert.False(viewer.IsSmoothScrolling);
            Advance(viewer, 1000);
            Assert.Equal(300, viewer.VerticalOffset);
        }));
    }

    [Fact]
    public void Viewer_ContentShrinkAndGrowthCannotRestoreStaleDestination()
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            viewer.Wheel(-120000);
            Advance(viewer, 16);
            var content = (Border)viewer.Content;
            content.Height = 50;
            viewer.UpdateLayout();
            Advance(viewer, 16);
            Assert.Equal(0, viewer.VerticalOffset);
            Assert.False(viewer.IsSmoothScrolling);
            content.Height = 2000;
            viewer.UpdateLayout();
            Advance(viewer, 1000);
            Assert.Equal(0, viewer.VerticalOffset);
        }));
    }

    [Theory]
    [InlineData(ScrollUnit.Pixel, true, false)]
    [InlineData(ScrollUnit.Item, false, false)]
    [InlineData(ScrollUnit.Pixel, true, true)]
    [InlineData(ScrollUnit.Item, false, true)]
    public void Viewer_VirtualizingPanelsRespectUnitsAndKeepVirtualization(ScrollUnit unit, bool smooth, bool wrap)
    {
        WpfTestThread.Run(() =>
        {
            var viewerFactory = new FrameworkElementFactory(typeof(TestViewer));
            viewerFactory.SetValue(System.Windows.Controls.ScrollViewer.CanContentScrollProperty, true);
            viewerFactory.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
            var panelFactory = new FrameworkElementFactory(wrap ? typeof(VirtualizingWrapPanel) : typeof(VirtualizingStackPanel));
            if (wrap)
            {
                panelFactory.SetValue(VirtualizingWrapPanel.ItemWidthProperty, 80d);
                panelFactory.SetValue(VirtualizingWrapPanel.ItemHeightProperty, 24d);
            }

            var items = new System.Windows.Controls.ListBox
            {
                Template = new ControlTemplate(typeof(System.Windows.Controls.ListBox)) { VisualTree = viewerFactory },
                ItemsSource = Enumerable.Range(0, 2000).ToArray(),
                ItemsPanel = new ItemsPanelTemplate(panelFactory),
                ItemContainerStyle = new Style(typeof(System.Windows.Controls.ListBoxItem))
                {
                    Setters = { new Setter(FrameworkElement.HeightProperty, 24d) },
                },
            };
            VirtualizingPanel.SetScrollUnit(items, unit);
            VirtualizingPanel.SetIsVirtualizing(items, true);
            VirtualizingPanel.SetVirtualizationMode(items, VirtualizationMode.Recycling);
            WithWindow(items, () =>
            {
                var viewer = FindDescendant<TestViewer>(items)!;
                var panel = FindDescendant<VirtualizingPanel>(items)!;
                Assert.NotNull(viewer);
                Assert.NotNull(panel);
                viewer.Wheel(-120);
                Assert.Equal(smooth, viewer.IsSmoothScrolling);
                Advance(viewer, 16);
                Assert.True(viewer.VerticalOffset > 0);
                if (smooth)
                {
                    Assert.True(viewer.IsSmoothScrolling);
                    Advance(viewer, 1000);
                    Assert.Equal(SystemParameters.WheelScrollLines * 16d, viewer.VerticalOffset, 6);
                }

                Assert.InRange(panel.Children.Count, 1, 100);
                Assert.True(viewer.CanContentScroll);
                Assert.Equal(unit, VirtualizingPanel.GetScrollUnit(items));
            });
        });
    }

    [Fact]
    public void Viewer_NestedWheelRouteSpillsToParentAtPendingBoundary()
    {
        WpfTestThread.Run(() =>
        {
            var inner = new TestViewer
            {
                Height = 100,
                Content = new Border { Height = 1000 },
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            };
            var content = new StackPanel();
            content.Children.Add(inner);
            content.Children.Add(new Border { Height = 2000 });
            var outer = new TestViewer { Content = content };
            WithWindow(outer, () =>
            {
                var first = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120000) { RoutedEvent = UIElement.MouseWheelEvent };
                ((UIElement)inner.Content).RaiseEvent(first);
                Assert.True(first.Handled);
                Assert.True(inner.IsSmoothScrolling);
                Assert.False(outer.IsSmoothScrolling);

                var second = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.MouseWheelEvent };
                ((UIElement)inner.Content).RaiseEvent(second);
                Assert.True(second.Handled);
                Assert.True(outer.IsSmoothScrolling);
                Advance(inner, 1000);
                Advance(outer, 1000);
                Assert.Equal(inner.ScrollableHeight, inner.VerticalOffset);
                Assert.True(outer.VerticalOffset > 0);
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Viewer_VariableHeightVirtualizationKeepsMovingAcrossRoundedFrames(bool rounding)
    {
        WpfTestThread.Run(() =>
        {
            var viewerFactory = new FrameworkElementFactory(typeof(TestViewer));
            viewerFactory.SetValue(System.Windows.Controls.ScrollViewer.CanContentScrollProperty, true);
            viewerFactory.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
            var items = new System.Windows.Controls.ListBox
            {
                UseLayoutRounding = rounding,
                Template = new ControlTemplate(typeof(System.Windows.Controls.ListBox)) { VisualTree = viewerFactory },
                ItemsSource = Enumerable.Range(0, 2000).Select(index => new { Index = index, Height = 18d + ((index % 7) * 6.3) }).ToArray(),
                ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel))),
                ItemContainerStyle = new Style(typeof(System.Windows.Controls.ListBoxItem))
                {
                    Setters = { new Setter(FrameworkElement.HeightProperty, new System.Windows.Data.Binding("Height")) },
                },
            };
            VirtualizingPanel.SetScrollUnit(items, ScrollUnit.Pixel);
            VirtualizingPanel.SetVirtualizationMode(items, VirtualizationMode.Recycling);
            WithWindow(items, () =>
            {
                var viewer = FindDescendant<TestViewer>(items)!;
                var panel = FindDescendant<VirtualizingStackPanel>(items)!;
                var expectedMotion = new SmoothScrollMotion(viewer.VerticalOffset);
                for (int input = 0; input < 40; input++)
                {
                    expectedMotion.MoveBy(
                        PassiveScrollViewer.GetSmoothWheelDistance(-30, SystemParameters.WheelScrollLines, viewer.ViewportHeight),
                        viewer.ScrollableHeight);
                    expectedMotion.Advance(TimeSpan.FromMilliseconds(16), Response);
                    viewer.Wheel(-30);
                    Advance(viewer, 16);
                    Assert.True(viewer.IsSmoothScrolling);
                    // Extent refinement may round the displayed offset, but must not accumulate
                    // rounding error in the continuous trajectory across successive frames.
                    double tolerance = rounding ? 0.51 / VisualTreeHelper.GetDpi(viewer).DpiScaleY : 0.01;
                    Assert.InRange(viewer.VerticalOffset, expectedMotion.Position - tolerance, expectedMotion.Position + tolerance);
                }

                // A rounded final write can produce no ScrollChanged. One extra frame must still
                // release the rendering subscription without resetting to an earlier offset.
                Advance(viewer, 1000);
                Advance(viewer, 16);
                Assert.False(viewer.IsSmoothScrolling);
                Assert.True(viewer.VerticalOffset > viewer.ViewportHeight);
                Assert.InRange(panel.Children.Count, 1, 100);

                viewer.Wheel(-120);
                Advance(viewer, 16);
                items.Height = 150;
                viewer.ScrollToVerticalOffset(300);
                viewer.UpdateLayout();
                Assert.False(viewer.IsSmoothScrolling);
                Advance(viewer, 1000);
                Assert.Equal(300, viewer.VerticalOffset);
            });
        });
    }

    [Fact]
    public void Viewer_UnknownLogicalProviderUsesNativeWheelBehavior()
    {
        WpfTestThread.Run(() => WithViewer(viewer =>
        {
            var stack = new StackPanel();
            for (int i = 0; i < 100; i++)
            {
                stack.Children.Add(new Border { Height = 24 });
            }

            viewer.Content = stack;
            viewer.CanContentScroll = true;
            viewer.UpdateLayout();
            viewer.Wheel(-120);
            viewer.UpdateLayout();
            Assert.False(viewer.IsSmoothScrolling);
            Assert.True(viewer.VerticalOffset > 0);
        }));
    }

    [Fact]
    public void Settings_RejectInvalidDuration()
    {
        WpfTestThread.Run(() =>
        {
            var viewer = new TestViewer();
            Assert.True(viewer.IsSmoothScrollingEnabled);
            Assert.Equal(Response, viewer.SmoothScrollDuration);
            Assert.Throws<ArgumentException>(() => viewer.SmoothScrollDuration = TimeSpan.Zero);
            Assert.Throws<ArgumentException>(() => viewer.SmoothScrollDuration = TimeSpan.FromSeconds(11));
        });
    }

    private static void Advance(PassiveScrollViewer viewer, double milliseconds)
    {
        viewer.AdvanceSmoothScrolling(TimeSpan.FromMilliseconds(milliseconds));
        viewer.UpdateLayout();
    }

    private static void WithViewer(Action<TestViewer> action)
    {
        var viewer = new TestViewer
        {
            Content = new Border { Width = 100, Height = 2000 },
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        WithWindow(viewer, () => action(viewer));
    }

    private static void WithWindow(FrameworkElement content, Action action)
    {
        var window = new Window
        {
            Content = content,
            Width = 200,
            Height = 200,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        try
        {
            window.Show();
            content.UpdateLayout();
            action();
        }
        finally
        {
            window.Close();
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    public sealed class TestViewer : PassiveScrollViewer
    {
        public bool AllowAnimations { get; set; } = true;

        protected override bool AreScrollAnimationsEnabled => AllowAnimations;

        public MouseWheelEventArgs Wheel(int delta, bool handled = false)
        {
            var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
            {
                RoutedEvent = MouseWheelEvent,
                Handled = handled,
            };
            OnMouseWheel(args);
            return args;
        }

        public void InvokePreviewMouseDown() => OnPreviewMouseDown(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));

        public KeyEventArgs Key(Key key)
        {
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(this),
                0,
                key
            )
            {
                RoutedEvent = PreviewKeyDownEvent,
                Source = this,
            };
            RaiseEvent(args);
            args.RoutedEvent = UIElement.KeyDownEvent;
            RaiseEvent(args);
            return args;
        }
    }
}
