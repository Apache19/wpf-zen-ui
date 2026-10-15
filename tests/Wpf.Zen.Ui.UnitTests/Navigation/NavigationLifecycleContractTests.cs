// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.DependencyInjection;
using Scope = Wpf.Zen.Ui.NavigationScope;

namespace Wpf.Zen.Ui.UnitTests.Navigation;

/// <summary>
/// Contract regressions for cases N01 through N12; controlled tasks advance every asynchronous
/// boundary.
/// </summary>
public partial class NavigationLifecycleContractTests
{
    /// <summary>
    /// N01: An invalidated service cannot access a new scope through a reused host.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void N01_StoppedNavigatorCannotControlReplacementScope(bool useDi)
    {
        WpfTestThread.Run(async () =>
        {
            using var root = CreateRoot();
            var host = new NavigationView();
            var oldScope = Attach(root, host, useDi);
            var oldNavigator = oldScope.Navigation;
            await oldScope.DisposeAsync();
            var current = Attach(root, host, useDi);
            try
            {
                Assert.Throws<ObjectDisposedException>(() => oldNavigator.Navigate(typeof(ProbePage)));
                Assert.Null(host.CurrentLocation);
                Assert.Null(GetFrame(host).Content);
            }
            finally
            {
                await current.DisposeAsync();
            }
        });
    }

    /// <summary>
    /// N02: Stopping during preparation prevents candidate commit and entry.
    /// </summary>
    [Fact]
    public void N02_StopDuringNavigatingPreventsCommitAndEntry()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var page = new ProbePage();
            var scope = Scope.Attach(host, new SinglePageProvider(page));
            Task? stopped = null;
            host.Navigating += (_, _) => stopped = scope.DisposeAsync();
            var result = scope.Navigation.Navigate(typeof(ProbePage));
            await PumpAsync();
            await scope.DisposeAsync();

