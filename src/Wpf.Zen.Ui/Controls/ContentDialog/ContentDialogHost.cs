// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays a modal <see cref="ContentDialog"/> over its content without creating a separate window.
/// </summary>
/// <remarks>
/// <para>
/// Wrap the page or region to cover in <see cref="ContentControl.Content"/> and call
/// <see cref="ShowAsync"/> to display a dialog and await its result. Each host displays one dialog
/// at a time and supports cancellation and configurable opening and closing animations.
/// </para>
/// <para>
/// While a dialog is open, the host disables interaction with the covered content while preserving
/// title bar window controls. Within this host, keyboard focus is restricted to the dialog and its
/// owned surfaces; other hosts and independent regions remain available. The host attempts to
/// restore the previous focus when the dialog closes. Hiding or unloading the host
/// ends the active dialog.
/// </para>
/// </remarks>
[TemplatePart(Name = "PART_ContentRoot", Type = typeof(UIElement))]
[TemplatePart(Name = "PART_DialogPresenter", Type = typeof(ContentDialogTransitionPresenter))]
public partial class ContentDialogHost : ContentControl
{
    private UIElement?                        _contentRoot;
    private ContentDialogTransitionPresenter? _dialogPresenter;
    private Session?                          _session;
    private bool                              _isPreparingPresentation;

    private sealed class Session(ContentDialog dialog, CancellationToken cancellationToken)
    {
        internal ContentDialog     Dialog            { get; } = dialog;
        internal CancellationToken CancellationToken { get; } = cancellationToken;

        // Outcome arbitrates the first close/cancel request; Finished is the public task and is
        // completed only by synchronous cleanup. Dispatcher shutdown cannot strand its continuation.
        internal TaskCompletionSource<object?> Outcome { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        internal TaskCompletionSource<object?> Finished { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        internal CancellationTokenRegistration Registration { get; set; }

        // Each presentation owns its work token and the recipients actually entered. Neither
        // DataContext replacement nor reuse of the dialog may redirect an earlier lifecycle.
        internal CancellationTokenSource   LifecycleCancellation { get; } = new();
        internal List<IContentDialogAware> LifecycleTargets      { get; } = [];
        internal bool                      IsLifecycleCancelled  { get; set; }
        internal Exception?                LifecycleFailure      { get; set; }

        // Synchronous shutdown cannot await Outcome. Cache the accepted business result so the
        // same cleanup path can preserve it when unloading wins the race with the async worker.
        internal object? OutcomeResult { get; set; }

        internal bool IsCleanedUp                { get; set; }
        internal bool IsRequestingClose          { get; set; }
        internal bool IsAvailabilityCheckPending { get; set; }

        // These belong to the occurrence, not the host: a callback may pump Dispatcher after
        // cleanup and start another presentation before the old isolation traversal unwinds.
        internal Dictionary<DependencyObject, ContentDialogInputIsolation> InputIsolation { get; } = [];
        internal bool                 IsUpdatingIsolation       { get; set; }
        internal bool                 NeedsIsolationRefresh     { get; set; }
        internal DispatcherOperation? IsolationRefreshOperation { get; set; }
    }

    #region DP: CurrentDialog

    private static readonly DependencyPropertyKey CurrentDialogPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(CurrentDialog),
            typeof(ContentDialog),
            typeof(ContentDialogHost),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="CurrentDialog"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CurrentDialogProperty
        = CurrentDialogPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the currently displayed dialog, or <see langword="null"/> when no dialog is displayed.
    /// </summary>
    /// <remarks>The dialog remains current throughout its opening and closing animations.</remarks>
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
    /// Gets or sets a value indicating whether dialog opening and closing animations are enabled.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true"/>. Animations also respect the system animation settings.
    /// Setting this property to <see langword="false"/> immediately finishes any active animation.
    /// </remarks>
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
    /// Displays a dialog over the host's content and asynchronously waits for it to close.
    /// </summary>
    /// <param name="dialog">
    /// The dialog to display. It must belong to the host's UI thread, have no parent and not already be open.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that cancels the operation and closes the dialog without raising
    /// <see cref="ContentDialog.Closing"/>, regardless of <see cref="ContentDialog.IsDismissible"/>.
    /// </param>
    /// <returns>
    /// A task that completes when the dialog has fully closed and the host is ready for reuse.
    /// Its result is the value supplied when closing, or <see langword="null"/> for dismissal.
    /// Token cancellation cancels the task.
    /// </returns>
    /// <remarks>
    /// The host must be loaded, visible and not already displaying a dialog. Hiding or unloading
    /// the host ends the operation with <see langword="null"/>, unless a result has already been accepted.
    /// Invalid arguments or an unavailable host are reported through the returned task.
    /// </remarks>
    public Task<object?> ShowAsync(ContentDialog dialog, CancellationToken cancellationToken = default)
    {
        bool ownsPreparation = false;
        // Validation failures are represented by the returned task, just like presentation failures.
        try
        {
            VerifyAccess();

            if (dialog is null)
            {
                throw new ArgumentNullException(nameof(dialog));
            }

            dialog.VerifyAccess();

            if (_isPreparingPresentation)
            {
                throw new InvalidOperationException(
                    "The host is preparing a dialog presentation."
                );
            }

            VerifyPresentationAvailable(dialog);
            cancellationToken.ThrowIfCancellationRequested();

            // ApplyTemplate invokes application overrides and can pump Dispatcher. Reserve the
            // host before entering those callbacks, without publishing an incomplete Session that
            // template replacement or unload could attempt to clean up. Only the owning call may
            // release this guard, including when a nested ShowAsync is rejected.
            _isPreparingPresentation = ownsPreparation = true;
            ApplyTemplate();

            // Template callbacks may hide the host, cancel the request, or attach the dialog to
            // another host. The reservation protects this host only; all other preconditions must
            // still hold before the session acquires logical ownership and input isolation.
            VerifyPresentationAvailable(dialog);
            cancellationToken.ThrowIfCancellationRequested();

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
        finally
        {
            if (ownsPreparation)
            {
                _isPreparingPresentation = false;
            }
        }
    }

    /// <summary>
    /// Checks mutable presentation prerequisites before and after template callbacks. This method
    /// does not acquire ownership; the preparation guard and Session provide that reservation.
    /// </summary>
    private void VerifyPresentationAvailable(ContentDialog dialog)
    {
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
    }

    private async Task PresentAsync(Session session, UIElement contentRoot)
    {
        var        dialog  = session.Dialog;
        object?    result  = null;
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

            StartModalInteraction(session, contentRoot);

            if (!IsActive(session))
            {
                return;
            }

            AddLogicalChild(dialog);

            if (!IsActive(session))
            {
                return;
            }

            dialog.Unloaded         += OnDialogUnloaded;
            dialog.IsVisibleChanged += OnDialogIsVisibleChanged;

            SetValue(CurrentDialogPropertyKey, dialog);

            if (!IsActive(session))
            {
                return;
            }

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
                    StartDialogLifecycle(session);
                    if (IsActive(session) && !session.Outcome.Task.IsCompleted)
                    {
                        dialog.NotifyOpened();
                    }
                }
            }

            result = await session.Outcome.Task.ConfigureAwait(true);

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
            CompleteSession(session, result, failure);
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
    private void CompleteSession(Session session, object? result, Exception? failure = null)
    {
        if (!IsActive(session))
        {
            return;
        }

        session.IsCleanedUp = true;
        // Settle an otherwise pending outcome before calling application cleanup. Late opening
        // failures are then traced by the observer rather than faulting an abandoned private task.
        session.Outcome.TrySetResult(result);

        var dialog = session.Dialog;

        // Every operation below can lead to application callbacks, including DP notifications,
        // focus restoration and virtual presenter methods. A failure must not skip later releases.
        // Keep the reservation through all callbacks so nested dispatcher frames cannot reuse it.

        CancelDialogLifecycle(session);

        Cleanup(dialog.MarkClosing);
        Cleanup(AbortTransition);

        Dispatcher.ShutdownStarted -= OnDispatcherShutdown;

        dialog.Unloaded         -= OnDialogUnloaded;
        dialog.IsVisibleChanged -= OnDialogIsVisibleChanged;

#pragma warning disable IDISP007 // The host creates and owns this session's cancellation registration.
        Cleanup(session.Registration.Dispose);
#pragma warning restore IDISP007
        Cleanup(() => SetValue(CurrentDialogPropertyKey, null));
        Cleanup(() => RemoveLogicalChild(dialog));
        Cleanup(() => StopModalInteraction(session));

        EndDialogLifecycle(session, result);

        Cleanup(() => dialog.NotifyClosed(result));
#pragma warning disable IDISP007 // This host owns the per-presentation cancellation source.
        Cleanup(session.LifecycleCancellation.Dispose);
#pragma warning restore IDISP007
        _session = null;

        failure ??= session.LifecycleFailure;

        // Forced removal can run before the async presentation worker observes an opening fault.
        // Preserve an already accepted failure just as OutcomeResult preserves an accepted choice.
        if (failure is null && session.Outcome.Task.IsFaulted)
        {
            failure = session.Outcome.Task.Exception!.InnerException;
        }

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

        return;

        void Cleanup(Action release)
        {
            try
            {
                release();
            }
            catch (Exception exception)
            {
                failure ??= exception;
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
                session.OutcomeResult = result;
                CancelDialogLifecycle(session);
                if (!IsActive(session))
                {
                    return;
                }

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
            CompleteSession(session, session.OutcomeResult);
        }
    }

    private void OnDialogUnloaded(object sender, RoutedEventArgs e)
    {
        CheckDialogAvailabilityAfterLayout();
    }

    private void OnDialogIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue)
        {
            CheckDialogAvailabilityAfterLayout();
        }
    }

