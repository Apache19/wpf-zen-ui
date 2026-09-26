// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Exercises target following against real HWND placement and existing popup behaviors.
/// </summary>
[Collection(nameof(KeyboardFocusTestCollection))]
public class PopupFollowTests
{
    /// <summary>
    /// Ensures native movement updates the shadow before yielding to dispatcher work.
    /// </summary>
    [Fact]
    public void ShadowMovesSynchronouslyWithoutRebuildingClip()
    {
        WpfTestThread.Run(async () =>
        {
            var target = new Border { Width = 100, Height = 30 };
            var window = CreateWindow(target);
            var child = new Border { Width = 60, Height = 25, Background = Brushes.White };
            var popup = CreatePopup(target, child);
            popup.AllowsTransparency = true;
            PopupShadow.SetIsEnabled(popup, true);
            try
            {
                window.Show();
                popup.IsOpen = true;
                await Task.Delay(200);
                Drain();
                var shadow = GetShadowRoot(popup);
                var initialPopup = child.PointToScreen(default);
                var initialShadow = shadow.PointToScreen(default);
                var clip = shadow.Clip;

                // No dispatcher drain between moves: an eventual-position assertion would miss
                // the visible lag caused by queuing shadow updates below input/render priority.
                for (var i = 1; i <= 20; i++)
                {
                    popup.HorizontalOffset = i * 2;
                    popup.VerticalOffset = i;
                    var delta = child.PointToScreen(default) - initialPopup;
                    Assert.True(delta.Length > 0);
                    AssertNear(delta, shadow.PointToScreen(default) - initialShadow);
                    Assert.Same(clip, shadow.Clip);
                }
            }
            finally
            {
                popup.IsOpen = false;
                PopupShadow.SetIsEnabled(popup, false);
                window.Close();
            }
        });
    }

    /// <summary>
    /// Verifies movement with alignment, gaps, bindings, slide animation and shadow enabled.
    /// </summary>
    [Theory]
    [InlineData("Window")]
    [InlineData("Scroll")]
    [InlineData("Layout")]
    [InlineData("Transform")]
    [InlineData("AncestorTransform")]
    [InlineData("Size")]
    public void FollowsTargetGeometryAndPreservesComposedBehaviors(string movement)
    {
        WpfTestThread.Run(async () =>
        {
            var target = new Border { Width = 100, Height = 30 };
            var canvas = new Canvas { Width = 700, Height = 700 };
            Canvas.SetLeft(target, 120);
            Canvas.SetTop(target, 140);
            canvas.Children.Add(target);
            var scroll = new System.Windows.Controls.ScrollViewer { Content = canvas };
            var window = CreateWindow(scroll);
            var child = new Border { Width = 60, Height = 25, Background = Brushes.White };
            var popup = CreatePopup(target, child);
            popup.HorizontalAlignment = HorizontalAlignment.Right;
            popup.AllowsTransparency = true;
            popup.PopupAnimation = PopupAnimation.Slide;
            var offsets = new Popup { HorizontalOffset = 3, VerticalOffset = 2 };
            BindingOperations.SetBinding(popup, Popup.HorizontalOffsetProperty,
                new Binding(nameof(Popup.HorizontalOffset)) { Source = offsets });
            BindingOperations.SetBinding(popup, Popup.VerticalOffsetProperty,
                new Binding(nameof(Popup.VerticalOffset)) { Source = offsets });
            PopupPlacementGap.SetVertical(popup, 6);
            PopupPlacementTracker.SetIsEnabled(popup, true);
            PopupShadow.SetIsEnabled(popup, true);
            PopupSlideAnimator.SetIsEnabled(popup, true);
            PopupFollow.SetIsEnabled(popup, true);
            try
            {
                window.Show();
                window.UpdateLayout();
                popup.IsOpen = true;
                await Task.Delay(250);
                Drain();
                var before = child.PointToScreen(default);
                var shadowBefore = GetShadowRoot(popup).PointToScreen(default);

                switch (movement)
                {
                    case "Window":
                        window.Left += 27;
                        window.Top += 19;
                        break;
                    case "Scroll":
                        scroll.ScrollToVerticalOffset(35);
                        break;
                    case "Layout":
                        Canvas.SetLeft(target, 165);
                        Canvas.SetTop(target, 180);
                        break;
                    case "Transform":
                        target.RenderTransform = new TranslateTransform(23, 17);
                        break;
                    case "AncestorTransform":
                        canvas.RenderTransform = new TranslateTransform(23, 17);
                        break;
                    case "Size":
                        target.Width += 30;
                        target.Height += 20;
                        break;
                }

                window.UpdateLayout();
                await Task.Delay(80);
                Drain();
                var after = child.PointToScreen(default);
                Assert.NotEqual(before, after);
                AssertNear(after - before, GetShadowRoot(popup).PointToScreen(default) - shadowBefore);
                var anchor = target.PointToScreen(new Point(target.ActualWidth, target.ActualHeight));
                var corner = child.PointToScreen(new Point(child.ActualWidth, 0));
                var dpi = VisualTreeHelper.GetDpi(target);
                AssertNear(new Vector(3 * dpi.DpiScaleX, 8 * dpi.DpiScaleY), corner - anchor);
                Assert.Equal(PopupPlacementDirection.Bottom, PopupPlacementTracker.GetActualPlacement(popup));
                Assert.True(BindingOperations.IsDataBound(popup, Popup.HorizontalOffsetProperty));
                Assert.True(BindingOperations.IsDataBound(popup, Popup.VerticalOffsetProperty));
                Assert.Equal(3, popup.HorizontalOffset);
                Assert.Equal(2, popup.VerticalOffset);
                Assert.Equal(PopupAnimation.Slide, popup.PopupAnimation);
                Assert.True(popup.IsOpen);
            }
            finally
            {
                popup.IsOpen = false;
                PopupFollow.SetIsEnabled(popup, false);
                PopupShadow.SetIsEnabled(popup, false);
                PopupSlideAnimator.SetIsEnabled(popup, false);
                PopupPlacementTracker.SetIsEnabled(popup, false);
                window.Close();
            }
        });
    }

