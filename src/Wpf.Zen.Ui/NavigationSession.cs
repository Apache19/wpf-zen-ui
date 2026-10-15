// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace Wpf.Zen.Ui;

/// <summary>
/// Coordinates stop, work draining, and dependency disposal for one attachment; later attachments
/// use a new identity.
/// </summary>
internal sealed class NavigationSession
{
    private readonly INavigationHost   _host;
    private readonly NavigationService _navigation;
    private readonly Func<Task>?       _releaseResources;
    private readonly Action?           _unregister;

    private readonly TaskCompletionSource<bool> _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    private readonly ConditionalWeakTable<object, InstanceActivity> _instanceActivities = new();

    private int  _state = (int)NavigationScopeState.Attaching;
    private int  _stopRequested;
    private bool _attachmentStarted;

    internal NavigationSession(
        INavigationHost   host,
        NavigationService navigation,
        Func<Task>?       releaseResources = null,
        Action?           unregister       = null)
    {
        _host             = host;
        _navigation       = navigation;
        _releaseResources = releaseResources;
        _unregister       = unregister;

        host.Dispatcher.ShutdownStarted += OnDispatcherShutdownStarted;
    }

    internal NavigationWorkRegistry Work     { get; } = new();
    internal NavigationFailures     Failures { get; } = new();

    internal NavigationScopeState State
    {
        get => (NavigationScopeState)Volatile.Read(ref _state);
    }

    internal bool IsStopping
    {
        get => Volatile.Read(ref _stopRequested) != 0;
    }

    internal Task Completion
    {
        get => _completion.Task;
    }

    /// <summary>
    /// Reuses instance records by reference so borrowed leases do not introduce duplicate disposal
    /// obligations.
    /// </summary>
    internal NavigationPageInstance RetainPage(
        NavigationPageLease lease, NavigationWorkRegistry.Lease work)
    {
        return NavigationPageOwnership.Retain(this, _host.Dispatcher, lease, work);
    }

    /// <summary>
    /// Serializes activations per instance; weak keys avoid extending the lifetime of departed,
    /// uncached pages.
    /// </summary>
    internal Task RememberActivation(object content, Task completion)
    {
        var previous = _instanceActivities.TryGetValue(content, out var activity)
            ? activity.Completion
            : Task.CompletedTask;

        _instanceActivities.Remove(content);
        _instanceActivities.Add(content, new InstanceActivity(completion));

        return previous;
    }

    private sealed class InstanceActivity(Task completion)
    {
        internal Task Completion { get; } = completion;
    }

    internal void BeginAttachment()
    {
        _attachmentStarted = true;
    }

    /// <summary>
    /// Opens admission after successful attachment unless an attachment callback has already
    /// requested stop.
    /// </summary>
    internal void CompleteAttachment()
    {
        Interlocked.CompareExchange(
            ref _state, (int)NavigationScopeState.Attached, (int)NavigationScopeState.Attaching
        );
    }

