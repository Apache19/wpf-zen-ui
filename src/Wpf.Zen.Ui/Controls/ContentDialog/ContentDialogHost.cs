// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Wraps ordinary Content and owns one modal dialog session above it.</summary>
/// <remarks>
/// ShowAsync rejects a busy, hidden or unloaded host and an already parented dialog. Its task
/// completes only after transitions, logical detachment, input release and Closed notification.
/// Token cancellation cancels the task; host removal returns null unless a result already won.
/// Templates must present Content in PART_ContentRoot and CurrentDialog in a separate overlay.
/// PART_ContentRoot (UIElement) is required for isolating the covered region; missing it rejects
/// presentation. The host disables branches below that part while preserving any TitleBar shell.
/// While a dialog is active, keyboard focus is confined to the dialog and any presentation surfaces it owns;
/// attempts to focus another element in the same WPF presentation root are cancelled. Focus is remembered before
/// isolation and restored during synchronous cleanup when the remembered element can still receive focus and the
/// user has not moved focus to another window.
/// Templates may provide PART_DialogPresenter as a <see cref="ContentDialogTransitionPresenter"/>.
/// That part owns presentation animation and is awaited by the host; templates without it retain the
/// complete modal lifecycle and transition immediately. Keep overlay hit testing active until
/// CurrentDialog becomes null, including the exit transition.
/// </remarks>
[TemplatePart(Name = "PART_ContentRoot", Type = typeof(UIElement))]
[TemplatePart(Name = "PART_DialogPresenter", Type = typeof(ContentDialogTransitionPresenter))]
public partial class ContentDialogHost : ContentControl
{
    private UIElement?                        _contentRoot;
    private ContentDialogTransitionPresenter? _dialogPresenter;
    private Session?                         _session;

    private sealed class Session(ContentDialog dialog, CancellationToken cancellationToken)
    {
        internal ContentDialog     Dialog            { get; } = dialog;
        internal CancellationToken CancellationToken { get; } = cancellationToken;

        // Outcome arbitrates the first close/cancel request; Finished is the public task and is
        // completed only by synchronous cleanup. Dispatcher shutdown cannot strand its continuation.
        internal TaskCompletionSource<object?> Outcome { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<object?> Finished { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal CancellationTokenRegistration Registration { get; set; }

        internal bool IsCleanedUp       { get; set; }
        internal bool IsRequestingClose { get; set; }
    }

    #region DP: CurrentDialog

    private static readonly DependencyPropertyKey CurrentDialogPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CurrentDialog),
            typeof(ContentDialog),
            typeof(ContentDialogHost),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="CurrentDialog"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CurrentDialogProperty =
        CurrentDialogPropertyKey.DependencyProperty;

    /// <summary>Gets the currently presented dialog, including transitions, or null when idle.</summary>
    public ContentDialog? CurrentDialog
    {
        get => (ContentDialog?)GetValue(CurrentDialogProperty);
    }

    #endregion

    #region DP: IsAnimationEnabled

    /// <summary>Identifies the <see cref="IsAnimationEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsAnimationEnabledProperty
        = DependencyProperty.Register(
            nameof(IsAnimationEnabled),
            typeof(bool),
            typeof(ContentDialogHost),
            new PropertyMetadata(true, OnIsAnimationEnabledChanged)
        );

