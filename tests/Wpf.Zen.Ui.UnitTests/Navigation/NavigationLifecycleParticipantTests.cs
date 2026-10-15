// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Wpf.Zen.Ui.Controls;
using Scope = Wpf.Zen.Ui.NavigationScope;

namespace Wpf.Zen.Ui.UnitTests.Navigation;

public partial class NavigationLifecycleContractTests
{
    /// <summary>
    /// Participants are frozen before entry and deduplicated by reference; partial entry failure
    /// still requires reverse-order exit.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Participants_SnapshotDeduplicatesAndUnwindsPartialEntry(bool failView)
    {
        WpfTestThread.Run(async () =>
        {
            var events = new List<string>();
            var model = new LifecycleParticipant(events, "model", !failView);
            var replacement = new LifecycleParticipant(events, "replacement", false);
            var page = new ParticipantPage(events, model, replacement, failView) { DataContext = model };
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Borrowed(page)));
            var errors = new List<NavigationCallbackException>();
            host.NavigationCallbackFailed += (_, args) =>
            {
                errors.Add(args.Exception);
                args.Handled = true;
            };

            await scope.Navigation.NavigateAsync(NavigationRequest.To<ParticipantPage>());
            await PumpAsync();
            Assert.Same(replacement, page.DataContext);
            Assert.Equal(new[] { "view.enter", "model.enter" }, events);
            await scope.DisposeAsync();

            Assert.Equal(new[] { "view.enter", "model.enter", "model.exit", "view.exit" }, events);
            var failure = Assert.Single(errors);
            Assert.Equal(NavigationCallbackStage.Entered, failure.Stage);
            Assert.Same(failView ? page : (object)model, failure.CallbackTarget);
        });
    }

    /// <summary>
    /// Background stop cancels, exits, and disposes on the UI thread; cancellation failures must
    /// not skip exit or owned disposal.
    /// </summary>
    [Fact]
    public void BackgroundStop_CancellationFailureStillExitsAndReleasesOwnedPage()
    {
        WpfTestThread.Run(async () =>
        {
            var page = new CancellationFailurePage();
            var host = new NavigationView();
            var scope = Scope.Attach(host, new DelegateProvider(_ => NavigationPageLease.Owned(page)));
            var errors = new List<NavigationCallbackException>();
            host.NavigationCallbackFailed += (_, args) =>
            {
                Assert.Equal(0, page.Disposals);
                Assert.True(page.Dispatcher.CheckAccess());
                errors.Add(args.Exception);
            };

            await scope.Navigation.NavigateAsync(NavigationRequest.To<CancellationFailurePage>());
            await PumpAsync();
            Task completion = scope.Completion;
            await Task.Run(scope.RequestStop);
            var failure = await Record.ExceptionAsync(() => completion.WaitAsync(
                TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

            Assert.NotNull(failure);
            var notification = Assert.Single(errors);
            var cancellation = Assert.IsType<AggregateException>(notification.InnerException);
            Assert.Contains(page.Failure, cancellation.InnerExceptions);
            Assert.Equal(1, page.Exits);
            Assert.Equal(1, page.Disposals);
            Assert.True(page.CleanupOnDispatcher);
            Assert.Null(host.CurrentLocation);
        });
    }

    /// <summary>
    /// Background requests revalidate old session identity if the session changes before execution.
    /// </summary>
    [Fact]
    public void QueuedOldNavigator_CannotExecuteAgainstReplacementSession()
    {
        WpfTestThread.Run(async () =>
        {
            var host = new NavigationView();
            var oldScope = Scope.Attach(host);
            Task<Task<NavigationResult>> submission = Task.Factory.StartNew(
                () => oldScope.Navigation.NavigateAsync(NavigationRequest.To<ProbePage>()),
                CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
            // Wait only for background queueing; UI execution remains pending without timing-based
            // ordering assumptions.
            Task<NavigationResult> pending = submission.GetAwaiter().GetResult();
            oldScope.RequestStop();
            var replacement = Scope.Attach(host);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
            Assert.Null(host.CurrentLocation);
            var result = await replacement.Navigation.NavigateAsync(NavigationRequest.To<OtherPage>());
            Assert.True(result.Succeeded);
            await oldScope.Completion;
            Assert.IsType<OtherPage>(host.CurrentLocation!.Page);
            await replacement.DisposeAsync();
        });
    }

    /// <summary>
    /// Changes DataContext during entry while exposing the original participant through a separate
    /// view model role.
    /// </summary>
    private sealed class ParticipantPage(
        List<string> events,
        LifecycleParticipant model,
        LifecycleParticipant replacement,
        bool fail) : Border, INavigationAware, INavigableView<object>
    {
        /// <inheritdoc />
        public object ViewModel { get; } = model;

        /// <inheritdoc />
        public void OnNavigationEntered()
        {
            events.Add("view.enter");
            DataContext = replacement;
            if (fail)
            {
                throw new InvalidOperationException("partial view entry");
            }
        }

        /// <inheritdoc />
        public void OnNavigationExited()
        {
            events.Add("view.exit");
        }
    }

    /// <summary>
    /// Records actual entry and exit, optionally simulating partial model initialization failure.
    /// </summary>
    private sealed class LifecycleParticipant(List<string> events, string name, bool fail) : INavigationAware
    {
        /// <inheritdoc />
        public void OnNavigationEntered()
        {
            events.Add(name + ".enter");
            if (fail)
            {
                throw new InvalidOperationException("partial model entry");
            }
        }

        /// <inheritdoc />
        public void OnNavigationExited()
        {
            events.Add(name + ".exit");
        }
    }

    /// <summary>
    /// An owned page throwing during cancellation verifies independent cleanup and Dispatcher
    /// affinity.
    /// </summary>
    private sealed class CancellationFailurePage : Border, IAsyncNavigationAware, IDisposable
    {
        internal Exception Failure { get; } = new InvalidOperationException("cancel callback");
        internal int Exits { get; private set; }
        internal int Disposals { get; private set; }
        internal bool CleanupOnDispatcher { get; private set; } = true;

        /// <inheritdoc />
        public Task OnNavigationEnteredAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => throw Failure);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void OnNavigationExited()
        {
            Exits++;
            CleanupOnDispatcher &= Dispatcher.CheckAccess();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Disposals++;
            CleanupOnDispatcher &= Dispatcher.CheckAccess();
        }
    }
}
