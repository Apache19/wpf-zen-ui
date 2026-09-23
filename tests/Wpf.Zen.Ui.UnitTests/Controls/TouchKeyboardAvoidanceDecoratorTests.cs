// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Exercises real WPF layout and subscription lifetime with controllable screen-space occlusion.
/// </summary>
public class TouchKeyboardAvoidanceDecoratorTests
{
    /// <summary>
    /// Decorative padding can overlap each keyboard edge without moving unobstructed content.
    /// Deeper occlusion moves only far enough to expose the body and hiding restores alignment.
    /// </summary>
    [Theory]
    [InlineData("bottom")]
    [InlineData("top")]
    [InlineData("left")]
    [InlineData("right")]
    public void OcclusionPadding_OnlyBodyTriggersMovement(string side)
    {
        WpfTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Decorator.OcclusionPadding = scope.Root.Padding;
            Rect original = scope.Bounds;
            foreach (double depth in new[] { 12.0, 60.0 })
            {
                Rect keyboard = side switch
                {
                    "bottom" => new Rect(0, original.Bottom - depth, 1000, 800),
                    "top" => new Rect(0, 0, 1000, original.Top + depth),
                    "left" => new Rect(0, 0, original.Left + depth, 800),
                    _ => new Rect(original.Right - depth, 0, 1000, 800),
                };
                scope.ShowKeyboard(keyboard);
                double displacement = Math.Max(0, depth - 24);
                Rect moved = scope.Bounds;
                Assert.Equal(original.Size, moved.Size);
                Assert.Equal(original.X + (side == "left" ? displacement
                    : side == "right" ? -displacement : 0), moved.X, 5);
                Assert.Equal(original.Y + (side == "top" ? displacement
                    : side == "bottom" ? -displacement : 0), moved.Y, 5);
                Assert.True(new Rect(scope.Decorator.RenderSize).Contains(moved));
            }

            scope.Source.Publish(Rect.Empty);
            scope.Decorator.UpdateLayout();
            Assert.Equal(original, scope.Bounds);
        });
    }

    /// <summary>
    /// The body can use space previously reserved from the keyboard, while padding at the host
    /// edge remains intact both before and after compression becomes necessary.
    /// </summary>
    [Fact]
    public void OcclusionPadding_PreservesHostInsetAndCompressesOnlyWhenBodyCannotFit()
    {
        WpfTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Decorator.OcclusionPadding = scope.Root.Padding;
            Rect original = scope.Bounds;
            scope.ShowKeyboard(new Rect(0, 280, 1000, 800));
            Assert.Equal(original.Size, scope.Bounds.Size);
            Assert.Equal(4, scope.Bounds.Top, 5);
            Assert.Equal(304, scope.Bounds.Bottom, 5);

            scope.ShowKeyboard(new Rect(0, 200, 1000, 800));
            Assert.Equal(0, scope.Bounds.Top, 5);
            Assert.Equal(224, scope.Bounds.Height, 5);
            Assert.Equal(176, scope.Content.RenderSize.Height, 5);
            Assert.Equal(24, scope.Content.TranslatePoint(new Point(), scope.Decorator).Y, 5);

            scope.Source.Publish(Rect.Empty);
            scope.Decorator.UpdateLayout();
            Assert.Equal(original, scope.Bounds);
        });
    }

    /// <summary>
    /// Each keyboard edge moves the complete child without accumulating offsets or changing size.
    /// </summary>
    [Theory]
    [InlineData("bottom")]
    [InlineData("top")]
    [InlineData("left")]
    [InlineData("right")]
    public void Occlusion_MovesWholeChildAndRestoresOriginalLayout(string side)
    {
        WpfTestThread.Run(() =>
        {
            using var scope = new Scope();
            Rect original = scope.Bounds;
            Rect keyboard = side switch
            {
                "bottom" => new Rect(0, original.Bottom - 60, 1000, 800),
                "top" => new Rect(0, 0, 1000, original.Top + 60),
                "left" => new Rect(0, 0, original.Left + 60, 800),
                _ => new Rect(original.Right - 60, 0, 1000, 800),
            };
            scope.ShowKeyboard(keyboard);

            Rect moved = scope.Bounds;
            Assert.Equal(original.Size, moved.Size);
            Assert.Equal(side == "left" ? original.X + 60 : side == "right" ? original.X - 60 : original.X,
                moved.X, 5);
            Assert.Equal(side == "top" ? original.Y + 60 : side == "bottom" ? original.Y - 60 : original.Y,
                moved.Y, 5);

            scope.ShowKeyboard(keyboard);
            Assert.Equal(moved, scope.Bounds);
            scope.Source.Publish(Rect.Empty);
            scope.Decorator.UpdateLayout();
            Assert.Equal(original, scope.Bounds);
        });
    }

    /// <summary>
    /// Insufficient space reduces the child's measurement and hiding restores its natural size.
    /// </summary>
    [Fact]
    public void Compression_RemeasuresContentAndRecoversOriginalSize()
    {
        WpfTestThread.Run(() =>
        {
            using var scope = new Scope();
            Rect original = scope.Bounds;
            scope.ShowKeyboard(new Rect(0, 200, 1000, 800));

            Assert.Equal(200, scope.Bounds.Height, 5);
            Assert.Equal(original.Width, scope.Bounds.Width, 5);
            Assert.Equal(0, scope.Bounds.Top, 5);
            Assert.Equal(152, scope.Content.RenderSize.Height, 5); // 24 DIP padding at each edge.

            scope.Source.Publish(Rect.Empty);
            scope.Decorator.UpdateLayout();
            Assert.Equal(original, scope.Bounds);
        });
    }

    /// <summary>
    /// The real dialog template keeps its footer visible and scrolls its body when compressed.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    public void Compression_RealDialogKeepsFooterAboveKeyboardAndScrollsBody(double occlusionPadding)
    {
        WpfTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Decorator.OcclusionPadding = new Thickness(occlusionPadding);
            scope.Decorator.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml"),
            });
            var dialog = new ContentDialog
            {
                Header = "Form",
                Content = new Border { Height = 1000 },
                Footer = new System.Windows.Controls.Button { Content = "Save" },
            };
            scope.Root.Child = dialog;
            scope.Decorator.UpdateLayout();
            scope.ShowKeyboard(new Rect(0, 400, 1000, 800));

            var scroll = Assert.IsAssignableFrom<System.Windows.Controls.ScrollViewer>(
                dialog.Template.FindName("PART_ContentScroll", dialog));
            var footer = Assert.IsType<Border>(dialog.Template.FindName("footerBorder", dialog));
            Point footerBottom = footer.TranslatePoint(new Point(0, footer.ActualHeight), scope.Decorator);
            Assert.True(scroll.ScrollableHeight > 0);
            Assert.True(footer.ActualHeight > 0);
            Assert.InRange(footerBottom.Y, 0, 376.01 + occlusionPadding);
        });
    }

    /// <summary>
    /// The default presentation template keeps collision insets synchronized with shadow padding.
    /// </summary>
    [Fact]
    public void DefaultTemplate_BindsOcclusionPaddingToPresentationPadding()
    {
        WpfTestThread.Run(() =>
        {
            using var scope = new Scope();
            var presenter = new ContentDialogTransitionPresenter();
            presenter.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml"),
            });
            scope.Window.Content = presenter;
            scope.Window.UpdateLayout();
            var root = Assert.IsType<Border>(presenter.Template.FindName("PART_DialogRoot", presenter));
            var decorator = Assert.IsType<TouchKeyboardAvoidanceDecorator>(VisualTreeHelper.GetParent(root));
            Assert.Equal(new Thickness(24), decorator.OcclusionPadding);

            root.Padding = new Thickness(10, 20, 30, 40);
            scope.Window.UpdateLayout();
            Assert.Equal(root.Padding, decorator.OcclusionPadding);
        });
    }

    /// <summary>
    /// Stretch sizing still fills the host and restores its full size when occlusion ends.
    /// </summary>
    [Fact]
    public void Stretch_UsesFullHostAndShrinksOnlyObstructedAxis()
    {
        WpfTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Root.HorizontalAlignment = HorizontalAlignment.Stretch;
            scope.Root.VerticalAlignment = VerticalAlignment.Stretch;
            scope.Decorator.UpdateLayout();
            Rect original = scope.Bounds;

            scope.ShowKeyboard(new Rect(700, 0, 400, 800));
            Assert.Equal(new Rect(0, 0, 700, original.Height), scope.Bounds);

            scope.Source.Publish(Rect.Empty);
            scope.Decorator.UpdateLayout();
            Assert.Equal(original, scope.Bounds);
        });
    }

    /// <summary>
    /// Visibility and visual-tree lifetime release each subscription and reject old callbacks.
    /// </summary>
    [Fact]
    public void HiddenAndUnloaded_DisposeSubscriptionAndIgnoreStaleCallbacks()
    {
        WpfTestThread.Run(() =>
        {
            using var scope = new Scope();
            Rect original = scope.Bounds;
            Action<Rect> stale = scope.Source.Callbacks[0];
            scope.Decorator.Visibility = Visibility.Collapsed;
            Assert.Equal(1, scope.Source.Disposals);
            scope.Decorator.Visibility = Visibility.Visible;
            scope.Decorator.UpdateLayout();
            Assert.Equal(2, scope.Source.Callbacks.Count);

            stale(new Rect(-20000, -20000, 40000, 40000));
            scope.Decorator.UpdateLayout();
            Assert.Equal(original, scope.Bounds);

            scope.Window.Content = null;
            scope.Window.UpdateLayout();
            scope.Window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.Equal(2, scope.Source.Disposals);

            scope.Window.Content = scope.Decorator;
            scope.Window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            scope.Decorator.UpdateLayout();
            Assert.Equal(3, scope.Source.Callbacks.Count);
            Assert.Equal(original, scope.Bounds);
        });
    }

    /// <summary>
    /// Resubscription reads existing occlusion and HWND movement updates local coordinates.
    /// </summary>
    [Fact]
    public void ExistingKeyboard_IsAppliedOnLoadAndWindowMovementReprojectsScreenCoordinates()
    {
        WpfTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.ShowKeyboard(new Rect(0, 500, 1000, 800));
            Rect moved = scope.Bounds;
            scope.Decorator.Visibility = Visibility.Collapsed;
            scope.Decorator.Visibility = Visibility.Visible;
            scope.Decorator.UpdateLayout();
            Assert.Equal(moved, scope.Bounds);

            scope.Window.Top -= 100;
            scope.Window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            scope.Decorator.UpdateLayout();
            Assert.Equal((scope.Decorator.ActualHeight - scope.Root.ActualHeight) / 2, scope.Bounds.Y, 5);
        });
    }

    /// <summary>
    /// Screen-to-local conversion respects scale without taking ownership of animation transforms.
    /// </summary>
    [Fact]
    public void LayoutScale_ConvertsScreenOcclusionToDipsWithoutChangingChildAnimation()
    {
        WpfTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Decorator.LayoutTransform = new ScaleTransform(1.25, 1.25);
            scope.Decorator.UpdateLayout();
            var animation = new ScaleTransform(0.98, 0.98);
            scope.Root.RenderTransform = animation;
            Rect original = scope.Bounds;
            scope.ShowKeyboard(new Rect(original.Right - 40, 0, 1000, 800));

            Assert.Equal(original.X - 40, scope.Bounds.X, 5);
            Assert.Equal(original.Size, scope.Bounds.Size);
            Assert.Same(animation, scope.Root.RenderTransform);
        });
    }

    private sealed class Scope : IDisposable
    {
        internal FakeSource Source { get; } = new();
        internal TouchKeyboardAvoidanceDecorator Decorator { get; }
        internal Border Root { get; }
        internal FlexibleContent Content { get; } = new();
        internal Window Window { get; }
        internal Rect Bounds => new(VisualTreeHelper.GetOffset(Root).X,
            VisualTreeHelper.GetOffset(Root).Y, Root.ActualWidth, Root.ActualHeight);

        internal Scope()
        {
            Root = new Border
            {
                Padding = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = Content,
            };
            Decorator = new TouchKeyboardAvoidanceDecorator(Source.Subscribe) { Child = Root };
            Window = new Window
            {
                Content = Decorator, Width = 1000, Height = 800, Left = -10000, Top = -10000,
                WindowStyle = WindowStyle.None, ShowInTaskbar = false,
            };
            Window.Show();
            Window.UpdateLayout();
        }

        internal void ShowKeyboard(Rect localBounds)
        {
            Source.Publish(new Rect(Decorator.PointToScreen(localBounds.TopLeft),
                Decorator.PointToScreen(localBounds.BottomRight)));
            Decorator.UpdateLayout();
        }

        public void Dispose()
        {
            Window.Close();
        }
    }

    private sealed class FlexibleContent : FrameworkElement
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(Math.Min(352, availableSize.Width), Math.Min(252, availableSize.Height));
        }
    }

    private sealed class FakeSource
    {
        internal List<Action<Rect>> Callbacks { get; } = [];
        internal int Disposals { get; private set; }
        private Rect _bounds = Rect.Empty;

        internal IDisposable Subscribe(HwndSource source, Action<Rect> changed)
        {
            Callbacks.Add(changed);
            changed(_bounds);
            return new Subscription(() => Disposals++);
        }

        internal void Publish(Rect bounds)
        {
            _bounds = bounds;
            Callbacks[Callbacks.Count - 1](bounds);
        }

        private sealed class Subscription(Action dispose) : IDisposable
        {
            public void Dispose()
            {
                dispose();
            }
        }
    }
}
