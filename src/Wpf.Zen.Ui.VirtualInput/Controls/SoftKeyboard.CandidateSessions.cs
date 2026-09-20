// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Owns the replaceable candidate source and rejects work from obsolete source sessions.
/// </summary>
public partial class SoftKeyboard
{
    private readonly SoftKeyboardOperationSlot _candidateRefreshOperation = new();
    private readonly SoftKeyboardOperationSlot _candidateInputOperation   = new();

#pragma warning disable IDISP006 // WPF controls release session-owned resources from their Unloaded lifecycle.
    private ISoftKeyboardCandidateSession? _candidateSession;
    private SoftKeyboardCandidateHost?     _candidateHost;
#pragma warning restore IDISP006

    private TaskCompletionSource<int> _candidateSnapshotChanged = CreateCandidateSnapshotSignal();

    private bool _candidateSessionTransitionActive;
    private int  _candidateSessionVersion;
    private int  _candidateRefreshVersion;

    #region DP: CandidateSource

    /// <summary>
    /// Identifies the <see cref="CandidateSource"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CandidateSourceProperty
        = DependencyProperty.Register(
            nameof(CandidateSource),
            typeof(ISoftKeyboardCandidateSource),
            typeof(SoftKeyboard),
            new PropertyMetadata(TsfSoftKeyboardCandidateSource.Default, OnCandidateSourceChanged),
            static value => value is ISoftKeyboardCandidateSource
        );

    /// <summary>
    /// Gets or sets the source that discovers and operates candidate pages for this keyboard.
    /// </summary>
    /// <remarks>
    /// Replacing the source cancels all work from its previous session. The default uses the
    /// dispatcher-scoped Windows Text Services Framework session.
    /// Null values are rejected by dependency-property validation. Clear the local value to
    /// restore the value supplied by a style or the default source.
    /// </remarks>
    public ISoftKeyboardCandidateSource CandidateSource
    {
        get => (ISoftKeyboardCandidateSource)GetValue(CandidateSourceProperty);
        set => SetValue(CandidateSourceProperty, value);
    }

    #endregion

    #region Read-Only DP: CandidateError

    private static readonly DependencyPropertyKey CandidateErrorPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(CandidateError),
            typeof(Exception),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="CandidateError"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CandidateErrorProperty
        = CandidateErrorPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the most recent candidate-source failure, or null after a successful request.
    /// </summary>
    /// <remarks>
    /// Cancellation and rejected results from expired sessions are not reported as failures.
    /// </remarks>
    public Exception? CandidateError => (Exception?)GetValue(CandidateErrorProperty);

    #endregion

    internal int CandidateSnapshotVersion { get; private set; }

    internal SoftKeyboardCandidateHost? ActiveCandidateHost => _candidateHost;

    internal void SetCandidateError(Exception? error)
    {
        SetValue(CandidateErrorPropertyKey, error);
    }

    /// <summary>
    /// Refreshes candidate discovery for the current source session.
    /// </summary>
    /// <param name="cancellationToken">Cancels the caller's request.</param>
    /// <returns>A task that completes when the active source finishes the refresh.</returns>
    public async Task RefreshCandidatesAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var state = EnsureCandidateSession();
        if (state.Session is null)
        {
            return;
        }

        var isInputCurrent = CaptureInputLifetime();

        using var operation = _candidateRefreshOperation.Start(
            cancellationToken, replace: true, SetCandidateError
        );

        if (operation is null)
        {
            return;
        }

        var context = new SoftKeyboardOperationContext(
            operation,
            () => isInputCurrent() && IsCandidateSessionCurrent(state.Version, state.Session)
        );

        if (!context.IsCurrent)
        {
            return;
        }

        var refreshVersion = ++_candidateRefreshVersion;

        using var refreshScope = _candidateHost!.EnterOperation(context, refreshVersion);

