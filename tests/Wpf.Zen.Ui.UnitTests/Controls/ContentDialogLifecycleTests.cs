// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Dialog = Wpf.Zen.Ui.Controls.ContentDialog;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public partial class ContentDialogTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lifecycle_UserAwaitPreservesDispatcherContext(bool callerWithoutContext) => Run(async scope =>
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = scope.CreateDialog();
        int dispatcherThread = Environment.CurrentManagedThreadId;
        bool resumed = false;
        dialog.DataContext = new DialogLifecycleProbe
        {
            Opening = async _ =>
            {
                Assert.Equal(dispatcherThread, Environment.CurrentManagedThreadId);
                Assert.IsType<DispatcherSynchronizationContext>(SynchronizationContext.Current);

                // The gate remains incomplete until ShowAsync has returned and opening has run,
                // so this verifies a real suspension rather than an inline completed-task await.
                await completion.Task;

                Assert.Equal(dispatcherThread, Environment.CurrentManagedThreadId);
                Assert.IsType<DispatcherSynchronizationContext>(SynchronizationContext.Current);
                dialog.VerifyAccess();
                dialog.Header = "Initialized";
                resumed = true;
                dialog.Close("Ready");
            },
        };

        SynchronizationContext? previousContext = SynchronizationContext.Current;
        Task<object?> presentation;
        try
        {
            if (callerWithoutContext)
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }

            presentation = scope.Host.ShowAsync(dialog);
            Assert.Same(callerWithoutContext ? null : previousContext, SynchronizationContext.Current);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        Drain();
        Assert.False(resumed);
        await Task.Run(() => completion.SetResult(), TestContext.Current.CancellationToken);
        object? result = await presentation;
        Assert.True(resumed);
        Assert.Equal("Initialized", dialog.Header);
        Assert.Equal("Ready", result);
    });

    [Fact]
    public void Lifecycle_WorkerFailureCancelsAndClosesOnDispatcher() => Run(scope =>
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Worker initialization failed");
        var dialog = scope.CreateDialog();
        int dispatcherThread = Environment.CurrentManagedThreadId;
        int cancellationThread = 0;
        int closedThread = 0;
        int closedEventThread = 0;
        var model = new DialogLifecycleProbe
        {
            Opening = _ => completion.Task,
            Closing = _ =>
            {
                closedThread = Environment.CurrentManagedThreadId;
                dialog.VerifyAccess();
                Assert.Null(scope.Host.CurrentDialog);
                Assert.True(scope.Background.IsEnabled);
            },
        };
        dialog.DataContext = model;
        dialog.Closed += (_, _) => closedEventThread = Environment.CurrentManagedThreadId;
        var presentation = scope.Host.ShowAsync(dialog);
        Drain();
        using var registration = model.Token.Register(() => cancellationThread = Environment.CurrentManagedThreadId);

        Task.Run(() => completion.SetException(failure), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Pump(presentation)));
        Assert.Equal(dispatcherThread, cancellationThread);
        Assert.Equal(dispatcherThread, closedThread);
        Assert.Equal(dispatcherThread, closedEventThread);
        Assert.False(dialog.IsOpen);
    });

    [Fact]
    public async Task Lifecycle_LateFailureIsObservedAfterDispatcherShutdown()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new DialogLifecycleProbe { Opening = _ => completion.Task };
        string marker = Guid.NewGuid().ToString();
        using var listener = new LifecycleFailureTraceListener(marker);
        Trace.Listeners.Add(listener);
        int dispatcherThread = 0;
        Task<object?>? presentation = null;
        try
        {
            WpfTestThread.Run(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                dispatcherThread = Environment.CurrentManagedThreadId;
                // Shutdown owns window teardown; disposing Scope would pump a stopped dispatcher.
#pragma warning disable IDISP001
                var scope = new Scope();
#pragma warning restore IDISP001
                var dialog = scope.CreateDialog();
                dialog.DataContext = model;
                presentation = scope.Host.ShowAsync(dialog);
                Drain();
                Assert.Equal(1, model.OpenedCount);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                Assert.True(presentation.IsCompletedSuccessfully);
                Assert.True(model.Token.IsCancellationRequested);
                Assert.Equal(1, model.ClosedCount);
            });

            // No dispatcher remains to run the observer. A late failure must still be consumed
            // and diagnosed without changing the already completed presentation result.
            completion.SetException(new InvalidOperationException(marker));
            int observerThread = await listener.Observed.Task.WaitAsync(TimeSpan.FromSeconds(8), TestContext.Current.CancellationToken);
            Assert.NotEqual(dispatcherThread, observerThread);
            Assert.Null(await presentation!);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public void Lifecycle_ViewAndModelReceivePairedNotificationsWithResult() => Run(async scope =>
    {
        var model = new DialogLifecycleProbe();
        var dialog = new AwareDialog { DataContext = model };
        var events = new List<string>();
        dialog.Probe.Opening = _ =>
        {
            Assert.True(dialog.IsLoaded);
            events.Add("view opened");
            return Task.CompletedTask;
        };
        model.Opening = _ =>
        {
            events.Add("model opened");
            return Task.CompletedTask;
        };
        dialog.Opened += (_, _) => events.Add("opened event");
        dialog.Probe.Closing = _ =>
        {
            Assert.Null(dialog.Parent);
            Assert.True(scope.Background.IsEnabled);
            Assert.True(dialog.Probe.Token.IsCancellationRequested);
            events.Add("view closed");
        };
        model.Closing = _ => events.Add("model closed");
        dialog.Closed += (_, _) => events.Add("closed event");

        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Equal(dialog.Probe.Token, model.Token);
        dialog.Close("Saved");
        object? result = await task;

        Assert.Equal("Saved", model.Result);
        Assert.Equal("Saved", result);
        Assert.Equal(new[] { "view opened", "model opened", "opened event", "view closed", "model closed", "closed event" }, events);
    });

    [Fact]
    public void Lifecycle_DirectContentAndSharedDataContextAreDeduplicated() => Run(scope =>
    {
        var model = new DialogLifecycleProbe();
        var view = new AwareDialog { DataContext = model };
        var dialog = scope.CreateDialog();
        dialog.Content = view;
        dialog.DataContext = model;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        dialog.Close();
        Pump(task);
        Assert.Equal(1, model.OpenedCount);
        Assert.Equal(1, model.ClosedCount);
        Assert.Equal(1, view.Probe.OpenedCount);
        Assert.Equal(1, view.Probe.ClosedCount);

        dialog = new AwareDialog();
        dialog.DataContext = dialog;
        task = scope.Host.ShowAsync(dialog);
        Drain();
        dialog.Close();
        Pump(task);
        Assert.Equal(1, ((AwareDialog)dialog).Probe.OpenedCount);
        Assert.Equal(1, ((AwareDialog)dialog).Probe.ClosedCount);
    });

    [Fact]
    public void Lifecycle_ModelReplacementDoesNotRedirectClosureAndReuseGetsFreshToken() => Run(scope =>
    {
        var first = new DialogLifecycleProbe();
        var next = new DialogLifecycleProbe();
        var dialog = scope.CreateDialog();
        dialog.DataContext = first;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        CancellationToken firstToken = first.Token;
        dialog.DataContext = next;
        dialog.Close();
        Pump(task);
        Assert.Equal(1, first.ClosedCount);
        Assert.Equal(0, next.ClosedCount);

        task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.True(firstToken.IsCancellationRequested);
        Assert.True(next.Token.CanBeCanceled);
        Assert.False(next.Token.IsCancellationRequested);
        Assert.NotEqual(firstToken, next.Token);
        dialog.Close();
        Pump(task);
        Assert.Equal(1, next.ClosedCount);
    });

    [Fact]
    public void Lifecycle_VetoKeepsWorkRunningAndAcceptedCloseCancelsBeforeExit() => Run(async scope =>
    {
        var stopped = new TaskCompletionSource();
        var model = new DialogLifecycleProbe
        {
            Opening = async token =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                finally
                {
                    stopped.SetResult();
                }
            },
        };
        var dialog = scope.CreateDialog();
        dialog.DataContext = model;
        bool veto = true;
        dialog.Closing += (_, e) => e.Cancel = veto;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        dialog.Close();
        Assert.False(model.Token.IsCancellationRequested);
        Assert.False(stopped.Task.IsCompleted);
        veto = false;
        dialog.Close("Done");
        Assert.True(model.Token.IsCancellationRequested);
        object? result = await task;
        await stopped.Task;
        Assert.Equal("Done", result);
    });

    [Theory]
    [InlineData("cancel")]
    [InlineData("hide")]
    [InlineData("unload")]
    public void Lifecycle_ForcedClosureCancelsWorkAndNotifiesOnce(string mode) => Run(scope =>
    {
        using var cancellation = new CancellationTokenSource();
        var model = new DialogLifecycleProbe();
        var dialog = scope.CreateDialog();
        dialog.DataContext = model;
        var task = scope.Host.ShowAsync(dialog, cancellation.Token);
        Drain();
        if (mode == "cancel")
        {
            Task.Run(() => cancellation.Cancel()).GetAwaiter().GetResult();
            Assert.ThrowsAny<OperationCanceledException>(() => Pump(task));
        }
        else
        {
            if (mode == "hide")
            {
                scope.Host.Visibility = Visibility.Hidden;
            }
            else
            {
                scope.Window.Content = null;
            }

            Pump(task);
        }

        Assert.True(model.Token.IsCancellationRequested);
        Assert.Equal(1, model.ClosedCount);
        Assert.Null(model.Result);
        Assert.Null(scope.Host.CurrentDialog);
    });

    [Fact]
    public void Lifecycle_ClosedBeforeEntranceDoesNotNotify() => Run(scope =>
    {
        var model = new DialogLifecycleProbe();
        var dialog = scope.CreateDialog();
        dialog.DataContext = model;
        var task = scope.Host.ShowAsync(dialog);
        dialog.Close();
        Pump(task);
        Assert.Equal(0, model.OpenedCount);
        Assert.Equal(0, model.ClosedCount);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lifecycle_OpeningFailureFaultsPresentationAndReleasesModalState(bool asynchronous) => Run(scope =>
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Initialization failed");
        var model = new DialogLifecycleProbe
        {
            Opening = _ => asynchronous ? completion.Task : throw failure,
        };
        var dialog = scope.CreateDialog();
        dialog.DataContext = model;
        var task = scope.Host.ShowAsync(dialog);
        if (asynchronous)
        {
            Drain();
            completion.SetException(failure);
        }

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Pump(task)));
        Assert.True(model.Token.IsCancellationRequested);
        Assert.Equal(1, model.ClosedCount);
        Assert.False(dialog.IsOpen);
        Assert.True(scope.Background.IsEnabled);
        Assert.Null(scope.Host.CurrentDialog);
    });

    [Fact]
    public void Lifecycle_UncooperativeOldTaskDoesNotBlockClosureOrFaultReuse() => Run(async scope =>
    {
        var completion = new TaskCompletionSource();
        var model = new DialogLifecycleProbe { Opening = _ => completion.Task };
        var dialog = scope.CreateDialog();
        dialog.DataContext = model;
        var first = scope.Host.ShowAsync(dialog);
        Drain();
        dialog.Close();
        await first;
        Assert.False(completion.Task.IsCompleted);

        model.Opening = _ => Task.CompletedTask;
        var second = scope.Host.ShowAsync(dialog);
        Drain();
        completion.SetException(new InvalidOperationException("Late failure"));
        Drain();
        Assert.False(second.IsCompleted);
        Assert.False(model.Token.IsCancellationRequested);
        dialog.Close("Second");
        object? result = await second;
        Assert.Equal("Second", result);
    });

    [Fact]
    public void Lifecycle_CancellationAndClosedFailuresStillReleaseAndNotifyOthers() => Run(scope =>
    {
        CancellationTokenRegistration registration = default;
        var dialog = new AwareDialog();
        dialog.Probe.Opening = token =>
        {
            registration = token.Register(() => throw new InvalidOperationException("Cancellation failed"));
            return Task.CompletedTask;
        };
        dialog.Probe.Closing = _ => throw new InvalidOperationException("Closure failed");
        var model = new DialogLifecycleProbe();
        dialog.DataContext = model;
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        dialog.Close();
        Assert.Throws<AggregateException>(() => Pump(task));
        registration.Dispose();
        Assert.True(closed);
        Assert.Equal(1, model.ClosedCount);
        Assert.False(dialog.IsOpen);
        Assert.Null(scope.Host.CurrentDialog);
        Assert.True(scope.Background.IsEnabled);
    });

    [Fact]
    public void Lifecycle_ReentrantHostRemovalPairsOnlyStartedRecipients() => Run(scope =>
    {
        var dialog = new AwareDialog();
        var model = new DialogLifecycleProbe();
        dialog.DataContext = model;
        dialog.Probe.Opening = _ =>
        {
            scope.Host.Visibility = Visibility.Hidden;
            return Task.CompletedTask;
        };
        var task = scope.Host.ShowAsync(dialog);
        Pump(task);
        Assert.Equal(1, dialog.Probe.OpenedCount);
        Assert.Equal(1, dialog.Probe.ClosedCount);
        Assert.True(dialog.Probe.Token.IsCancellationRequested);
        Assert.Equal(0, model.OpenedCount);
        Assert.Equal(0, model.ClosedCount);
    });

    [Fact]
    public void Lifecycle_DispatcherShutdownCancelsAndClosesSynchronously()
    {
        WpfTestThread.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            // Shutdown tears down the window; Scope.Dispose would attempt to pump a stopped dispatcher.
#pragma warning disable IDISP001
            var scope = new Scope();
#pragma warning restore IDISP001
            var model = new DialogLifecycleProbe();
            var dialog = scope.CreateDialog();
            dialog.DataContext = model;
            var task = scope.Host.ShowAsync(dialog);
            Drain();
            Dispatcher.CurrentDispatcher.InvokeShutdown();
            Assert.True(task.IsCompletedSuccessfully);
            Assert.True(model.Token.IsCancellationRequested);
            Assert.Equal(1, model.ClosedCount);
        });
    }

    // Match a unique diagnostic so unrelated Trace output cannot satisfy the shutdown assertion.
    private sealed class LifecycleFailureTraceListener(string marker) : TraceListener
    {
        public TaskCompletionSource<int> Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Write(string? message) { }

        public override void WriteLine(string? message)
        {
            if (message?.Contains(marker, StringComparison.Ordinal) == true)
            {
                Observed.TrySetResult(Environment.CurrentManagedThreadId);
            }
        }
    }

    private sealed class DialogLifecycleProbe : IContentDialogAware
    {
        public Func<CancellationToken, Task> Opening { get; set; } = _ => Task.CompletedTask;
        public Action<object?>? Closing { get; set; }
        public CancellationToken Token { get; private set; }
        public int OpenedCount { get; private set; }
        public int ClosedCount { get; private set; }
        public object? Result { get; private set; }

        public Task OnDialogOpenedAsync(CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            OpenedCount++;
            return Opening(cancellationToken);
        }

        public void OnDialogClosed(object? result)
        {
            Result = result;
            ClosedCount++;
            Closing?.Invoke(result);
        }
    }

    private sealed class AwareDialog : Dialog, IContentDialogAware
    {
        public DialogLifecycleProbe Probe { get; } = new();
        public Task OnDialogOpenedAsync(CancellationToken cancellationToken) => Probe.OnDialogOpenedAsync(cancellationToken);
        public void OnDialogClosed(object? result) => Probe.OnDialogClosed(result);
    }
}
