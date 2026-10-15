// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.DependencyInjection;
using Scope = Wpf.Zen.Ui.NavigationScope;

namespace Wpf.Zen.Ui.UnitTests.Navigation;

public partial class NavigationLifecycleContractTests
{
    /// <summary>
    /// Converts Mining cache modes and slow-work boundaries into general regressions covering both
    /// scope implementations.
    /// </summary>
    [Theory]
    [InlineData(NavigationCacheMode.Disabled, false)]
    [InlineData(NavigationCacheMode.Evictable, false)]
    [InlineData(NavigationCacheMode.Permanent, false)]
    [InlineData(NavigationCacheMode.Disabled, true)]
    [InlineData(NavigationCacheMode.Evictable, true)]
    [InlineData(NavigationCacheMode.Permanent, true)]
    public void StopMatrix_DrainsDepartedEntryBeforeOwnedResources(
        NavigationCacheMode mode, bool useDi)
    {
        WpfTestThread.Run(async () =>
        {
            var page = new ProbePage { Pending = NewCompletion() };
            var registrations = new ServiceCollection();
            registrations.AddNavigation();
            registrations.AddScoped<DisposableProbe>();
            registrations.AddScoped(_ => page);
            using var root = registrations.BuildServiceProvider();
            var host = new NavigationView
            {
                MenuItemsSource = new[]
                {
                    new NavigationItem("Slow", typeof(ProbePage)) { NavigationCacheMode = mode },
                    new NavigationItem("Other", typeof(OtherPage))
                }
            };
            var provider = new DelegateProvider(type => type == typeof(ProbePage)
                ? NavigationPageLease.Borrowed(page)
                : NavigationPageLease.Owned(new OtherPage()));
            var scope = useDi
                ? root.GetRequiredService<INavigationScopeFactory>().Attach(host)
                : Scope.Attach(host, provider);
            var resource = useDi ? scope.Services.GetRequiredService<DisposableProbe>() : null;
            try
            {
                var first = await scope.Navigation.NavigateAsync(NavigationRequest.To<ProbePage>());
                Assert.True(first.Succeeded);
                await PumpAsync();
                Assert.Equal(1, page.Entries);
                var next = await scope.Navigation.NavigateAsync(NavigationRequest.To<OtherPage>());
                Assert.True(next.Succeeded);
                await PumpAsync();
                Assert.True(page.Token.IsCancellationRequested);
                Assert.Equal(1, page.Exits);

                Task stopped = scope.DisposeAsync();
                Assert.False(stopped.IsCompleted);
                Assert.False(resource?.Disposed ?? false);
                Assert.Null(host.CurrentLocation);
                page.Pending.TrySetResult(true);
                await stopped.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Assert.True(resource?.Disposed ?? true);
            }
            finally
            {
                page.Pending.TrySetResult(true);
                await scope.DisposeAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
        });
    }

    /// <summary>
    /// Cached reentry waits for old work, uses a fresh token, and never enters the same model
    /// concurrently.
    /// </summary>
    [Theory]
    [InlineData(NavigationCacheMode.Evictable)]
    [InlineData(NavigationCacheMode.Permanent)]
    public void CachedInstance_ReentryWaitsForPreviousActivation(NavigationCacheMode mode)
    {
        WpfTestThread.Run(async () =>
        {
            var page = new ProbePage { Pending = NewCompletion() };
            var provider = new DelegateProvider(type => type == typeof(ProbePage)
                ? NavigationPageLease.Borrowed(page)
                : NavigationPageLease.Owned(new OtherPage()));
            var host = new NavigationView
            {
                MenuItemsSource = new[]
                {
                    new NavigationItem("Cached", typeof(ProbePage)) { NavigationCacheMode = mode }
                }
            };
            var scope = Scope.Attach(host, provider);
            try
            {
                await scope.Navigation.NavigateAsync(NavigationRequest.To<ProbePage>());
                await PumpAsync();
                var oldToken = page.Token;
                await scope.Navigation.NavigateAsync(NavigationRequest.To<OtherPage>());
                await scope.Navigation.NavigateAsync(NavigationRequest.To<ProbePage>());
                await PumpAsync();
                Assert.Same(page, GetFrame(host).Content);
                Assert.Equal(1, page.Entries);
                Assert.Equal(1, page.Exits);
                page.Pending.TrySetResult(true);
                await PumpAsync();
                Assert.Equal(2, page.Entries);
                Assert.NotEqual(oldToken, page.Token);
                Assert.False(page.Token.IsCancellationRequested);
            }
            finally
            {
                page.Pending.TrySetResult(true);
                await scope.DisposeAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
        });
    }

    /// <summary>
    /// The same object with a different parameter still produces a new committed semantic record
    /// and activation.
    /// </summary>
    [Fact]
    public void SameObjectParameterChange_CommitsAndCreatesFreshActivation()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new ProbePage();
            var host = new NavigationView
            {
                MenuItemsSource = new[]
                {
                    new NavigationItem("Same", typeof(ProbePage))
                    {
                        NavigationCacheMode = NavigationCacheMode.Permanent
                    }
                }
            };
            var scope = Scope.Attach(host, new SinglePageProvider(page));
            try
            {
                var first = NavigationRequest.To(typeof(ProbePage), "first");
                Assert.True((await scope.Navigation.NavigateAsync(first)).Succeeded);
                await PumpAsync();
                var second = NavigationRequest.To(typeof(ProbePage), "second");
                var result = await scope.Navigation.NavigateAsync(second).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Assert.True(result.WasCommitted);
                await PumpAsync();
                Assert.Equal(2, page.Entries);
                Assert.Equal(1, page.Exits);
                Assert.True(host.CanGoBack);
                Assert.Equal("second", Wpf.Zen.Ui.Navigation.GetParameter(page));
            }
            finally
            {
                await scope.DisposeAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
        });
    }