    /// <summary>
    /// Gets or sets a value indicating whether host transitions are enabled.
    /// Default true; system client-area animation settings are also honored.
    /// Turning it off finishes an active transition immediately.
    /// </summary>
    public bool IsAnimationEnabled
    {
        get => (bool)GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    #endregion

    static ContentDialogHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ContentDialogHost),
            new FrameworkPropertyMetadata(typeof(ContentDialogHost))
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialogHost"/> class.
    /// The host is initially idle; Content remains the ordinary page throughout each session.
    /// </summary>
    public ContentDialogHost()
    {
        Unloaded += (_, _) => ForceEnd();

        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                ForceEnd();
            }
        };
    }

    /// <summary>
    /// Shows a dialog and returns its business result, or null for user dismissal/removal.
    /// </summary>
    /// <param name="dialog">
    /// An unparented dialog on the host dispatcher.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the task after cleanup, bypassing Closing and IsDismissible.
    /// </param>
    /// <returns>
    /// The result after all presentation resources have been released.
    /// </returns>
    public Task<object?> ShowAsync(ContentDialog dialog, CancellationToken cancellationToken = default)
    {
        // Validation failures are represented by the returned task, just like presentation failures.
        try
        {
            VerifyAccess();

            if (dialog is null)
            {
                throw new ArgumentNullException(nameof(dialog));
            }

            dialog.VerifyAccess();

            if (_session is not null || dialog.IsOpen)
            {
                throw new InvalidOperationException(
                    "The host or dialog is already in use. Await the current presentation before reuse."
                );
            }

            if (!IsLoaded || !IsVisible)
            {
                throw new InvalidOperationException("ContentDialogHost must be loaded and visible.");
            }

            if (dialog.Parent is not null || VisualTreeHelper.GetParent(dialog) is not null)
            {
                throw new InvalidOperationException("The dialog must not already have a parent.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            ApplyTemplate();

            if (_contentRoot is not { } contentRoot)
            {
                throw new InvalidOperationException("The host template must provide PART_ContentRoot.");
            }

            var session = _session = new Session(dialog, cancellationToken);

            // HwndSource/interop callers can be on the correct UI thread without a WPF
            // SynchronizationContext. Capture Dispatcher explicitly for our awaits, then restore
            // the caller's context; presentation cleanup must never resume on the thread pool.
            var previousContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(Dispatcher)
                );

                _ = PresentAsync(session, contentRoot);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }

            return session.Finished.Task;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<object?>(cancellationToken);
        }
        catch (Exception exception)
        {
            return Task.FromException<object?>(exception);
        }
    }

    private async Task PresentAsync(Session session, UIElement contentRoot)
    {
        var        dialog  = session.Dialog;
        Exception? failure = null;

        try
        {
            Dispatcher.ShutdownStarted += OnDispatcherShutdown;

            // The worker callback only arbitrates the outcome and schedules UI work. It never
            // waits for Dispatcher, so disposing the registration during cleanup cannot deadlock.
            session.Registration = session.CancellationToken.Register(() =>
                {
                    if (session.Outcome.TrySetCanceled(session.CancellationToken)
                        && !Dispatcher.HasShutdownStarted)
                    {
                        _ = Dispatcher.BeginInvoke(
                            new Action(() =>
                                {
                                    if (IsActive(session))
                                    {
                                        AbortTransition();
                                    }
                                }
                            )
                        );
                    }
                }
            );

            dialog.Attach(this);

            if (!IsActive(session))
            {
                return;
            }

            StartModalInteraction(dialog, contentRoot);
            AddLogicalChild(dialog);

            if (!IsActive(session))
            {
                return;
            }

            SetValue(CurrentDialogPropertyKey, dialog);

            if (!IsActive(session))
            {
                return;
            }

            dialog.Unloaded += OnDialogUnloaded;

            // Let data templates and Loaded finish before focus and entrance. Loaded priority is
            // deliberately used instead of an idle priority: WPF services it after layout/render
            // but before input, so a stream of mouse or keyboard messages cannot starve the first
            // presentation. Missing presenters release the session instead of leaving an invisible
            // modal operation pending.
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            // Awaiting a DispatcherOperation installs a synchronization context carrying that
            // operation's priority. Do not let Loaded leak into the remaining lifecycle; subsequent
            // awaits are lifecycle work rather than part of WPF's layout/Loaded pass and resume at
            // the dispatcher's normal priority.
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher)
            );

            if (!IsActive(session))
            {
                return;
            }

            if (!dialog.IsLoaded || !dialog.IsVisible)
            {
                session.Outcome.TrySetResult(null);
            }

            if (!session.Outcome.Task.IsCompleted)
            {
                dialog.SetInitialFocus();

                if (!IsActive(session))
                {
                    return;
                }

                if (!session.Outcome.Task.IsCompleted)
                {
                    await OpenDialogAsync().ConfigureAwait(true);
                }

                if (!IsActive(session))
                {
                    return;
                }

                if (!session.Outcome.Task.IsCompleted)
                {
                    dialog.NotifyOpened();
                }
            }

            await session.Outcome.Task.ConfigureAwait(true);

            if (!IsActive(session))
            {
                return;
            }

            dialog.MarkClosing();

            await CloseDialogAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (session.Outcome.Task.IsCanceled)
        {
            // Cleanup below completes the public task with the original cancellation token.
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            CompleteSession(session, failure);
        }
    }

    private bool IsActive(Session session)
    {
        return ReferenceEquals(_session, session) && !session.IsCleanedUp;
    }

    /// <summary>
    /// Performs all WPF cleanup synchronously. Unload and Dispatcher shutdown use this same path
    /// instead of relying on an async continuation that may never run after the UI thread exits.
    /// </summary>
    private void CompleteSession(Session session, Exception? failure = null)
    {
        if (!IsActive(session))
        {
            return;
        }

        session.IsCleanedUp = true;

        var dialog = session.Dialog;
        var result = session.Outcome.Task.Status == TaskStatus.RanToCompletion ? session.Outcome.Task.Result : null;

        try
        {
            dialog.MarkClosing();

            AbortTransition();

            Dispatcher.ShutdownStarted -= OnDispatcherShutdown;

#pragma warning disable IDISP007 // The host creates and owns this session's cancellation registration.
            session.Registration.Dispose();
#pragma warning restore IDISP007

            dialog.Unloaded -= OnDialogUnloaded;

            try
            {
                SetValue(CurrentDialogPropertyKey, null);

                RemoveLogicalChild(dialog);
            }
            finally
            {
                // Keep the reservation through Closed, but release input before restoring focus.
                // Nested finally blocks also release the session if an application focus handler throws.
                try
                {
                    StopModalInteraction(dialog);
                }
                finally
                {
                    dialog.NotifyClosed(result);
                }
            }
        }
        catch (Exception exception)
        {
            failure ??= exception;
        }
        finally
        {
            _session = null;

            if (failure is not null)
            {
                session.Finished.TrySetException(failure);
            }
            else if (session.Outcome.Task.IsCanceled)
            {
                session.Finished.TrySetCanceled(session.CancellationToken);
            }
            else
            {
                session.Finished.TrySetResult(result);
            }
        }
    }

    private void OnDispatcherShutdown(object? sender, EventArgs e)
    {
        ForceEnd();
    }

    internal void RequestClose(ContentDialog dialog, object? result)
    {
        var session = _session;

        if (session is null
            || session.Dialog != dialog
            || session.Outcome.Task.IsCompleted
            || session.IsRequestingClose)
        {
            return;
        }

        session.IsRequestingClose = true;

        try
        {
            if (dialog.AcceptClose(result)
                && ReferenceEquals(_session, session)
                && session.Outcome.TrySetResult(result))
            {
                dialog.MarkClosing();
                RedirectTransitionToClosed();
            }
        }
        finally
        {
            session.IsRequestingClose = false;
        }
    }

    private void ForceEnd()
    {
        if (_session is { } session)
        {
            session.Outcome.TrySetResult(null);
            CompleteSession(session);
        }
    }

    private void OnDialogUnloaded(object sender, RoutedEventArgs e)
    {
        var session = _session;

        // Template replacement may transiently detach the dialog. Loaded priority provides a
        // post-layout check without allowing continuous input to postpone mandatory cleanup.
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
                {
                    if (session is not null
                        && ReferenceEquals(_session, session)
                        && !session.Dialog.IsLoaded)
                    {
                        ForceEnd();
                    }
                }
            )
        );
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        // The outgoing presenter may own a controllable clock whose completion is awaited by the
        // session worker. Reset it before WPF detaches the old template so no animation storage or
        // continuation can remain tied to an unreachable visual tree.
        AbortTransition();
        _dialogPresenter = null;

        base.OnApplyTemplate();

        SetContentRoot(GetTemplateChild("PART_ContentRoot") as UIElement);
        _dialogPresenter = GetTemplateChild("PART_DialogPresenter") as ContentDialogTransitionPresenter;

        // Applying a template must establish a stable pose without a transition. Template
        // initialization must not count as an entrance; the presentation pipeline is the sole place
        // that starts one, so a cold-start dialog cannot replay its animation.
        if (_session is null || _session.Dialog.IsClosing)
        {
            _dialogPresenter?.Reset();
        }
        else
        {
            _dialogPresenter?.SnapTo(isOpen: true);
        }

        if (_session is not null && GetTemplateChild("PART_ContentRoot") is not UIElement)
        {
            ForceEnd();
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        AbortTransition();
        _dialogPresenter = null;
        SetContentRoot(null);

        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <summary>Gets the logical children, including the presented dialog alongside ordinary Content.</summary>
    protected override IEnumerator LogicalChildren
    {
        get
        {
            var children  = new List<object>();
            var inherited = base.LogicalChildren;

            if (inherited is not null)
            {
                while (inherited.MoveNext())
                {
                    children.Add(inherited.Current);
                }
            }

            if (CurrentDialog is { } dialog)
            {
                children.Add(dialog);
            }

            return ((IEnumerable)children).GetEnumerator();
        }
    }
}
