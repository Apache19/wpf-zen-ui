// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Threading;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls;

public class NavigationViewTests
{
    [Fact]
    public void NavigateByType_DoesNotCreateMenuItems_WhenPageIsNotInMenu()
    {
        RunInSta(() =>
        {
            var navigationView = new TestNavigationView();

            Assert.True(navigationView.Navigate(typeof(FirstPage)));
            Assert.IsType<FirstPage>(navigationView.LastNavigatedPage);
            Assert.Equal(0, navigationView.NavigationItemCount);
            Assert.Equal(0, navigationView.NavigationStackCount);
            Assert.Null(navigationView.SelectedItem);
        });
    }

    [Fact]
    public void NavigateByKey_SelectsTheMatchingVariant()
    {
        RunInSta(() =>
        {
            var firstItem = new NavigationViewItem("First", typeof(FirstPage)) { TargetPageVariant = "first" };
            var secondItem = new NavigationViewItem("Second", typeof(FirstPage)) { TargetPageVariant = "second" };
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(firstItem);
            navigationView.MenuItems.Add(secondItem);

            Assert.True(navigationView.Navigate(new NavigationKey(typeof(FirstPage), "second")));
            Assert.Same(secondItem, navigationView.SelectedItem);
            Assert.Same(secondItem, navigationView.GetNavigationItem(new NavigationKey(typeof(FirstPage), "second")));
        });
    }

    [Fact]
    public void CachedVariants_UseDifferentPageInstances()
    {
        RunInSta(() =>
        {
            var firstItem = new NavigationViewItem("First", typeof(FirstPage))
            {
                TargetPageVariant = "first",
                NavigationCacheMode = NavigationCacheMode.Enabled,
            };
            var secondItem = new NavigationViewItem("Second", typeof(FirstPage))
            {
                TargetPageVariant = "second",
                NavigationCacheMode = NavigationCacheMode.Enabled,
            };
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(firstItem);
            navigationView.MenuItems.Add(secondItem);

            Assert.True(navigationView.Navigate(new NavigationKey(typeof(FirstPage), "first")));
            var firstInstance = navigationView.LastNavigatedPage;
            Assert.True(navigationView.Navigate(new NavigationKey(typeof(FirstPage), "second")));
            var secondInstance = navigationView.LastNavigatedPage;

            Assert.NotSame(firstInstance, secondInstance);
        });
    }

    [Fact]
    public void GoBack_UsesTheFullNavigationKey()
    {
        RunInSta(() =>
        {
            var navigationView = new TestNavigationView();

            Assert.True(navigationView.Navigate(new NavigationKey(typeof(FirstPage), "first")));
            Assert.True(navigationView.Navigate(new NavigationKey(typeof(FirstPage), "second")));
            Assert.True(navigationView.CanGoBack);

            Assert.True(navigationView.GoBack());
            Assert.Equal(new NavigationKey(typeof(FirstPage), "first"), navigationView.CurrentJournalEntry);
            Assert.False(navigationView.CanGoBack);
        });
    }

    [Fact]
    public void NavigateToSameKey_WithDataContext_UpdatesTheCachedPage()
    {
        RunInSta(() =>
        {
            var key = new NavigationKey(typeof(FirstPage), "first");
            var navigationItem = new NavigationViewItem("First", typeof(FirstPage))
            {
                TargetPageVariant = "first",
                NavigationCacheMode = NavigationCacheMode.Enabled,
            };
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(navigationItem);
            var firstDataContext = new object();
            var secondDataContext = new object();

            Assert.True(navigationView.Navigate(key, firstDataContext));
            var page = Assert.IsType<FirstPage>(navigationView.LastNavigatedPage);
            Assert.Same(firstDataContext, page.DataContext);

            Assert.True(navigationView.Navigate(key, secondDataContext));
            Assert.Same(page, navigationView.LastNavigatedPage);
            Assert.Same(secondDataContext, page.DataContext);
        });
    }

    [Fact]
    public void GoForward_RestoresTheEntryRemovedByGoBack()
    {
        RunInSta(() =>
        {
            var firstKey = new NavigationKey(typeof(FirstPage), "first");
            var secondKey = new NavigationKey(typeof(FirstPage), "second");
            var navigationView = new TestNavigationView();

            Assert.True(navigationView.Navigate(firstKey));
            Assert.True(navigationView.Navigate(secondKey));
            Assert.True(navigationView.GoBack());

            Assert.True(navigationView.GoForward());
            Assert.Equal(secondKey, navigationView.CurrentJournalEntry);
            Assert.True(navigationView.CanGoBack);
            Assert.False(navigationView.GoForward());
        });
    }

