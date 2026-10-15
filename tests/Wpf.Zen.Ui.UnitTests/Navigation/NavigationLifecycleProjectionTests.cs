// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Scope = Wpf.Zen.Ui.NavigationScope;

namespace Wpf.Zen.Ui.UnitTests.Navigation;

public sealed partial class NavigationLifecycleContractTests
{
    /// <summary>
    /// After header DataContext bridging reenters stop, old publication must not restore content or
    /// toolbar items.
    /// </summary>
    [Fact]
    public void HeaderDataContextBridge_StopCannotRestoreOldContribution()
    {
        WpfTestThread.Run(async () =>
        {
            var model = new object();
            var page = new ProbePage { DataContext = model };
            var header = new Border();
            NavigationView.SetPageHeaderContent(page, header);
            NavigationView.GetPageHeaderItems(page).Add(new Border());
            var host = new NavigationView();
            var scope = Scope.Attach(host, new SinglePageProvider(page));
            header.DataContextChanged += (_, args) =>
            {
                if (ReferenceEquals(args.NewValue, model))
                {
                    scope.RequestStop();
                }
            };
            var result = await scope.Navigation.NavigateAsync(NavigationRequest.To<ProbePage>());
            await scope.Completion;
            Assert.True(result.WasCommitted);
            Assert.Null(host.CurrentPageHeaderContent);
            Assert.Null(host.CurrentPageHeaderItems);
            Assert.Equal(0, page.Entries);
        });
    }

    /// <summary>
    /// If menu activation notification stops the session, invalidated activation extension hooks
    /// are not invoked.
    /// </summary>
    [Fact]
    public void ItemActiveProperty_StopSuppressesStaleActivationHook()
    {
        WpfTestThread.Run(async () =>
        {
            var item = new ActivationHookItem { TargetPageType = typeof(ProbePage) };
            var host = new NavigationView();
            host.MenuItems.Add(item);
            var scope = Scope.Attach(host);
            item.Stop = scope.RequestStop;
            var result = await scope.Navigation.NavigateAsync(NavigationRequest.To<ProbePage>());
            await scope.Completion;
            Assert.True(result.WasCommitted);
            Assert.Equal(0, item.Activated);
            Assert.False(item.IsActive);
            Assert.Null(host.SelectedItem);
            Assert.Null(host.CurrentLocation);
        });
    }