            Assert.NotNull(stopped);
            Assert.False(result.Succeeded);
            Assert.Null(GetFrame(host).Content);
            Assert.Null(host.CurrentLocation);
            Assert.False(host.CanGoBack);
            Assert.Null(host.SelectedItem);
            Assert.Null(Wpf.Zen.Ui.Navigation.GetService(page));
            Assert.Equal(0, page.Entries);
            Assert.Equal(0, page.Exits);
        });
    }

    /// <summary>
    /// N03: Real visual unload ends activation; reattachment creates a fresh token and entry
    /// obligation for the same instance.
    /// </summary>
    [Fact]
    public void N03_ReloadCreatesFreshActivationForSamePage()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var page = new ProbePage();
            var scope = Scope.Attach(host, new SinglePageProvider(page));
            var window = new Window { Content = host, ShowInTaskbar = false, Width = 500, Height = 400 };
            try
            {
                window.Show();
                scope.Navigation.Navigate(typeof(ProbePage));
                await PumpAsync();
                Assert.Equal(1, page.Entries);
                var firstToken = page.Token;
                window.Content = null;
                await PumpAsync();
                Assert.Equal(1, page.Exits);
                Assert.True(firstToken.IsCancellationRequested);

                window.Content = host;
                await PumpAsync();
                Assert.Equal(2, page.Entries);
                Assert.NotEqual(firstToken, page.Token);
                Assert.False(page.Token.IsCancellationRequested);
                Assert.Same(page, GetFrame(host).Content);
            }
            finally
            {
                await scope.DisposeAsync();
                window.Close();
            }

            Assert.Equal(2, page.Exits);
        });
    }

    /// <summary>
    /// N04: Stop during location publication prevents the outer call from reusing a transferred
    /// current record.
    /// </summary>
    [Fact]
    public void N04_StopDuringLocationNotificationDoesNotResumeStalePublication()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var scope = Scope.Attach(host);
            host.CurrentLocationChanged += (_, _) =>
            {
                if (host.CurrentLocation is not null)
                {
                    scope.Dispose();
                }
            };
            try
            {
                var error = Record.Exception(() => scope.Navigation.Navigate(typeof(ProbePage)));
                Assert.Null(error);
                await PumpAsync();
                Assert.Null(host.CurrentLocation);
                Assert.Null(host.SelectedItem);
                Assert.False(host.CanGoBack);
                Assert.Null(GetFrame(host).Content);
            }
            finally
            {
                await scope.DisposeAsync();
            }
        });
    }

    /// <summary>
    /// N05: Late entry failure notification belongs to stop completion; dependencies used by the
    /// handler must remain alive.
    /// </summary>
    [Fact]
    public void N05_AsyncErrorNotificationCompletesBeforeDependencyDisposal()
    {
        WpfTestThread.Run(async () =>
        {
            using var root = CreateRoot();
            var host = new NavigationView();
            var scope = Attach(root, host, true);
            var dependency = scope.Services.GetRequiredService<DisposableProbe>();
            var page = new ProbePage { Pending = NewCompletion() };
            bool? notifiedWhileAlive = null;
            host.NavigationCallbackFailed += (_, args) =>
            {
                notifiedWhileAlive = !dependency.Disposed;
                args.Handled = true;
            };
            host.ReplaceContent(page);
            await PumpAsync();
            Task stopped = scope.DisposeAsync();
            Assert.False(stopped.IsCompleted);
            page.Pending.SetException(new InvalidOperationException("late-entry"));
            await stopped.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(notifiedWhileAlive);
            Assert.True(dependency.Disposed);
        });
    }

    /// <summary>
    /// N06: Frame cancellation preserves the entire previous page projection as well as preventing
    /// candidate entry.
    /// </summary>
    [Fact]
    public void N06_FrameCancellationPreservesCommittedProjection()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var firstItem = new NavigationItem("First", typeof(ProbePage));
            var secondItem = new NavigationItem("Second", typeof(OtherPage));
            host.MenuItemsSource = new[] { firstItem, secondItem };
            var scope = Scope.Attach(host);
            try
            {
                scope.Navigation.Navigate(typeof(ProbePage), "original");
                await PumpAsync();
                var frame = GetFrame(host);
                var first = Assert.IsType<ProbePage>(frame.Content);
                var location = host.CurrentLocation;
                var selection = host.SelectedItem;
                FrameworkElement? candidate = null;
                frame.Navigating += (_, args) =>
                {
                    if (args.Content is OtherPage page)
                    {
                        candidate = page;
                        args.Cancel = true;
                    }
                };

                var result = scope.Navigation.Navigate(typeof(OtherPage), "cancelled");
                await PumpAsync();
                Assert.False(result.Succeeded);
                Assert.Same(first, frame.Content);
                Assert.Same(location, host.CurrentLocation);
                Assert.Same(selection, host.SelectedItem);
                Assert.False(host.CanGoBack);
                Assert.False(host.CanGoForward);
                Assert.Same(host, first.GetValue(NavigationView.NavigationParentProperty));
                Assert.Same(scope.Navigation, Wpf.Zen.Ui.Navigation.GetService(first));
                Assert.Equal("original", Wpf.Zen.Ui.Navigation.GetParameter(first));
                Assert.NotNull(candidate);
                Assert.Null(Wpf.Zen.Ui.Navigation.GetService(candidate));
                Assert.NotSame(host, candidate.GetValue(NavigationView.NavigationParentProperty));
                Assert.Equal(1, first.Entries);
                Assert.Equal(0, first.Exits);
            }
            finally
            {
                await scope.DisposeAsync();
            }
        });
    }

    /// <summary>
    /// N07: Child container dependencies supporting only asynchronous disposal must be awaited.
    /// </summary>
    [Fact]
    public void N07_AsyncOnlyDependencyIsAwaitedExactlyOnce()
    {
        WpfTestThread.Run(async () =>
        {
            using var root = CreateRoot();
            var scope = Attach(root, new NavigationView(), true);
            var resource = scope.Services.GetRequiredService<AsyncDisposableProbe>();
            Task stopped = scope.DisposeAsync();
            try
            {
                Assert.Equal(1, resource.DisposeCount);
                Assert.False(stopped.IsCompleted);
            }
            finally
            {
                resource.Completion.TrySetResult(true);
            }

            await stopped.WaitAsync(TimeSpan.FromSeconds(10));
            await scope.DisposeAsync();
            Assert.Equal(1, resource.DisposeCount);
        });
    }

    /// <summary>
    /// N08: A new scope on the same host does not wait for the old scope's slow work.
    /// </summary>
    [Fact]
    public void N08_ReplacementScopeDoesNotDrainPreviousScopeWork()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var previous = Scope.Attach(host);
            var page = new ProbePage { Pending = NewCompletion() };
            host.ReplaceContent(page);
            await PumpAsync();
            Task previousStop = previous.DisposeAsync();
            var current = Scope.Attach(host);
            Task currentStop = current.DisposeAsync();
            try
            {
                Assert.False(previousStop.IsCompleted);
                await PumpAsync();
                Assert.True(currentStop.IsCompletedSuccessfully);
            }
            finally
            {
                page.Pending.TrySetResult(true);
                await previousStop.WaitAsync(TimeSpan.FromSeconds(10));
                await currentStop.WaitAsync(TimeSpan.FromSeconds(10));
            }
        });
    }

    /// <summary>
    /// N09: Concurrent host and container failures preserve both original exceptions.
    /// </summary>
    [Fact]
    public void N09_DetachmentAndContainerFailuresAreBothPreserved()
    {
        WpfTestThread.Run(async () =>
        {
            using var root = CreateRoot();
            var detachFailure = new InvalidOperationException("detach");
            var host = new FaultHost { DetachFailure = detachFailure };
            var scope = root.GetRequiredService<INavigationScopeFactory>().Attach(host);
            var resource = scope.Services.GetRequiredService<DisposableProbe>();
            resource.Failure = new InvalidOperationException("dispose");
            Exception? error = await Record.ExceptionAsync(scope.DisposeAsync);
            Assert.NotNull(error);
            var failures = Flatten(error);
            Assert.Contains(detachFailure, failures);
            Assert.Contains(resource.Failure, failures);
            Assert.True(resource.Disposed);
            Assert.Equal(1, host.DetachCount);
        });
    }

    /// <summary>
    /// N10: Partial host attachment failure requires undoing host and route attachment before
    /// retry.
    /// </summary>
    [Fact]
    public void N10_PartialAttachmentRollsBackHostAndRoute()
    {
        WpfTestThread.Run(async () =>
        {
            using var root = CreateRoot();
            var host = new FaultHost { AttachFailure = new InvalidOperationException("attach") };
            var factory = root.GetRequiredService<INavigationScopeFactory>();
            Assert.Throws<InvalidOperationException>(() => factory.Attach(host, "rollback"));
            Assert.Equal(1, host.DetachCount);
            Assert.Null(host.AttachedService);
            host.AttachFailure = null;
            var scope = factory.Attach(host, "rollback");
            await scope.DisposeAsync();
        });
    }

    /// <summary>
    /// N11: Navigation ownership records finally dispose unregistered pages created by
    /// ActivatorUtilities.
    /// </summary>
    [Fact]
    public void N11_UnregisteredOwnedPageIsDisposedExactlyOnce()
    {
        WpfTestThread.Run(async () =>
        {
            using var root = CreateRoot();
            var host = new NavigationView();
            var scope = Attach(root, host, true);
            scope.Navigation.Navigate(typeof(OwnedPage));
            await PumpAsync();
            var page = Assert.IsType<OwnedPage>(GetFrame(host).Content);
            await scope.DisposeAsync();
            await scope.DisposeAsync();
            Assert.Equal(1, page.DisposeCount);
        });
    }

    /// <summary>
    /// N12: A stopped Dispatcher cannot deliver UI cleanup; stop must report restricted cleanup
    /// failure.
    /// </summary>
    [Fact]
    public async Task N12_LateShutdownReportsIncompleteUiCleanup()
    {
        INavigationScope? scope = null;
        WpfTestThread.Run(() => scope = Scope.Attach(new FaultHost()));
        Assert.NotNull(scope);
        Exception? error = await Record.ExceptionAsync(scope.DisposeAsync);
        Assert.NotNull(error);
        Assert.Contains("Dispatcher", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns independent child container resources so root disposal cannot conceal scope leaks.
    /// </summary>
    private static ServiceProvider CreateRoot()
    {
        var services = new ServiceCollection();
        services.AddNavigation();
        services.AddScoped<DisposableProbe>();
        services.AddScoped<AsyncDisposableProbe>();
        return services.BuildServiceProvider();
    }

    private static INavigationScope Attach(ServiceProvider root, NavigationView host, bool useDi)
    {
        if (useDi)
        {
            var factory = root.GetRequiredService<INavigationScopeFactory>();
            return factory.Attach(host);
        }

        return Scope.Attach(host);
    }

    /// <summary>
    /// Pumps to idle so actual Frame commits and Loaded/Unloaded receive deterministic execution
    /// opportunities.
    /// </summary>
    private static async Task PumpAsync()
    {
        await Dispatcher.CurrentDispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
    }

    private static TaskCompletionSource<bool> NewCompletion()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static Frame GetFrame(NavigationView host)
    {
        var field = typeof(NavigationView).GetField("_navigationFrame", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return Assert.IsType<Frame>(field.GetValue(host));
    }

    private static IReadOnlyList<Exception> Flatten(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            return aggregate.Flatten().InnerExceptions;
        }

        return new[] { exception };
    }

    private sealed class SinglePageProvider(ProbePage page) : INavigationViewPageProvider
    {
        /// <inheritdoc />
        public NavigationPageLease GetPage(Type pageType)
        {
            return NavigationPageLease.Borrowed(page);
        }
    }

    /// <summary>
    /// A page with controlled entry completion, retaining tokens and counts to verify pairing and
    /// reactivation.
    /// </summary>
    public sealed class ProbePage : Border, IAsyncNavigationAware
    {
        /// <summary>
        /// Gets actual entry count to detect cancelled candidate entry and duplicate activation.
        /// </summary>
        public int Entries { get; private set; }
        /// <summary>
        /// Gets delivered exit count, which must match actual entry obligations.
        /// </summary>
        public int Exits { get; private set; }
        /// <summary>
        /// Gets the latest activation token so cancellation remains inspectable after departure.
        /// </summary>
        public CancellationToken Token { get; private set; }
        /// <summary>
        /// Gets the test-controlled entry completion gate; null means synchronous completion.
        /// </summary>
        public TaskCompletionSource<bool>? Pending { get; init; }

        /// <inheritdoc />
        public Task OnNavigationEnteredAsync(CancellationToken cancellationToken)
        {
            Entries++;
            Token = cancellationToken;
            return Pending?.Task ?? Task.CompletedTask;
        }

        /// <inheritdoc />
        public void OnNavigationExited()
        {
            Exits++;
        }
    }

    /// <summary>
    /// Provides a distinct instance type to verify new pages do not wait for old page
    /// initialization.
    /// </summary>
    public sealed class OtherPage : Border { }

    /// <summary>
    /// Records final disposal counts for container-free and ActivatorUtilities pages.
    /// </summary>
    public sealed class OwnedPage : Border, IDisposable
    {
        /// <summary>
        /// Gets final disposal count; owned leases must ensure exactly one disposal.
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class DisposableProbe : IDisposable
    {
        public bool Disposed { get; private set; }
        public Exception? Failure { get; set; }

        public void Dispose()
        {
            Disposed = true;
            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }

    private sealed class AsyncDisposableProbe : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }
        public TaskCompletionSource<bool> Completion { get; } = NewCompletion();

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            await Completion.Task;
        }
    }

    /// <summary>
    /// Injects failures at scope factory infrastructure boundaries independently of WPF exception
    /// handling.
    /// </summary>
    private sealed class FaultHost : INavigationHost
    {
        public Dispatcher Dispatcher { get; } = Dispatcher.CurrentDispatcher;
        public INavigationState NavigationState { get; } = NSubstitute.Substitute.For<INavigationState>();
        public bool CanGoBack => false;
        public bool CanGoForward => false;
        public INavigationService? AttachedService { get; private set; }
        public Exception? AttachFailure { get; set; }
        public Exception? DetachFailure { get; init; }
        public int DetachCount { get; private set; }

        public NavigationResult Navigate(NavigationRequest request, CancellationToken cancellationToken = default)
        {
            return NavigationResult.Rejected;
        }

        public NavigationResult RequestGoBack(CancellationToken cancellationToken = default)
        {
            return NavigationResult.Rejected;
        }

        public NavigationResult RequestGoForward(CancellationToken cancellationToken = default)
        {
            return NavigationResult.Rejected;
        }

        public void ClearNavigationHistory() { }

        public void AttachNavigationScope(INavigationService navigation, INavigationViewPageProvider provider)
        {
            Assert.Null(AttachedService);
            AttachedService = navigation;
            if (AttachFailure is not null)
            {
                throw AttachFailure;
            }
        }

        public Task DetachNavigationScope(INavigationService navigation)
        {
            Assert.Same(AttachedService, navigation);
            AttachedService = null;
            DetachCount++;
            return DetachFailure is null ? Task.CompletedTask : Task.FromException(DetachFailure);
        }
    }
}
