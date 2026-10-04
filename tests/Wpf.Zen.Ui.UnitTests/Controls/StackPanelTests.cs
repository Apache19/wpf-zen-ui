// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Zen.Ui.Controls;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Exercises slot geometry, gap pixels, variable-sized virtualization and generator lifetime.
/// </summary>
public class StackPanelTests
{
    /// <summary>
    /// Collapsed slots disappear while Hidden slots and child margins retain their layout space.
    /// </summary>
    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void NaturalLayout_SpacesOnlyParticipatingSlots_AndPreservesMargins(Orientation orientation)
    {
        WpfTestThread.Run(() =>
        {
            var panel = new UiStackPanel { Orientation = orientation, Spacing = 7 };
            var first = new Border { Width = 20, Height = 20, Margin = new Thickness(2) };
            var hidden = new Border { Width = 10, Height = 10, Visibility = Visibility.Hidden };
            var collapsed = new Border { Width = 100, Height = 100, Visibility = Visibility.Collapsed };
            panel.Children.Add(first);
            panel.Children.Add(collapsed);
            panel.Children.Add(hidden);
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(41, Along(panel.DesiredSize, orientation));
            panel.Arrange(new Rect(new Size(100, 100)));
            Assert.Equal(31, Along(LayoutInformation.GetLayoutSlot(hidden).Location, orientation));
            Assert.Equal(new Thickness(2), first.Margin);

            collapsed.Visibility = Visibility.Visible;
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(148, Along(panel.DesiredSize, orientation));
        });
    }

    /// <summary>
    /// Rendered pixels distinguish gaps from child margins and verify invalidation after resizing.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Painting_FillsOnlyGapSlots_AndUpdatesWithoutPanelResize(bool virtualPanel)
    {
        WpfTestThread.Run(() =>
        {
            Panel panel = virtualPanel
                ? new UiVirtualizingStackPanel { Spacing = 10, SpacingBrush = Brushes.Red }
                : new UiStackPanel { Spacing = 10, SpacingBrush = Brushes.Red };
            panel.Background = Brushes.White;
            var first = new Border { Height = 20, Background = Brushes.Blue, Margin = new Thickness(2) };
            var second = new Border { Height = 20, Background = Brushes.Blue };
            panel.Children.Add(first);
            panel.Children.Add(second);
            Layout(panel, new Size(100, 100));
            Assert.Equal(Colors.Red, Pixel(panel, 50, 28));
            Assert.Equal(Colors.White, Pixel(panel, 50, 22));
            Assert.Equal(Colors.White, Pixel(panel, 50, 60));
            first.Height = 30;
            Layout(panel, new Size(100, 100));
            Assert.Equal(Colors.Blue, Pixel(panel, 50, 28));
            Assert.Equal(Colors.Red, Pixel(panel, 50, 38));
            second.Visibility = Visibility.Collapsed;
            Layout(panel, new Size(100, 100));
            Assert.Equal(Colors.White, Pixel(panel, 50, 38));
        });
    }

    /// <summary>
    /// Both dependency properties reject values that cannot form a finite, nonnegative gap.
    /// </summary>
    [Theory]
    [InlineData(-1D)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidSpacing_IsRejected(double value)
    {
        WpfTestThread.Run(() =>
        {
            Assert.Throws<ArgumentException>(() => new UiStackPanel { Spacing = value });
            Assert.Throws<ArgumentException>(() => new UiVirtualizingStackPanel { Spacing = value });
        });
    }

    /// <summary>
    /// Large lists remain bounded, reuse containers and include spacing in both scrolling axes.
    /// </summary>
    [Theory]
    [InlineData(Orientation.Vertical, ScrollUnit.Pixel)]
    [InlineData(Orientation.Horizontal, ScrollUnit.Pixel)]
    [InlineData(Orientation.Vertical, ScrollUnit.Item)]
    [InlineData(Orientation.Horizontal, ScrollUnit.Item)]
    public void Virtualization_ScrollsAndRecycles_WithSpacingInExtent(Orientation orientation, ScrollUnit unit)
    {
        WpfTestThread.Run(() =>
        {
            ListBox list = CreateList(1000, orientation, unit);
            Host(list);
            UiVirtualizingStackPanel panel = Find<UiVirtualizingStackPanel>(list)!;
            Assert.True(panel.IsVirtualizationActive);
            Assert.InRange(panel.Children.Count, 1, 15);
            Assert.Equal(unit == ScrollUnit.Pixel ? 29990 : 1000,
                orientation == Orientation.Vertical ? panel.ExtentHeight : panel.ExtentWidth, 3);
            UIElement[] original = panel.Children.Cast<UIElement>().ToArray();
            SetOffset(panel, orientation, unit == ScrollUnit.Pixel ? 3000 : 100);
            list.UpdateLayout();
            Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(100));
            Assert.Null(list.ItemContainerGenerator.ContainerFromIndex(0));
            SetOffset(panel, orientation, unit == ScrollUnit.Pixel ? 6000 : 200);
            list.UpdateLayout();
            Assert.Contains(panel.Children.Cast<UIElement>(), original.Contains);
            Assert.InRange(panel.Children.Count, 1, 15);
            AssertGaps(panel, orientation, 10);
        });
    }

