// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests;

public class PopupPlacementTrackerTests
{
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
