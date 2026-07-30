// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls;

[Collection(nameof(KeyboardFocusTestCollection))]
public class PopupAnimatorTests
{
    [Theory]
    [InlineData(0D, 167D, false)]
    [InlineData(0.09D, 167D, false)]
    [InlineData(-90D, 0D, false)]
    [InlineData(-90D, 167D, true)]
    public void HasAnimatedContentBounds_ReflectsEffectiveMovement(
        double startOffset,
        double duration,
        bool expected)
    {
        RunOnStaThread(
            () =>
            {
                var popup = new Popup();
                PopupAnimator.SetAnimationSettings(
                    popup,
                    new PopupAnimationSettings { StartOffset = startOffset, Duration = duration }
                );

                Assert.Equal(expected, PopupAnimator.HasAnimatedContentBounds(popup));
            }
        );
    }

    [Fact]
    public void ClosingPopupOutsideInput_CompletesWindowDestroyImmediately()
    {
        RunOnStaThread(
            () =>
            {
                var placementTarget = new Border { Width = 100D, Height = 32D };
                var window = new Window
                {
                    Width = 200D,
                    Height = 120D,
                    ShowInTaskbar = false,
                    Content = placementTarget,
                };
                var popup = new Popup
                {
                    AllowsTransparency = true,
                    Child = new Border { Width = 120D, Height = 80D },
                    PlacementTarget = placementTarget,
                    StaysOpen = false,
                };
                var closedCount = 0;

                PopupAnimator.SetIsEnabled(popup, true);
                popup.Closed += (_, _) => closedCount++;

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    popup.IsOpen = true;

                    Assert.True(popup.IsOpen);

                    popup.IsOpen = false;

                    // Outside a routed input item there is no control handler that still needs PopupRoot. Completing
                    // destruction now prevents a synchronous reopen from reusing its last compositor surface.
                    Assert.Equal(1, closedCount);
                }
                finally
                {
                    popup.IsOpen = false;
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void ProgrammaticCloseThenReopen_DestroysOldWindowBeforeReopening()
    {
        RunOnStaThread(
            () =>
            {
                var placementTarget = new Border { Width = 100D, Height = 32D };
                var window = new Window
                {
                    Width = 200D,
                    Height = 120D,
                    ShowInTaskbar = false,
                    Content = placementTarget,
                };
                var popup = new Popup
                {
                    AllowsTransparency = true,
                    Child = new Border { Width = 120D, Height = 80D },
                    PlacementTarget = placementTarget,
                };
                var closedCount = 0;
                var openedCount = 0;

                PopupAnimator.SetIsEnabled(popup, true);
                popup.Closed += (_, _) => closedCount++;
                popup.Opened += (_, _) => openedCount++;

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    popup.IsOpen = true;
                    popup.IsOpen = false;
                    popup.IsOpen = true;

                    Assert.True(popup.IsOpen);
                    Assert.Equal(1, closedCount);
                    Assert.Equal(2, openedCount);
                }
                finally
                {
                    popup.IsOpen = false;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void ClosingPopupDuringInput_DefersWindowDestroyUntilRouteCompletes()
    {
        RunOnStaThread(
            () =>
            {
                var placementTarget = new Border { Width = 100D, Height = 32D };
                var window = new Window
                {
                    Width = 200D,
                    Height = 120D,
                    ShowInTaskbar = false,
                    Content = placementTarget,
                };
                var popup = new Popup
                {
                    AllowsTransparency = true,
                    Child = new Border { Width = 120D, Height = 80D },
                    PlacementTarget = placementTarget,
                };
                var closedCount = 0;
                var inputHandlerCompleted = false;

                PopupAnimator.SetIsEnabled(popup, true);
                popup.Closed += (_, _) =>
                {
                    Assert.True(inputHandlerCompleted);
                    closedCount++;
                };
                placementTarget.PreviewMouseLeftButtonDown += (_, _) =>
                {
                    popup.IsOpen = false;

                    Assert.Equal(0, closedCount);
                    inputHandlerCompleted = true;
                };

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    popup.IsOpen = true;

                    _ = InputManager.Current.ProcessInput(CreatePreviewMouseLeftButtonDown(placementTarget));

                    Assert.True(inputHandlerCompleted);
                    Assert.Equal(1, closedCount);
                    Assert.False(popup.IsOpen);
                }
                finally
                {
                    popup.IsOpen = false;
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void InputClose_ForceClosesBeforeNormalPriorityReopen()
    {
        RunOnStaThread(
            () =>
            {
                var placementTarget = new Border { Width = 100D, Height = 32D };
                var window = new Window
                {
                    Width = 200D,
                    Height = 120D,
                    ShowInTaskbar = false,
                    Content = placementTarget,
                };
                var popup = new Popup
                {
                    AllowsTransparency = true,
                    Child = new Border { Width = 120D, Height = 80D },
                    PlacementTarget = placementTarget,
                };
                var closedCount = 0;
                var openedCount = 0;

                PopupAnimator.SetIsEnabled(popup, true);
                popup.Closed += (_, _) => closedCount++;
                popup.Opened += (_, _) => openedCount++;
                placementTarget.PreviewMouseLeftButtonDown += (_, _) =>
                {
                    popup.IsOpen = false;
                    _ = window.Dispatcher.BeginInvoke(
                        DispatcherPriority.Normal,
                        new Action(() => popup.IsOpen = true)
                    );
                };

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    popup.IsOpen = true;

                    _ = InputManager.Current.ProcessInput(CreatePreviewMouseLeftButtonDown(placementTarget));

                    // PostProcessInput runs before control returns to the Dispatcher, so the old HWND is already gone
                    // even though the requested reopen has a priority higher than WPF's Input-priority destroy timer.
                    Assert.Equal(1, closedCount);
                    Assert.Equal(1, openedCount);

                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    Assert.True(popup.IsOpen);
                    Assert.Equal(2, openedCount);
                }
                finally
                {
                    popup.IsOpen = false;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    window.Close();
                }
            }
        );
    }

    private static MouseButtonEventArgs CreatePreviewMouseLeftButtonDown(UIElement source)
    {
        return new MouseButtonEventArgs(
            Mouse.PrimaryDevice,
            Environment.TickCount,
            MouseButton.Left
        )
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            Source = source,
        };
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
