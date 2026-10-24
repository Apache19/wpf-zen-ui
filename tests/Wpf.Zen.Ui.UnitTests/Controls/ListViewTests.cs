// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using NativeListViewItem = System.Windows.Controls.ListViewItem;
using ScrollSpillMode = Wpf.Zen.Ui.Controls.ScrollSpillMode;
using UiListItemContainerStyleSelector = Wpf.Zen.Ui.Controls.ListItemContainerStyleSelector;
using UiListView = Wpf.Zen.Ui.Controls.ListView;
using UiListViewItem = Wpf.Zen.Ui.Controls.ListViewItem;
using UiScrollViewer = Wpf.Zen.Ui.Controls.UiScrollViewer;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies ListView containers, grouped scrolling, and GridView header resizing.
/// </summary>
public class ListViewTests
{
    /// <summary>
    /// Both ListView templates forward attached scroll-spill settings through active bindings.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListViewTemplate_ForwardsScrollSpillModeWithoutPathErrors(bool useGridView)
    {
        RunOnStaThread(() =>
        {
            var listView = new UiListView { Width = 300, Height = 150 };
            if (useGridView)
            {
                listView.View = new Wpf.Zen.Ui.Controls.GridView();
            }

            Arrange(listView, 300, 150);
            var viewer = Assert.IsType<UiScrollViewer>(FindVisualChild<UiScrollViewer>(listView));
            Assert.Equal(ScrollSpillMode.Disabled, viewer.ScrollSpillMode);

            var expression = System.Windows.Data.BindingOperations.GetBindingExpression(
                viewer,
                UiScrollViewer.ScrollSpillModeProperty
            );
            Assert.NotNull(expression);
            Assert.Equal(System.Windows.Data.BindingStatus.Active, expression.Status);
            Assert.Same(UiScrollViewer.ScrollSpillModeProperty, expression.ParentBinding.Path.PathParameters[0]);

            UiScrollViewer.SetScrollSpillMode(listView, ScrollSpillMode.Native);
            Assert.Equal(ScrollSpillMode.Native, viewer.ScrollSpillMode);
            UiScrollViewer.SetScrollSpillMode(listView, ScrollSpillMode.Enabled);
            Assert.Equal(ScrollSpillMode.Enabled, viewer.ScrollSpillMode);

            viewer.ScrollSpillMode = ScrollSpillMode.Disabled;
            UiScrollViewer.SetScrollSpillMode(listView, ScrollSpillMode.Native);
            Assert.Equal(ScrollSpillMode.Disabled, viewer.ScrollSpillMode);
        });
    }

    /// <summary>
    /// Generated containers receive the selector's positional styles, including a single item.
    /// </summary>
    [Fact]
    public void ListItemContainerStyleSelector_AppliesSpecificPositionalRulesToListView()
    {
        RunOnStaThread(static () =>
        {
            var selector = new UiListItemContainerStyleSelector
            {
                DefaultStyle = CreateTaggedListViewItemStyle("default"),
                EvenItemStyle = CreateTaggedListViewItemStyle("even"),
                FirstItemStyle = CreateTaggedListViewItemStyle("first"),
                LastItemStyle = CreateTaggedListViewItemStyle("last"),
                OddItemStyle = CreateTaggedListViewItemStyle("odd"),
                SingleItemStyle = CreateTaggedListViewItemStyle("single"),
            };
            var listView = new UiListView
            {
                Height = 200,
                ItemContainerStyleSelector = selector,
                ItemsSource = new[] { "A", "B", "C", "D" },
                Width = 300,
            };

            Arrange(listView, 300, 200);

            Assert.Equal("first", GetListViewItem(listView, 0).Tag);
            Assert.Equal("odd", GetListViewItem(listView, 1).Tag);
            Assert.Equal("even", GetListViewItem(listView, 2).Tag);
            Assert.Equal("last", GetListViewItem(listView, 3).Tag);

            listView.ItemsSource = new[] { "only" };
            listView.UpdateLayout();

            Assert.Equal("single", GetListViewItem(listView, 0).Tag);
        });
    }

