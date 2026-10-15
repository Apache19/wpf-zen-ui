// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Threading;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Independently owns an activation's participant snapshot, cancellation source, and work; cached
/// pages never reuse it.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "Stop seals activation; ReleaseWork disposes the CTS only after every user completes.")]
internal sealed class NavigationActivation
{
    private readonly NavigationSession                   _session;
    private readonly Dispatcher                          _dispatcher;
    private readonly Action<NavigationCallbackException> _reportFailure;
    private readonly NavigationWorkRegistry.Lease        _sessionLease;
    private readonly NavigationPageInstance              _instance;
    private readonly CancellationTokenSource             _cancellation     = new();
    private readonly List<INavigationExited>             _entered          = [];
    private readonly List<Exception>                     _reportedFailures = [];

    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _pending = 1;
    private int _dispatchCompleted;
    private int _stopping;
    private int _completed;

    internal NavigationActivation(
        NavigationSession                   session,
        Dispatcher                          dispatcher,
        NavigationPageInstance              instance,
        NavigationTarget?                   target,
        Action<NavigationCallbackException> reportFailure)
    {
        _session       = session;
        _dispatcher    = dispatcher;
        _reportFailure = reportFailure;
        _sessionLease  = session.Work.Acquire();
        _instance      = instance;

        instance.Retain();

        Content = instance.Page;
        Target  = target;
        Token   = _cancellation.Token;
    }

    internal object            Content    { get; }
    internal NavigationTarget? Target     { get; }
    internal CancellationToken Token      { get; }
    internal bool              IsStopping => Volatile.Read(ref _stopping) != 0 || _session.IsStopping;
    internal Task              Completion => _completion.Task;

    /// <summary>
    /// Waits for the same instance's previous activation to drain before Dispatcher entry;
    /// different instances remain independent.
    /// </summary>
    internal async Task EnterAsync(Task predecessor)
    {
        try
        {
            await predecessor;

            if (IsStopping)
            {
                return;
            }

            var participants = GetParticipants(Content);

            foreach (var participant in participants)
            {
                if (IsStopping)
                {
                    break;
                }

                // Register exit before invoking entry; a partially completed entry that throws
                // still requires exit.
                _entered.Add(participant);

                if (participant is INavigationAware synchronous)
                {
                    using var callback = new NavigationCallbackContext(_session);
                    try
                    {
                        synchronous.OnNavigationEntered();
                    }
                    catch (Exception exception)
                    {
                        await ReportFailureAsync(participant, NavigationCallbackStage.Entered, exception);
                    }
                }

                if (IsStopping)
                {
                    break;
                }

                if (participant is INavigationActivationAware contextual)
                {
                    var context = new NavigationActivationContext(this, participant);
                    StartEntry(participant, () => contextual.OnNavigationEnteredAsync(context));
                }
                else if (participant is IAsyncNavigationAware asynchronous)
                {
                    StartEntry(participant, () => asynchronous.OnNavigationEnteredAsync(Token));
                }
            }
        }
        catch (Exception exception)
        {
            await ReportFailureAsync(Content, NavigationCallbackStage.Entered, exception);
        }
        finally
        {
            CompleteDispatch();
        }
    }

    /// <summary>
    /// Relinquishes pending entry obligations when Loaded dispatch is aborted so the token and
    /// session can complete.
    /// </summary>
    internal void CompleteDispatch()
    {
        if (Interlocked.Exchange(ref _dispatchCompleted, 1) == 0)
        {
            ReleaseWork();
        }
    }

    /// <summary>
    /// Marks stopping and acquires all cleanup obligations before cancellation and reverse-order
    /// exit; reentrancy cannot duplicate delivery.
    /// </summary>
    internal void Stop()
    {
        _dispatcher.VerifyAccess();

        Interlocked.Increment(ref _pending);

        if (Interlocked.Exchange(ref _stopping, 1) != 0)
        {
            ReleaseWork();
            return;
        }

        var participants = _entered.ToArray();

        _entered.Clear();

        try
        {
            using var callback = new NavigationCallbackContext(_session);

            try
            {
                _cancellation.Cancel();
            }
            catch (Exception exception)
            {
                StartFailureDelivery(Content, NavigationCallbackStage.Exited, exception);
            }

            for (int index = participants.Length - 1; index >= 0; index--)
            {
                var participant = participants[index];
                try
                {
                    participant.OnNavigationExited();
                }
                catch (Exception exception)
                {
                    StartFailureDelivery(participant, NavigationCallbackStage.Exited, exception);
                }
            }
        }
        finally
        {
            ReleaseWork();
        }
    }

