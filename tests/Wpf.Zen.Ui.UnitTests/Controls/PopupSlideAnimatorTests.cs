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
using System.Windows.Data;
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
    [InlineData(90D, 0D, false)]
    [InlineData(90D, 167D, true)]
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
    public void DefaultSettings_LimitNativeSlideToNinetyPixels()
    {
        var settings = PopupAnimationSettings.Default;

        Assert.Equal(PopupSlideAnimator.MaximumSlideDistance, settings.StartOffset);
        Assert.Equal(167D, settings.Duration);
        var easing = Assert.IsType<CircleEase>(settings.EasingFunction);
        Assert.Equal(EasingMode.EaseOut, easing.EasingMode);
    }

    [Fact]
    public void EnablingAndDisabling_DoesNotChangePopupAnimationBinding()
    {
        RunOnStaThread(
            () =>
            {
                var animationSource = new Popup
                {
                    AllowsTransparency = true,
                    PopupAnimation = PopupAnimation.Fade,
                };
                var popup = new Popup
                {
                    AllowsTransparency = true,
                };
                BindingOperations.SetBinding(
                    popup,
                    Popup.PopupAnimationProperty,
                    new Binding(nameof(Popup.PopupAnimation)) { Source = animationSource }
                );

                PopupSlideAnimator.SetIsEnabled(popup, true);
                Assert.True(BindingOperations.IsDataBound(popup, Popup.PopupAnimationProperty));
                Assert.Equal(PopupAnimation.Fade, popup.PopupAnimation);

                animationSource.PopupAnimation = PopupAnimation.Scroll;
                Assert.Equal(PopupAnimation.Scroll, popup.PopupAnimation);

                PopupSlideAnimator.SetIsEnabled(popup, false);
                Assert.True(BindingOperations.IsDataBound(popup, Popup.PopupAnimationProperty));
                Assert.Equal(PopupAnimation.Scroll, popup.PopupAnimation);
            }
        );
    }

    [Fact]
    public void OpeningNonSlidePopup_DoesNotInvokeConfiguredEasingOrChangeMode()
    {
        RunOnStaThread(
            () =>
            {
                var placementTarget = new Border { Width = 120D, Height = 32D };
                var easing = new TrackingEasingFunction();
                var window = new Window
                {
                    Width = 320D,
                    Height = 240D,
                    ShowInTaskbar = false,
                    Content = placementTarget,
                };
                var popup = new Popup
                {
                    AllowsTransparency = true,
                    Child = new Border { Width = 140D, Height = 100D },
                    PlacementTarget = placementTarget,
                    PopupAnimation = PopupAnimation.Fade,
                };

                PopupSlideAnimator.SetAnimationSettings(
                    popup,
                    new PopupAnimationSettings { EasingFunction = easing }
                );

                try
                {
                    window.Show();
                    popup.IsOpen = true;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    Assert.Equal(PopupAnimation.Fade, popup.PopupAnimation);
                    Assert.False(PopupSlideAnimator.IsAnimationActive(popup));
                    Assert.Equal(0, easing.InvocationCount);
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
    public void OpeningSlidePopup_UsesConfiguredEasing()
    {
        RunOnStaThread(
            () =>
            {
                var placementTarget = new Border { Width = 120D, Height = 32D };
                var easing = new TrackingEasingFunction();
                var window = new Window
                {
                    Width = 320D,
                    Height = 240D,
                    ShowInTaskbar = false,
                    Content = placementTarget,
                };
                var popup = new Popup
                {
                    AllowsTransparency = true,
                    Child = new Border { Width = 140D, Height = 100D },
                    PlacementTarget = placementTarget,
                    PopupAnimation = PopupAnimation.Slide,
                };
                var wasActiveWhenOpened = false;

                PopupSlideAnimator.SetAnimationSettings(
                    popup,
                    new PopupAnimationSettings { Duration = 500D, EasingFunction = easing }
                );
                popup.Opened += (_, _) => wasActiveWhenOpened = PopupSlideAnimator.IsAnimationActive(popup);

                try
                {
                    window.Show();
                    popup.IsOpen = true;
                    Thread.Sleep(50);
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    Assert.Equal(PopupAnimation.Slide, popup.PopupAnimation);
                    Assert.True(wasActiveWhenOpened);
                    Assert.True(PopupSlideAnimator.IsAnimationActive(popup));
                    Assert.True(easing.InvocationCount > 0);
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
    public void OpeningPopup_ConcealsColdFirstFrameThenStartsClampedSlide()
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
                    PopupAnimation = PopupAnimation.Slide,
                    StaysOpen = false,
                };

                PopupSlideAnimator.SetAnimationSettings(
                    popup,
                    new PopupAnimationSettings { StartOffset = 240D }
                );
                double? opacityWhenOpened = null;
                popup.Opened += (_, _) =>
                {
                    opacityWhenOpened = Assert.IsAssignableFrom<UIElement>(
                        (PresentationSource.FromVisual(popupChild) as HwndSource)?.RootVisual
                    ).Opacity;
                };

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    popup.IsOpen = true;

                    // WM_SHOWWINDOW and Opened both run inside Popup.IsOpen's synchronous opening stack. The root
                    // must remain transparent here; otherwise any one-time work later in that stack is exposed as a
                    // stationary, partially translated first frame.
                    Assert.Equal(0D, opacityWhenOpened);

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
                var popupChild = new Border { Width = 120D, Height = 80D };
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
                    Child = popupChild,
                    PlacementTarget = placementTarget,
                    PopupAnimation = PopupAnimation.Slide,
                };
                var closedCount = 0;

                PopupSlideAnimator.SetIsEnabled(popup, true);
                popup.Closed += (_, _) => closedCount++;

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    popup.IsOpen = true;
                    var popupRoot = Assert.IsAssignableFrom<UIElement>(
                        (PresentationSource.FromVisual(popupChild) as HwndSource)?.RootVisual
                    );
                    var translateTransform = GetNativeTranslateTransform(popupRoot);
                    Assert.Equal(0D, popupRoot.Opacity);

                    popup.IsOpen = false;

                    // WPF intentionally queues Popup HWND destruction at Input priority. The lightweight animator
                    // must not turn this into the synchronous ForceClose behavior used by the legacy animator.
                    Assert.Equal(0, closedCount);

                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    Assert.Equal(1, closedCount);
                    Assert.Equal(1D, popupRoot.Opacity);
                    Assert.Equal(0D, translateTransform.Y);
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
                    PopupAnimation = PopupAnimation.Slide,
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
    public void PlacementChangedAfterShowWindow_UsesFinalDirectionWhenDeferredAnimationStarts()
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
                    PopupAnimation = PopupAnimation.Slide,
                };

                PopupSlideAnimator.SetAnimationSettings(
                    popup,
                    new PopupAnimationSettings { Duration = 10_000D }
                );

                // PopupSlideAnimator intercepts WM_SHOWWINDOW before Popup.Opened. Changing placement here models
                // the template/layout correction that occurs when an actual-placement trigger changes Popup size.
                // The deferred clock must use this final WPF candidate instead of the pre-Opened direction.
                popup.Opened += (_, _) => popup.Placement = PlacementMode.Bottom;

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    popup.IsOpen = true;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    var popupRoot = Assert.IsAssignableFrom<UIElement>(
                        (PresentationSource.FromVisual(popupChild) as HwndSource)?.RootVisual
                    );
                    Assert.True(
                        popupRoot.PointToScreen(default).Y >=
                        placementTarget.PointToScreen(new Point(0D, placementTarget.ActualHeight)).Y
                    );
                    Assert.InRange(GetCurrentOffset(popupChild), -90D, -1D);
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
                    PopupAnimation = PopupAnimation.Slide,
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
    public void OpeningSlidePopup_ConcealsDetachedShadowUntilReplacementClockStarts()
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
                    PopupAnimation = PopupAnimation.Slide,
                };

                PopupSlideAnimator.SetIsEnabled(popup, true);
                PopupShadow.SetIsEnabled(popup, true);

                Grid? shadowRootWhenOpened = null;
                popup.Opened += (_, _) => shadowRootWhenOpened = GetShadowRoot(popup);

                try
                {
                    window.Show();
                    window.UpdateLayout();
                    popup.IsOpen = true;

                    // The shadow HWND is independent from PopupRoot and therefore needs its own reveal gate. At the
                    // synchronous Opened boundary, the custom clock is still deferred and neither surface may expose
                    // an end-position frame.
                    Assert.NotNull(shadowRootWhenOpened);
                    Assert.Equal(0D, shadowRootWhenOpened.Opacity);

                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    var popupRoot = Assert.IsAssignableFrom<UIElement>(
                        (PresentationSource.FromVisual(popupChild) as HwndSource)?.RootVisual
                    );
                    Assert.True(GetNativeTranslateTransform(popupRoot).HasAnimatedProperties);
                    Assert.Equal(1D, popupRoot.Opacity);
                    Assert.Equal(1D, GetShadowRoot(popup).Opacity);
                }
                finally
                {
                    popup.IsOpen = false;
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    PopupShadow.SetIsEnabled(popup, false);
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void ContextMenu_ExplicitSlideOverridesWpfSystemAnimationOnFirstOpening()
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
                window.Resources[SystemParameters.MenuPopupAnimationKey] = PopupAnimation.Fade;
                PopupSlideAnimator.SetIsEnabled(contextMenu, true);
                contextMenu.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Slide);
                Assert.Null(GetPrivateRootPopupOrNull(contextMenu));

                try
                {
                    window.Show();
                    window.UpdateLayout();

                    // WPF creates the private Popup itself. SourceChanged fires after that construction but before
                    // SetupAnimations, allowing the explicit attached value to replace the system Fade on the first
                    // opening without invoking ContextMenu.HookupParentPopup ahead of its normal lifecycle.
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
                var target = new Border { Width = 100, Height = 32 };
                root.Children.Add(target);

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

                    flyout.ShowAt(target);
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                    Thread.Sleep(250);
                    window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

                    var popup = Assert.IsType<Popup>(LogicalTreeHelper.GetParent(flyout));
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

    private static Grid GetShadowRoot(DependencyObject owner)
    {
        var controllerProperty = Assert.IsType<DependencyProperty>(
            typeof(PopupShadow)
               .GetField("ShadowControllerProperty", BindingFlags.Static | BindingFlags.NonPublic)
              ?.GetValue(null)
        );
        var controller = Assert.IsAssignableFrom<object>(owner.GetValue(controllerProperty));

        return Assert.IsType<Grid>(
            controller
               .GetType()
               .GetField("_shadowRoot", BindingFlags.Instance | BindingFlags.NonPublic)
              ?.GetValue(controller)
        );
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

    private sealed class TrackingEasingFunction : IEasingFunction
    {
        public int InvocationCount { get; private set; }

        public double Ease(double normalizedTime)
        {
            InvocationCount++;
            return normalizedTime;
        }
    }
}
