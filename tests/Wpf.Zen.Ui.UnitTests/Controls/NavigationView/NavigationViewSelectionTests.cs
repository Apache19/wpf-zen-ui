// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using NativeScrollViewer = System.Windows.Controls.ScrollViewer;
using NavigationControl = Wpf.Zen.Ui.Controls.NavigationView;
using SuggestBox = Wpf.Zen.Ui.Controls.AutoSuggestBox;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

public class NavigationViewSelectionTests
{
    [Fact]
    public void PublicActivation_UpdatesRegisteredBranchesWithoutChangingNavigation()
    {
        WpfTestThread.Run(() =>
        {
            var first = new NavigationViewItem("First", typeof(FirstPage));
            var second = new NavigationViewItem("Second", typeof(SecondPage));
            var branch = new NavigationViewItem { Header = "Branch" };
            branch.Items.Add(first);
            branch.Items.Add(second);
            var navigation = new NavigationControl();
            navigation.MenuItems.Add(branch);

            Assert.True(navigation.NavigateAndCommit(typeof(FirstPage)));
            first.Deactivate(navigation);
            Assert.False(first.IsActive);
            Assert.False(branch.IsDescendantActive);

            first.Activate(navigation);
            Assert.True(first.IsActive);
            Assert.True(branch.IsDescendantActive);
            second.Activate(navigation);
            first.Deactivate(navigation);
            Assert.True(branch.IsDescendantActive);
            second.Deactivate(navigation);
            Assert.False(branch.IsBranchActive);
            Assert.Same(first, navigation.SelectedItem);
            Assert.IsType<FirstPage>(navigation.CurrentLocation?.Page);
            Assert.False(navigation.CanGoBack);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReloadedDataBranches_ProjectSelectionAndRevealHeader(bool navigateWhileUnloaded)
    {
        WpfTestThread.Run(() =>
        {
            var first = new NavigationItem("First", typeof(FirstPage));
            var second = new NavigationItem("Second", typeof(SecondPage));
            var branchA = new NavigationItem("Branch A") { Children = new[] { first } };
            var branchB = new NavigationItem("Branch B") { Children = new[] { second } };
            var navigation = new NavigationControl { IsPaneOpen = true };
            navigation.MenuItems.Add(branchA);
            for (var index = 0; index < 35; index++)
            {
                navigation.MenuItems.Add(new NavigationItem($"Before {index}"));
            }

            navigation.MenuItems.Add(branchB);
            var window = CreateWindow(navigation);
            try
            {
                window.Show();
                DrainDispatcher();
                var list = Assert.IsType<NavigationViewList>(navigation.Template.FindName("PART_MenuItemsList", navigation));
                var rootA = Assert.IsType<NavigationViewItem>(list.ItemContainerGenerator.ContainerFromItem(branchA));
                var rootB = Assert.IsType<NavigationViewItem>(list.ItemContainerGenerator.ContainerFromItem(branchB));
                var scroller = Assert.IsAssignableFrom<NativeScrollViewer>(list.Template.FindName("ScrollViewer", list));
                Assert.True(navigation.NavigateAndCommit(typeof(FirstPage)));
                DrainDispatcher();
                Assert.True(rootA.IsDescendantActive);

                window.Content = null;
                DrainDispatcher();
                if (navigateWhileUnloaded)
                {
                    Assert.True(navigation.NavigateAndCommit(typeof(SecondPage)));
                }

                window.Content = navigation;
                DrainDispatcher();
                // This is the retained-container path: no generator preparation repairs state.
                Assert.Same(rootB, list.ItemContainerGenerator.ContainerFromItem(branchB));
                if (!navigateWhileUnloaded)
                {
                    Assert.True(navigation.NavigateAndCommit(typeof(SecondPage)));
                    DrainDispatcher();
                }

                Assert.False(rootA.IsBranchActive);
                Assert.True(rootB.IsDescendantActive);
                Assert.False(rootB.IsExpanded);
                Assert.Same(second, navigation.SelectedItem);
                AssertHeaderInViewport(rootB, scroller);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void GeneratedContainerReplacement_PreservesEntryActivation()
    {
        WpfTestThread.Run(() =>
        {
            var leaf = new NavigationItem("Leaf", typeof(FirstPage));
            var branch = new NavigationItem("Branch") { Children = new[] { leaf } };
            var navigation = new NavigationControl { IsPaneOpen = true };
            navigation.MenuItems.Add(branch);
            var window = CreateWindow(navigation);
            try
            {
                window.Show();
                DrainDispatcher();
                var list = Assert.IsType<NavigationViewList>(navigation.Template.FindName("PART_MenuItemsList", navigation));
                var root = Assert.IsType<NavigationViewItem>(list.ItemContainerGenerator.ContainerFromItem(branch));
                root.IsExpanded = true;
                DrainDispatcher();
                Assert.True(navigation.NavigateAndCommit(typeof(FirstPage)));
                DrainDispatcher();
                var original = Assert.IsType<NavigationViewItem>(root.ItemContainerGenerator.ContainerFromItem(leaf));

                // Generator cleanup must only clear the discarded visual, not deactivate the entry.
                root.Items.Refresh();
                DrainDispatcher();
                var replacement = Assert.IsType<NavigationViewItem>(root.ItemContainerGenerator.ContainerFromItem(leaf));
                Assert.NotSame(original, replacement);
                Assert.False(original.IsActive);
                Assert.True(replacement.IsActive);
                Assert.True(root.IsDescendantActive);

                // Public deactivation must survive the same replacement instead of being resurrected.
                replacement.Deactivate(navigation);
                Assert.False(root.IsBranchActive);
                root.Items.Refresh();
                DrainDispatcher();
                var inactive = Assert.IsType<NavigationViewItem>(root.ItemContainerGenerator.ContainerFromItem(leaf));
                Assert.NotSame(replacement, inactive);
                Assert.False(inactive.IsActive);
                Assert.False(root.IsBranchActive);
                Assert.Same(leaf, navigation.SelectedItem);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void UnrealizedDescendant_UpdatesBranchAcrossNavigationAndCollectionChanges()
    {
        WpfTestThread.Run(() =>
        {
            var leaf = new NavigationItem("Leaf", typeof(FirstPage));
            var children = new ObservableCollection<object> { leaf };
            var branch = new NavigationViewItem { Header = "Branch", ItemsSource = children };
            var root = new NavigationViewItem { Header = "Root" };
            root.Items.Add(branch);
            var navigation = new NavigationControl();
            navigation.MenuItems.Add(root);
            navigation.MenuItems.Add(new NavigationItem("Other", typeof(SecondPage)));

            Assert.True(navigation.NavigateAndCommit(typeof(FirstPage)));
            Assert.Null(branch.ItemContainerGenerator.ContainerFromItem(leaf));
            Assert.True(root.IsDescendantActive);
            Assert.True(branch.IsDescendantActive);
            Assert.False(root.IsActive);
            Assert.False(branch.IsExpanded);
            Assert.Same(leaf, navigation.SelectedItem);

            Assert.True(navigation.NavigateAndCommit(typeof(SecondPage)));
            Assert.False(root.IsBranchActive);
            Assert.False(branch.IsBranchActive);
            Assert.True(navigation.GoBackAndCommit());
            Assert.True(root.IsBranchActive);

            children.Clear();
            Assert.False(root.IsBranchActive);
            Assert.False(branch.IsBranchActive);
            Assert.Null(navigation.SelectedItem);
            Assert.IsType<FirstPage>(navigation.CurrentLocation?.Page);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ExternalNavigation_RevealsVisibleHeaderWithoutExpanding(bool expanded, bool navigateBeforeLoad)
    {
        WpfTestThread.Run(() =>
        {
            var leaf = new NavigationItem("Destination", typeof(FirstPage));
            var branch = new NavigationItem("Branch") { Children = new[] { leaf } };
            var navigation = new NavigationControl
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
                AutoSuggestBox = new SuggestBox(),
            };
            for (var index = 0; index < 35; index++)
            {
                navigation.MenuItems.Add(new NavigationItem($"Before {index}"));
            }

            navigation.MenuItems.Add(branch);
            for (var index = 0; index < 35; index++)
            {
                navigation.MenuItems.Add(new NavigationItem($"After {index}"));
            }

            if (navigateBeforeLoad)
            {
                Assert.True(navigation.NavigateAndCommit(typeof(FirstPage)));
            }

            var window = CreateWindow(navigation);
            try
            {
                window.Show();
                DrainDispatcher();
                var list = Assert.IsType<NavigationViewList>(navigation.Template.FindName("PART_MenuItemsList", navigation));
                var root = Assert.IsType<NavigationViewItem>(list.ItemContainerGenerator.ContainerFromItem(branch));
                var scroller = Assert.IsAssignableFrom<NativeScrollViewer>(list.Template.FindName("ScrollViewer", list));
                Assert.True(scroller.ScrollableHeight > 0);

                if (!navigateBeforeLoad)
                {
                    root.IsExpanded = expanded;
                    DrainDispatcher();
                    scroller.ScrollToTop();
                    DrainDispatcher();
                    Assert.Equal(0, scroller.VerticalOffset);

                    navigation.AutoSuggestBox.RaiseEvent(new AutoSuggestBoxQuerySubmittedEventArgs(
                        SuggestBox.QuerySubmittedEvent, navigation.AutoSuggestBox)
                    {
                        QueryText = "Destination",
                    });
                    DrainDispatcher();
                }

                Assert.Same(leaf, navigation.SelectedItem);
                Assert.True(root.IsDescendantActive);
                Assert.False(root.IsActive);
                Assert.Equal(expanded, root.IsExpanded);
                Assert.False(root.IsSubmenuOpen);
                Assert.True(scroller.VerticalOffset > 0);

                var visibleItem = expanded
                    ? Assert.IsType<NavigationViewItem>(root.ItemContainerGenerator.ContainerFromItem(leaf))
                    : root;
                AssertHeaderInViewport(visibleItem, scroller);
                if (expanded)
                {
                    Assert.True(visibleItem.IsActive);
                }

                // A canceled request must preserve both the current branch and the user's scroll.
                scroller.ScrollToTop();
                DrainDispatcher();
                navigation.Navigating += (_, args) => args.Cancel = true;
                Assert.False(navigation.NavigateAndCommit(typeof(SecondPage)));
                DrainDispatcher();
                Assert.True(root.IsBranchActive);
                Assert.Equal(0, scroller.VerticalOffset);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void NestedCollapsedBranch_RevealsNearestVisibleAncestorAndKeepsLatestNavigation()
    {
        WpfTestThread.Run(() =>
        {
            var leaf = new NavigationViewItem("Leaf", typeof(FirstPage));
            var branch = new NavigationViewItem { Header = "Branch" };
            branch.Items.Add(leaf);
            var root = new NavigationViewItem { Header = "Root", IsExpanded = true };
            for (var index = 0; index < 35; index++)
            {
                root.Items.Add(new NavigationViewItem { Header = $"Before {index}" });
            }

            root.Items.Add(branch);
            var other = new NavigationViewItem("Other", typeof(SecondPage));
            var navigation = new NavigationControl { IsPaneOpen = true };
            navigation.MenuItems.Add(other);
            navigation.MenuItems.Add(root);
            var window = CreateWindow(navigation);
            try
            {
                window.Show();
                DrainDispatcher();
                var list = Assert.IsType<NavigationViewList>(navigation.Template.FindName("PART_MenuItemsList", navigation));
                var scroller = Assert.IsAssignableFrom<NativeScrollViewer>(list.Template.FindName("ScrollViewer", list));

                Assert.True(navigation.NavigateAndCommit(typeof(FirstPage)));
                DrainDispatcher();
                Assert.True(branch.IsDescendantActive);
                Assert.False(branch.IsExpanded);
                Assert.True(root.IsExpanded);
                AssertHeaderInViewport(branch, scroller);

                Assert.True(navigation.NavigateAndCommit(typeof(SecondPage)));
                Assert.True(navigation.NavigateAndCommit(typeof(FirstPage)));
                Assert.True(navigation.NavigateAndCommit(typeof(SecondPage)));
                DrainDispatcher();
                AssertHeaderInViewport(other, scroller);
                Assert.False(root.IsBranchActive);
                Assert.False(branch.IsBranchActive);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    private static void AssertHeaderInViewport(NavigationViewItem item, NativeScrollViewer scroller)
    {
        var header = Assert.IsAssignableFrom<ButtonBase>(item.Template.FindName("PART_HeaderButton", item));
        var viewport = FindDescendant<ScrollContentPresenter>(scroller)!;
        Rect bounds = header.TransformToAncestor(viewport).TransformBounds(new Rect(header.RenderSize));
        Assert.True(
            bounds.Top >= -1 && bounds.Bottom <= viewport.ActualHeight + 1,
            $"Header {bounds} is outside viewport height {viewport.ActualHeight}.");
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            var match = child as T ?? FindDescendant<T>(child);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static Window CreateWindow(object content) => new()
    {
        Content = content,
        Width = 900,
        Height = 360,
        Left = -10_000,
        Top = -10_000,
        ShowActivated = false,
        ShowInTaskbar = false,
        WindowStyle = WindowStyle.None,
    };

    private static void DrainDispatcher() =>
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

    private sealed class FirstPage : Border { }

    private sealed class SecondPage : Border { }
}
