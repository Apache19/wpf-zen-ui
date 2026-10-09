// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies docking compatibility, measured gaps, rendered separators and dynamic layout changes.
/// </summary>
public class DockPanelTests
{
    /// <summary>
    /// Zero spacing preserves native desired sizes and layout slots, including collapsed fill.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ZeroSpacing_MatchesNativeDockPanel(bool fill, bool collapseLast)
    {
        WpfTestThread.Run(() =>
        {
            var native = new DockPanel { LastChildFill = fill };
            var panel = new UiDockPanel { LastChildFill = fill };
            foreach (var dock in new[] { Dock.Left, Dock.Top, Dock.Right, Dock.Bottom, Dock.Left })
            {
                AddChild(native, dock, 23, 17);
                AddChild(panel, dock, 23, 17);
            }

            if (collapseLast)
            {
                native.Children[4].Visibility = Visibility.Collapsed;
                panel.Children[4].Visibility = Visibility.Collapsed;
            }

            foreach (var available in new[] { new Size(120, 100), new Size(30, 20) })
            {
                Layout(native, available);
                Layout(panel, available);
                Assert.Equal(native.DesiredSize, panel.DesiredSize);
                for (var index = 0; index < 5; index++)
                {
                    if (panel.Children[index].Visibility == Visibility.Collapsed)
                    {
                        continue;
                    }

                    var expected = Slot(native.Children[index]);
                    var actual = Slot(panel.Children[index]);
                    Assert.Equal(expected, actual);
                }
            }
        });
    }

    /// <summary>
    /// Every docking edge contributes a measured gap and paints the strip before the fill region.
    /// </summary>
    [Theory]
    [InlineData(Dock.Left, 25, 50, 30, 0, 70, 100)]
    [InlineData(Dock.Right, 75, 50, 0, 0, 70, 100)]
    [InlineData(Dock.Top, 50, 25, 0, 30, 100, 70)]
    [InlineData(Dock.Bottom, 50, 75, 0, 0, 100, 70)]
    public void DockEdges_MeasureArrangeAndPaintGap(
        Dock dock, int pixelX, int pixelY, double x, double y, double width, double height)
    {
        WpfTestThread.Run(() =>
        {
            var panel = new UiDockPanel { Spacing = 10, SpacingBrush = Brushes.Red };
            AddChild(panel, dock, 20, 20);
            var fill = new Border();
            panel.Children.Add(fill);
            var horizontal = dock == Dock.Left || dock == Dock.Right;
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var expectedSize = horizontal ? new Size(30, 20) : new Size(20, 30);
            Assert.Equal(expectedSize, panel.DesiredSize);
            Layout(panel, new Size(100, 100));
            Assert.Equal(new Rect(x, y, width, height), Slot(fill));
            Assert.Equal(Colors.Red, Pixel(panel, pixelX, pixelY));
            Assert.Equal(default(Color), Pixel(panel, 50, 50));
        });
    }

    /// <summary>
    /// Mixed docking uses the remaining cross-axis region and preserves child margin bindings.
    /// </summary>
    [Fact]
    public void MixedDocking_PaintsOnlySeparators_AndUpdatesAtConstantPanelSize()
    {
        WpfTestThread.Run(() =>
        {
            var brush = new SolidColorBrush(Colors.Red);
            var panel = new UiDockPanel
            {
                Spacing = 10, SpacingBrush = brush, Background = Brushes.White
            };
            var top = AddChild(panel, Dock.Top, double.NaN, 20);
            var binding = new Binding(nameof(Border.Margin))
            {
                Source = new Border { Margin = new Thickness(2) }
            };
            top.SetBinding(FrameworkElement.MarginProperty, binding);
            AddChild(panel, Dock.Left, 20, double.NaN);
            var fill = new Border();
            panel.Children.Add(fill);
            Layout(panel, new Size(100, 100));
            Assert.Equal(new Rect(30, 34, 70, 66), Slot(fill));
            Assert.Equal(Colors.White, Pixel(panel, 50, 22));
            Assert.Equal(Colors.Red, Pixel(panel, 50, 28));
            Assert.Equal(Colors.Red, Pixel(panel, 25, 50));
            Assert.Equal(Colors.White, Pixel(panel, 25, 10));
            Assert.Same(binding, BindingOperations.GetBinding(top, FrameworkElement.MarginProperty));

            brush.Color = Colors.Green;
            panel.UpdateLayout();
            Assert.Equal(Colors.Green, Pixel(panel, 50, 28));
            top.Height = 30;
            Layout(panel, new Size(100, 100));
            Assert.Equal(Colors.White, Pixel(panel, 50, 28));
            Assert.Equal(Colors.Green, Pixel(panel, 50, 38));

            panel.Spacing = 4;
            Layout(panel, new Size(100, 100));
            Assert.Equal(new Rect(24, 38, 76, 62), Slot(fill));
            panel.SpacingBrush = null;
            panel.UpdateLayout();
            Assert.Equal(Colors.White, Pixel(panel, 22, 50));
        });
    }