    /// <summary>
    /// Ensures custom callbacks run on movement and stop when following is inactive.
    /// </summary>
    [Fact]
    public void CustomPlacementRetainsCallbackAndStopsAfterDisableOrClose()
    {
        WpfTestThread.Run(async () =>
        {
            var target = new Border { Width = 100, Height = 30 };
            var canvas = new Canvas();
            Canvas.SetLeft(target, 100);
            Canvas.SetTop(target, 100);
            canvas.Children.Add(target);
            var window = CreateWindow(canvas);
            var child = new Border { Width = 60, Height = 25 };
            var popup = CreatePopup(target, child);
            var calls = 0;
            CustomPopupPlacementCallback callback = (_, size, offset) =>
            {
                calls++;
                return new[] { new CustomPopupPlacement(new Point(0, size.Height) + (Vector)offset,
                    PopupPrimaryAxis.Horizontal) };
            };
            popup.Placement = PlacementMode.Custom;
            popup.CustomPopupPlacementCallback = callback;
            try
            {
                window.Show();
                popup.IsOpen = true;
                Drain();
                PopupFollow.SetIsEnabled(popup, true);
                await Task.Delay(80);
                var baseline = calls;
                await Task.Delay(80);
                Assert.Equal(baseline, calls);

                Canvas.SetTop(target, 145);
                window.UpdateLayout();
                await Task.Delay(80);
                Assert.True(calls > baseline);
                Assert.Same(callback, popup.CustomPopupPlacementCallback);
                AssertNear(new Vector(), child.PointToScreen(default)
                    - target.PointToScreen(new Point(0, target.ActualHeight)));

                PopupFollow.SetIsEnabled(popup, false);
                baseline = calls;
                window.Left += 21;
                await Task.Delay(80);
                Assert.Equal(baseline, calls);
                Assert.True(popup.IsOpen);

                PopupFollow.SetIsEnabled(popup, true);
                popup.IsOpen = false; // Cancel a queued update before it can run.
                baseline = calls;
                window.Left += 21;
                await Task.Delay(80);
                Assert.Equal(baseline, calls);

                popup.IsOpen = true;
                await Task.Delay(80);
                window.Left += 21;
                await Task.Delay(80);
                AssertNear(new Vector(), child.PointToScreen(default)
                    - target.PointToScreen(new Point(0, target.ActualHeight)));
            }
            finally
            {
                popup.IsOpen = false;
                PopupFollow.SetIsEnabled(popup, false);
                window.Close();
            }
        });
    }

