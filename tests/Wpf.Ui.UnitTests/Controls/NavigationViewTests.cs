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
            Assert.Equal(0, navigationView.PageTypeDictionaryCount);
            Assert.Equal(0, navigationView.PageKeyDictionaryCount);
            Assert.Equal(0, navigationView.NavigationStackCount);
            Assert.Null(navigationView.SelectedItem);
        });
    }

    [Fact]
    public void GoBack_NavigatesWithoutMenuItemMappings()
    {
        RunInSta(() =>
        {
            var navigationView = new TestNavigationView();

            Assert.True(navigationView.Navigate(typeof(FirstPage)));
            Assert.True(navigationView.Navigate(typeof(SecondPage)));
            Assert.True(navigationView.CanGoBack);

            Assert.True(navigationView.GoBack());
            Assert.IsType<FirstPage>(navigationView.LastNavigatedPage);
            Assert.False(navigationView.CanGoBack);
        });
    }

    [Fact]
    public void RemovedMenuItem_IsRemovedFromUiDictionaries_ButItsRouteRemainsUsable()
    {
        RunInSta(() =>
        {
            var navigationView = new TestNavigationView();
            var navigationItem = new NavigationViewItem("First", typeof(FirstPage))
            {
                TargetPageTag = "first",
            };

            navigationView.MenuItems.Add(navigationItem);

            Assert.Equal(1, navigationView.PageTypeDictionaryCount);
            Assert.Equal(2, navigationView.PageKeyDictionaryCount);

            navigationView.MenuItems.Remove(navigationItem);

            Assert.Equal(0, navigationView.PageTypeDictionaryCount);
            Assert.Equal(0, navigationView.PageKeyDictionaryCount);
            Assert.True(navigationView.Navigate("first"));
            Assert.IsType<FirstPage>(navigationView.LastNavigatedPage);
            Assert.Null(navigationView.SelectedItem);
        });
    }

    [Fact]
    public void NestedMenuChanges_RebuildUiDictionaries()
    {
        RunInSta(() =>
        {
            var navigationView = new TestNavigationView();
            var parentItem = new NavigationViewItem("Parent", typeof(FirstPage))
            {
                TargetPageTag = "parent",
            };
            var childItem = new NavigationViewItem("Child", typeof(SecondPage))
            {
                TargetPageTag = "child",
            };

            parentItem.MenuItems.Add(childItem);
            navigationView.MenuItems.Add(parentItem);

            Assert.Equal(2, navigationView.PageTypeDictionaryCount);
            Assert.Equal(4, navigationView.PageKeyDictionaryCount);

            parentItem.MenuItems.Remove(childItem);

            Assert.Equal(1, navigationView.PageTypeDictionaryCount);
            Assert.Equal(2, navigationView.PageKeyDictionaryCount);
            Assert.True(navigationView.Navigate("child"));
            Assert.IsType<SecondPage>(navigationView.LastNavigatedPage);
        });
    }

    [Fact]
    public void RemovingCurrentMenuItem_ClearsItsUiState()
    {
        RunInSta(() =>
        {
            var navigationView = new TestNavigationView();
            var navigationItem = new NavigationViewItem("First", typeof(FirstPage))
            {
                TargetPageTag = "first",
            };

            navigationView.MenuItems.Add(navigationItem);
            Assert.True(navigationView.Navigate("first"));
            Assert.Same(navigationItem, navigationView.SelectedItem);
            Assert.Equal(1, navigationView.NavigationStackCount);
            Assert.Equal(1, navigationView.PageToNavigationItemDictionaryCount);

            navigationView.MenuItems.Remove(navigationItem);

            Assert.Null(navigationView.SelectedItem);
            Assert.Equal(0, navigationView.NavigationStackCount);
            Assert.Equal(0, navigationView.PageToNavigationItemDictionaryCount);
            Assert.True(navigationView.Navigate(typeof(SecondPage)));
            Assert.True(navigationView.GoBack());
            Assert.IsType<FirstPage>(navigationView.LastNavigatedPage);
        });
    }

    [Fact]
    public void ChangingNavigationItemTarget_RebuildsUiDictionaries()
    {
        RunInSta(() =>
        {
            var navigationView = new TestNavigationView();
            var navigationItem = new NavigationViewItem("First", typeof(FirstPage))
            {
                TargetPageTag = "old-tag",
            };

            navigationView.MenuItems.Add(navigationItem);

            navigationItem.TargetPageTag = "new-tag";
            navigationItem.TargetPageType = typeof(SecondPage);

            Assert.False(navigationView.ContainsPageKey("old-tag"));
            Assert.True(navigationView.ContainsPageKey("new-tag"));
            Assert.False(navigationView.ContainsPageType(typeof(FirstPage)));
            Assert.True(navigationView.ContainsPageType(typeof(SecondPage)));
        });
    }

    [Fact]
    public void MenuItemsSourceReplaceAndReset_RebuildUiDictionaries()
    {
        RunInSta(() =>
        {
            var firstItem = new NavigationViewItem("First", typeof(FirstPage))
            {
                TargetPageTag = "first",
            };
            var secondItem = new NavigationViewItem("Second", typeof(SecondPage))
            {
                TargetPageTag = "second",
            };
            var source = new ObservableCollection<object> { firstItem };
            var navigationView = new TestNavigationView { MenuItemsSource = source };

            source[0] = secondItem;

            Assert.False(navigationView.ContainsPageKey("first"));
            Assert.True(navigationView.ContainsPageKey("second"));
            Assert.False(navigationView.ContainsPageType(typeof(FirstPage)));
            Assert.True(navigationView.ContainsPageType(typeof(SecondPage)));

            source.Clear();

            Assert.Equal(0, navigationView.PageKeyDictionaryCount);
            Assert.Equal(0, navigationView.PageTypeDictionaryCount);
        });
    }

    [Fact]
    public void FooterMenuItemsSourceChanges_RebuildUiDictionaries()
    {
        RunInSta(() =>
        {
            var source = new ObservableCollection<object>();
            var navigationView = new TestNavigationView { FooterMenuItemsSource = source };
            var navigationItem = new NavigationViewItem("First", typeof(FirstPage))
            {
                TargetPageTag = "footer",
            };

            source.Add(navigationItem);
            Assert.True(navigationView.ContainsPageKey("footer"));
            Assert.True(navigationView.ContainsPageType(typeof(FirstPage)));

            source.Remove(navigationItem);
            Assert.False(navigationView.ContainsPageKey("footer"));
            Assert.False(navigationView.ContainsPageType(typeof(FirstPage)));
        });
    }

    [Fact]
    public void NestedMenuItemsSourceChanges_RebuildUiDictionaries()
    {
        RunInSta(() =>
        {
            var childItems = new ObservableCollection<object>();
            var parentItem = new NavigationViewItem("Parent", typeof(FirstPage))
            {
                TargetPageTag = "parent",
                MenuItemsSource = childItems,
            };
            var childItem = new NavigationViewItem("Child", typeof(SecondPage))
            {
                TargetPageTag = "child",
            };
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(parentItem);

            childItems.Add(childItem);
            Assert.True(navigationView.ContainsPageKey("child"));
            Assert.True(navigationView.ContainsPageType(typeof(SecondPage)));

            childItems.Remove(childItem);
            Assert.False(navigationView.ContainsPageKey("child"));
            Assert.False(navigationView.ContainsPageType(typeof(SecondPage)));
        });
    }

    [Fact]
    public void MovingDuplicateTargets_UpdatesTheDictionaryToTheFirstMenuItem()
    {
        RunInSta(() =>
        {
            var firstItem = new NavigationViewItem("First", typeof(FirstPage))
            {
                TargetPageTag = "shared",
            };
            var secondItem = new NavigationViewItem("Second", typeof(FirstPage))
            {
                TargetPageTag = "shared",
            };
            var navigationView = new TestNavigationView();
            navigationView.MenuItems.Add(firstItem);
            navigationView.MenuItems.Add(secondItem);

            Assert.Same(firstItem, navigationView.GetPageItem("shared"));
            Assert.Same(firstItem, navigationView.GetPageItem(typeof(FirstPage)));

            ((ObservableCollection<object>)navigationView.MenuItems).Move(1, 0);

            Assert.Same(secondItem, navigationView.GetPageItem("shared"));
            Assert.Same(secondItem, navigationView.GetPageItem(typeof(FirstPage)));
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

        public int PageKeyDictionaryCount => PageIdOrTargetTagNavigationViewsDictionary.Count;

        public int PageTypeDictionaryCount => PageTypeNavigationViewsDictionary.Count;

        public int PageToNavigationItemDictionaryCount => PageToNavigationItemDictionary.Count;

        public int NavigationStackCount => NavigationStack.Count;

        public bool ContainsPageKey(string key) =>
            PageIdOrTargetTagNavigationViewsDictionary.ContainsKey(key);

        public bool ContainsPageType(Type pageType) =>
            PageTypeNavigationViewsDictionary.ContainsKey(pageType);

        public INavigationViewItem GetPageItem(string key) =>
            PageIdOrTargetTagNavigationViewsDictionary[key];

        public INavigationViewItem GetPageItem(Type pageType) =>
            PageTypeNavigationViewsDictionary[pageType];

        protected override void OnNavigated(object page)
        {
            LastNavigatedPage = page;
            base.OnNavigated(page);
        }
    }

    public sealed class FirstPage : FrameworkElement { }

    public sealed class SecondPage : FrameworkElement { }
}
