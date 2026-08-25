// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.DependencyInjection;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

public class NavigationViewAuditTests
{
    [Fact]
    public void ReplacingAndReloadingAutoSuggestBoxMaintainsOnlyTheCurrentSubscription()
    {
        WpfTestThread.Run(() =>
        {
            var firstBox = new AutoSuggestBox();
            var secondBox = new AutoSuggestBox();
            var firstItem = new NavigationItem("First", typeof(FirstPage));
            var secondItem = new NavigationItem("Second", typeof(SecondPage));
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                AutoSuggestBox = firstBox,
                MenuItemsSource = new[] { firstItem, secondItem },
            };
            var window = CreateWindow(navigationView);

            try
            {
                window.Show();
                DrainDispatcher();

                navigationView.AutoSuggestBox = secondBox;
                Assert.Empty(firstBox.OriginalItemsSource.Cast<object>());

                RaiseQuery(firstBox, "First");
                Assert.Null(navigationView.CurrentLocation);

                RaiseQuery(secondBox, "First");
                Assert.IsType<FirstPage>(navigationView.CurrentLocation?.Page);

                Assert.True(navigationView.Navigate(typeof(SecondPage)));
                Assert.True(navigationView.CanGoBack);
                window.Content = null;
                DrainDispatcher();
                window.Content = navigationView;
                DrainDispatcher();
                Assert.True(navigationView.CanGoBack);

                RaiseQuery(secondBox, "First");
                Assert.IsType<FirstPage>(navigationView.CurrentLocation?.Page);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void StructuralRouteReturnsFalseInsteadOfActivatingANullPageType()
    {
        WpfTestThread.Run(() =>
        {
            var header = new NavigationViewItem
            {
                Header = "Settings",
                TargetPageTag = "settings",
            };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                MenuItemsSource = new[] { header },
            };

            Assert.False(navigationView.Navigate("settings"));
            Assert.Null(navigationView.CurrentLocation);
        });
    }

    [Fact]
    public void NestedNavigationFromNavigatingIsRejectedWithoutCreatingGhostHistory()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                MenuItemsSource = new object[]
                {
                    new NavigationItem("First", typeof(FirstPage)),
                    new NavigationItem("Second", typeof(SecondPage)),
                    new NavigationItem("Redirect", typeof(RedirectPage)),
                },
            };
            Assert.True(navigationView.Navigate(typeof(FirstPage)));

            bool nestedResult = true;
            navigationView.Navigating += (_, args) =>
            {
                if (args.Page is SecondPage)
                {
                    nestedResult = navigationView.Navigate(typeof(RedirectPage));
                }
            };

            Assert.True(navigationView.Navigate(typeof(SecondPage)));
            Assert.False(nestedResult);
            Assert.True(navigationView.GoBack());
            Assert.IsType<FirstPage>(navigationView.CurrentLocation?.Page);
            Assert.False(navigationView.CanGoBack);
        });
    }

    [Fact]
    public void RoutedEventsPreserveSourceAndOriginalHandlerExceptions()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                MenuItemsSource = new object[]
                {
                    new NavigationItem("First", typeof(FirstPage)),
                    new NavigationItem("Second", typeof(SecondPage)),
                },
            };
            Assert.True(navigationView.Navigate(typeof(FirstPage)));

            navigationView.BackRequested += (_, args) =>
            {
                Assert.Same(navigationView, args.Source);
                Assert.Same(navigationView, args.OriginalSource);
            };
            Assert.True(navigationView.Navigate(typeof(SecondPage)));
            Assert.True(navigationView.GoBack());

            navigationView.Navigated += (_, _) => throw new AuditException();
            Assert.Throws<AuditException>(() => navigationView.Navigate(typeof(SecondPage)));
        });
    }

    [Fact]
    public void ApplicationReplacementOfTitleBarContentIsNotOverwritten()
    {
        WpfTestThread.Run(() =>
        {
            var titleBar = new Wpf.Zen.Ui.Controls.TitleBar();
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView { TitleBar = titleBar };
            var applicationContent = new Border();

            titleBar.LeftContent = applicationContent;
            navigationView.IsPaneOpen = !navigationView.IsPaneOpen;

            Assert.Same(applicationContent, titleBar.LeftContent);
        });
    }

    [Fact]
    public void NavigationAutomationExposesNamesAndMatchingSelectionContainer()
    {
        WpfTestThread.Run(() =>
        {
            var item = new NavigationViewItem(typeof(FirstPage)) { Header = "First" };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                MenuItemsSource = new[] { item },
            };
            var window = CreateWindow(navigationView);

            try
            {
                window.Show();
                DrainDispatcher();
                Assert.True(navigationView.Navigate(typeof(FirstPage)));

                _ = CreatePeer(window);
                var navigationPeer = CreatePeer(navigationView);
                var selectionProvider = Assert.IsAssignableFrom<ISelectionProvider>(
                    navigationPeer.GetPattern(PatternInterface.Selection)
                );
                Assert.Single(selectionProvider.GetSelection());

                var itemProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(
                    CreatePeer(item).GetPattern(PatternInterface.SelectionItem)
                );
                Assert.True(itemProvider.IsSelected);

                var backButton = GetPrivateButton(navigationView, "_paneBackButton");
                var toggleButton = GetPrivateButton(navigationView, "_paneToggleButton");
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(backButton)));
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(toggleButton)));
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void RemovedDirectContainerReleasesItsNavigationHost()
    {
        WpfTestThread.Run(() =>
        {
            var item = new NavigationViewItem(typeof(FirstPage)) { Header = "First" };
            var items = new ObservableCollection<object> { item };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView { MenuItemsSource = items };
            var window = CreateWindow(navigationView);

            try
            {
                window.Show();
                DrainDispatcher();
                Assert.Same(navigationView, Wpf.Zen.Ui.Controls.NavigationView.GetNavigationParent(item));

                items.Remove(item);
                DrainDispatcher();

                Assert.Null(Wpf.Zen.Ui.Controls.NavigationView.GetNavigationParent(item));
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void ScopeWaitsForOutstandingEntryBeforeDisposingServices()
    {
        WpfTestThread.Run(() =>
        {
            var services = new ServiceCollection();
            _ = services.AddNavigation();
            _ = services.AddScoped<AuditDisposable>();
            _ = services.AddTransient<DeferredEntryPage>();
            using ServiceProvider provider = services.BuildServiceProvider();
            INavigationScopeFactory factory = provider.GetRequiredService<INavigationScopeFactory>();
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            DeferredEntryPage? page = null;
            navigationView.Navigated += (_, args) => page = Assert.IsType<DeferredEntryPage>(args.Page);
            INavigationScope scope = factory.Attach(navigationView);

            Assert.True(scope.Navigation.Navigate(typeof(DeferredEntryPage)).Succeeded);
            DrainDispatcher();
            Assert.NotNull(page);

            scope.Dispose();
            Assert.True(page.EntryToken.IsCancellationRequested);
            Assert.False(page.Dependency.IsDisposed);

            page.CompleteEntry();
            scope.DisposeAsync().GetAwaiter().GetResult();
            Assert.True(page.Dependency.IsDisposed);
        });
    }

    [Fact]
    public void PendingLifecycleDeliveryDoesNotRunAfterScopeDisposal()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            var page = new CountingPage();
            using INavigationScope scope = NavigationScope.Attach(
                navigationView,
                new SinglePageProvider(page)
            );

            Assert.True(scope.Navigation.Navigate(typeof(CountingPage)).Succeeded);
            scope.Dispose();
            int enteredAtDispose = page.EnteredCount;
            int exitedAtDispose = page.ExitedCount;

            DrainDispatcher();

            Assert.Equal(enteredAtDispose, page.EnteredCount);
            Assert.Equal(exitedAtDispose, page.ExitedCount);
        });
    }

    private static Window CreateWindow(object content) => new()
    {
        Content = content,
        Width = 100,
        Height = 100,
        Left = -10_000,
        Top = -10_000,
        ShowActivated = false,
        ShowInTaskbar = false,
        WindowStyle = WindowStyle.None,
    };

    private static void RaiseQuery(AutoSuggestBox box, string text)
    {
        box.RaiseEvent(
            new AutoSuggestBoxQuerySubmittedEventArgs(AutoSuggestBox.QuerySubmittedEvent, box)
            {
                QueryText = text,
            }
        );
    }

    private static AutomationPeer CreatePeer(UIElement element) =>
        UIElementAutomationPeer.CreatePeerForElement(element)
        ?? (AutomationPeer)element
            .GetType()
            .GetMethod("OnCreateAutomationPeer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(element, null)!;

    private static NavigationViewButton GetPrivateButton(
        Wpf.Zen.Ui.Controls.NavigationView navigationView,
        string fieldName) =>
        (NavigationViewButton)typeof(Wpf.Zen.Ui.Controls.NavigationView)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(navigationView)!;

    private static void DrainDispatcher() =>
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);

    private sealed class FirstPage : Border { }

    private sealed class SecondPage : Border { }

    private sealed class RedirectPage : Border { }

    private sealed class AuditException : Exception { }

    private sealed class AuditDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class DeferredEntryPage(AuditDisposable dependency)
        : Border, ICancellableAsyncNavigationAware
    {
        private readonly TaskCompletionSource<bool> _entryCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public AuditDisposable Dependency { get; } = dependency;

        public CancellationToken EntryToken { get; private set; }

        public Task OnNavigationEnteredAsync(CancellationToken cancellationToken)
        {
            EntryToken = cancellationToken;
            return _entryCompletion.Task;
        }

        public void OnNavigationExited() { }

        public void CompleteEntry() => _entryCompletion.TrySetResult(true);
    }

    private sealed class CountingPage : Border, INavigationAware
    {
        public int EnteredCount { get; private set; }

        public int ExitedCount { get; private set; }

        public void OnNavigationEntered()
        {
            EnteredCount++;
        }

        public void OnNavigationExited() => ExitedCount++;
    }

    private sealed class SinglePageProvider(CountingPage page) : INavigationViewPageProvider
    {
        public object GetPage(Type pageType) => page;
    }
}