        try
        {
            await state.Session.RefreshAsync(operation.Token).ConfigureAwait(true);

            if (context.IsCurrent)
            {
                SetValue(CandidateErrorPropertyKey, null);
            }
        }
        catch (OperationCanceledException) when (!context.IsCurrent)
        {
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (context.IsCurrent)
            {
                SetValue(CandidateErrorPropertyKey, exception);
                if (context.IsCurrent)
                {
                    ReleaseCandidatePresentation();
                }
            }
        }
    }

    /// <summary>
    /// Commits a candidate from the currently published snapshot.
    /// </summary>
    /// <param name="candidate">The candidate selected by the application or candidate list.</param>
    /// <param name="cancellationToken">Cancels the caller's request.</param>
    /// <returns>True when the active source handled and completed the operation.</returns>
    public Task<bool> CommitCandidateAsync(
        SoftKeyboardCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        VerifyAccess();

        if (candidate is null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        if (cancellationToken.IsCancellationRequested
            || !_candidates.Contains(candidate) || !PrepareCandidateOperation())
        {
            return Task.FromResult(false);
        }

        return RunCandidateOperationAsync(
            (session, token) => session.CommitAsync(candidate, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Moves the active candidate page by one relative step.
    /// </summary>
    /// <param name="delta">
    /// A negative value selects the previous page; a positive value selects the next.
    /// </param>
    /// <param name="cancellationToken">Cancels the caller's request.</param>
    /// <returns>
    /// True when the active source handled and completed the operation.
    /// </returns>
    public Task<bool> MoveCandidatePageAsync(
        int delta,
        CancellationToken cancellationToken = default)
    {
        VerifyAccess();

        if (delta == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        if (cancellationToken.IsCancellationRequested
            || !PrepareCandidateOperation())
        {
            return Task.FromResult(false);
        }

        return RunCandidateOperationAsync(
            (session, token) => session.MovePageAsync(delta, token),
            cancellationToken
        );
    }

    /// <summary>
    /// Starts a refresh whose task handles cancellation and reports source failures to the control.
    /// </summary>
    internal void RequestCandidateRefresh()
    {
        _ = RefreshCandidatesAsync();
    }

    /// <summary>
    /// Asks the active source to return presentation ownership and reports cleanup failure.
    /// </summary>
    internal void ReleaseCandidatePresentation()
    {
        try
        {
            _candidateSession?.ReleasePresentation();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SetValue(CandidateErrorPropertyKey, exception);
        }
    }

    /// <summary>
    /// Revokes refresh publications and candidate input together before cancellation callbacks run.
    /// </summary>
    internal void CancelCandidateOperations()
    {
        _candidateRefreshVersion++;

        SoftKeyboardOperationSlot.CancelTogether(
            _candidateRefreshOperation, _candidateInputOperation, SetCandidateError
        );
    }

    /// <summary>
    /// Injects candidate navigation only while the original operation still owns input.
    /// </summary>
    internal bool SendCandidateNavigationKey(
        Key key, ModifierKeys modifiers, Func<bool> isCurrent)
    {
        return TrySendVirtualKey(key, modifiers, isCurrent);
    }

    /// <summary>
    /// Keeps a selection or page request tied to the snapshot preceding reentrant focus callbacks.
    /// </summary>
    private bool PrepareCandidateOperation()
    {
        var isInputCurrent  = CaptureInputLifetime();
        var sessionVersion  = _candidateSessionVersion;
        var snapshotVersion = CandidateSnapshotVersion;
        var prepared        = CanPresentCandidates && PrepareTargetForInput();

        var current = prepared && isInputCurrent()
            && sessionVersion == _candidateSessionVersion && snapshotVersion == CandidateSnapshotVersion;

        if (prepared && !current)
        {
            Debug.WriteLine(
                "[SoftKeyboard.Operations] Stale REQUEST rejected; kind=Candidate, stage=Prepare."
            );
        }

        return current;
    }

    /// <summary>
    /// Marshals publication to the dispatcher and revalidates queued operation ownership there.
    /// </summary>
    internal void PublishCandidateSnapshot(
        SoftKeyboardCandidateHost host,
        SoftKeyboardCandidateSnapshot snapshot,
        int? refreshVersion,
        SoftKeyboardOperationContext? context)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(
                new Action(() => PublishCandidateSnapshot(host, snapshot, refreshVersion, context))
            );

            return;
        }

        if ((context is not null && !context.CanPublish)
            || !ReferenceEquals(host, _candidateHost)
            || (refreshVersion.HasValue && refreshVersion.Value != _candidateRefreshVersion)
            || !host.CanPresentCandidates)
        {
            return;
        }

        ReplaceCandidates(
            snapshot.Candidates,
            snapshot.PresentationState,
            snapshot.PageNumber,
            snapshot.PageCount,
            snapshot.SelectedCandidateIndex
        );
    }

    /// <summary>
    /// Waits for publication beyond a captured snapshot version; timeout returns false.
    /// Caller cancellation is propagated so a compound candidate operation can stop immediately.
    /// </summary>
    internal async Task<bool> WaitForCandidateSnapshotChangeAsync(
        int version,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (version != CandidateSnapshotVersion)
        {
            return true;
        }

        // Capture the current signal after checking the version on the dispatcher; publication
        // replaces this signal, so a later page update wakes precisely these waiters.
        var signal = _candidateSnapshotChanged.Task;

        using var timeoutCancellation = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCancellation.Token,
            cancellationToken
        );

        var cancellation = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        using var registration = linked.Token.Register(
            () => cancellation.TrySetResult(false)
        );

        var completed = await Task.WhenAny(signal, cancellation.Task).ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();

        return ReferenceEquals(completed, signal) && version != CandidateSnapshotVersion;
    }

    internal IReadOnlyList<ISoftKeyboardTsfCandidateBehavior> GetTsfCandidateBehaviors()
    {
        return
        [
            ..
            ResolveExtensions()
                .Select(static extension => extension.CandidateBehavior)
                .Where(static behavior => behavior is not null)
                .Cast<ISoftKeyboardTsfCandidateBehavior>()
        ];
    }

    /// <summary>
    /// Holds one input operation across provider execution, publication and error callbacks.
    /// Snapshot changes after invocation may acknowledge the operation without expiring its result.
    /// </summary>
    private async Task<bool> RunCandidateOperationAsync(
        Func<ISoftKeyboardCandidateSession, CancellationToken, Task<SoftKeyboardCandidateOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        var snapshotVersion = CandidateSnapshotVersion;
        var isInputCurrent  = CaptureInputLifetime();

        var state = EnsureCandidateSession();
        if (state.Session is null)
        {
            return false;
        }

        using var execution = _candidateInputOperation.Start(
            cancellationToken, replace: false, SetCandidateError
        );

        if (execution is null)
        {
            return false;
        }

        var context = new SoftKeyboardOperationContext(
            execution,
            () => isInputCurrent() && IsCandidateSessionCurrent(state.Version, state.Session),
            () => snapshotVersion == CandidateSnapshotVersion
        );

        if (!context.IsCurrent)
        {
            return false;
        }

        using var operationScope = _candidateHost!.EnterOperation(context);

        SetValue(CandidateErrorPropertyKey, null);

        try
        {
            // Error observers can synchronously replace the source or target.
            if (!context.IsCurrent || snapshotVersion != CandidateSnapshotVersion)
            {
                return false;
            }

            var result = await operation(state.Session, execution.Token).ConfigureAwait(true);

            if (!context.IsCurrent)
            {
                Debug.WriteLine(
                    "[SoftKeyboard.Operations] Stale COMPLETION rejected; kind=Candidate."
                );
                return false;
            }

            if (result is null)
            {
                throw new InvalidOperationException("A candidate session returned a null operation result.");
            }

            if (result.Error is not null)
            {
                SetValue(CandidateErrorPropertyKey, result.Error);
            }

            return context.IsCurrent && result is { Handled: true, Succeeded: true };
        }
        catch (OperationCanceledException) when (!context.IsCurrent)
        {
            return false;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (context.IsCurrent)
            {
                SetValue(CandidateErrorPropertyKey, exception);
                if (context.IsCurrent)
                {
                    ReleaseCandidatePresentation();
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Creates one source session and rejects registration completed after a reentrant source
    /// change.
    /// </summary>
    private (ISoftKeyboardCandidateSession? Session, int Version) EnsureCandidateSession()
    {
        if (_candidateSession is not null)
        {
            return (_candidateSession, _candidateSessionVersion);
        }

        if (_candidateSessionTransitionActive)
        {
            return (null, _candidateSessionVersion);
        }

        _candidateSessionTransitionActive = true;

        var host = new SoftKeyboardCandidateHost(this);
        _candidateHost = host;

        // Source creation may publish or replace the source synchronously. Install the host and
        // generation first, then accept the returned session only if both still belong here.
        var version = ++_candidateSessionVersion;

        try
        {
            var session = CandidateSource.CreateSession(host)
                ?? throw new InvalidOperationException("The candidate source returned a null session.");

            if (version != _candidateSessionVersion || !ReferenceEquals(host, _candidateHost))
            {
                ReleaseCandidateSession(session, host);
                return (null, _candidateSessionVersion);
            }

            _candidateSession = session;
            return (session, version);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (ReferenceEquals(host, _candidateHost))
            {
                _candidateHost = null;
                SetValue(CandidateErrorPropertyKey, exception);
            }

            host.Expire();
            return (null, _candidateSessionVersion);
        }
        finally
        {
            _candidateSessionTransitionActive = false;

            if (version != _candidateSessionVersion && CanPresentCandidates)
            {
                QueueCandidateBridgeRefresh();
            }
        }
    }

    /// <summary>
    /// Expires candidate work and independently releases presentation and session resources.
    /// Both cleanup steps are attempted so a presentation failure cannot retain source
    /// registrations.
    /// </summary>
    private void ResetCandidateSession()
    {
        var version = ++_candidateSessionVersion;
        var session = _candidateSession;
        var host    = _candidateHost;

        var transitionActive = _candidateSessionTransitionActive;

        _candidateSessionTransitionActive = true;

        _candidateSession = null;
        _candidateHost    = null;

        try
        {
            // Registration stays closed until the old native host has finished unregistering.
            CancelCandidateOperations();

            if (version == _candidateSessionVersion)
            {
                ReplaceCandidates([], SoftKeyboardCandidatePresentationState.None);
            }

            if (!transitionActive)
            {
                ReleaseCandidateSession(session, host);
            }
        }
        finally
        {
            _candidateSessionTransitionActive = transitionActive;
            if (!transitionActive && version != _candidateSessionVersion && CanPresentCandidates)
            {
                QueueCandidateBridgeRefresh();
            }
        }
    }

    /// <summary>
    /// Releases presentation and registrations independently while keeping the detached host alive
    /// for cleanup.
    /// </summary>
    private void ReleaseCandidateSession(ISoftKeyboardCandidateSession? session, SoftKeyboardCandidateHost? host)
    {
        Exception? cleanupError = null;

        var version = _candidateSessionVersion;

        try
        {
            session?.ReleasePresentation();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            cleanupError = exception;
        }

        try
        {
#pragma warning disable IDISP007 // This source session was created by the keyboard and detached before cleanup.
            session?.Dispose();
#pragma warning restore IDISP007
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            cleanupError = cleanupError is null
                ? exception
                : new AggregateException("Candidate session cleanup failed.", cleanupError, exception);
        }
        finally
        {
            // Native sessions still need their host to unregister during Dispose. Publications
            // already fail ownership checks because this host was detached before cleanup began.
            host?.Expire();
        }

        if (cleanupError is not null && version == _candidateSessionVersion)
        {
            SetValue(CandidateErrorPropertyKey, cleanupError);
        }
    }

    /// <summary>
    /// Checks both generation and session identity after source callbacks or asynchronous work.
    /// </summary>
    private bool IsCandidateSessionCurrent(int version, ISoftKeyboardCandidateSession session)
    {
        return version == _candidateSessionVersion && ReferenceEquals(session, _candidateSession);
    }

    /// <summary>
    /// Creates a change signal whose waiters resume outside the snapshot publication call stack.
    /// </summary>
    private static TaskCompletionSource<int> CreateCandidateSnapshotSignal()
    {
        return new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Expires the old source and rediscovers candidates when this control can present them.
    /// </summary>
    private static void OnCandidateSourceChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var keyboard = (SoftKeyboard)dependencyObject;

        keyboard.SetValue(CandidateErrorPropertyKey, null);
        keyboard.ResetCandidateSession();

        if (keyboard is { IsLoaded: true, IsOpen: true, IsVisible: true, IsEnabled: true })
        {
            keyboard.RequestCandidateRefresh();
        }
    }
}
