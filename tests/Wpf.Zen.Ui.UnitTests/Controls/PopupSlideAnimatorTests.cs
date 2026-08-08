// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

[Collection(nameof(KeyboardFocusTestCollection))]
public class PopupSlideAnimatorTests
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
                PopupSlideAnimator.SetAnimationSettings(
                    popup,
                    new PopupAnimationSettings { StartOffset = startOffset, Duration = duration }
                );

                Assert.Equal(expected, PopupSlideAnimator.HasAnimatedContentBounds(popup));
            }
        );
    }

    [Fact]
    public void DefaultSettings_PreserveWpfUiSlideMotion()
    {
        var settings = PopupAnimationSettings.Default;
        var easing = Assert.IsType<CircleEase>(settings.EasingFunction);

        Assert.Equal(-90D, settings.StartOffset);
        Assert.Equal(167D, settings.Duration);
        Assert.Equal(EasingMode.EaseOut, easing.EasingMode);
    }

    [Fact]
    public void DisablingNativeSlide_RestoresConfiguredCustomAnimator()
    {
        RunOnStaThread(
            () =>
            {
                var popup = new Popup { AllowsTransparency = true };
                PopupAnimator.SetIsEnabled(popup, true);

                PopupSlideAnimator.SetIsEnabled(popup, true);
                Assert.Equal(PopupAnimation.Slide, popup.PopupAnimation);
                Assert.False(PopupAnimator.IsCustomAnimationActive(popup));

                PopupSlideAnimator.SetIsEnabled(popup, false);
                Assert.Equal(PopupAnimation.None, popup.PopupAnimation);
                Assert.True(PopupAnimator.IsCustomAnimationActive(popup));
            }
        );
    }

    [Fact]
    public void OpeningPopup_ReplacesNativeSlideWithoutAnimatingOpacity()
    {
        RunOnStaThread(
            () =>
            {
                var placementTarget = new Border { Width = 120D, Height = 32D };
                var popupChild = new Border { Width = 160D, Height = 180D };
                var window = new Window
                {
                    Width = 420D,
                    Height = 360D,
                    Left = 100D,
                    Top = 100D,
                    ShowInTaskbar = false,
                    Content = placementTarget,
                };
                var popup = new Popup
                {
                    AllowsTransparency = true,
                    Child = popupChild,
                    Placement = PlacementMode.Bottom,
                    PlacementTarget = placementTarget,
                    StaysOpen = false,
                };

                PopupSlideAnimator.SetIsEnabled(popup, true);

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    popup.IsOpen = true;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    var popupRoot = Assert.IsAssignableFrom<UIElement>(
                        (PresentationSource.FromVisual(popupChild) as HwndSource)?.RootVisual
                    );
                    var translateTransform = GetNativeTranslateTransform(popupRoot);

                    Assert.Equal(PopupAnimation.Slide, popup.PopupAnimation);
                    Assert.True(translateTransform.HasAnimatedProperties);
                    Assert.InRange(translateTransform.Y, -90D, -1D);
                    Assert.Equal(1D, popupRoot.Opacity);
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
    public void ProgrammaticClose_DoesNotForcePopupWindowDestruction()
    {
        RunOnStaThread(
            () =>
            {
                var placementTarget = new Border { Width = 100D, Height = 32D };
                var window = new Window
                {
                    Width = 240D,
                    Height = 180D,
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

                PopupSlideAnimator.SetIsEnabled(popup, true);
                popup.Closed += (_, _) => closedCount++;

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    popup.IsOpen = true;
                    popup.IsOpen = false;

                    // WPF intentionally queues Popup HWND destruction at Input priority. The lightweight animator
                    // must not turn this into the synchronous ForceClose behavior used by the legacy animator.
                    Assert.Equal(0, closedCount);

                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    Assert.Equal(1, closedCount);
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
    public void PopupPlacedAboveTarget_ReversesTheWpfUiSlideOffset()
    {
        RunOnStaThread(
            () =>
            {
                var placementTarget = new Border { Width = 120D, Height = 32D };
                var popupChild = new Border { Width = 160D, Height = 120D };
                var window = new Window
                {
                    Width = 420D,
                    Height = 420D,
                    Left = 100D,
                    Top = 100D,
                    ShowInTaskbar = false,
                    Content = placementTarget,
                };
                var popup = new Popup
                {
                    AllowsTransparency = true,
                    Child = popupChild,
                    Placement = PlacementMode.Top,
                    PlacementTarget = placementTarget,
                };

                PopupSlideAnimator.SetIsEnabled(popup, true);

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    popup.IsOpen = true;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    Assert.InRange(GetCurrentOffset(popupChild), 1D, 90D);
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
    public void ProgrammaticCloseThenReopen_ReusesWpfPopupLifecycleAndRestartsSlide()
    {
        RunOnStaThread(
            () =>
            {
                var placementTarget = new Border { Width = 100D, Height = 32D };
                var popupChild = new Border { Width = 120D, Height = 100D };
                var window = new Window
                {
                    Width = 240D,
                    Height = 200D,
                    ShowInTaskbar = false,
                    Content = placementTarget,
                };
                var popup = new Popup
                {
                    AllowsTransparency = true,
                    Child = popupChild,
                    PlacementTarget = placementTarget,
                };
                var openedOffsets = new List<double>();
                var closedCount = 0;

                PopupSlideAnimator.SetIsEnabled(popup, true);
                popup.Closed += (_, _) => closedCount++;

                try
                {
                    window.Show();
                    window.UpdateLayout();

                    popup.IsOpen = true;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    openedOffsets.Add(GetCurrentOffset(popupChild));

                    popup.IsOpen = false;
                    popup.IsOpen = true;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    openedOffsets.Add(GetCurrentOffset(popupChild));

                    Assert.True(popup.IsOpen);
                    Assert.Equal(0, closedCount);
                    Assert.Equal(2, openedOffsets.Count);
                    Assert.All(openedOffsets, offset => Assert.InRange(offset, -90D, -1D));
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
    public void ContextMenuAndToolTip_DoNotCreateTheirPrivatePopupBeforeOpening()
    {
        RunOnStaThread(
            () =>
            {
                var contextMenu = new ContextMenu();
                var toolTip = new ToolTip();

                PopupSlideAnimator.SetIsEnabled(contextMenu, true);
                PopupSlideAnimator.SetIsEnabled(toolTip, true);

                Assert.Null(GetPrivateRootPopupOrNull(contextMenu));
                Assert.Null(GetPrivateRootPopupOrNull(toolTip));
            }
        );
    }

    [Fact]
    public void OpeningContextMenu_NativeSlideOverridesCustomAnimatorAndReplacesItsClock()
    {
        RunOnStaThread(
            () =>
            {
                var placementTarget = new Border { Width = 120D, Height = 32D };
                var contextMenu = new ContextMenu
                {
                    Placement = PlacementMode.Bottom,
                    PlacementTarget = placementTarget,
                    Items = { new MenuItem { Header = "Item" } },
                };
                var window = new Window
                {
                    Width = 320D,
                    Height = 240D,
                    ShowInTaskbar = false,
                    Content = placementTarget,
                };

                // UiContextMenuStyle enables PopupAnimator. Keep that condition explicit so this regression test
                // verifies the public behaviors' precedence even when it runs without application theme resources.
                PopupAnimator.SetIsEnabled(contextMenu, true);
                PopupSlideAnimator.SetIsEnabled(contextMenu, true);
                Assert.Null(GetPrivateRootPopupOrNull(contextMenu));

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    contextMenu.IsOpen = true;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    var popupRoot = Assert.IsAssignableFrom<UIElement>(
                        (PresentationSource.FromVisual(contextMenu) as HwndSource)?.RootVisual
                    );
                    var translateTransform = GetNativeTranslateTransform(popupRoot);

                    var privateRootPopup = GetPrivateRootPopup(contextMenu);
                    Assert.Same(privateRootPopup, LogicalTreeHelper.GetParent(contextMenu));
                    Assert.Equal(PopupAnimation.Slide, privateRootPopup.PopupAnimation);
                    Assert.True(translateTransform.HasAnimatedProperties);
                    Assert.InRange(translateTransform.Y, -90D, -1D);

                    Thread.Sleep(250);
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    Assert.True(contextMenu.IsOpen);
                    Assert.Equal(1D, popupRoot.Opacity);
                    Assert.InRange(Math.Abs(translateTransform.Y), 0D, 0.1D);
                }
                finally
                {
                    contextMenu.IsOpen = false;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void DefaultFlyout_ShowCreatesAVisiblePopupSource()
    {
        RunOnStaThread(
            () =>
            {
                var flyout = new Flyout
                {
                    Content = new System.Windows.Controls.TextBlock { Text = "Flyout content" },
                };
                var root = new Grid();
                root.Resources.MergedDictionaries.Add(
                    new ResourceDictionary
                    {
                        Source = new Uri(
                            "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml",
                            UriKind.Absolute
                        ),
                    }
                );
                root.Children.Add(flyout);

                var window = new Window
                {
                    Width = 320D,
                    Height = 240D,
                    ShowInTaskbar = false,
                    Content = root,
                };

                try
                {
                    window.Show();
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    flyout.Show();
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    Thread.Sleep(250);
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    var popup = Assert.IsType<Popup>(flyout.Template.FindName("PART_Popup", flyout));
                    var popupChild = Assert.IsAssignableFrom<Visual>(popup.Child);
                    var popupSource = Assert.IsType<HwndSource>(PresentationSource.FromVisual(popupChild));
                    var popupRoot = Assert.IsAssignableFrom<UIElement>(popupSource.RootVisual);

                    Assert.True(flyout.IsOpen);
                    Assert.True(popup.IsOpen);
                    Assert.True(popupRoot.IsVisible);
                    Assert.Equal(1D, popupRoot.Opacity);
                    Assert.InRange(Math.Abs(GetNativeTranslateTransform(popupRoot).Y), 0D, 0.1D);
                }
                finally
                {
                    flyout.Hide();
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void ContextMenuService_OpensDefaultStyledContextMenu()
    {
        RunOnStaThread(
            () =>
            {
                var contextMenu = new ContextMenu { Items = { new MenuItem { Header = "Item" } } };
                var target = new Border
                {
                    Width = 120D,
                    Height = 32D,
                    ContextMenu = contextMenu,
                };
                var window = CreateWindowWithDefaultResources(target);
                var popupControlService = GetPopupControlService();

                try
                {
                    window.Show();
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    var opened = Assert.IsType<bool>(
                        GetPopupControlServiceType()
                           .GetMethod(
                                "RaiseContextMenuOpeningEvent",
                                BindingFlags.Instance | BindingFlags.NonPublic,
                                null,
                                [typeof(IInputElement), typeof(double), typeof(double), typeof(bool)],
                                null
                            )
                          ?.Invoke(popupControlService, [target, 1D, 1D, false])
                    );
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    Assert.True(opened);
                    Assert.True(contextMenu.IsOpen);
                    Assert.NotNull(PresentationSource.FromVisual(contextMenu));
                }
                finally
                {
                    contextMenu.IsOpen = false;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void ToolTipService_OpensDefaultStyledToolTip()
    {
        RunOnStaThread(
            () =>
            {
                var target = new Border { Width = 120D, Height = 32D, ToolTip = "Tooltip content" };
                var window = CreateWindowWithDefaultResources(target);
                var popupControlService = GetPopupControlService();
                var serviceType = GetPopupControlServiceType();

                try
                {
                    window.Show();
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    serviceType
                       .GetMethod("ShowToolTip", BindingFlags.Instance | BindingFlags.NonPublic)
                      ?.Invoke(popupControlService, [target, false]);
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    var toolTip = Assert.IsType<ToolTip>(
                        serviceType
                           .GetProperty("CurrentToolTip", BindingFlags.Instance | BindingFlags.NonPublic)
                          ?.GetValue(popupControlService)
                    );

                    Assert.True(toolTip.IsOpen);
                    Assert.NotNull(PresentationSource.FromVisual(toolTip));
                }
                finally
                {
                    serviceType
                       .GetMethod("DismissCurrentToolTip", BindingFlags.Instance | BindingFlags.NonPublic)
                      ?.Invoke(popupControlService, null);
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    window.Close();
                }
            }
        );
    }

    private static TranslateTransform GetNativeTranslateTransform(UIElement popupRoot)
    {
        var clippingDecorator = Assert.IsType<Decorator>(VisualTreeHelper.GetChild(popupRoot, 0));
        var animationLayer = Assert.IsAssignableFrom<UIElement>(clippingDecorator.Child);

        return Assert.IsType<TranslateTransform>(animationLayer.RenderTransform);
    }

    private static double GetCurrentOffset(Visual popupChild)
    {
        var popupRoot = Assert.IsAssignableFrom<UIElement>(
            (PresentationSource.FromVisual(popupChild) as HwndSource)?.RootVisual
        );

        return GetNativeTranslateTransform(popupRoot).Y;
    }

    private static Popup GetPrivateRootPopup(DependencyObject owner)
    {
        return Assert.IsType<Popup>(GetPrivateRootPopupOrNull(owner));
    }

    private static Popup? GetPrivateRootPopupOrNull(DependencyObject owner)
    {
        var field = owner.GetType().GetField("_parentPopup", BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(owner) as Popup;
    }

    private static Window CreateWindowWithDefaultResources(UIElement content)
    {
        var root = new Grid();
        root.Resources.MergedDictionaries.Add(
            new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml",
                    UriKind.Absolute
                ),
            }
        );
        root.Children.Add(content);

        return new Window
        {
            Width = 320D,
            Height = 240D,
            ShowInTaskbar = false,
            Content = root,
        };
    }

    private static Type GetPopupControlServiceType()
    {
        var serviceType = typeof(FrameworkElement).Assembly.GetType(
            "System.Windows.Controls.PopupControlService"
        );
        Assert.NotNull(serviceType);

        return serviceType;
    }

    private static object GetPopupControlService()
    {
        var service = GetPopupControlServiceType()
                     .GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic)
                    ?.GetValue(null);
        Assert.NotNull(service);

        return service;
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }
}
