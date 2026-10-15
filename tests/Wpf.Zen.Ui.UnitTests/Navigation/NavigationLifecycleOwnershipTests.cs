// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Scope = Wpf.Zen.Ui.NavigationScope;

namespace Wpf.Zen.Ui.UnitTests.Navigation;

public sealed partial class NavigationLifecycleContractTests
{
    /// <summary>
    /// The direct control entry point returns the same stop task after detachment, exposing
    /// operations still draining.
    /// </summary>
    [Fact]
    public void ImplicitScope_RepeatedStopReturnsSameDrainTask()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var page = new ContextPage();
            var release = NewCompletion();
            Assert.True(host.ReplaceContent(page));
            await PumpAsync();
            Task operation = page.Context!.RunAsync(_ => release.Task);
            Task first = host.StopNavigationAsync();
            Task second = host.StopNavigationAsync();
            Assert.Same(first, second);
            Assert.False(first.IsCompleted);
            release.SetResult(true);
            await operation;
            await first;
            Assert.Same(first, host.StopNavigationAsync());
        });
    }

    /// <summary>
    /// Disposal during LRU eviction may stop the host; entries are neither released twice nor
    /// restored after clearing.
    /// </summary>
    [Fact]
    public void CacheEvictionDisposer_CanStopScopeWithoutResurrectingCache()
    {
        WpfTestThread.Run(async () =>
        {
            var first = new MatrixFirstPage();
            var second = new MatrixSecondPage();
            INavigationScope? scope = null;
            var host = new NavigationView
            {
                NavigationCacheSize = 1,
                MenuItemsSource = new[]
                {
                    new NavigationItem("First", typeof(MatrixFirstPage)),
                    new NavigationItem("Second", typeof(MatrixSecondPage))
                }
            };
            scope = Scope.Attach(host, new DelegateProvider(type => type == typeof(MatrixFirstPage)
                ? NavigationPageLease.Owned(first, () =>
                {
                    first.Dispose();
                    scope!.RequestStop();
                    return Task.CompletedTask;
                })
                : NavigationPageLease.Owned(second)));
            await scope.Navigation.NavigateAsync(NavigationRequest.To<MatrixFirstPage>());
            await PumpAsync();
            var result = await scope.Navigation.NavigateAsync(NavigationRequest.To<MatrixSecondPage>());
            await scope.Completion;
            await PumpAsync();
            Assert.True(result.WasCommitted);
            Assert.Equal(1, first.Disposals);
            Assert.Equal(1, second.Disposals);
            Assert.Null(host.CurrentLocation);
            Assert.Null(GetFrame(host).Content);
        });
    }

    /// <summary>
    /// Cleanup transfers a complete cache snapshot, attempts all releases, and preserves reentrant
    /// additions.
    /// </summary>
    [Fact]
    public void CacheClear_ReentrancyAndFailuresDoNotCorruptNewEntries()
    {
        NavigationCache? cache = null;
        var released = new List<object?>();
        var first = new object();
        var second = new object();
        var replacement = new object();
        cache = new NavigationCache(value =>
        {
            released.Add(value);
            if (ReferenceEquals(value, first))
            {
                cache!.Remember(typeof(string), NavigationCacheMode.Permanent, 2, replacement);
                throw new InvalidOperationException("first release");
            }

            throw new ArgumentException("second release");
        });
        cache.Remember(typeof(int), NavigationCacheMode.Permanent, 2, first);
        cache.Remember(typeof(bool), NavigationCacheMode.Permanent, 2, second);
        var failure = Assert.Throws<AggregateException>(cache.Clear);
        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.Equal(new[] { first, second }, released);
        Assert.True(cache.TryGetValue(typeof(string), NavigationCacheMode.Permanent, out var retained));
        Assert.Same(replacement, retained);
    }

    /// <summary>
    /// A borrowed page cannot belong to two sessions concurrently; it may be borrowed again after
    /// the old activation drains.
    /// </summary>
    [Fact]
    public void BorrowedInstance_CannotJoinAnotherSessionBeforePreviousWorkDrains()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new ProbePage { Pending = NewCompletion() };
            var provider = new SinglePageProvider(page);
            var host = new NavigationView();
            var previous = Scope.Attach(host, provider);
            await previous.Navigation.NavigateAsync(NavigationRequest.To<ProbePage>());
            await PumpAsync();
            Task stopped = previous.DisposeAsync();
            var current = Scope.Attach(host, provider);
            var rejected = await current.Navigation.NavigateAsync(NavigationRequest.To<ProbePage>());
            Assert.Equal(NavigationResultStatus.Failed, rejected.Status);
            Assert.IsType<InvalidOperationException>(rejected.Failure);
            Assert.Equal(1, page.Entries);
            page.Pending.SetResult(true);
            await stopped;
            var accepted = await current.Navigation.NavigateAsync(NavigationRequest.To<ProbePage>());
            Assert.True(accepted.Succeeded);
            await PumpAsync();
            Assert.Equal(2, page.Entries);
            await current.DisposeAsync();
        });
    }

    /// <summary>
    /// Returning a finally disposed owned page fails explicitly, preventing repeated disposal or
    /// activation.
    /// </summary>
    [Fact]
    public void DisposedOwnedInstance_CannotBeResurrectedByProvider()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new OwnedPage();
            var host = new NavigationView();
            var provider = new DelegateProvider(_ => NavigationPageLease.Owned(page));
            var previous = Scope.Attach(host, provider);
            await previous.Navigation.NavigateAsync(NavigationRequest.To<OwnedPage>());
            await previous.DisposeAsync();
            var current = Scope.Attach(host, provider);
            var rejected = await current.Navigation.NavigateAsync(NavigationRequest.To<OwnedPage>());
            Assert.Equal(NavigationResultStatus.Failed, rejected.Status);
            Assert.IsType<ObjectDisposedException>(rejected.Failure);
            Assert.Equal(1, page.DisposeCount);
            await current.DisposeAsync();
        });
    }

    /// <summary>
    /// Consecutive pre-commit requests commit in queue order without nesting a later request inside
    /// earlier projection callbacks.
    /// </summary>
    [Fact]
    public void RequestsBeforeFirstCommit_AreCommittedInOrder()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var scope = Scope.Attach(host);
            var order = new List<Type>();
            host.Navigated += (_, args) => order.Add(args.Page.GetType());
            var first = scope.Navigation.Navigate(typeof(ProbePage));
            var second = scope.Navigation.Navigate(typeof(OtherPage));
            Assert.Equal(NavigationResultStatus.Accepted, first.Status);
            Assert.Equal(NavigationResultStatus.Accepted, second.Status);
            Assert.True((await first.Completion).Succeeded);
            Assert.True((await second.Completion).Succeeded);
            Assert.Equal(new[] { typeof(ProbePage), typeof(OtherPage) }, order);
            Assert.True(host.CanGoBack);
            await scope.DisposeAsync();
        });
    }

    /// <summary>
    /// Injects failure before Frame commit dispatch to verify adapter results, candidate release,
    /// and preserved projections.
    /// </summary>
    [Fact]
    public void FrameFailureBeforeCommit_IsObservedAndReclaimsCandidate()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var candidate = new OwnedPage();
            var scope = Scope.Attach(host, new DelegateProvider(type => type == typeof(OwnedPage)
                ? NavigationPageLease.Owned(candidate) : NavigationPageLease.Owned(new OtherPage())));
            await scope.Navigation.NavigateAsync(NavigationRequest.To<OtherPage>());
            var previous = host.CurrentLocation;
            var frame = GetFrame(host);
            var expected = new InvalidOperationException("injected Frame failure");
            frame.Navigating += (_, args) =>
            {
                if (args.Content is OwnedPage)
                {
                    var transaction = args.ExtraData;
                    frame.Dispatcher.InvokeAsync(() =>
                    {
                        // WPF failure event constructors are internal; inject transport failure
                        // using the signature in official source.
                        var constructors = typeof(NavigationFailedEventArgs).GetConstructors(
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        var constructor = Assert.Single(constructors);
                        var arguments = new object?[] { null, transaction, frame, null, null, expected };
                        var failure = (NavigationFailedEventArgs)constructor.Invoke(arguments);
                        var handler = typeof(NavigationView).GetMethod("OnNavigationFrameFailed",
                            BindingFlags.NonPublic | BindingFlags.Instance)!;
                        handler.Invoke(host, new object[] { frame, failure });
                        Assert.True(failure.Handled);
                        frame.StopLoading();
                    }, DispatcherPriority.Send);
                }
            };
            var result = await scope.Navigation.NavigateAsync(NavigationRequest.To<OwnedPage>());
            await PumpAsync();
            Assert.Equal(NavigationResultStatus.Failed, result.Status);
            Assert.False(result.WasCommitted);
            Assert.Same(expected, result.Failure);
            Assert.Same(previous, host.CurrentLocation);
            Assert.Same(previous!.Page, frame.Content);
            Assert.Equal(1, candidate.DisposeCount);
            Assert.False(host.CanGoBack);
            await scope.DisposeAsync();
        });
    }

    /// <summary>
    /// The same exception from nested registered tasks is notified once, including when
    /// initialization observes a command failure.
    /// </summary>
    [Fact]
    public void NestedRegisteredOperation_ReportsOriginalFailureOnce()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new ContextPage();
            var expected = new InvalidOperationException("nested operation");
            page.Enter = () => page.Context!.RunAsync(_ => Task.FromException(expected));
            var host = new NavigationView();
            var failures = new List<NavigationCallbackException>();
            host.NavigationCallbackFailed += (_, args) =>
            {
                failures.Add(args.Exception);
                args.Handled = true;
            };
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Borrowed(page)));
            await scope.Navigation.NavigateAsync(NavigationRequest.To<ContextPage>());
            await PumpAsync();
            await scope.DisposeAsync();
            var failure = Assert.Single(failures);
            Assert.Equal(NavigationCallbackStage.Operation, failure.Stage);
            Assert.Same(expected, failure.InnerException);
        });
    }

    /// <summary>
    /// Dispatcher shutdown during draining ends with restricted cleanup failure, without hanging or
    /// prematurely disposing dependencies.
    /// </summary>
    [Fact]
    public async Task DispatcherShutdownDuringDrain_ReportsFailureAndRetainsInUseDependency()
    {
        INavigationScope? scope = null;
        var release = NewCompletion();
        int disposals = 0;
        WpfTestThread.Run(async () =>
        {
            var page = new ProbePage { Pending = release };
            var host = new NavigationView();
            scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Owned(page, () =>
            {
                disposals++;
                return Task.CompletedTask;
            })));
            await scope.Navigation.NavigateAsync(NavigationRequest.To<ProbePage>());
            await PumpAsync();
            scope.RequestStop();
            Assert.False(scope.Completion.IsCompleted);
            // WpfTestThread shuts down the real STA Dispatcher after the action returns, simulating
            // bypass of normal shutdown.
        });
        var failure = await Record.ExceptionAsync(async () =>
            await scope!.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.NotNull(failure);
        Assert.Contains("Dispatcher", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, disposals);
        release.TrySetResult(true);
    }

    /// <summary>
    /// Cancelled queued requests and requests awaiting Frame commit must neither publish
    /// projections nor leak candidates.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationBeforeCommit_ReclaimsAcceptedRequest(bool queued)
    {
        WpfTestThread.Run(async () =>
        {
            using var cancellation = new CancellationTokenSource();
            var pages = new List<OwnedPage>();
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(type =>
            {
                if (type == typeof(OtherPage))
                {
                    return NavigationPageLease.Owned(new OtherPage());
                }

                var page = new OwnedPage();
                pages.Add(page);
                return NavigationPageLease.Owned(page);
            }));
            Task<NavigationResult>? pending = null;
            if (queued)
            {
                host.Navigating += (_, _) =>
                {
                    pending = scope.Navigation.NavigateAsync(NavigationRequest.To<OwnedPage>(), cancellation.Token);
                    cancellation.Cancel();
                };
                Assert.True((await scope.Navigation.NavigateAsync(NavigationRequest.To<OtherPage>())).Succeeded);
            }
            else
            {
                pending = scope.Navigation.NavigateAsync(NavigationRequest.To<OwnedPage>(), cancellation.Token);
                cancellation.Cancel();
            }

            var result = await pending!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(NavigationResultStatus.Cancelled, result.Status);
            await PumpAsync();
            Assert.Equal(queued ? typeof(OtherPage) : null, GetFrame(host).Content?.GetType());
            Assert.Equal(queued ? typeof(OtherPage) : null, host.CurrentLocation?.Target.PageType);
            Assert.False(host.CanGoBack);
            Assert.False(host.CanGoForward);
            Assert.All(pages, page => Assert.Equal(1, page.DisposeCount));
            await scope.DisposeAsync();
        });
    }

    /// <summary>
    /// Borrowed instances still exit, while final disposal remains the caller's responsibility.
    /// </summary>
    [Fact]
    public void BorrowedPage_IsNeverDisposedByNavigation()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new MatrixFirstPage();
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Borrowed(page)));
            await scope.Navigation.NavigateAsync(NavigationRequest.To<MatrixFirstPage>());
            await PumpAsync();
            await scope.DisposeAsync();
            Assert.Equal(1, page.Exits);
            Assert.Equal(0, page.Disposals);
            page.Dispose();
            Assert.Equal(1, page.Disposals);
        });
    }

    /// <summary>
    /// When page and model reference the same object, an owned lease disposes it once.
    /// </summary>
    [Fact]
    public void OwnedLease_DeduplicatesPageAndModelReference()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new OwnedPage();
            var lease = NavigationPageLease.Owned(page, page);
            Task first = lease.DisposeAsync();
            Task second = lease.DisposeAsync();
            Assert.Same(first, second);
            await first;
            Assert.Equal(1, page.DisposeCount);
        });
    }

    /// <summary>
    /// Eviction removes cache retention first and defers final disposal while the old activation
    /// still has work.
    /// </summary>
    [Fact]
    public void CacheEviction_WaitsForOldOperationThenDisposesExactlyOnce()
    {
        WpfTestThread.Run(async () =>
        {
            var release = NewCompletion();
            var page = new ContextPage();
            int disposals = 0;
            var host = new NavigationView
            {
                NavigationCacheSize = 1,
                MenuItemsSource = new[]
                {
                    new NavigationItem("Slow", typeof(ContextPage)),
                    new NavigationItem("Other", typeof(OtherPage))
                }
            };
            var scope = Scope.Attach(host, new DelegateProvider(type => type == typeof(ContextPage)
                ? NavigationPageLease.Owned(page, () =>
                {
                    disposals++;
                    return Task.CompletedTask;
                })
                : NavigationPageLease.Owned(new OtherPage())));
            await scope.Navigation.NavigateAsync(NavigationRequest.To<ContextPage>());
            await PumpAsync();
            var work = page.Context!.RunAsync(_ => release.Task);
            await scope.Navigation.NavigateAsync(NavigationRequest.To<OtherPage>());
            await PumpAsync();
            Assert.Equal(0, disposals);
            release.SetResult(true);
            await work;
            await PumpAsync();
            Assert.Equal(1, disposals);
            await scope.DisposeAsync();
            Assert.Equal(1, disposals);
        });
    }

    /// <summary>
    /// After a location getter stops the scope, old resolution must not restore cleared
    /// projections.
    /// </summary>
    [Fact]
    public void LocationGetter_StopCannotRepublishOldPage()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new SourceProbePage();
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Owned(page)));
            page.Read = scope.RequestStop;
            var result = await scope.Navigation.NavigateAsync(NavigationRequest.To<SourceProbePage>());
            await scope.Completion;
            await PumpAsync();
            Assert.True(result.WasCommitted);
            Assert.Null(host.CurrentLocation);
            Assert.Empty(host.Breadcrumbs);
            Assert.Null(GetFrame(host).Content);
            Assert.Equal(0, page.Subscribers);
            Assert.Equal(1, page.Disposals);
        });
    }

    /// <summary>
    /// Post-commit location extension failure preserves failure and commit status while other
    /// projections reference the actual page.
    /// </summary>
    [Fact]
    public void LocationGetterFailure_KeepsCommittedPageAndReportsFailure()
    {
        WpfTestThread.Run(async () =>
        {
            var expected = new InvalidOperationException("location getter");
            var page = new SourceProbePage { Read = () => throw expected };
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(type => type == typeof(SourceProbePage)
                ? NavigationPageLease.Owned(page) : NavigationPageLease.Owned(new OtherPage())));
            await scope.Navigation.NavigateAsync(NavigationRequest.To<OtherPage>());
            var result = await scope.Navigation.NavigateAsync(NavigationRequest.To<SourceProbePage>());
            Assert.Equal(NavigationResultStatus.Failed, result.Status);
            Assert.True(result.WasCommitted);
            Assert.Same(expected, result.Failure);
            Assert.Same(page, GetFrame(host).Content);
            Assert.Same(page, host.CurrentLocation!.Page);
            Assert.Equal(typeof(SourceProbePage), host.CurrentLocation.Target.PageType);
            Assert.Empty(host.Breadcrumbs);
            Assert.True(host.CanGoBack);
            Assert.Same(scope.Navigation, Wpf.Zen.Ui.Navigation.GetService(page));
            await scope.DisposeAsync();
            Assert.Equal(1, page.Disposals);
        });
    }

    /// <summary>
    /// Location unbinding failure must not skip page disposal, context clearing, or other
    /// independent cleanup.
    /// </summary>
    [Fact]
    public void LocationUnsubscribeFailure_DoesNotSkipRemainingCleanup()
    {
        WpfTestThread.Run(async () =>
        {
            var expected = new InvalidOperationException("location unsubscribe");
            var page = new SourceProbePage { Remove = () => throw expected };
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Owned(page)));
            await scope.Navigation.NavigateAsync(NavigationRequest.To<SourceProbePage>());
            var failure = await Record.ExceptionAsync(scope.DisposeAsync);
            Assert.Same(expected, failure);
            Assert.Null(Wpf.Zen.Ui.Navigation.GetContext(page));
            Assert.Null(host.CurrentLocation);
            Assert.Equal(0, page.Subscribers);
            Assert.Equal(1, page.Disposals);
        });
    }

    /// <summary>
    /// Allows independent failure injection in external location extension calls, event accessors,
    /// and disposal.
    /// </summary>
    private sealed class SourceProbePage : Border, INavigationLocationSource, IDisposable
    {
        private EventHandler? _changed;
        internal Action? Read { get; set; }
        internal Action? Remove { get; set; }
        internal int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        internal int Disposals { get; private set; }

        /// <inheritdoc />
        public event EventHandler? LocationChanged
        {
            add => _changed += value;
            remove
            {
                _changed -= value;
                Remove?.Invoke();
            }
        }

        /// <inheritdoc />
        public NavigationLocationDescriptor GetNavigationLocation(NavigationLocationContext context)
        {
            Read?.Invoke();
            return new NavigationLocationDescriptor(content: "Dynamic");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Disposals++;
        }
    }
}