    /// <summary>
    /// Distant item and end requests cover the viewport after size estimates are corrected.
    /// </summary>
    [Theory]
    [InlineData(ScrollUnit.Pixel)]
    [InlineData(ScrollUnit.Item)]
    public void ScrollIntoView_AndEndScrolling_MeasureVariableSizesWithoutBlankViewport(ScrollUnit unit)
    {
        WpfTestThread.Run(() =>
        {
            ListBox list = CreateList(1000, Orientation.Vertical, unit, variable: true);
            Host(list);
            UiVirtualizingStackPanel panel = Find<UiVirtualizingStackPanel>(list)!;
            list.ScrollIntoView(list.Items[777]);
            list.UpdateLayout();
            Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(777));
            AssertGaps(panel, Orientation.Vertical, 10);
            panel.SetVerticalOffset(double.PositiveInfinity);
            list.UpdateLayout();
            var last = (UIElement)list.ItemContainerGenerator.ContainerFromIndex(999);
            Assert.NotNull(last);
            Rect slot = LayoutInformation.GetLayoutSlot((FrameworkElement)last);
            Assert.Equal(panel.ActualHeight, slot.Bottom, 3);
            Assert.True(panel.Children.Cast<FrameworkElement>().Any(child =>
                LayoutInformation.GetLayoutSlot(child).Top <= 0));
            Assert.InRange(panel.Children.Count, 1, 20);
        });
    }

    /// <summary>
    /// Collection edits preserve the visible container and update gaps, indices and empty extents.
    /// </summary>
    [Fact]
    public void CollectionChanges_AndSpacingChanges_RebuildLayoutAtCurrentScrollPosition()
    {
        WpfTestThread.Run(() =>
        {
            ListBox list = CreateList(1000, Orientation.Vertical, ScrollUnit.Pixel);
            var items = (ObservableCollection<Item>)list.ItemsSource;
            Host(list);
            UiVirtualizingStackPanel panel = Find<UiVirtualizingStackPanel>(list)!;
            panel.SetVerticalOffset(3000);
            list.UpdateLayout();
            var anchor = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(100);
            double anchorTop = LayoutInformation.GetLayoutSlot(anchor).Top;
            items.Insert(0, new Item(45));
            list.UpdateLayout();
            Assert.Same(anchor, list.ItemContainerGenerator.ContainerFromIndex(101));
            Assert.Equal(anchorTop, LayoutInformation.GetLayoutSlot(anchor).Top, 3);
            items.RemoveAt(110);
            items.Move(105, 106);
            items[103] = new Item(60);
            list.UpdateLayout();
            AssertGaps(panel, Orientation.Vertical, 10);
            panel.Spacing = 5;
            list.UpdateLayout();
            AssertGaps(panel, Orientation.Vertical, 5);
            items.Clear();
            list.UpdateLayout();
            Assert.Empty(panel.Children);
            Assert.Equal(0, panel.ExtentHeight);
            Assert.Equal(0, panel.VerticalOffset);
            items.Add(new Item(20));
            list.UpdateLayout();
            Assert.Equal(20, panel.ExtentHeight);
        });
    }

    /// <summary>
    /// Cache counts, complete-layout fallback and runtime container visibility affect realization.
    /// </summary>
    [Fact]
    public void Cache_DisabledVirtualization_AndContainerVisibility_AreHonored()
    {
        WpfTestThread.Run(() =>
        {
            ListBox list = CreateList(100, Orientation.Vertical, ScrollUnit.Pixel);
            VirtualizingPanel.SetCacheLengthUnit(list, VirtualizationCacheLengthUnit.Item);
            VirtualizingPanel.SetCacheLength(list, new VirtualizationCacheLength(2, 3));
            Host(list);
            UiVirtualizingStackPanel panel = Find<UiVirtualizingStackPanel>(list)!;
            panel.SetVerticalOffset(300);
            list.UpdateLayout();
            Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(8));
            Assert.Null(list.ItemContainerGenerator.ContainerFromIndex(7));
            Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(19));
            VirtualizingPanel.SetIsVirtualizing(list, false);
            list.UpdateLayout();
            Assert.False(panel.IsVirtualizationActive);
            Assert.Equal(100, panel.Children.Count);
            ((UIElement)list.ItemContainerGenerator.ContainerFromIndex(10)).Visibility = Visibility.Collapsed;
            list.UpdateLayout();
            Assert.Equal(2960, panel.ExtentHeight);
            AssertGaps(panel, Orientation.Vertical, 10);
        });
    }

    /// <summary>
    /// Public XAML resolves the panels and their shared dependency properties.
    /// </summary>
    [Fact]
    public void BothPanels_AreAvailableInPublicXamlNamespace()
    {
        WpfTestThread.Run(() =>
        {
            foreach (string name in new[] { "UiStackPanel", "UiVirtualizingStackPanel" })
            {
                var panel = (Panel)XamlReader.Parse($"<ui:{name} xmlns:ui=\"http://schemas.thezendev.com/wpf/2027\" Spacing=\"8\" SpacingBrush=\"Red\" Orientation=\"Horizontal\" />");
                Assert.Equal(8D, panel.GetValue(UiStackPanel.SpacingProperty));
            }
        });
    }

    /// <summary>
    /// Group generators use their own item sequence and retain complete layout geometry.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupedItems_FallBackToCompleteGroupLayout(bool virtualizeGroups)
    {
        WpfTestThread.Run(() =>
        {
            ListBox list = CreateList(30, Orientation.Vertical, ScrollUnit.Pixel, variable: true);
            var view = new ListCollectionView((ObservableCollection<Item>)list.ItemsSource);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Item.Length)));
            list.GroupStyle.Add(new GroupStyle { Panel = list.ItemsPanel });
            list.ItemsSource = view;
            VirtualizingPanel.SetIsVirtualizingWhenGrouping(list, virtualizeGroups);
            Host(list);
            UiVirtualizingStackPanel panel = Find<UiVirtualizingStackPanel>(list)!;
            Assert.False(panel.CanHierarchicallyScrollAndVirtualize);
            Assert.False(panel.IsVirtualizationActive);
            Assert.Equal(7, panel.Children.Count);
            AssertGaps(panel, Orientation.Vertical, 10);
            Assert.True(panel.ExtentHeight > panel.ViewportHeight);
            foreach (UIElement group in panel.Children)
            {
                UiVirtualizingStackPanel members = Find<UiVirtualizingStackPanel>(group)!;
                Assert.False(members.IsVirtualizationActive);
                Assert.False(members.CanHierarchicallyScrollAndVirtualize);
            }

            for (int index = 0; index < list.Items.Count; index++)
            {
                Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(index));
            }

            view.GroupDescriptions.Clear();
            list.UpdateLayout();
            Assert.True(Find<UiVirtualizingStackPanel>(list)!.IsVirtualizationActive);
        });
    }

    /// <summary>
    /// Runtime layout settings refresh realized geometry and scroll units.
    /// </summary>
    [Fact]
    public void Resize_Orientation_AndScrollUnitChanges_KeepValidLayout()
    {
        WpfTestThread.Run(() =>
        {
            ListBox list = CreateList(1000, Orientation.Vertical, ScrollUnit.Pixel, variable: true);
            Host(list);
            UiVirtualizingStackPanel panel = Find<UiVirtualizingStackPanel>(list)!;
            panel.SetVerticalOffset(1000);
            list.UpdateLayout();
            panel.Spacing = 25;
            list.UpdateLayout();
            AssertGaps(panel, Orientation.Vertical, 25);
            VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Item);
            list.UpdateLayout();
            Assert.Equal(1000, panel.ExtentHeight);
            panel.LineDown();
            list.UpdateLayout();
            AssertGaps(panel, Orientation.Vertical, 25);
            Layout((FrameworkElement)list.Parent, new Size(300, 600));
            Assert.InRange(panel.Children.Count, 2, 40);
            panel.Orientation = Orientation.Horizontal;
            System.Windows.Controls.ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Auto);
            list.UpdateLayout();
            Assert.True(panel.IsVirtualizationActive);
            AssertGaps(panel, Orientation.Horizontal, 25);
        });
    }

    /// <summary>
    /// Repeated distant offsets across strongly varying sizes leave no unrealized viewport holes.
    /// </summary>
    [Fact]
    public void RepeatedRandomScrolling_RealizesTheViewportWithVariableAndZeroSizes()
    {
        WpfTestThread.Run(() =>
        {
            ListBox list = CreateList(1000, Orientation.Vertical, ScrollUnit.Pixel);
            var items = (ObservableCollection<Item>)list.ItemsSource;
            for (int index = 0; index < items.Count; index++)
            {
                items[index] = new Item(index % 9 == 0 ? 350 : index % 6 == 0 ? 0 : 8);
            }

            Host(list);
            UiVirtualizingStackPanel panel = Find<UiVirtualizingStackPanel>(list)!;
            var random = new Random(472);
            for (int iteration = 0; iteration < 60; iteration++)
            {
                panel.SetVerticalOffset(random.NextDouble() * panel.ExtentHeight);
                list.UpdateLayout();
                AssertGaps(panel, Orientation.Vertical, 10);
                Rect[] slots = panel.Children.Cast<FrameworkElement>()
                    .Select(LayoutInformation.GetLayoutSlot).ToArray();
                Assert.True(slots.Min(rect => rect.Top) <= 10.001);
                Assert.True(slots.Max(rect => rect.Bottom) >= panel.ActualHeight - 0.001);
                Assert.InRange(slots.Length, 1, 35);
            }
        });
    }

    /// <summary>
    /// Descendant rectangles are revealed on both axes with viewport-relative return bounds.
    /// </summary>
    [Fact]
    public void MakeVisible_RevealsBothAxes_AndRejectsInvalidOffsets()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new UiVirtualizingStackPanel
            {
                CanHorizontallyScroll = true,
                CanVerticallyScroll = true,
                Spacing = 10,
            };
            for (int index = 0; index < 4; index++)
            {
                panel.Children.Add(new Border { Width = 300, Height = 100 });
            }

            Layout(panel, new Size(200, 200));
            Rect visible = panel.MakeVisible(panel.Children[3], new Rect(250, 0, 30, 100));
            Layout(panel, new Size(200, 200));
            Assert.Equal(80, panel.HorizontalOffset);
            Assert.Equal(230, panel.VerticalOffset);
            Assert.Equal(new Rect(170, 100, 30, 100), visible);
            Assert.Throws<ArgumentOutOfRangeException>(() => panel.SetVerticalOffset(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => panel.SetHorizontalOffset(double.NaN));
            Assert.Equal(Rect.Empty, panel.MakeVisible(new Border(), new Rect(0, 0, 10, 10)));
        });
    }

    /// <summary>
    /// Remeasure preserves explicit container edits and Standard mode discards outgoing containers.
    /// </summary>
    [Fact]
    public void ContainerSizeChange_AndStandardMode_KeepTheViewportFilled()
    {
        WpfTestThread.Run(() =>
        {
            ListBox list = CreateList(100, Orientation.Vertical, ScrollUnit.Pixel);
            VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Standard);
            Host(list);
            UiVirtualizingStackPanel panel = Find<UiVirtualizingStackPanel>(list)!;
            var first = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0);
            first.Height = 2;
            list.UpdateLayout();
            Assert.Equal(2, LayoutInformation.GetLayoutSlot(first).Height);
            AssertGaps(panel, Orientation.Vertical, 10);
            panel.SetVerticalOffset(600);
            list.UpdateLayout();
            Assert.Null(list.ItemContainerGenerator.ContainerFromIndex(0));
            Assert.DoesNotContain(first, panel.Children.Cast<UIElement>());
            panel.SetVerticalOffset(0);
            list.UpdateLayout();
            AssertGaps(panel, Orientation.Vertical, 10);
            Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(0));
        });
    }

    /// <summary>
    /// Collapsed boundary items do not add leading or trailing spacing to the extent.
    /// </summary>
    [Fact]
    public void LeadingAndTrailingCollapsedItems_HaveNoOuterGaps()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new UiVirtualizingStackPanel { Spacing = 8 };
            panel.Children.Add(new Border { Height = 20, Visibility = Visibility.Collapsed });
            panel.Children.Add(new Border { Height = 30 });
            panel.Children.Add(new Border { Height = 20, Visibility = Visibility.Collapsed });
            panel.Measure(new Size(100, double.PositiveInfinity));
            Assert.Equal(30, panel.DesiredSize.Height);
            panel.Arrange(new Rect(0, 0, 100, 100));
            Assert.Equal(0, LayoutInformation.GetLayoutSlot((FrameworkElement)panel.Children[1]).Top);
            panel.Children[1].Visibility = Visibility.Collapsed;
            panel.Measure(new Size(100, double.PositiveInfinity));
            Assert.Equal(0, panel.DesiredSize.Height);
        });
    }

    private static ListBox CreateList(int count, Orientation orientation, ScrollUnit unit, bool variable = false)
    {
        var factory = new FrameworkElementFactory(typeof(UiVirtualizingStackPanel));
        factory.SetValue(UiVirtualizingStackPanel.SpacingProperty, 10D);
        factory.SetValue(UiVirtualizingStackPanel.OrientationProperty, orientation);
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(orientation == Orientation.Vertical
            ? FrameworkElement.HeightProperty : FrameworkElement.WidthProperty, new Binding(nameof(Item.Length))));
        var list = new ListBox
        {
            ItemsPanel = new ItemsPanelTemplate(factory),
            ItemContainerStyle = style,
            ItemsSource = new ObservableCollection<Item>(Enumerable.Range(0, count)
                .Select(index => new Item(variable ? 10 + ((index % 7) * 11) : 20))),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        System.Windows.Controls.ScrollViewer.SetCanContentScroll(list, true);
        System.Windows.Controls.ScrollViewer.SetHorizontalScrollBarVisibility(list,
            orientation == Orientation.Horizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetScrollUnit(list, unit);
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        VirtualizingPanel.SetCacheLength(list, new VirtualizationCacheLength(0));
        return list;
    }

    private static void Host(FrameworkElement element)
    {
        var host = new Grid();
        host.Children.Add(element);
        Layout(host, new Size(200, 200));
    }

    private static void Layout(FrameworkElement element, Size size)
    {
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();
    }

    private static void AssertGaps(UiVirtualizingStackPanel panel, Orientation orientation, double spacing)
    {
        Rect[] slots = panel.Children.Cast<FrameworkElement>()
            .Where(child => child.Visibility != Visibility.Collapsed)
            .Select(LayoutInformation.GetLayoutSlot)
            .OrderBy(rect => Along(rect.Location, orientation)).ToArray();
        for (int index = 1; index < slots.Length; index++)
        {
            Assert.Equal(spacing, Along(slots[index].Location, orientation)
                - Along(slots[index - 1].Location, orientation)
                - Along(slots[index - 1].Size, orientation), 3);
        }
    }

    private static Color Pixel(FrameworkElement element, int x, int y)
    {
        var bitmap = new RenderTargetBitmap(100, 100, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    private static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            if (Find<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static double Along(Size size, Orientation orientation)
    {
        return orientation == Orientation.Vertical ? size.Height : size.Width;
    }

    private static double Along(Point point, Orientation orientation)
    {
        return orientation == Orientation.Vertical ? point.Y : point.X;
    }

    private static void SetOffset(UiVirtualizingStackPanel panel, Orientation orientation, double offset)
    {
        if (orientation == Orientation.Vertical)
        {
            panel.SetVerticalOffset(offset);
        }
        else
        {
            panel.SetHorizontalOffset(offset);
        }
    }

    /// <summary>
    /// Supplies a reference-identity data item with a bindable size for the generated container.
    /// </summary>
    /// <param name="length">The container's desired length along the stack.</param>
    public sealed class Item(double length)
    {
        /// <summary>
        /// Gets the length used by the test item-container style.
        /// </summary>
        public double Length { get; } = length;
    }
}