    [Fact]
    public void NavigateAfterGoBack_ClearsForwardHistory()
    {
        RunInSta(() =>
        {
            var navigationView = new TestNavigationView();

            Assert.True(navigationView.Navigate(new NavigationKey(typeof(FirstPage), "first")));
            Assert.True(navigationView.Navigate(new NavigationKey(typeof(FirstPage), "second")));
            Assert.True(navigationView.GoBack());
            Assert.True(navigationView.Navigate(typeof(SecondPage)));

            Assert.False(navigationView.GoForward());
        });
    }

    [Fact]
    public void ClearCache_RemovesCachedPageInstances()
    {
        RunInSta(() =>
        {
            var firstKey = new NavigationKey(typeof(FirstPage), "first");
            var secondKey = new NavigationKey(typeof(SecondPage), "second");
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(
                new NavigationViewItem("First", typeof(FirstPage))
                {
                    TargetPageVariant = "first",
                    NavigationCacheMode = NavigationCacheMode.Enabled,
                }
            );
            navigationView.MenuItems.Add(
                new NavigationViewItem("Second", typeof(SecondPage))
                {
                    TargetPageVariant = "second",
                    NavigationCacheMode = NavigationCacheMode.Enabled,
                }
            );

            Assert.True(navigationView.Navigate(firstKey));
            var firstInstance = navigationView.LastNavigatedPage;
            Assert.True(navigationView.Navigate(secondKey));

            navigationView.ClearCache();

            Assert.True(navigationView.Navigate(firstKey));
            Assert.NotSame(firstInstance, navigationView.LastNavigatedPage);
        });
    }

    [Fact]
    public void DisablingCache_RemovesTheExistingCachedPageInstance()
    {
        RunInSta(() =>
        {
            var firstKey = new NavigationKey(typeof(FirstPage), "first");
            var firstItem = new NavigationViewItem("First", typeof(FirstPage))
            {
                TargetPageVariant = "first",
                NavigationCacheMode = NavigationCacheMode.Enabled,
            };
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(firstItem);

            Assert.True(navigationView.Navigate(firstKey));
            var cachedInstance = navigationView.LastNavigatedPage;

            firstItem.NavigationCacheMode = NavigationCacheMode.Disabled;

            Assert.True(navigationView.Navigate(firstKey));
            Assert.NotSame(cachedInstance, navigationView.LastNavigatedPage);
        });
    }

    [Fact]
    public void RemovedMenuItem_IsRemovedFromLookup_ButItsKeyRemainsNavigable()
    {
        RunInSta(() =>
        {
            var key = new NavigationKey(typeof(FirstPage), "first");
            var navigationItem = new NavigationViewItem("First", typeof(FirstPage)) { TargetPageVariant = "first" };
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(navigationItem);

            Assert.Equal(1, navigationView.NavigationItemCount);
            navigationView.MenuItems.Remove(navigationItem);

            Assert.Equal(0, navigationView.NavigationItemCount);
            Assert.True(navigationView.Navigate(key));
            Assert.IsType<FirstPage>(navigationView.LastNavigatedPage);
            Assert.Null(navigationView.SelectedItem);
        });
    }

    [Fact]
    public void NestedMenuChanges_RebuildNavigationItems()
    {
        RunInSta(() =>
        {
            var parentItem = new NavigationViewItem("Parent", typeof(FirstPage)) { TargetPageVariant = "parent" };
            var childItem = new NavigationViewItem("Child", typeof(SecondPage)) { TargetPageVariant = "child" };
            var navigationView = new TestNavigationView();

            parentItem.MenuItems.Add(childItem);
            navigationView.MenuItems.Add(parentItem);
            Assert.Equal(2, navigationView.NavigationItemCount);

            parentItem.MenuItems.Remove(childItem);
            Assert.Equal(1, navigationView.NavigationItemCount);
            Assert.False(navigationView.ContainsNavigationKey(new NavigationKey(typeof(SecondPage), "child")));
        });
    }

    [Fact]
    public void RemovingCurrentMenuItem_ClearsItsUiState()
    {
        RunInSta(() =>
        {
            var key = new NavigationKey(typeof(FirstPage), "first");
            var navigationItem = new NavigationViewItem("First", typeof(FirstPage)) { TargetPageVariant = "first" };
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(navigationItem);

            Assert.True(navigationView.Navigate(key));
            Assert.Same(navigationItem, navigationView.SelectedItem);
            Assert.Equal(1, navigationView.NavigationStackCount);
            Assert.Equal(1, navigationView.PageToNavigationItemDictionaryCount);

            navigationView.MenuItems.Remove(navigationItem);

            Assert.Null(navigationView.SelectedItem);
            Assert.Equal(0, navigationView.NavigationStackCount);
            Assert.Equal(0, navigationView.PageToNavigationItemDictionaryCount);
        });
    }

