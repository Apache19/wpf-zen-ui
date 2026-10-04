// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Compares WPF and custom panel contracts using real presentation sources and item generators.
/// </summary>
public class StackPanelCompatibilityTests
{
    /// <summary>
    /// Offsets use the active scroll unit and remain relative to the full content after scrolling.
    /// </summary>
    [Theory]
    [InlineData(Orientation.Vertical, ScrollUnit.Pixel)]
    [InlineData(Orientation.Vertical, ScrollUnit.Item)]
    [InlineData(Orientation.Horizontal, ScrollUnit.Pixel)]
    [InlineData(Orientation.Horizontal, ScrollUnit.Item)]
    public void ItemOffset_MatchesNativeUnits(Orientation orientation, ScrollUnit unit)
    {
        WpfTestThread.Run(() =>
        {
            foreach (bool custom in new[] { false, true })
            {
                ListBox list = Create(custom, unit, orientation: orientation);
                using HwndSource source = Host(list);
                VirtualizingPanel panel = Find(list)!;
                Assert.Throws<ArgumentNullException>(() => panel.GetItemOffset(null!));
                Assert.Equal(0, panel.GetItemOffset(new Border()));
                foreach (int index in new[] { 2, 60 })
                {
                    list.ScrollIntoView(list.Items[index]);
                    Settle(list);
                    var target = (UIElement)list.ItemContainerGenerator.ContainerFromIndex(index);
                    Assert.NotNull(target);
                    Assert.Equal(unit == ScrollUnit.Pixel ? index * 20 : index,
                        panel.GetItemOffset(target), 3);
                }

                if (panel is UiVirtualizingStackPanel spaced)
                {
                    spaced.Spacing = 7;
                    list.UpdateLayout();
                    list.ScrollIntoView(list.Items[60]);
                    Settle(list);
                    Assert.Equal(unit == ScrollUnit.Pixel ? 60 * 27 : 60,
                        panel.GetItemOffset((UIElement)list.ItemContainerGenerator.ContainerFromIndex(60)), 3);
                }
            }
        });
    }

    /// <summary>
    /// Both event registration APIs allow cancellation and later release in either cleanup mode.
    /// </summary>
    [Theory]
    [InlineData(VirtualizationMode.Recycling, false)]
    [InlineData(VirtualizationMode.Recycling, true)]
    [InlineData(VirtualizationMode.Standard, false)]
    [InlineData(VirtualizationMode.Standard, true)]
    public void Cleanup_CanBeCanceledAndHandlerRemoved(VirtualizationMode mode, bool customApi)
    {
        WpfTestThread.Run(() =>
        {
            foreach (bool custom in new[] { false, true })
            {
                ListBox list = Create(custom, ScrollUnit.Pixel);
                VirtualizingPanel.SetVirtualizationMode(list, mode);
                int notifications = 0;
                UIElement? first = null;
                CleanUpVirtualizedItemEventHandler handler = (_, args) =>
                {
                    notifications++;
                    Assert.Same(list.ItemContainerGenerator.ItemFromContainer(args.UIElement), args.Value);
                    args.Cancel = ReferenceEquals(args.UIElement, first);
                };
                if (customApi)
                {
                    UiVirtualizingStackPanel.AddCleanUpVirtualizedItemHandler(list, handler);
                }
                else
                {
                    VirtualizingStackPanel.AddCleanUpVirtualizedItemHandler(list, handler);
                }

                using HwndSource source = Host(list);
                VirtualizingPanel panel = Find(list)!;
                first = (UIElement)list.ItemContainerGenerator.ContainerFromIndex(0);
                // Native Standard mode batches cleanup until enough offscreen children accumulate.
                foreach (double offset in new[] { 500D, 1000D, 1500D, 1800D })
                {
                    ((IScrollInfo)panel).SetVerticalOffset(offset);
                    Settle(list);
                }

                Assert.True(notifications > 0, $"custom={custom}, mode={mode}");
                Assert.Same(first, list.ItemContainerGenerator.ContainerFromIndex(0));
                if (customApi)
                {
                    UiVirtualizingStackPanel.RemoveCleanUpVirtualizedItemHandler(list, handler);
                }
                else
                {
                    VirtualizingStackPanel.RemoveCleanUpVirtualizedItemHandler(list, handler);
                }

                int previous = notifications;
                foreach (double offset in new[] { 1300D, 800D, 300D })
                {
                    ((IScrollInfo)panel).SetVerticalOffset(offset);
                    Settle(list);
                }

                Assert.Null(list.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.Equal(previous, notifications);
            }
        });
    }

    /// <summary>
    /// Shrinking a realized item reduces cross-axis extent and clamps an obsolete scroll offset.
    /// </summary>
    [Theory]
    [InlineData(Orientation.Vertical)]
    [InlineData(Orientation.Horizontal)]
    public void CrossAxisShrink_UpdatesExtentAndOffset(Orientation orientation)
    {
        WpfTestThread.Run(() =>
        {
            foreach (bool custom in new[] { false, true })
            {
                ListBox list = Create(custom, ScrollUnit.Pixel, 1, orientation);
                using HwndSource source = Host(list);
                var child = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0);
                var scrolling = (IScrollInfo)Find(list)!;
                DependencyProperty size = orientation == Orientation.Vertical
                    ? FrameworkElement.WidthProperty : FrameworkElement.HeightProperty;
                child.SetValue(size, 600D);
                list.UpdateLayout();
                Assert.True((orientation == Orientation.Vertical
                    ? scrolling.ExtentWidth : scrolling.ExtentHeight) >= 600);
                if (custom && orientation == Orientation.Vertical)
                {
                    scrolling.SetHorizontalOffset(250);
                }
                else if (custom)
                {
                    scrolling.SetVerticalOffset(250);
                }

                list.UpdateLayout();
                child.SetValue(size, 100D);
                Settle(list);
                Assert.True((orientation == Orientation.Vertical
                    ? scrolling.ExtentWidth : scrolling.ExtentHeight) < 400,
                    $"custom={custom}, extent={scrolling.ExtentWidth}/{scrolling.ExtentHeight}");
                Assert.Equal(0, orientation == Orientation.Vertical
                    ? scrolling.HorizontalOffset : scrolling.VerticalOffset);
            }
        });
    }