    /// <summary>
    /// Brings Mining's three cache modes and commit boundaries into library tests, verifying full
    /// projections and disposal ownership.
    /// </summary>
    [Theory]
    [MemberData(nameof(CommitBoundaries))]
    public void CommitMatrix_PreservesProjectionAndOwnership(NavigationCacheMode mode, int boundary)
    {
        WpfTestThread.Run(async () =>
        {
            var created = new List<MatrixPage>();
            var provider = new DelegateProvider(type =>
            {
                MatrixPage page = type == typeof(MatrixFirstPage)
                    ? new MatrixFirstPage()
                    : type == typeof(MatrixSecondPage) ? new MatrixSecondPage() : new MatrixThirdPage();
                created.Add(page);
                return NavigationPageLease.Owned(page);
            });
            var firstItem = new NavigationItem("First", typeof(MatrixFirstPage)) { NavigationCacheMode = mode };
            var secondItem = new NavigationItem("Second", typeof(MatrixSecondPage)) { NavigationCacheMode = mode };
            var thirdItem = new NavigationItem("Third", typeof(MatrixThirdPage)) { NavigationCacheMode = mode };
            var host = new NavigationView { MenuItemsSource = new[] { firstItem, secondItem, thirdItem } };
            var window = new Window { Content = host, Width = 350, Height = 250, ShowInTaskbar = false };
            window.Show();
            var scope = Scope.Attach(host, provider);
            try
            {
                Assert.True((await scope.Navigation.NavigateAsync(NavigationRequest.To<MatrixFirstPage>())).Succeeded);
                await PumpAsync();
                var frame = GetFrame(host);
                var first = Assert.IsType<MatrixFirstPage>(frame.Content);
                var location = host.CurrentLocation;
                NavigationResult? redirected = null;
                host.Navigating += (_, args) =>
                {
                    if (args.Page is not MatrixSecondPage)
                    {
                        return;
                    }

                    if (boundary == 0)
                    {
                        args.Cancel = true;
                    }
                    else if (boundary == 2)
                    {
                        scope.RequestStop();
                    }
                    else if (boundary == 6)
                    {
                        host.Dispatcher.InvokeAsync(frame.StopLoading, DispatcherPriority.Normal);
                    }
                };
                frame.Navigating += (_, args) =>
                {
                    if (boundary == 1 && args.Content is MatrixSecondPage)
                    {
                        args.Cancel = true;
                    }
                };
                host.CurrentLocationChanged += (_, _) =>
                {
                    if (boundary == 3 && host.CurrentLocation?.Page is MatrixSecondPage)
                    {
                        scope.RequestStop();
                    }
                };
                host.Navigated += (_, args) =>
                {
                    if (args.Page is MatrixSecondPage)
                    {
                        if (boundary == 4)
                        {
                            scope.RequestStop();
                        }
                        else if (boundary == 5)
                        {
                            redirected = scope.Navigation.Navigate(typeof(MatrixThirdPage));
                        }
                    }
                };

                var result = await scope.Navigation.NavigateAsync(NavigationRequest.To<MatrixSecondPage>("second"));
                if (redirected is { } queued)
                {
                    Assert.True((await queued.Completion).Succeeded);
                }

                await PumpAsync();
                if (boundary is 0 or 1 or 6)
                {
                    Assert.Equal(NavigationResultStatus.Cancelled, result.Status);
                    Assert.Same(first, frame.Content);
                    Assert.Same(location, host.CurrentLocation);
                    Assert.Same(firstItem, host.SelectedItem);
                    Assert.False(host.CanGoBack);
                    Assert.False(host.CanGoForward);
                    Assert.Same(scope.Navigation, Wpf.Zen.Ui.Navigation.GetService(first));
                    Assert.Same(host, first.GetValue(NavigationView.NavigationParentProperty));
                    Assert.Equal(0, first.Exits);
                    var abandoned = Assert.Single(created.OfType<MatrixSecondPage>());
                    Assert.Equal(0, abandoned.Entries);
                    Assert.Equal(1, abandoned.Disposals);
                }
                else if (boundary is 2 or 3 or 4)
                {
                    await scope.Completion;
                    Assert.Equal(boundary == 2 ? NavigationResultStatus.Cancelled : NavigationResultStatus.Succeeded,
                        result.Status);
                    Assert.Null(frame.Content);
                    Assert.Null(host.CurrentLocation);
                    Assert.Null(host.SelectedItem);
                    Assert.Empty(host.Breadcrumbs);
                    Assert.False(host.CanGoBack);
                    Assert.False(host.CanGoForward);
                }
                else
                {
                    Assert.True(result.Succeeded);
                    Assert.IsType<MatrixThirdPage>(frame.Content);
                    Assert.Same(frame.Content, host.CurrentLocation!.Page);
                    Assert.Same(thirdItem, host.SelectedItem);
                    Assert.True(host.CanGoBack);
                    Assert.False(host.CanGoForward);
                    Assert.Equal(1, first.Exits);
                }

                await scope.DisposeAsync();
                Assert.All(created, page =>
                {
                    Assert.Equal(page.Entries, page.Exits);
                    Assert.Equal(1, page.Disposals);
                    Assert.Null(Wpf.Zen.Ui.Navigation.GetContext(page));
                    Assert.Null(page.GetValue(NavigationView.NavigationParentProperty));
                });
            }
            finally
            {
                await scope.DisposeAsync();
                window.Close();
            }
        });
    }