    /// <summary>
    /// Stop waits for registered post-initialization work that continues cleanup after
    /// cancellation.
    /// </summary>
    [Fact]
    public void RegisteredOperation_IsIncludedAfterInitializationCompletes()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new ContextPage();
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Borrowed(page)));
            var pending = NewCompletion();
            try
            {
                await scope.Navigation.NavigateAsync(NavigationRequest.To<ContextPage>());
                await PumpAsync();
                Assert.NotNull(page.Context);
                Task operation = page.Context.RunAsync(_ => pending.Task);
                Task stopped = scope.DisposeAsync();
                Assert.False(stopped.IsCompleted);
                Assert.True(page.Context.CancellationToken.IsCancellationRequested);
                Assert.Throws<ObjectDisposedException>(() =>
                {
                    _ = page.Context.RunAsync(_ => Task.CompletedTask);
                });
                pending.TrySetResult(true);
                await operation;
                await stopped.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            finally
            {
                pending.TrySetResult(true);
                await scope.DisposeAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
        });
    }

    /// <summary>
    /// Callbacks may request stop; awaiting their own completion boundary fails immediately instead
    /// of hanging.
    /// </summary>
    [Fact]
    public void CallbackSelfWait_IsRejectedButRequestStopIsAllowed()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new ContextPage();
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Borrowed(page)));
            Exception? rejected = null;
            page.Enter = () =>
            {
                rejected = Record.Exception(() =>
                {
                    _ = scope.DisposeAsync();
                });
                scope.RequestStop();
                return Task.CompletedTask;
            };
            await scope.Navigation.NavigateAsync(NavigationRequest.To<ContextPage>());
            await PumpAsync();
            await scope.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.IsType<InvalidOperationException>(rejected);
            Assert.Equal(1, page.Exits);
        });
    }

    /// <summary>
    /// Handler failure preserves both the original entry failure and handler failure, with one
    /// notification.
    /// </summary>
    [Fact]
    public void ErrorHandlerFailure_PreservesBothFailuresBeforeServicesDispose()
    {
        WpfTestThread.Run(async () =>
        {
            using var root = CreateRoot();
            var host = new NavigationView();
            var scope = Attach(root, host, true);
            var dependency = scope.Services.GetRequiredService<DisposableProbe>();
            var page = new ProbePage { Pending = NewCompletion() };
            var entryFailure = new InvalidOperationException("entry");
            var handlerFailure = new InvalidOperationException("handler");
            int notifications = 0;
            host.NavigationCallbackFailed += (_, _) =>
            {
                notifications++;
                Assert.False(dependency.Disposed);
                throw handlerFailure;
            };
            host.ReplaceContent(page);
            await PumpAsync();
            Task stopped = scope.DisposeAsync();
            page.Pending.TrySetException(entryFailure);
            var failure = await Record.ExceptionAsync(() => stopped.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.NotNull(failure);
            var errors = Flatten(failure);
            Assert.Contains(handlerFailure, errors);
            Assert.Contains(errors, error => error is NavigationCallbackException callback
                && ReferenceEquals(callback.InnerException, entryFailure));
            Assert.Equal(1, notifications);
            Assert.True(dependency.Disposed);
        });
    }

    /// <summary>
    /// Cancelling a candidate immediately transfers final disposal, including asynchronous disposal
    /// in scope completion.
    /// </summary>
    [Fact]
    public void CancelledOwnedCandidate_IsReleasedBeforeScopeCompletes()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new AsyncOwnedPage();
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Owned(page)));
            host.Navigating += (_, args) => args.Cancel = true;
            var result = await scope.Navigation.NavigateAsync(NavigationRequest.To<AsyncOwnedPage>());
            Assert.Equal(NavigationResultStatus.Cancelled, result.Status);
            Assert.Equal(1, page.AsyncDisposals);
            Assert.Equal(0, page.SyncDisposals);
            Task stopped = scope.DisposeAsync();
            Assert.False(stopped.IsCompleted);
            page.Release.TrySetResult(true);
            await stopped.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(1, page.AsyncDisposals);
        });
    }

    private sealed class DelegateProvider(Func<Type, NavigationPageLease> create) : INavigationViewPageProvider
    {
        public NavigationPageLease GetPage(Type pageType)
        {
            return create(pageType);
        }
    }

    private sealed class ContextPage : Border, INavigationActivationAware
    {
        internal NavigationActivationContext? Context { get; private set; }
        internal Func<Task>? Enter { get; set; }
        internal int Exits { get; private set; }

        public Task OnNavigationEnteredAsync(NavigationActivationContext context)
        {
            Context = context;
            return Enter?.Invoke() ?? Task.CompletedTask;
        }

        public void OnNavigationExited()
        {
            Exits++;
        }
    }

    private sealed class AsyncOwnedPage : Border, IAsyncDisposable, IDisposable
    {
        internal TaskCompletionSource<bool> Release { get; } = NewCompletion();
        internal int AsyncDisposals { get; private set; }
        internal int SyncDisposals { get; private set; }

        public async ValueTask DisposeAsync()
        {
            AsyncDisposals++;
            await Release.Task;
        }

        public void Dispose()
        {
            SyncDisposals++;
        }
    }
}