    /// <summary>
    /// Registers command work before execution; the returned task preserves original errors and
    /// internal observation includes error delivery.
    /// </summary>
    internal Task RunOperationAsync(object participant, Func<CancellationToken, Task> operation)
    {
        _dispatcher.VerifyAccess();

        if (IsStopping)
        {
            throw new ObjectDisposedException(nameof(NavigationActivationContext));
        }

        Interlocked.Increment(ref _pending);

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        StartOperation(participant, () => operation(Token), completion);

        return completion.Task;
    }

    private void StartEntry(object participant, Func<Task> entry)
    {
        Interlocked.Increment(ref _pending);
        StartOperation(participant, entry, null);
    }

    /// <summary>
    /// Direct UI calls may lack an ambient context; establish Dispatcher continuations for the
    /// entire asynchronous callback.
    /// </summary>
    private void StartOperation(object participant, Func<Task> operation, TaskCompletionSource<bool>? result)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;

        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(_dispatcher)
        );

        try
        {
            _ = InvokeOperationAsync(participant, operation, result);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private async Task InvokeOperationAsync(
        object                      participant,
        Func<Task>                  operation,
        TaskCompletionSource<bool>? result)
    {
        using var callback = new NavigationCallbackContext(_session);

        try
        {
            Task task = operation() ?? throw new InvalidOperationException("Navigation work returned null.");

            await task;

            result?.TrySetResult(true);
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested)
        {
            result?.TrySetCanceled();
        }
        catch (Exception exception)
        {
            var stage = result is null ? NavigationCallbackStage.Entered : NavigationCallbackStage.Operation;

            await ReportFailureAsync(participant, stage, exception);

            result?.TrySetException(exception);
        }
        finally
        {
            ReleaseWork();
        }
    }

    private void StartFailureDelivery(object participant, NavigationCallbackStage stage, Exception exception)
    {
        Interlocked.Increment(ref _pending);
        _ = CompleteFailureDeliveryAsync(participant, stage, exception);
    }

    private async Task CompleteFailureDeliveryAsync(
        object participant, NavigationCallbackStage stage, Exception exception)
    {
        try
        {
            await ReportFailureAsync(participant, stage, exception);
        }
        finally
        {
            ReleaseWork();
        }
    }

    /// <summary>
    /// Releases work only after error notification finishes on the owning Dispatcher; handler
    /// failures do not recurse.
    /// </summary>
    private async Task ReportFailureAsync(object participant, NavigationCallbackStage stage, Exception exception)
    {
        lock (_reportedFailures)
        {
            foreach (Exception reported in _reportedFailures)
            {
                if (ReferenceEquals(reported, exception))
                {
                    return;
                }
            }

            _reportedFailures.Add(exception);
        }

        var failure = new NavigationCallbackException(stage, Content, participant, Target, exception);

        try
        {
            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                throw new InvalidOperationException("Navigation error delivery requires a live Dispatcher.");
            }

            if (_dispatcher.CheckAccess())
            {
                DeliverFailure(failure);
            }
            else
            {
                await _dispatcher.InvokeAsync(() => DeliverFailure(failure));
            }
        }
        catch (Exception notificationFailure)
        {
            _session.Failures.Add(failure);
            _session.Failures.Add(notificationFailure);
        }
    }

    private void DeliverFailure(NavigationCallbackException failure)
    {
        using var callback = new NavigationCallbackContext(_session);
        _reportFailure(failure);
    }

    private void ReleaseWork()
    {
        int remaining = Interlocked.Decrement(ref _pending);

        if (remaining == 0 && Volatile.Read(ref _stopping) != 0
            && Interlocked.Exchange(ref _completed, 1) == 0)
        {
            _cancellation.Dispose();
            _completion.TrySetResult(true);
            _instance.Release();
            _sessionLease.Dispose();
        }
    }

    /// <summary>
    /// Snapshots view and model identities at entry and deduplicates by reference; an exit-only
    /// interface does not establish entry.
    /// </summary>
    private static IReadOnlyList<INavigationExited> GetParticipants(object content)
    {
        var result = new List<INavigationExited>(2);

        AddParticipant(result, content);

        if (content is FrameworkElement element)
        {
            AddParticipant(result, element.DataContext);
        }

        if (content is INavigableView<object> navigable)
        {
            AddParticipant(result, navigable.ViewModel);
        }

        return result;
    }

    private static void AddParticipant(List<INavigationExited> participants, object? candidate)
    {
        if (candidate is not INavigationExited participant
            || candidate is not (INavigationAware or IAsyncNavigationAware or INavigationActivationAware))
        {
            return;
        }

        if (!participants.Any(existing => ReferenceEquals(existing, participant)))
        {
            participants.Add(participant);
        }
    }
}