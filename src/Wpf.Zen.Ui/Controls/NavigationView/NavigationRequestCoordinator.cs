// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Threading;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Serializes preparation and Frame commit within a session, queueing callback requests without
/// nested execution.
/// </summary>
internal sealed class NavigationRequestCoordinator(
    NavigationSession             session,
    Dispatcher                    dispatcher,
    Action<NavigationTransaction> begin,
    Action                        cancelFrame)
{
    private readonly Queue<NavigationTransaction> _queued = new();

    private DispatcherOperation? _scheduled;
    private bool                 _stopped;

    internal NavigationTransaction? Current { get; private set; }

    internal NavigationResult Submit(
        NavigationRequest? request           = null,
        NavigationIntent   intent            = NavigationIntent.Navigate,
        NavigationEntry?   entry             = null,
        object?            content           = null,
        Type?              contentType       = null,
        CancellationToken  cancellationToken = default)
    {
        dispatcher.VerifyAccess();

        if (_stopped || session.IsStopping || cancellationToken.IsCancellationRequested)
        {
            return new NavigationResult(NavigationResultStatus.Cancelled);
        }

        var transaction = new NavigationTransaction(
            session, request, intent, entry, content, contentType, cancellationToken, OnCompleted
        );

        _queued.Enqueue(transaction);

        transaction.RegisterCancellation(
            () =>
            {
                _ = dispatcher.InvokeAsync(() => Cancel(transaction), DispatcherPriority.Send);
            }
        );

        if (Current is null && _scheduled is null)
        {
            StartNext();
        }

        return transaction.Completion.IsCompleted
            ? transaction.Completion.GetAwaiter().GetResult()
            : NavigationResult.Pending(transaction.Completion);
    }

    /// <summary>
    /// Stop immediately cancels uncommitted requests; committed requests retain their actual commit
    /// status.
    /// </summary>
    internal void Stop()
    {
        _stopped = true;

        var scheduled = _scheduled;

        _scheduled = null;

        scheduled?.Abort();

        while (_queued.Count > 0)
        {
            var queued = _queued.Dequeue();
            queued.Finish(new NavigationResult(NavigationResultStatus.Cancelled));
        }

        var current = Current;
        if (current is not null)
        {
            current.Finish(current.IsCommitted
                ? NavigationResult.Success
                : new NavigationResult(NavigationResultStatus.Cancelled));
        }
    }

    private void StartNext()
    {
        _scheduled = null;

        if (_stopped || session.IsStopping)
        {
            Stop();
            return;
        }

        if (Current is not null || _queued.Count == 0)
        {
            return;
        }

        var transaction = _queued.Dequeue();
        if (transaction.Completion.IsCompleted)
        {
            StartNext();
            return;
        }

        Current = transaction;

        transaction.Execute(
            () =>
            {
                if (transaction.CancellationToken.IsCancellationRequested)
                {
                    transaction.Finish(new NavigationResult(NavigationResultStatus.Cancelled));
                }
                else
                {
                    begin(transaction);
                }
            }
        );
    }

    private void OnCompleted(NavigationTransaction transaction)
    {
        if (ReferenceEquals(Current, transaction))
        {
            Current = null;
        }

        if (!_stopped && !session.IsStopping && _queued.Count > 0 && _scheduled is null)
        {
            _scheduled = dispatcher.InvokeAsync(StartNext, DispatcherPriority.Normal);

            _scheduled.Aborted += (_, _) => Stop();
        }
    }

    private void Cancel(NavigationTransaction transaction)
    {
        if (transaction.IsCommitted || transaction.Completion.IsCompleted)
        {
            return;
        }

        // StopLoading synchronously invokes external NavigationStopped callbacks. Include the
        // entire call in request execution;
        // Finish inside the event only records a result. Wait for notification to return before
        // deciding cancellation or failure.
        transaction.Execute(
            () =>
            {
                if (ReferenceEquals(Current, transaction))
                {
                    cancelFrame();
                }

                transaction.Finish(new NavigationResult(NavigationResultStatus.Cancelled));
            }
        );
    }
}

/// <summary>
/// The request's history intent; every intent uses the same preparation, Frame commit, and
/// activation entry points.
/// </summary>
internal enum NavigationIntent
{
    Navigate,
    Back,
    Forward,
    Replace,
    ClearHistory
}

/// <summary>
/// Owns request identity and completion; retains the session work lease until external callbacks
/// return.
/// </summary>
internal sealed class NavigationTransaction
{
    private readonly NavigationWorkRegistry.Lease  _work;
    private readonly Action<NavigationTransaction> _completed;

    private readonly TaskCompletionSource<NavigationResult> _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    private NavigationResult?             _result;
    private int                           _executionDepth;
    private CancellationTokenRegistration _cancellationRegistration;

    internal NavigationTransaction(
        NavigationSession             session,
        NavigationRequest?            request,
        NavigationIntent              intent,
        NavigationEntry?              entry,
        object?                       content,
        Type?                         contentType,
        CancellationToken             cancellationToken,
        Action<NavigationTransaction> completed)
    {
        Session           = session;
        Request           = request;
        Intent            = intent;
        Entry             = entry;
        Content           = content;
        ContentType       = contentType;
        CancellationToken = cancellationToken;

        _completed = completed;
        _work      = session.Work.Acquire();
    }

    internal NavigationSession                           Session            { get; }
    internal NavigationRequest?                          Request            { get; set; }
    internal NavigationIntent                            Intent             { get; }
    internal NavigationEntry?                            Entry              { get; set; }
    internal object?                                     Content            { get; set; }
    internal NavigationPageInstance?                     Instance           { get; set; }
    internal Type?                                       ContentType        { get; }
    internal CancellationToken                           CancellationToken  { get; }
    internal NavigationJournalEntry?                     Occurrence         { get; set; }
    internal NavigationView.NavigationTransitionSettings TransitionSettings { get; set; }
    internal bool                                        IsCommitted        { get; set; }
    internal Task<NavigationResult>                      Completion         => _completion.Task;

    internal NavigationWorkRegistry.Lease AcquireCleanupWork()
    {
        return _work.Fork();
    }

    internal void RegisterCancellation(Action cancel)
    {
        _cancellationRegistration = CancellationToken.Register(cancel);
    }

    internal void Execute(Action action)
    {
        _executionDepth++;

        try
        {
            action();
        }
        catch (Exception exception)
        {
            Finish(NavigationResult.FromFailure(exception, IsCommitted));
        }
        finally
        {
            _executionDepth--;
            CompleteIfReady();
        }
    }

    internal void Finish(NavigationResult result)
    {
        if (_completion.Task.IsCompleted)
        {
            return;
        }

        if (_result is null || result.Status == NavigationResultStatus.Failed)
        {
            _result = result;
        }

        CompleteIfReady();
    }

    private void CompleteIfReady()
    {
        if (_executionDepth != 0 || _result is not { } result || _completion.Task.IsCompleted)
        {
            return;
        }

        _completion.TrySetResult(result);
        _cancellationRegistration.Dispose();
        _completed(this);

        Instance?.Release();

        _work.Dispose();
    }
}

/// <summary>
/// A committed semantic history record storing request and breadcrumb snapshots without retaining
/// page instances.
/// </summary>
internal sealed class NavigationJournalEntry(NavigationEntry entry, NavigationRequest request)
{
    internal NavigationEntry                  Entry              { get; }      = entry;
    internal NavigationRequest                Request            { get; }      = request;
    internal IReadOnlyList<BreadcrumbSegment> BreadcrumbSnapshot { get; set; } = [];
}