    /// <summary>
    /// Ends a session whose surface remains unloaded or hidden after layout. Template replacement
    /// can briefly change both states, so never force cleanup from the property notification itself.
    /// </summary>
    private void CheckDialogAvailabilityAfterLayout()
    {
        var session = _session;
        if (session is null || !IsActive(session) || session.IsAvailabilityCheckPending)
        {
            return;
        }

        session.IsAvailabilityCheckPending = true;

        // Template replacement may transiently detach the dialog. Loaded priority provides a
        // post-layout check without allowing continuous input to postpone mandatory cleanup.
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
                {
                    session.IsAvailabilityCheckPending = false;
                    if (IsActive(session)
                        && (!session.Dialog.IsLoaded || !session.Dialog.IsVisible))
                    {
                        ForceEnd();
                    }
                }
            )
        );
    }

    /// <inheritdoc />
    /// <remarks>
    /// Custom templates must place <see cref="ContentControl.Content"/> in a <see cref="UIElement"/>
    /// named <c>PART_ContentRoot</c> and display <see cref="CurrentDialog"/> in a separate overlay.
    /// Without the content root, the host cannot display dialogs. The optional
    /// <c>PART_DialogPresenter</c>, of type <see cref="ContentDialogTransitionPresenter"/>, enables
    /// animations; without it, dialogs open and close immediately. The overlay must block input to
    /// covered content until <see cref="CurrentDialog"/> becomes <see langword="null"/>.
    /// </remarks>
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

    /// <summary>Gets the host's content and any currently displayed dialog as logical children.</summary>
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

            return new LogicalChildrenEnumerator(children);
        }
    }
}