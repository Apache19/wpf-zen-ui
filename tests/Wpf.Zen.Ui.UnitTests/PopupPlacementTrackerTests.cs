// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Interop;

namespace Wpf.Zen.Ui.UnitTests;

public class PopupPlacementTrackerTests
{
    /// <summary>
    /// Verifies that changing axes or disabling one gap preserves the other gap and base rectangle.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HorizontalGapTracksPlacementAndRestoresRectangle(bool useFlyout)
    {
        WpfTestThread.Run(() =>
        {
            DependencyObject owner = useFlyout ? new Flyout() : new Popup();
            var rectangle = new Rect(10D, 20D, 80D, 40D);
            owner.SetValue(Popup.PlacementRectangleProperty, rectangle);
            owner.SetValue(Popup.PlacementProperty, PlacementMode.Right);

            PopupPlacementGap.SetHorizontal(owner, 6D);
            PopupPlacementGap.SetVertical(owner, 4D);
            Assert.Equal(new Rect(4D, 20D, 92D, 40D), owner.GetValue(Popup.PlacementRectangleProperty));

            owner.SetValue(Popup.PlacementProperty, PlacementMode.Left);
            Assert.Equal(new Rect(4D, 20D, 92D, 40D), owner.GetValue(Popup.PlacementRectangleProperty));
            owner.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            Assert.Equal(new Rect(10D, 16D, 80D, 48D), owner.GetValue(Popup.PlacementRectangleProperty));

            PopupPlacementGap.SetVertical(owner, 0D);
            Assert.Equal(rectangle, owner.GetValue(Popup.PlacementRectangleProperty));
            owner.SetValue(Popup.PlacementProperty, PlacementMode.Right);
            Assert.Equal(new Rect(4D, 20D, 92D, 40D), owner.GetValue(Popup.PlacementRectangleProperty));

            PopupPlacementGap.SetHorizontal(owner, 0D);
            Assert.Equal(rectangle, owner.GetValue(Popup.PlacementRectangleProperty));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnopenedPopupWithPlacementBehaviorCanBeCollected(bool gap)
    {
        WpfTestThread.Run(() =>
        {
            _ = PopupPlacementGap.GetVertical(new Border());
            var reference = CreateCollectiblePopup(gap);
            Drain();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.False(reference.IsAlive);
        });
    }

    // Keep the popup out of the GC assertion's stack frame, including in Debug builds.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateCollectiblePopup(bool gap)
    {
        var popup = new Popup
        {
            PlacementTarget = new Border(),
            Child = new Border(),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        if (gap)
        {
            PopupPlacementGap.SetVertical(popup, 4D);
        }

        return new WeakReference(popup);
    }

    [Theory]
    [InlineData(false, false, 1D)]
    [InlineData(false, true, 1D)]
    [InlineData(true, false, 1D)]
    [InlineData(true, true, 1D)]
    [InlineData(false, false, 1.5D)]
    [InlineData(true, false, 1.5D)]
    public void AlignmentUsesTransformedScreenExtents(bool vertical, bool center, double popupScale)
    {
        WpfTestThread.Run(() =>
        {
            // Load the library policy even when this native-Popup test runs in isolation.
            _ = PopupPlacementGap.GetVertical(new Border());
            var scale = new ScaleTransform(2D, 2D);
            var target = new Border
            {
                Width = 100D, Height = 60D,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                LayoutTransform = scale,
            };
            var child = new Border { Width = 40D, Height = 20D };
            var popup = new Popup
            {
                PlacementTarget = target, Child = child,
                Placement = vertical ? PlacementMode.Right : PlacementMode.Bottom,
                HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Right,
                VerticalAlignment = center ? VerticalAlignment.Center : VerticalAlignment.Bottom,
                LayoutTransform = new ScaleTransform(popupScale, popupScale),
                PopupAnimation = PopupAnimation.None,
            };
            var window = new Window
            {
                Width = 500D, Height = 400D, Content = target,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowActivated = false, ShowInTaskbar = false,
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                popup.IsOpen = true;
                Drain();
                AssertAligned();

                // Neither a changed target transform nor a resized content surface may leave the cached anchor stale.
                scale.ScaleX = 1.5D;
                scale.ScaleY = 1.25D;
                child.Width = 50D;
                child.Height = 30D;
                window.UpdateLayout();
                Drain();
                AssertAligned();

                popup.IsOpen = false;
                Drain();
                popup.IsOpen = true;
                Drain();
                AssertAligned();
            }
            finally
            {
                popup.IsOpen = false;
                window.Close();
            }

            void AssertAligned()
            {
                var fraction = center ? 0.5D : 1D;
                var targetPoint = target.PointToScreen(new Point(
                    target.ActualWidth * fraction, target.ActualHeight * fraction));
                var childPoint = child.PointToScreen(new Point(
                    child.ActualWidth * fraction, child.ActualHeight * fraction));
                Assert.True(Math.Abs(vertical ? childPoint.Y - targetPoint.Y : childPoint.X - targetPoint.X) <= 1.1D, $"Target={targetPoint}; Child={childPoint}; Rect={popup.PlacementRectangle}");
            }
        });
    }

    [Fact]
    public void AlignmentTracksReplacementContentTargetAndBoundRectangle()
    {
        WpfTestThread.Run(() =>
        {
            var source = new Flyout();
            var target = new Border();
            target.Measure(new Size(100D, 40D));
            target.Arrange(new Rect(0D, 0D, 100D, 40D));
            var child = new Border();
            child.Measure(new Size(40D, 20D));
            child.Arrange(new Rect(0D, 0D, 40D, 20D));
            var popup = new Popup
            {
                PlacementTarget = target, Child = child,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            popup.SetBinding(Popup.PlacementRectangleProperty, new Binding(nameof(Flyout.PlacementRectangle))
            {
                Source = source,
            });
            source.PlacementRectangle = new Rect(10D, 5D, 80D, 30D);
            Assert.Equal(new Rect(50D, 5D, 40D, 30D), popup.PlacementRectangle);
            source.PlacementRectangle = new Rect(20D, 5D, 100D, 30D);
            Assert.Equal(new Rect(80D, 5D, 40D, 30D), popup.PlacementRectangle);

            var replacement = new Border();
            replacement.Measure(new Size(20D, 10D));
            replacement.Arrange(new Rect(0D, 0D, 20D, 10D));
            popup.Child = replacement;
            Assert.Equal(new Rect(100D, 5D, 20D, 30D), popup.PlacementRectangle);

            source.PlacementRectangle = Rect.Empty;
            popup.PlacementTarget = replacement;
            Assert.Equal(new Rect(0D, 0D, 20D, 10D), popup.PlacementRectangle);
            popup.HorizontalAlignment = HorizontalAlignment.Stretch;
            Assert.Equal(Rect.Empty, popup.PlacementRectangle);
            Assert.NotNull(BindingOperations.GetBindingExpression(popup, Popup.PlacementRectangleProperty));
        });
    }

    [Fact]
    public void PopupAlignmentDoesNotOverrideFrameworkAlignmentMetadata()
    {
        WpfTestThread.Run(() =>
        {
            Assert.Same(
                FrameworkElement.HorizontalAlignmentProperty.GetMetadata(typeof(FrameworkElement)),
                FrameworkElement.HorizontalAlignmentProperty.GetMetadata(typeof(Popup))
            );
            Assert.Same(
                FrameworkElement.VerticalAlignmentProperty.GetMetadata(typeof(FrameworkElement)),
                FrameworkElement.VerticalAlignmentProperty.GetMetadata(typeof(Popup))
            );
        });
    }

    [Fact]
    public void PopupAlignmentAdjustsAnchorRectangleAndPreservesOffsets()
    {
        WpfTestThread.Run(() =>
        {
            var target = new Border();
            target.Measure(new Size(100D, 40D));
            target.Arrange(new Rect(0D, 0D, 100D, 40D));

            var child = new Border();
            child.Measure(new Size(40D, 20D));
            child.Arrange(new Rect(0D, 0D, 40D, 20D));

            var popup = new Popup
            {
                PlacementTarget = target,
                Child = child,
                Placement = PlacementMode.Bottom,
                HorizontalOffset = 7D,
                VerticalOffset = 3D,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            Assert.Equal(new Rect(30D, 0D, 40D, 40D), popup.PlacementRectangle);
            Assert.Equal(7D, popup.HorizontalOffset);
            Assert.Equal(3D, popup.VerticalOffset);

            popup.HorizontalAlignment = HorizontalAlignment.Right;
            Assert.Equal(new Rect(60D, 0D, 40D, 40D), popup.PlacementRectangle);

            popup.HorizontalAlignment = HorizontalAlignment.Stretch;
            Assert.Equal(Rect.Empty, popup.PlacementRectangle);
        });
    }

    [Fact]
    public void PopupAlignmentAndOffsetsProduceExpectedScreenPosition()
    {
        WpfTestThread.Run(() =>
        {
            var target = new Border
            {
                Width = 100D,
                Height = 40D,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var child = new Border { Width = 40D, Height = 20D };
            var popup = new Popup
            {
                PlacementTarget = target,
                Child = child,
                Placement = PlacementMode.Bottom,
                PopupAnimation = PopupAnimation.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalOffset = 7D,
                VerticalOffset = 3D,
            };
            var window = new Window
            {
                Width = 300D,
                Height = 240D,
                Content = target,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowActivated = false,
                ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                popup.IsOpen = true;
                Drain();

                var dpi = VisualTreeHelper.GetDpi(window);
                var targetTopLeft = target.PointToScreen(default);
                var popupTopLeft = child.PointToScreen(default);
                var alignedOffset = ((target.ActualWidth - child.ActualWidth) / 2D)
                    + popup.HorizontalOffset;
                var expectedX = targetTopLeft.X + (alignedOffset * dpi.DpiScaleX);
                var expectedY = targetTopLeft.Y
                    + ((target.ActualHeight + popup.VerticalOffset) * dpi.DpiScaleY);

                Assert.InRange(Math.Abs(popupTopLeft.X - expectedX), 0D, 1.1D);
                Assert.InRange(Math.Abs(popupTopLeft.Y - expectedY), 0D, 1.1D);
            }
            finally
            {
                popup.IsOpen = false;
                window.Close();
            }
        });
    }

    [Fact]
    public void VerticalPopupAlignmentUsesPlacementTargetHeight()
    {
        WpfTestThread.Run(() =>
        {
            var target = new Border();
            target.Measure(new Size(100D, 60D));
            target.Arrange(new Rect(0D, 0D, 100D, 60D));

            var child = new Border();
            child.Measure(new Size(30D, 20D));
            child.Arrange(new Rect(0D, 0D, 30D, 20D));

            var popup = new Popup
            {
                PlacementTarget = target,
                Child = child,
                Placement = PlacementMode.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                VerticalOffset = -5D,
            };

            Assert.Equal(new Rect(0D, 40D, 100D, 20D), popup.PlacementRectangle);
            Assert.Equal(-5D, popup.VerticalOffset);
        });
    }

    [Fact]
    public void FlyoutAlignmentAdjustsItsPopupPlacementContractAndPreservesOffsets()
    {
        WpfTestThread.Run(() =>
        {
            var target = new Border();
            target.Measure(new Size(120D, 50D));
            target.Arrange(new Rect(0D, 0D, 120D, 50D));

            var flyout = new Flyout
            {
                Width = 40D,
                Height = 20D,
                PlacementTarget = target,
                Placement = PlacementMode.Bottom,
                HorizontalOffset = 6D,
                VerticalOffset = 3D,
            };
            flyout.Measure(new Size(40D, 20D));
            flyout.Arrange(new Rect(0D, 0D, 40D, 20D));

            flyout.HorizontalAlignment = HorizontalAlignment.Right;

            Assert.Equal(new Rect(80D, 0D, 40D, 50D), flyout.PlacementRectangle);
            Assert.Equal(6D, flyout.HorizontalOffset);
            Assert.Equal(3D, flyout.VerticalOffset);

            flyout.HorizontalAlignment = HorizontalAlignment.Stretch;
            Assert.Equal(Rect.Empty, flyout.PlacementRectangle);
        });
    }

    [Fact]
    public void ContextMenuAlignmentComposesWithExplicitPlacementRectangle()
    {
        WpfTestThread.Run(() =>
        {
            var target = new Border();
            target.Measure(new Size(120D, 50D));
            target.Arrange(new Rect(0D, 0D, 120D, 50D));

            var contextMenu = new ContextMenu
            {
                Width = 40D,
                Height = 20D,
                PlacementTarget = target,
                Placement = PlacementMode.Bottom,
                PlacementRectangle = new Rect(10D, 5D, 80D, 30D),
                HorizontalOffset = 6D,
            };
            contextMenu.Measure(new Size(40D, 20D));
            contextMenu.Arrange(new Rect(0D, 0D, 40D, 20D));
            contextMenu.HorizontalAlignment = HorizontalAlignment.Right;
            contextMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, contextMenu));

            Assert.Equal(HorizontalAlignment.Right, contextMenu.HorizontalAlignment);
            Assert.Equal(new Rect(50D, 5D, 40D, 30D), contextMenu.PlacementRectangle);
            Assert.Equal(6D, contextMenu.HorizontalOffset);
        });
    }

    [Fact]
    public void PopupAlignmentComposesWithDirectionAwareGap()
    {
        WpfTestThread.Run(() =>
        {
            var target = new Border();
            target.Measure(new Size(100D, 40D));
            target.Arrange(new Rect(0D, 0D, 100D, 40D));

            var child = new Border();
            child.Measure(new Size(40D, 20D));
            child.Arrange(new Rect(0D, 0D, 40D, 20D));

            var popup = new Popup
            {
                PlacementTarget = target,
                Child = child,
                Placement = PlacementMode.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            PopupPlacementGap.SetVertical(popup, 4D);

            Assert.Equal(new Rect(30D, -4D, 40D, 48D), popup.PlacementRectangle);

            PopupPlacementGap.SetVertical(popup, 0D);
            Assert.Equal(new Rect(30D, 0D, 40D, 40D), popup.PlacementRectangle);

            popup.HorizontalAlignment = HorizontalAlignment.Stretch;
            Assert.Equal(Rect.Empty, popup.PlacementRectangle);
        });
    }

    [Fact]
    public void VerticalGapExpandsImplicitAndExplicitPlacementRectanglesAndRestoresBaseValue()
    {
        WpfTestThread.Run(() =>
        {
            var target = new Border { Width = 80D, Height = 40D };
            var popup = new Popup { PlacementTarget = target };
            var window = new Window
            {
                Width = 300D, Height = 300D, Content = target,
                ShowActivated = false, ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                PopupPlacementGap.SetVertical(popup, 4D);
                Assert.Equal(new Rect(0D, -4D, 80D, 48D), popup.PlacementRectangle);

                target.Height = 60D;
                window.UpdateLayout();
                Assert.Equal(new Rect(0D, -4D, 80D, 68D), popup.PlacementRectangle);

                popup.Placement = PlacementMode.Left;
                Assert.Equal(Rect.Empty, popup.PlacementRectangle);
                popup.Placement = PlacementMode.Bottom;
                Assert.Equal(new Rect(0D, -4D, 80D, 68D), popup.PlacementRectangle);

                popup.PlacementRectangle = new Rect(2D, 3D, 20D, 10D);
                Assert.Equal(new Rect(2D, -1D, 20D, 18D), popup.PlacementRectangle);

                PopupPlacementGap.SetVertical(popup, 0D);
                Assert.Equal(new Rect(2D, 3D, 20D, 10D), popup.PlacementRectangle);
            }
            finally
            {
                PopupPlacementGap.SetVertical(popup, 0D);
                window.Close();
            }
        });
    }

    [Fact]
    public void VerticalGapKeepsTopPlacementStableInsideSignedOffsetOscillationBand()
    {
        WpfTestThread.Run(() =>
        {
            const double gap = 4D;

            var target = new Border { Width = 80D, Height = 40D };
            var child = new Border { Width = 50D, Height = 30D };
            var popup = new Popup
            {
                PlacementTarget = target, Child = child, Placement = PlacementMode.Bottom,
                PopupAnimation = PopupAnimation.None,
            };
            var window = new Window
            {
                Width = 300D, Height = 200D, Content = target,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                var dpi = VisualTreeHelper.GetDpi(window);
                var targetBottom = target.PointToScreen(new Point(0D, target.ActualHeight)).Y;
                var monitorBounds = UnsafeNativeMethods.GetMonitorBounds(
                    new Rect(target.PointToScreen(default), new Size(1D, 1D))
                );

                // Put the target precisely where +gap makes Bottom overflow and -gap makes it fit. A trigger that
                // flips Popup.VerticalOffset from +gap to -gap therefore oscillates at this location. The expanded
                // placement rectangle scores both sides with the same gap and has only one stable result.
                var popupHeightInPixels = child.Height * dpi.DpiScaleY;
                var desiredTargetBottom = monitorBounds.Bottom - popupHeightInPixels;
                window.Top += (desiredTargetBottom - targetBottom) / dpi.DpiScaleY;
                window.UpdateLayout();

                PopupPlacementGap.SetVertical(popup, gap);
                PopupPlacementTracker.SetIsEnabled(popup, true);
                popup.IsOpen = true;
                Drain();

                var expectedPopupBottom = target.PointToScreen(default).Y - (gap * dpi.DpiScaleY);
                var stablePopupBottom = child.PointToScreen(new Point(0D, child.ActualHeight)).Y;

                Assert.Equal(PopupPlacementDirection.Top, PopupPlacementTracker.GetActualPlacement(popup));
                Assert.InRange(Math.Abs(stablePopupBottom - expectedPopupBottom), 0D, 1.1D);
                Assert.Equal(0D, popup.VerticalOffset);

                for (var i = 0; i < 5; i++)
                {
                    Drain();
                    Assert.Equal(PopupPlacementDirection.Top, PopupPlacementTracker.GetActualPlacement(popup));
                    Assert.InRange(
                        Math.Abs(child.PointToScreen(new Point(0D, child.ActualHeight)).Y - stablePopupBottom),
                        0D,
                        0.1D
                    );
                }
            }
            finally
            {
                popup.IsOpen = false;
                PopupPlacementTracker.SetIsEnabled(popup, false);
                PopupPlacementGap.SetVertical(popup, 0D);
                window.Close();
            }
        });
    }

    [Fact]
    public void VerticalGapPreservesAndTracksPlacementRectangleBinding()
    {
        WpfTestThread.Run(() =>
        {
            var target = new Border { Width = 80D, Height = 40D };
            var source = new Flyout();
            var popup = new Popup { PlacementTarget = target };
            popup.SetBinding(
                Popup.PlacementRectangleProperty,
                new Binding(nameof(Flyout.PlacementRectangle)) { Source = source }
            );
            var window = new Window
            {
                Width = 300D, Height = 300D, Content = target,
                ShowActivated = false, ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                PopupPlacementGap.SetVertical(popup, 4D);
                Assert.NotNull(BindingOperations.GetBindingExpression(popup, Popup.PlacementRectangleProperty));
                Assert.Equal(new Rect(0D, -4D, 80D, 48D), popup.PlacementRectangle);

                source.PlacementRectangle = new Rect(2D, 3D, 20D, 10D);
                Assert.Equal(new Rect(2D, -1D, 20D, 18D), popup.PlacementRectangle);
                Assert.NotNull(BindingOperations.GetBindingExpression(popup, Popup.PlacementRectangleProperty));

                PopupPlacementGap.SetVertical(popup, 0D);
                Assert.Equal(source.PlacementRectangle, popup.PlacementRectangle);
                Assert.NotNull(BindingOperations.GetBindingExpression(popup, Popup.PlacementRectangleProperty));
            }
            finally
            {
                PopupPlacementGap.SetVertical(popup, 0D);
                window.Close();
            }
        });
    }

    [Fact]
    public void CandidateSurvivesOffsetsAndUpdatesWithoutMovingWindow()
    {
        WpfTestThread.Run(() =>
        {
            var target = new Border { Width = 80, Height = 40 };
            var window = new Window
            {
                Width = 300, Height = 300, Content = target,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowActivated = false, ShowInTaskbar = false,
            };
            var child = new Border { Width = 50, Height = 30 };
            var popup = new Popup
            {
                PlacementTarget = target, Child = child, Placement = PlacementMode.Bottom,
                PopupAnimation = PopupAnimation.None, VerticalOffset = -100,
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                PopupPlacementTracker.SetIsEnabled(popup, true);
                popup.IsOpen = true;
                Drain();

                // The negative offset places the window above the target, but WPF still selected Bottom.
                Assert.True(child.PointToScreen(new Point(0, 30)).Y < target.PointToScreen(default).Y);
                Assert.Equal(PopupPlacementDirection.Bottom, PopupPlacementTracker.GetActualPlacement(popup));
                Assert.False(PopupPlacementTracker.GetIsPlacementEstimated(popup));

                // Both candidates lie outside the monitor and are nudged to the same edge. Changing only
                // Placement changes WPF's flags, but does not require SetWindowPos or a native move notification.
                popup.VerticalOffset = -10000;
                Drain();
                var before = child.PointToScreen(default);
                popup.Placement = PlacementMode.Top;
                Drain();
                Assert.Equal(before, child.PointToScreen(default));
                Assert.Equal(PopupPlacementDirection.Top, PopupPlacementTracker.GetActualPlacement(popup));
                Assert.Equal(PopupPlacementDirection.Top, PopupPlacementTracker.GetActualPlacement(child));

                popup.Placement = PlacementMode.Custom;
                popup.CustomPopupPlacementCallback = (_, _, _) =>
                    new[] { new CustomPopupPlacement(new Point(0, 60), PopupPrimaryAxis.Horizontal) };
                Drain();
                Assert.True(PopupPlacementTracker.GetIsPlacementEstimated(popup));
                Assert.True(PopupPlacementTracker.GetIsPlacementEstimated(child));

                // Disable with a deferred read pending; it must not resurrect the controller or its values.
                popup.HorizontalOffset = 5;
                PopupPlacementTracker.SetIsEnabled(popup, false);
                Drain();
                Assert.Equal(PopupPlacementDirection.None, PopupPlacementTracker.GetActualPlacement(popup));
                Assert.False(PopupPlacementTracker.GetIsPlacementEstimated(popup));

                PopupPlacementTracker.SetIsEnabled(popup, true);
                Drain();
                Assert.True(PopupPlacementTracker.GetIsPlacementEstimated(popup));
                popup.IsOpen = false;
                Drain();
                Assert.Equal(PopupPlacementDirection.None, PopupPlacementTracker.GetActualPlacement(popup));
                Assert.False(PopupPlacementTracker.GetIsPlacementEstimated(popup));
            }
            finally
            {
                popup.IsOpen = false;
                PopupPlacementTracker.SetIsEnabled(popup, false);
                window.Close();
            }
        });
    }

    private static void Drain()
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
}