    [Fact]
    public void ChangingNavigationItemTarget_RebuildsNavigationItems()
    {
        RunInSta(() =>
        {
            var navigationItem = new NavigationViewItem("First", typeof(FirstPage)) { TargetPageVariant = "old" };
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(navigationItem);

            navigationItem.TargetPageVariant = "new";
            navigationItem.TargetPageType = typeof(SecondPage);

            Assert.False(navigationView.ContainsNavigationKey(new NavigationKey(typeof(FirstPage), "old")));
            Assert.True(navigationView.ContainsNavigationKey(new NavigationKey(typeof(SecondPage), "new")));
        });
    }

    [Fact]
    public void MenuItemsSourceReplaceAndReset_RebuildNavigationItems()
    {
        RunInSta(() =>
        {
            var firstItem = new NavigationViewItem("First", typeof(FirstPage)) { TargetPageVariant = "first" };
            var secondItem = new NavigationViewItem("Second", typeof(SecondPage)) { TargetPageVariant = "second" };
            var source = new ObservableCollection<object> { firstItem };
            var navigationView = new TestNavigationView { MenuItemsSource = source };

            source[0] = secondItem;

            Assert.False(navigationView.ContainsNavigationKey(new NavigationKey(typeof(FirstPage), "first")));
            Assert.True(navigationView.ContainsNavigationKey(new NavigationKey(typeof(SecondPage), "second")));

            source.Clear();
            Assert.Equal(0, navigationView.NavigationItemCount);
        });
    }

    [Fact]
    public void FooterMenuItemsSourceChanges_RebuildNavigationItems()
    {
        RunInSta(() =>
        {
            var source = new ObservableCollection<object>();
            var navigationView = new TestNavigationView { FooterMenuItemsSource = source };
            var key = new NavigationKey(typeof(FirstPage), "footer");
            var navigationItem = new NavigationViewItem("First", typeof(FirstPage)) { TargetPageVariant = "footer" };

            source.Add(navigationItem);
            Assert.True(navigationView.ContainsNavigationKey(key));

            source.Remove(navigationItem);
            Assert.False(navigationView.ContainsNavigationKey(key));
        });
    }

    [Fact]
    public void NestedMenuItemsSourceChanges_RebuildNavigationItems()
    {
        RunInSta(() =>
        {
            var childItems = new ObservableCollection<object>();
            var parentItem = new NavigationViewItem("Parent", typeof(FirstPage))
            {
                TargetPageVariant = "parent",
                MenuItemsSource = childItems,
            };
            var childItem = new NavigationViewItem("Child", typeof(SecondPage)) { TargetPageVariant = "child" };
            var childKey = new NavigationKey(typeof(SecondPage), "child");
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(parentItem);

            childItems.Add(childItem);
            Assert.True(navigationView.ContainsNavigationKey(childKey));

            childItems.Remove(childItem);
            Assert.False(navigationView.ContainsNavigationKey(childKey));
        });
    }

    [Fact]
    public void DuplicateNavigationKeys_UseTheFirstRegisteredItem()
    {
        RunInSta(() =>
        {
            var key = new NavigationKey(typeof(FirstPage), "shared");
            var firstItem = new NavigationViewItem("First", typeof(FirstPage)) { TargetPageVariant = "shared" };
            var secondItem = new NavigationViewItem("Second", typeof(FirstPage)) { TargetPageVariant = "shared" };
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(firstItem);
            navigationView.MenuItems.Add(secondItem);

            Assert.Equal(2, navigationView.MenuItems.Count);
            Assert.Same(firstItem, navigationView.GetNavigationItem(key));

            ((ObservableCollection<object>)navigationView.MenuItems).Move(1, 0);

            Assert.Same(secondItem, navigationView.GetNavigationItem(key));
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class TestNavigationView : NavigationView
    {
        public TestNavigationView()
        {
            NavigationViewContentPresenter = new NavigationViewContentPresenter();
        }

        public object? LastNavigatedPage { get; private set; }

        public int NavigationItemCount => NavigationItems.Count;

        public int PageToNavigationItemDictionaryCount => PageToNavigationItemDictionary.Count;

        public int NavigationStackCount => NavigationStack.Count;

        public NavigationKey CurrentJournalEntry => Journal[^1];

        public bool ContainsNavigationKey(NavigationKey key) => NavigationItems.ContainsKey(key);

        public INavigationViewItem GetNavigationItem(NavigationKey key) => NavigationItems[key];

        protected override void OnNavigated(object page)
        {
            LastNavigatedPage = page;
            base.OnNavigated(page);
        }
    }

    public sealed class FirstPage : FrameworkElement { }

    public sealed class SecondPage : FrameworkElement { }
}