    /// <summary>
    /// Provides three cache modes and seven commit boundaries, with full projection assertions for
    /// every combination.
    /// </summary>
    public static IEnumerable<object[]> CommitBoundaries()
    {
        foreach (var mode in new[] { NavigationCacheMode.Disabled, NavigationCacheMode.Evictable,
                     NavigationCacheMode.Permanent })
        {
            for (int boundary = 0; boundary < 7; boundary++)
            {
                yield return new object[] { mode, boundary };
            }
        }
    }

    /// <summary>
    /// Stop inside a context property callback must not repopulate cleared page context.
    /// </summary>
    [Fact]
    public void ContextNotification_StopLeavesNoPartialContext()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new ContextNotificationPage();
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Borrowed(page)));
            page.Changed = context =>
            {
                if (context is not null)
                {
                    Assert.Same(scope.Navigation, context.Service);
                    Assert.Same(host, context.Host);
                    Assert.Equal("parameter", context.Parameter);
                    scope.RequestStop();
                }
            };
            var result = await scope.Navigation.NavigateAsync(NavigationRequest.To<ContextNotificationPage>("parameter"));
            await scope.Completion;
            await PumpAsync();
            Assert.True(result.WasCommitted);
            Assert.Null(Wpf.Zen.Ui.Navigation.GetContext(page));
            Assert.Null(host.CurrentLocation);
            Assert.Null(GetFrame(host).Content);
        });
    }

    /// <summary>
    /// History clearing during back preparation follows request order without invalidating the
    /// index being prepared.
    /// </summary>
    [Fact]
    public void ClearHistoryDuringBackRequest_IsSerializedAfterCommit()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var scope = Scope.Attach(host);
            await scope.Navigation.NavigateAsync(NavigationRequest.To<ProbePage>());
            await scope.Navigation.NavigateAsync(NavigationRequest.To<OtherPage>());
            host.BackRequested += (_, _) => host.ClearJournal();
            var result = await scope.Navigation.GoBackAsync();
            await PumpAsync();
            Assert.True(result.Succeeded);
            Assert.IsType<ProbePage>(GetFrame(host).Content);
            Assert.False(host.CanGoBack);
            Assert.False(host.CanGoForward);
            await scope.DisposeAsync();
        });
    }

    /// <summary>
    /// Records ownership and activation pairing; caching affects instance lifetime rather than exit
    /// obligations.
    /// </summary>
    private abstract class MatrixPage : Border, INavigationAware, IDisposable
    {
        internal int Entries { get; private set; }
        internal int Exits { get; private set; }
        internal int Disposals { get; private set; }

        /// <inheritdoc />
        public void OnNavigationEntered()
        {
            Entries++;
        }

        /// <inheritdoc />
        public void OnNavigationExited()
        {
            Exits++;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Disposals++;
        }
    }

    private sealed class MatrixFirstPage : MatrixPage { }
    private sealed class MatrixSecondPage : MatrixPage { }
    private sealed class MatrixThirdPage : MatrixPage { }

    private sealed class ContextNotificationPage : Border
    {
        internal Action<NavigationContext?>? Changed { get; set; }

        /// <inheritdoc />
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == Wpf.Zen.Ui.Navigation.ContextProperty)
            {
                Changed?.Invoke((NavigationContext?)e.NewValue);
            }
        }
    }

    /// <summary>
    /// Injects session stop between active property notification and subsequent extension hooks.
    /// </summary>
    private sealed class ActivationHookItem : NavigationViewItem
    {
        internal Action? Stop { get; set; }
        internal int Activated { get; private set; }

        /// <inheritdoc />
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == IsActiveProperty && e.NewValue is true)
            {
                Stop?.Invoke();
            }
        }

        /// <inheritdoc />
        protected override void OnActivated(NavigationView navigationView)
        {
            Activated++;
            base.OnActivated(navigationView);
        }
    }
}