    /// <summary>
    /// Atomically closes admission and publishes stable completion before route and host cleanup
    /// that may reenter.
    /// </summary>
    internal void RequestStop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) != 0)
        {
            return;
        }

        Volatile.Write(ref _state, (int)NavigationScopeState.Stopping);

        Failures.Try(_unregister);

        _ = CompleteStopAsync();
    }

    /// <summary>
    /// Keeps UI cleanup and disposal on the host Dispatcher; calls after shutdown fail explicitly.
    /// </summary>
    private async Task CompleteStopAsync()
    {
        try
        {
            var dispatcher = _host.Dispatcher;
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                if (dispatcher.CheckAccess() && _attachmentStarted)
                {
                    // ShutdownStarted still permits synchronous unbinding on the current stack;
                    // asynchronous disposal requires normal shutdown.
                    try
                    {
                        var detachment = _host.DetachNavigationScope(_navigation);
                        if (detachment.IsCompleted)
                        {
                            detachment.GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception exception)
                    {
                        Failures.Add(exception);
                    }

                    _ = Work.SealAndDrain();
                }

                throw new InvalidOperationException(
                    "The navigation Dispatcher has shut down before UI cleanup completed."
                );
            }

            if (dispatcher.CheckAccess())
            {
                await StopOnDispatcherAsync();
            }
            else
            {
                var cleanup = await dispatcher.InvokeAsync(StopOnDispatcherAsync).Task.ConfigureAwait(false);
                await cleanup.ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            Failures.Add(exception);
        }
        finally
        {
            _host.Dispatcher.ShutdownStarted -= OnDispatcherShutdownStarted;

            Volatile.Write(ref _state, (int)NavigationScopeState.Disposed);

            var failure = Failures.Exception;
            if (failure is null)
            {
                _completion.TrySetResult(true);
            }
            else
            {
                _completion.TrySetException(failure);
            }
        }
    }

    /// <summary>
    /// Ends waiting with explicit restricted cleanup failure when normal shutdown is bypassed,
    /// preserving dependencies still in use.
    /// </summary>
    private void OnDispatcherShutdownStarted(object? sender, EventArgs e)
    {
        _host.Dispatcher.ShutdownStarted -= OnDispatcherShutdownStarted;

        if (_completion.Task.IsCompleted)
        {
            return;
        }

        Failures.Add(new InvalidOperationException(
            "The navigation Dispatcher started shutdown before scope cleanup completed; dependent resources remain retained."
        ));

        RequestStop();
        Volatile.Write(ref _state, (int)NavigationScopeState.Disposed);
        _completion.TrySetException(Failures.Exception!);
    }

    /// <summary>
    /// Delivers detachment before closing ordinary registration; admitted work may derive its own
    /// cleanup obligations.
    /// </summary>
    private async Task StopOnDispatcherAsync()
    {
        // Set context explicitly so synchronous entry without a message loop preserves Dispatcher
        // affinity for disposal.
        var previous = SynchronizationContext.Current;

        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(_host.Dispatcher)
        );

        try
        {
            Task detachment;

            try
            {
                detachment = _attachmentStarted
                    ? _host.DetachNavigationScope(_navigation)
                    : Task.CompletedTask;
            }
            catch (Exception exception)
            {
                Failures.Add(exception);
                detachment = Task.CompletedTask;
            }

            Volatile.Write(ref _state, (int)NavigationScopeState.Draining);

            var drained = Work.SealAndDrain();

            await Failures.TryAsync(detachment);
            await drained;

            if (_releaseResources is not null)
            {
                try
                {
                    using var callback = new NavigationCallbackContext(this);
                    await _releaseResources();
                }
                catch (Exception exception)
                {
                    Failures.Add(exception);
                }
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>
    /// Prevents callbacks from awaiting stop completion that includes themselves; requesting stop
    /// remains allowed.
    /// </summary>
    internal void VerifyCanWait()
    {
        NavigationCallbackContext.VerifyCanWait(Completion);
    }
}

/// <summary>
/// Preserves original cleanup failures, leaving one failure unwrapped and aggregating multiple
/// independent failures.
/// </summary>
internal sealed class NavigationFailures
{
    private readonly object          _gate     = new();
    private readonly List<Exception> _failures = [];

    internal Exception? Exception
    {
        get
        {
            lock (_gate)
            {
                return _failures.Count switch
                {
                    0 => null,
                    1 => _failures[0],
                    _ => new AggregateException("Multiple navigation lifecycle steps failed.", _failures.ToArray())
                };
            }
        }
    }

    internal void Add(Exception exception)
    {
        lock (_gate)
        {
            if (!_failures.Contains(exception))
            {
                _failures.Add(exception);
            }
        }
    }

    internal void Try(Action? action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception exception)
        {
            Add(exception);
        }
    }

    internal async Task TryAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            Add(exception);
        }
    }

    internal void ThrowIfAny()
    {
        var failure = Exception;
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

/// <summary>
/// Closes the work set using obligation counts so cleanup derived by completion callbacks is not
/// lost to a Task.WhenAll snapshot.
/// </summary>
internal sealed class NavigationWorkRegistry
{
    private readonly object _gate = new();

    private readonly TaskCompletionSource<bool> _drained = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    private int  _count;
    private bool _sealed;

    internal Lease Acquire()
    {
        lock (_gate)
        {
            if (_sealed)
            {
                throw new InvalidOperationException("Navigation work admission is closed.");
            }

            _count++;
            return new Lease(this);
        }
    }

    internal Task SealAndDrain()
    {
        lock (_gate)
        {
            _sealed = true;
            CompleteIfDrained();
            return _drained.Task;
        }
    }

    private void CompleteIfDrained()
    {
        if (_sealed && _count == 0)
        {
            _drained.TrySetResult(true);
        }
    }

    /// <summary>
    /// An admitted work obligation that may derive error delivery or cleanup while its parent
    /// remains alive.
    /// </summary>
    internal sealed class Lease(NavigationWorkRegistry owner) : IDisposable
    {
        private bool _released;

        internal Lease Fork()
        {
            lock (owner._gate)
            {
                if (_released)
                {
                    throw new ObjectDisposedException(nameof(Lease));
                }

                owner._count++;
                return new Lease(owner);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (owner._gate)
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                owner._count--;
                owner.CompleteIfDrained();
            }
        }
    }
}

/// <summary>
/// Tracks lifecycle callbacks in the asynchronous execution flow to reject callback self-waits
/// explicitly.
/// </summary>
internal sealed class NavigationCallbackContext : IDisposable
{
    private static readonly AsyncLocal<NavigationCallbackContext?> Current = new();

    private readonly NavigationCallbackContext? _previous;
    private readonly NavigationSession          _session;

    private bool _active = true;

    internal NavigationCallbackContext(NavigationSession session)
    {
        _session      = session;
        _previous     = Current.Value;
        Current.Value = this;
    }

    /// <summary>
    /// Identifies obligations by stable completion task, preserving self-wait checks after host
    /// detachment.
    /// </summary>
    internal static void VerifyCanWait(Task completion)
    {
        for (var context = Current.Value; context is not null; context = context._previous)
        {
            if (context._active && ReferenceEquals(context._session.Completion, completion))
            {
                throw new InvalidOperationException(
                    "A navigation callback cannot await its own scope completion. Call RequestStop instead.");
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _active       = false;
        Current.Value = _previous;
    }
}