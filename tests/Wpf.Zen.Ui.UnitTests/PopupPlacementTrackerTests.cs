// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

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