    /// <summary>
    /// Covers the implicit visual parent and a later explicit target in another HWND.
    /// </summary>
    [Fact]
    public void ResolvesImplicitTargetAndSwitchesPresentationSources()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new Grid { Width = 100, Height = 30 };
            var other = new Border { Width = 100, Height = 30 };
            var first = CreateWindow(host);
            var second = CreateWindow(other);
            second.Left += 100;
            var child = new Border { Width = 60, Height = 25 };
            var popup = CreatePopup(null, child);
            host.Children.Add(popup);
            PopupFollow.SetIsEnabled(popup, true);
            try
            {
                first.Show();
                second.Show();
                popup.IsOpen = true;
                await Task.Delay(80);
                first.Left += 25;
                await Task.Delay(80);
                AssertNear(new Vector(), child.PointToScreen(default)
                    - host.PointToScreen(new Point(0, host.ActualHeight)));
                popup.PlacementTarget = other;
                await Task.Delay(80);
                second.Left += 25;
                await Task.Delay(80);
                AssertNear(new Vector(), child.PointToScreen(default)
                    - other.PointToScreen(new Point(0, other.ActualHeight)));
            }
            finally
            {
                popup.IsOpen = false;
                PopupFollow.SetIsEnabled(popup, false);
                first.Close();
                second.Close();
            }
        });
    }

    /// <summary>
    /// Keeps mouse and absolute placement independent of placement-target movement.
    /// </summary>
    [Theory]
    [InlineData(PlacementMode.Absolute)]
    [InlineData(PlacementMode.AbsolutePoint)]
    [InlineData(PlacementMode.Mouse)]
    [InlineData(PlacementMode.MousePoint)]
    public void ScreenRelativeModesRetainTheirPosition(PlacementMode placement)
    {
        WpfTestThread.Run(async () =>
        {
            var target = new Border { Width = 100, Height = 30 };
            var window = CreateWindow(target);
            var child = new Border { Width = 60, Height = 25 };
            var popup = CreatePopup(target, child);
            popup.Placement = placement;
            PopupFollow.SetIsEnabled(popup, true);
            try
            {
                window.Show();
                popup.IsOpen = true;
                await Task.Delay(80);
                var before = child.PointToScreen(default);
                window.Left += 25;
                target.RenderTransform = new TranslateTransform(40, 20);
                await Task.Delay(80);
                Assert.Equal(before, child.PointToScreen(default));
            }
            finally
            {
                popup.IsOpen = false;
                PopupFollow.SetIsEnabled(popup, false);
                window.Close();
            }
        });
    }

    /// <summary>
    /// Covers render-only animation and resumption after the target leaves the visual tree.
    /// </summary>
    [Fact]
    public void FollowsAnimationAndReconnectedTarget()
    {
        WpfTestThread.Run(async () =>
        {
            var transform = new TranslateTransform();
            var target = new Border { Width = 100, Height = 30, RenderTransform = transform };
            var canvas = new Canvas();
            Canvas.SetLeft(target, 100);
            Canvas.SetTop(target, 100);
            canvas.Children.Add(target);
            var window = CreateWindow(canvas);
            var child = new Border { Width = 60, Height = 25 };
            var popup = CreatePopup(target, child);
            PopupFollow.SetIsEnabled(popup, true);
            try
            {
                window.Show();
                popup.IsOpen = true;
                await Task.Delay(80);
                transform.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(0, 80, TimeSpan.FromMilliseconds(180)));
                await Task.Delay(260);
                AssertNear(new Vector(), child.PointToScreen(default)
                    - target.PointToScreen(new Point(0, target.ActualHeight)));

                canvas.Children.Remove(target);
                await Task.Delay(80);
                Assert.True(popup.IsOpen);
                transform.BeginAnimation(TranslateTransform.XProperty, null);
                Canvas.SetTop(target, 180);
                canvas.Children.Add(target);
                await Task.Delay(80);
                AssertNear(new Vector(), child.PointToScreen(default)
                    - target.PointToScreen(new Point(0, target.ActualHeight)));
            }
            finally
            {
                popup.IsOpen = false;
                PopupFollow.SetIsEnabled(popup, false);
                window.Close();
            }
        });
    }

    /// <summary>
    /// Verifies WPF can switch sides near a screen edge while retaining symmetric target spacing.
    /// </summary>
    [Fact]
    public void MovingToScreenEdgeRecomputesDirectionAndGap()
    {
        WpfTestThread.Run(async () =>
        {
            var target = new Border { Width = 100, Height = 30 };
            var canvas = new Canvas();
            Canvas.SetLeft(target, 100);
            Canvas.SetTop(target, 50);
            canvas.Children.Add(target);
            var window = CreateWindow(canvas);
            window.WindowStyle = WindowStyle.None;
            window.Height = 100;
            var child = new Border { Width = 60, Height = 140 };
            var popup = CreatePopup(target, child);
            PopupFollow.SetIsEnabled(popup, true);
            PopupPlacementGap.SetVertical(popup, 6);
            PopupPlacementTracker.SetIsEnabled(popup, true);
            try
            {
                window.Show();
                popup.IsOpen = true;
                await Task.Delay(80);
                Assert.Equal(PopupPlacementDirection.Bottom, PopupPlacementTracker.GetActualPlacement(popup));
                window.Top = SystemParameters.WorkArea.Bottom - window.Height;
                await Task.Delay(80);
                Drain();
                Assert.Equal(PopupPlacementDirection.Top, PopupPlacementTracker.GetActualPlacement(popup));
                var gap = target.PointToScreen(default).Y
                    - child.PointToScreen(new Point(0, child.ActualHeight)).Y;
                Assert.InRange(gap, 6 * VisualTreeHelper.GetDpi(target).DpiScaleY - 1.1,
                    6 * VisualTreeHelper.GetDpi(target).DpiScaleY + 1.1);
            }
            finally
            {
                popup.IsOpen = false;
                PopupFollow.SetIsEnabled(popup, false);
                PopupPlacementTracker.SetIsEnabled(popup, false);
                window.Close();
            }
        });
    }

    /// <summary>
    /// Guards against global event subscriptions retaining inactive popup controllers.
    /// </summary>
    [Fact]
    public void UnopenedAndClosedPopupsCanBeCollected()
    {
        WpfTestThread.Run(() =>
        {
            var unopened = CreateCollectiblePopup(false);
            var closed = CreateCollectiblePopup(true);
            Drain();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.False(unopened.IsAlive);
            Assert.False(closed.IsAlive);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateCollectiblePopup(bool open)
    {
        var popup = CreatePopup(null, new Border { Width = 20, Height = 20 });
        PopupFollow.SetIsEnabled(popup, true);
        if (open)
        {
            popup.IsOpen = true;
            Drain();
            popup.IsOpen = false;
        }

        return new WeakReference(popup);
    }

    private static Popup CreatePopup(UIElement? target, UIElement child)
    {
        return new Popup { PlacementTarget = target, Child = child, Placement = PlacementMode.Bottom };
    }

    private static Window CreateWindow(UIElement content)
    {
        return new Window
        {
            Width = 450, Height = 350, Left = 100, Top = 100, Content = content,
            ShowActivated = false, ShowInTaskbar = false,
        };
    }

    private static void AssertNear(Vector expected, Vector actual)
    {
        Assert.True(Math.Abs(expected.X - actual.X) <= 1.1 && Math.Abs(expected.Y - actual.Y) <= 1.1,
            $"Expected {expected}; actual {actual}.");
    }

    private static Grid GetShadowRoot(Popup popup)
    {
        var property = (DependencyProperty)typeof(PopupShadow).GetField("ShadowControllerProperty",
            BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var controller = popup.GetValue(property);
        return (Grid)controller.GetType().GetField("_shadowRoot",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
    }

    private static void Drain()
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
}