    private static ListBox Create(bool custom, ScrollUnit unit, int count = 100,
        Orientation orientation = Orientation.Vertical, double spacing = 0)
    {
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(orientation == Orientation.Vertical
            ? FrameworkElement.HeightProperty : FrameworkElement.WidthProperty, 20D));
        var factory = new FrameworkElementFactory(
            custom ? typeof(UiVirtualizingStackPanel) : typeof(VirtualizingStackPanel));
        factory.SetValue(custom ? UiVirtualizingStackPanel.OrientationProperty
            : VirtualizingStackPanel.OrientationProperty, orientation);
        if (custom)
        {
            factory.SetValue(UiVirtualizingStackPanel.SpacingProperty, spacing);
        }

        var list = new ListBox
        {
            ItemsPanel = new ItemsPanelTemplate(factory),
            ItemsSource = Enumerable.Range(0, count).ToArray(),
            ItemContainerStyle = style,
        };
        System.Windows.Controls.ScrollViewer.SetCanContentScroll(list, true);
        System.Windows.Controls.ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        System.Windows.Controls.ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(list, unit);
        VirtualizingPanel.SetCacheLength(list, new VirtualizationCacheLength(0));
        return list;
    }

    /// <summary>
    /// Supplies the visibility and deferred layout lifecycle used by native group containers.
    /// </summary>
    private static HwndSource Host(FrameworkElement element)
    {
        var source = new HwndSource(new HwndSourceParameters("Stack compatibility tests")
        {
            Width = 300,
            Height = 200,
            WindowStyle = unchecked((int)0x80000000),
        });
        var grid = new Grid();
        grid.Children.Add(element);
        source.RootVisual = grid;
        grid.Measure(new Size(300, 200));
        grid.Arrange(new Rect(0, 0, 300, 200));
        grid.UpdateLayout();
        return source;
    }

    private static void Settle(FrameworkElement element)
    {
        element.UpdateLayout();
        element.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(element.UpdateLayout));
    }

    private static VirtualizingPanel? Find(DependencyObject root)
    {
        if (root is VirtualizingPanel panel)
        {
            return panel;
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (Find(VisualTreeHelper.GetChild(root, index)) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }
}