    /// <summary>
    /// Collection edits update positional styles without replacing surviving item containers.
    /// </summary>
    [Fact]
    public void ListItemContainerStyleSelector_ReevaluatesSurvivingListViewContainersAfterCollectionChanges()
    {
        RunOnStaThread(static () =>
        {
            var items = new ObservableCollection<string> { "A", "B" };
            var listView = new UiListView
            {
                Height = 200,
                ItemContainerStyleSelector = new UiListItemContainerStyleSelector
                {
                    DefaultStyle = CreateTaggedListViewItemStyle("default"),
                    FirstItemStyle = CreateTaggedListViewItemStyle("first"),
                    LastItemStyle = CreateTaggedListViewItemStyle("last"),
                    OddItemStyle = CreateTaggedListViewItemStyle("odd"),
                    SingleItemStyle = CreateTaggedListViewItemStyle("single"),
                },
                ItemsSource = items,
                Width = 300,
            };

            Arrange(listView, 300, 200);
            UiListViewItem secondContainer = GetListViewItem(listView, 1);
            Assert.Equal("last", secondContainer.Tag);

            items.Add("C");
            listView.UpdateLayout();

            Assert.Same(secondContainer, GetListViewItem(listView, 1));
            Assert.Equal("odd", secondContainer.Tag);
            Assert.Equal("last", GetListViewItem(listView, 2).Tag);

            items.RemoveAt(0);
            listView.UpdateLayout();

            Assert.Same(secondContainer, GetListViewItem(listView, 0));
            Assert.Equal("first", secondContainer.Tag);

            items.RemoveAt(1);
            listView.UpdateLayout();

            Assert.Equal("single", GetListViewItem(listView, 0).Tag);
        });
    }

    /// <summary>
    /// Explicit container styles remain authoritative when collection positions change.
    /// </summary>
    [Fact]
    public void ListItemContainerStyleSelector_PreservesExplicitListViewContainerStyle()
    {
        RunOnStaThread(static () =>
        {
            Style explicitStyle = CreateTaggedListViewItemStyle("explicit");
            var explicitContainer = new UiListViewItem { Content = "A", Style = explicitStyle };
            var listView = new UiListView
            {
                Height = 200,
                ItemContainerStyleSelector = new UiListItemContainerStyleSelector
                {
                    FirstItemStyle = CreateTaggedListViewItemStyle("first"),
                    LastItemStyle = CreateTaggedListViewItemStyle("last"),
                },
                Width = 300,
            };
            listView.Items.Add(explicitContainer);
            listView.Items.Add("B");

            Arrange(listView, 300, 200);
            Style runtimeStyle = CreateTaggedListViewItemStyle("runtime");
            UiListViewItem generatedContainer = GetListViewItem(listView, 1);
            generatedContainer.Style = runtimeStyle;
            listView.Items.Insert(0, "new");
            listView.UpdateLayout();

            Assert.Same(explicitStyle, explicitContainer.Style);
            Assert.Equal("explicit", explicitContainer.Tag);
            Assert.Same(runtimeStyle, generatedContainer.Style);
            Assert.Equal("runtime", generatedContainer.Tag);
        });
    }

    /// <summary>
    /// Native and library ListViewItem instances are accepted as their own containers.
    /// </summary>
    [Fact]
    public void ListView_AcceptsNativeAndFluentItemsAsContainers()
    {
        RunOnStaThread(static () =>
        {
            var listView = new TestListView();

            Assert.True(listView.IsOwnContainer(new NativeListViewItem()));
            Assert.True(listView.IsOwnContainer(new UiListViewItem()));
            Assert.False(listView.IsOwnContainer("data item"));
        });
    }

    /// <summary>
    /// Generated item presenters retain the ListView's content template selector.
    /// </summary>
    [Fact]
    public void ListViewItemTemplate_ForwardsContentTemplateSelector()
    {
        RunOnStaThread(static () =>
        {
            var selector = new TestTemplateSelector();
            var listView = new UiListView
            {
                Height = 100,
                ItemTemplateSelector = selector,
                ItemsSource = new[] { "First" },
                Width = 300,
            };

            Arrange(listView, 300, 100);

            var item = Assert.IsType<UiListViewItem>(
                listView.ItemContainerGenerator.ContainerFromIndex(0)
            );
            var presenter = Assert.IsType<ContentPresenter>(FindVisualChild<ContentPresenter>(item));

            Assert.Same(selector, presenter.ContentTemplateSelector);
        });
    }

