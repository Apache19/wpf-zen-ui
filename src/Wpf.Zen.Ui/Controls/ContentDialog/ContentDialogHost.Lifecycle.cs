// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Coordinates view and view-model work for each dialog presentation.
/// </summary>
public partial class ContentDialogHost
{
    /// <summary>
    /// Captures participants while inherited DataContext and the loaded content are available.
    /// The snapshot pairs closure with the actual recipients of opening, even after detachment.
    /// </summary>
    private void StartDialogLifecycle(Session session)
    {
        var dialog  = session.Dialog;
        var targets = new List<IContentDialogAware>();

        AddTarget(dialog);
        AddTarget(dialog.DataContext);
        AddTarget(dialog.Content);

        if (dialog.Content is FrameworkElement content)
        {
            AddTarget(content.DataContext);
        }

        foreach (var target in targets)
        {
            // User callbacks can synchronously close, hide the host or pump Dispatcher. Never
            // deliver an opening callback after cleanup, nor append recipients after closure.
            if (!IsActive(session) || session.Outcome.Task.IsCompleted)
            {
                break;
            }

            session.LifecycleTargets.Add(target);
            _ = ObserveDialogOpeningAsync(session, target);
        }

        return;

        void AddTarget(object? candidate)
        {
            if (candidate is IContentDialogAware target
                && !targets.Any(existing => ReferenceEquals(existing, target)))
            {
                targets.Add(target);
            }
        }
    }

    /// <summary>
    /// Observes application work independently of the UI worker. Observation must survive
    /// dispatcher shutdown, and a late failure must never complete another presentation's task.
    /// </summary>
    private static async Task ObserveDialogOpeningAsync(Session session, IContentDialogAware target)
    {
        CancellationToken token = session.LifecycleCancellation.Token;

        try
        {
            Task task = target.OnDialogOpenedAsync(token)
                ?? throw new InvalidOperationException(
                    $"{target.GetType().FullName}.{nameof(IContentDialogAware.OnDialogOpenedAsync)} returned null."
                );

            // The callback has already started on Dispatcher; its own awaits choose their own
            // context. Only our task observation opts out, so it can process a completion even
            // after Dispatcher shutdown. Keep all continuations and catch blocks below free of
            // WPF access: they may run on a worker thread. UI cleanup belongs to PresentAsync.
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Closing cancels initialization without cancelling the accepted business result.
        }
        catch (Exception exception)
        {
            // Outcome is thread-safe and belongs to this occurrence only. PresentAsync observes
            // the failure on Dispatcher and performs the same cleanup as presentation failures.
            if (!session.Outcome.TrySetException(exception))
            {
                Trace.TraceError("ContentDialog opening callback failed after closure: {0}", exception);
            }
        }
    }

    /// <summary>
    /// Cancels before exit animation or forced cleanup. User cancellation registrations may
    /// throw or reenter; recording failure lets mandatory modal cleanup continue in either case.
    /// </summary>
    private static void CancelDialogLifecycle(Session session)
    {
        if (session.IsLifecycleCancelled)
        {
            return;
        }

        session.IsLifecycleCancelled = true;
        try
        {
            session.LifecycleCancellation.Cancel();
        }
        catch (Exception exception)
        {
            session.LifecycleFailure ??= exception;
        }
    }

    /// <summary>Notifies every entered participant, isolating failures until cleanup is complete.</summary>
    private static void EndDialogLifecycle(Session session, object? result)
    {
        foreach (var target in session.LifecycleTargets)
        {
            try
            {
                target.OnDialogClosed(result);
            }
            catch (Exception exception)
            {
                session.LifecycleFailure ??= exception;
            }
        }

        session.LifecycleTargets.Clear();
    }
}
