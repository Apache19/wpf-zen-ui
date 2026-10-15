// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading.Tasks;
using System.Threading;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.DependencyInjection;
using Scope = Wpf.Zen.Ui.NavigationScope;

namespace Wpf.Zen.Ui.UnitTests.Navigation;

public sealed partial class NavigationLifecycleContractTests
{
    /// <summary>
    /// Without an ambient UI synchronization context, registered work still delivers errors and
    /// releases retention on the Dispatcher.
    /// </summary>
    [Fact]
    public void Review_OperationWithoutAmbientContextDrainsOnDispatcher()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var page = new ContextPage();
            var released = false;
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Owned(page, () =>
            {
                Assert.True(host.Dispatcher.CheckAccess());
                released = true;
                return Task.CompletedTask;
            })));
            await scope.Navigation.NavigateAsync(NavigationRequest.To<ContextPage>());
            await PumpAsync();
            var gate = NewCompletion();
            Task operation;
            var previous = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                operation = page.Context!.RunAsync(_ => gate.Task);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }

            Task stopped = scope.DisposeAsync();
            Assert.False(stopped.IsCompleted);
            gate.SetResult(true);
            await operation;
            await stopped.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(released);
        });
    }

    /// <summary>
    /// Frame stop notification failures during cancellation belong to the request result rather
    /// than an unowned dispatch exception.
    /// </summary>
    [Fact]
    public void Review_FrameStopNotificationFailureIsObservedByRequest()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var scope = Scope.Attach(host);
            using var cancellation = new CancellationTokenSource();
            var expected = new InvalidOperationException("stop notification");
            var frame = GetFrame(host);
            frame.NavigationStopped += (_, _) => throw expected;
            Task<NavigationResult> navigation = scope.Navigation.NavigateAsync(
                NavigationRequest.To<OtherPage>(), cancellation.Token);
            cancellation.Cancel();
            var result = await navigation;
            Assert.Same(expected, result.Failure);
            Assert.False(result.WasCommitted);
            Assert.Null(host.CurrentLocation);
            Assert.Null(frame.Content);
            await scope.DisposeAsync();
        });
    }

    /// <summary>
    /// Concurrent exit, error notification, page disposal, and model disposal failures preserve all
    /// four errors and execute each step once.
    /// </summary>
    [Fact]
    public void Review_MultipleCleanupFailuresPreserveEveryOriginal()
    {
        WpfTestThread.Run(async () =>
        {
            var exitFailure = new InvalidOperationException("exit");
            var notificationFailure = new InvalidOperationException("notification");
            var pageFailure = new InvalidOperationException("page disposal");
            var modelFailure = new InvalidOperationException("model disposal");
            var page = new ReviewExitPage
            {
                Exit = () => throw exitFailure,
                DisposeAction = () => throw pageFailure
            };
            var model = new ReviewAsyncResource
            {
                Release = async () =>
                {
                    await Task.Yield();
                    throw modelFailure;
                }
            };
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Owned(page, model)));
            host.NavigationCallbackFailed += (_, _) => throw notificationFailure;
            await scope.Navigation.NavigateAsync(NavigationRequest.To<ReviewExitPage>());
            await PumpAsync();
            var failure = await Record.ExceptionAsync(scope.DisposeAsync);
            Assert.NotNull(failure);
            var errors = Flatten(failure);
            Assert.Equal(4, errors.Count);
            Assert.Contains(errors, error => error is NavigationCallbackException callback
                && ReferenceEquals(callback.InnerException, exitFailure));
            Assert.Contains(notificationFailure, errors);
            Assert.Contains(pageFailure, errors);
            Assert.Contains(modelFailure, errors);
            Assert.Equal(1, page.Exits);
            Assert.Equal(1, page.Disposals);
            Assert.Equal(1, model.Disposals);
            Assert.Null(host.CurrentLocation);
        });
    }

    /// <summary>
    /// After detachment, old operations still cannot use control convenience methods to await their
    /// own drain task.
    /// </summary>
    [Fact]
    public void Review_DetachedHostRejectsSelfWait()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new ContextPage();
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Borrowed(page)));
            await scope.Navigation.NavigateAsync(NavigationRequest.To<ContextPage>());
            await PumpAsync();
            var resume = NewCompletion();
            Exception? failure = null;
            Task operation = page.Context!.RunAsync(async token =>
            {
                await resume.Task;
                failure = Record.Exception(() => { _ = host.StopNavigationAsync(); });
            });
            Task stopped = scope.DisposeAsync();
            Assert.False(stopped.IsCompleted);
            resume.SetResult(true);
            await operation;
            await stopped;
            Assert.IsType<InvalidOperationException>(failure);
        });
    }

    /// <summary>
    /// Stop requested by old page exit after commit must still clear old page and menu projections
    /// and host references.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Review_OutgoingExitStopClearsBothPages(bool replace)
    {
        WpfTestThread.Run(async () =>
        {
            var page = new ReviewExitPage();
            var next = new OtherPage();
            var item = new NavigationViewItem { TargetPageType = typeof(ReviewExitPage) };
            var host = new NavigationView();
            host.MenuItems.Add(item);
            var provider = new DelegateProvider(type => NavigationPageLease.Borrowed(
                type == typeof(ReviewExitPage) ? page : next));
            var scope = Scope.Attach(host, provider);
            await scope.Navigation.NavigateAsync(NavigationRequest.To<ReviewExitPage>());
            await PumpAsync();
            Assert.True(item.IsActive);
            page.Exit = scope.RequestStop;

            if (replace)
            {
                Assert.True(host.ReplaceContent(next));
                await PumpAsync();
            }
            else
            {
                var result = await scope.Navigation.NavigateAsync(NavigationRequest.To<OtherPage>());
                Assert.True(result.WasCommitted);
            }

            await scope.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await PumpAsync();
            Assert.Equal(1, page.Exits);
            Assert.False(item.IsActive);
            Assert.Null(page.GetValue(NavigationView.NavigationParentProperty));
            Assert.Null(next.GetValue(NavigationView.NavigationParentProperty));
            Assert.Null(Wpf.Zen.Ui.Navigation.GetContext(page));
            Assert.Null(Wpf.Zen.Ui.Navigation.GetContext(next));
            Assert.Null(host.CurrentLocation);
            Assert.Null(host.SelectedItem);
            Assert.False(host.CanGoBack);
            Assert.Null(GetFrame(host).Content);
        });
    }

    /// <summary>
    /// Asynchronous page and container disposal belong to stop completion and must reject awaiting
    /// their own scope.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Review_ResourceDisposalRejectsScopeSelfWait(bool containerOwned)
    {
        WpfTestThread.Run(async () =>
        {
            var resource = new ReviewAsyncResource();
            var registrations = new ServiceCollection();
            registrations.AddNavigation();
            registrations.AddScoped(_ => resource);
            await using var root = registrations.BuildServiceProvider();
            var host = new NavigationView();
            var scope = containerOwned
                ? root.GetRequiredService<INavigationScopeFactory>().Attach(host)
                : Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Owned(
                    new OtherPage(), resource)));
            if (containerOwned)
            {
                scope.Services.GetRequiredService<ReviewAsyncResource>();
            }

            resource.Release = async () =>
            {
                // A controlled recovery gate lets incorrect implementations finish; assertions
                // verify immediate rejection without hanging.
                Task ownCompletion = scope.DisposeAsync();
                resource.WaitAttempted = true;
                await Task.WhenAny(ownCompletion, resource.Escape.Task);
            };
            await scope.Navigation.NavigateAsync(NavigationRequest.To<OtherPage>());
            await PumpAsync();
            Task stopped = scope.DisposeAsync();
            await PumpAsync();
            resource.Escape.TrySetResult(true);
            var failure = await Record.ExceptionAsync(() => stopped.WaitAsync(
                TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.False(resource.WaitAttempted);
            Assert.IsType<InvalidOperationException>(failure);
            Assert.Equal(1, resource.Disposals);
        });
    }

    /// <summary>
    /// A participant that can request stop during exit, retaining its page to inspect all previous
    /// projections.
    /// </summary>
    private sealed class ReviewExitPage : Border, INavigationAware, IDisposable
    {
        internal Action? Exit { get; set; }
        internal Action? DisposeAction { get; set; }
        internal int Exits { get; private set; }
        internal int Disposals { get; private set; }

        /// <inheritdoc />
        public void Dispose()
        {
            Disposals++;
            DisposeAction?.Invoke();
        }

        /// <inheritdoc />
        public void OnNavigationEntered()
        {
        }

        /// <inheritdoc />
        public void OnNavigationExited()
        {
            Exits++;
            Exit?.Invoke();
        }
    }

    /// <summary>
    /// A controlled resource for both page and container asynchronous disposal paths.
    /// </summary>
    private sealed class ReviewAsyncResource : IAsyncDisposable
    {
        internal Func<Task>? Release { get; set; }
        internal TaskCompletionSource<bool> Escape { get; } = NewCompletion();
        internal bool WaitAttempted { get; set; }
        internal int Disposals { get; private set; }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            Disposals++;
            if (Release is not null)
            {
                await Release();
            }
        }
    }
}