    /// <summary>
    /// Grouped ListView uses logical scrolling only when grouped virtualization is enabled.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupedListView_UsesLogicalScrollingOnlyWhenGroupedVirtualizationIsEnabled(
        bool isVirtualizingWhenGrouping
    )
    {
        RunOnStaThread(() =>
        {
            var listView = new UiListView
            {
                Height = 160,
                ItemsSource = new[] { "A", "A", "B" },
                Width = 400,
            };
            listView.GroupStyle.Add(new GroupStyle());
            listView.Items.GroupDescriptions.Add(new PropertyGroupDescription());
            listView.Items.Refresh();
            VirtualizingPanel.SetIsVirtualizingWhenGrouping(listView, isVirtualizingWhenGrouping);

            Arrange(listView, 400, 160);

            Assert.True(listView.IsGrouping);
            var scrollViewer = Assert.IsType<UiScrollViewer>(
                FindVisualChild<UiScrollViewer>(listView)
            );
            Assert.Equal(isVirtualizingWhenGrouping, scrollViewer.CanContentScroll);
        });
    }

    /// <summary>
    /// The trailing resize handle follows the last GridView column after column reordering.
    /// </summary>
    [Fact]
    public void FluentColumnHeaders_DisableTheLastTrailingResizeHandle()
    {
        RunOnStaThread(static () =>
        {
            var view = new Wpf.Zen.Ui.Controls.GridView();
            view.Columns.Add(new Wpf.Zen.Ui.Controls.GridViewColumn { Header = "First", Width = new GridLength(1, GridUnitType.Star) });
            view.Columns.Add(new Wpf.Zen.Ui.Controls.GridViewColumn { Header = "Last", Width = new GridLength(1, GridUnitType.Star) });
            var listView = new UiListView
            {
                Height = 160,
                ItemsSource = new[] { "Value" },
                View = view,
                Width = 500,
            };
            var root = new StackPanel();
            root.Children.Add(listView);
            var window = new Window
            {
                Content = root,
                Height = 360,
                ShowInTaskbar = false,
                Width = 540,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                window.Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                GridViewColumnHeader[] listHeaders = FindVisualChildren<GridViewColumnHeader>(listView)
                    .Where(static header => header.Role == GridViewColumnHeaderRole.Normal)
                    .ToArray();
                GridViewColumnHeader firstListHeader = listHeaders.Single(header =>
                    ReferenceEquals(header.Column, view.Columns[0])
                );
                GridViewColumnHeader lastListHeader = listHeaders.Single(header =>
                    ReferenceEquals(header.Column, view.Columns[1])
                );
                Thumb firstListGripper = Assert.IsType<Thumb>(
                    firstListHeader.Template.FindName("PART_HeaderGripper", firstListHeader)
                );
                Thumb lastListGripper = Assert.IsType<Thumb>(
                    lastListHeader.Template.FindName("PART_HeaderGripper", lastListHeader)
                );

                Assert.True(firstListGripper.IsHitTestVisible);
                Assert.False(lastListGripper.IsHitTestVisible);

                view.Columns.Move(1, 0);
                window.Dispatcher.Invoke(
                    static () => { },
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle
                );

                Assert.False(firstListGripper.IsHitTestVisible);
                Assert.True(lastListGripper.IsHitTestVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static UiListViewItem GetListViewItem(UiListView listView, int index)
    {
        return Assert.IsType<UiListViewItem>(listView.ItemContainerGenerator.ContainerFromIndex(index));
    }

    private static Style CreateTaggedListViewItemStyle(string tag)
    {
        var style = new Style(typeof(NativeListViewItem));
        style.Setters.Add(new Setter(FrameworkElement.TagProperty, tag));
        return style;
    }

    private static void Arrange(FrameworkElement element, double width, double height)
    {
        // A control receives its implicit theme style only after it participates in a logical tree.
        // Host it exactly as an application would instead of testing an uninitialized bare element.
        var host = new Grid();
        host.Children.Add(element);
        host.Measure(new Size(width, height));
        host.Arrange(new Rect(0, 0, width, height));
        host.UpdateLayout();
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T result)
            {
                return result;
            }

            if (FindVisualChild<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T result)
            {
                yield return result;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class TestListView : UiListView
    {
        /// <summary>
        /// Exposes native container recognition without generating item containers.
        /// </summary>
        public bool IsOwnContainer(object item)
        {
            return IsItemItsOwnContainerOverride(item);
        }
    }

    private sealed class TestTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return new DataTemplate();
        }
    }
}