    /// <summary>
    /// Hidden children keep gaps; collapsed children remove them while preserving fill ownership.
    /// </summary>
    [Fact]
    public void VisibilityAndCollectionChanges_RecomputeGapsAndKeepNativeFillOwnership()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new UiDockPanel { Spacing = 10, SpacingBrush = Brushes.Red };
            AddChild(panel, Dock.Left, 20, 20);
            var middle = AddChild(panel, Dock.Left, 20, 20);
            var last = AddChild(panel, Dock.Left, 20, 20);
            middle.Visibility = Visibility.Hidden;
            Layout(panel, new Size(100, 100));
            Assert.Equal(60, Slot(last).X);
            Assert.Equal(Colors.Red, Pixel(panel, 55, 50));
            middle.Visibility = Visibility.Collapsed;
            Layout(panel, new Size(100, 100));
            Assert.Equal(30, Slot(last).X);
            last.Visibility = Visibility.Collapsed;
            Layout(panel, new Size(100, 100));
            Assert.Equal(20, Slot(panel.Children[0]).Width);
            Assert.Equal(default(Color), Pixel(panel, 25, 50));

            panel.Children.Remove(last);
            panel.Children.Remove(middle);
            Layout(panel, new Size(100, 100));
            Assert.Equal(100, Slot(panel.Children[0]).Width);
            panel.LastChildFill = false;
            Layout(panel, new Size(100, 100));
            Assert.Equal(20, Slot(panel.Children[0]).Width);
        });
    }

    /// <summary>
    /// Gaps constrain later measurement and stay absent after the final non-fill child.
    /// </summary>
    [Fact]
    public void FiniteConstraint_ReservesSpacingBeforeMeasuringFollowingChild()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new UiDockPanel
            {
                LastChildFill = false, Spacing = 10, SpacingBrush = Brushes.Red
            };
            AddChild(panel, Dock.Left, 20, 20);
            var probe = new MeasureProbe();
            panel.Children.Add(probe);
            Layout(panel, new Size(100, 100));
            Assert.Equal(new Size(70, 100), probe.Constraint);
            Assert.Equal(default(Color), Pixel(panel, 50, 50));
        });
    }

    /// <summary>
    /// Empty panels and sole children have no gaps; zero-sized slots still separate siblings.
    /// </summary>
    [Fact]
    public void EmptyAndZeroSizedChildren_OnlySeparateParticipatingSiblings()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new UiDockPanel { Spacing = 10, SpacingBrush = Brushes.Red };
            Layout(panel, new Size(100, 100));
            Assert.Equal(new Size(), panel.DesiredSize);
            Assert.Equal(default(Color), Pixel(panel, 5, 50));
            var zero = AddChild(panel, Dock.Left, 0, 0);
            Layout(panel, new Size(100, 100));
            Assert.Equal(default(Color), Pixel(panel, 5, 50));
            var fill = new Border();
            panel.Children.Add(fill);
            Layout(panel, new Size(100, 100));
            Assert.Equal(new Rect(10, 0, 90, 100), Slot(fill));
            Assert.Equal(Colors.Red, Pixel(panel, 5, 50));

            DockPanel.SetDock(zero, Dock.Top);
            Layout(panel, new Size(100, 100));
            Assert.Equal(new Rect(0, 10, 100, 90), Slot(fill));
            Assert.Equal(default(Color), Pixel(panel, 5, 50));
            Assert.Equal(Colors.Red, Pixel(panel, 50, 5));
        });
    }

    /// <summary>
    /// A separator larger than the available region cannot paint earlier docking allocations.
    /// </summary>
    [Fact]
    public void OversizedSpacing_ClipsToRemainingRegion()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new UiDockPanel { Spacing = 80, SpacingBrush = Brushes.Red };
            AddChild(panel, Dock.Left, 20, 20);
            AddChild(panel, Dock.Right, 20, 20);
            panel.Children.Add(new Border());
            Layout(panel, new Size(100, 100));
            Assert.Equal(default(Color), Pixel(panel, 10, 50));
            Assert.Equal(Colors.Red, Pixel(panel, 50, 50));
            Assert.Equal(new Rect(100, 0, 0, 100), Slot(panel.Children[2]));
        });
    }

    /// <summary>
    /// Invalid spacing cannot enter the layout algorithm.
    /// </summary>
    [Theory]
    [InlineData(-1D)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidSpacing_IsRejected(double value)
    {
        WpfTestThread.Run(() =>
        {
            Assert.Throws<ArgumentException>(() => new UiDockPanel { Spacing = value });
        });
    }

    /// <summary>
    /// Creates a child with explicit dock and dimensions for deterministic layout assertions.
    /// </summary>
    private static Border AddChild(DockPanel panel, Dock dock, double width, double height)
    {
        var child = new Border { Width = width, Height = height };
        DockPanel.SetDock(child, dock);
        panel.Children.Add(child);
        return child;
    }

    /// <summary>
    /// Completes layout and pending rendering invalidation without a visible window.
    /// </summary>
    private static void Layout(FrameworkElement panel, Size size)
    {
        panel.Measure(size);
        panel.Arrange(new Rect(size));
        panel.UpdateLayout();
    }

    /// <summary>
    /// Reads the layout allocation rather than the child's aligned render bounds.
    /// </summary>
    private static Rect Slot(UIElement child)
    {
        var element = (FrameworkElement)child;
        return LayoutInformation.GetLayoutSlot(element);
    }

    /// <summary>
    /// Samples opaque or transparent pixels to distinguish gaps from ordinary empty space.
    /// </summary>
    private static Color Pixel(FrameworkElement panel, int x, int y)
    {
        var bitmap = new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(panel);
        var bytes = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), bytes, 4, 0);
        return Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
    }

    /// <summary>
    /// Records the constraint seen by a later child independently of the panel's desired size.
    /// </summary>
    private sealed class MeasureProbe : FrameworkElement
    {
        /// <summary>
        /// Gets the last measurement constraint supplied by the parent.
        /// </summary>
        public Size Constraint { get; private set; }

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            Constraint = availableSize;
            return new Size(10, 10);
        }
    }
}
