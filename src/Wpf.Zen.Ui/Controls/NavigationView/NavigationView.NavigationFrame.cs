// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using Wpf.Zen.Ui.Animations;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <content>
/// Defines the WPF <see cref="Frame"/> used as the stable navigation surface of
/// <see cref="NavigationView"/>.
/// </content>
public partial class NavigationView
{
    /// <summary>
    /// The content from the latest completed navigation.
    /// </summary>
    /// <remarks>
    /// Tracking only content confirmed by <see cref="Frame.Navigated"/> prevents a failed, stopped,
    /// or superseded request from becoming part of the public navigation lifecycle.
    /// </remarks>
    private object? _navigationAwareContent;

    /// <summary>The semantic occurrence represented by <see cref="_navigationAwareContent"/>.</summary>
    private NavigationTarget? _navigationAwareTarget;

    /// <summary>
    /// Owns cancellation for the asynchronous entry callbacks of the current committed
    /// navigation occurrence.
    /// </summary>
    private NavigationEnterOperation? _navigationEnterOperation;

    /// <summary>
    /// The Loaded-priority lifecycle delivery associated with the latest Frame commit.
    /// </summary>
    /// <remarks>
    /// Keeping the operation explicit lets scope detachment abort work that has not started. A bare
    /// BeginInvoke delegate would otherwise enter or exit pages after the owning scope had returned
    /// from detachment and possibly released its services.
    /// </remarks>
    private DispatcherOperation? _navigationTransitionOperation;

    /// <summary>
    /// Creates the frame that owns WPF navigation state for this control.
    /// </summary>
    /// <remarks>
    /// The frame is deliberately composed rather than subclassed. No additional public control is
    /// required: <see cref="NavigationView"/> owns the navigation policy, while WPF's
    /// <see cref="Frame"/> supplies the navigation service and its required
    /// <c>PART_FrameCP</c> presentation contract.
    ///
    /// The frame owns a private WPF journal so its entries can never leak into an ancestor
    /// <see cref="NavigationWindow"/>. <see cref="NavigationView"/> maintains its own semantic
    /// journal, so frame entries are removed after navigation and the frame's routed navigation
    /// commands are removed below.
    /// </remarks>
    private static Frame CreateNavigationFrame()
    {
        var frame = new Frame
        {
            Focusable = false,
            FocusVisualStyle = null,
            IsTabStop = false,
            JournalOwnership = JournalOwnership.OwnsJournal,
            NavigationUIVisibility = NavigationUIVisibility.Hidden,
            SnapsToDevicePixels = true,
        };

        RemoveFrameNavigationCommandBindings(frame);

        return frame;
    }

    /// <summary>
    /// Removes command bindings that WPF installs when a <see cref="Frame"/> owns its journal.
    /// </summary>
    /// <remarks>
    /// WPF's <c>Frame.SwitchToOwnJournal</c> installs handlers for back, forward, journal,
    /// refresh, and stop commands. Leaving those handlers in place would make F5 refresh the
    /// current page behind <see cref="NavigationView"/>'s journal and lifecycle model, and their
    /// <c>CanExecute</c> handlers can consume commands even when the private frame journal is
    /// empty. Removing the bindings disables only frame-owned navigation; it does not swallow raw
    /// keyboard or mouse input, so a page or an ancestor remains free to handle those gestures.
    /// </remarks>
    private static void RemoveFrameNavigationCommandBindings(Frame frame)
    {
        for (int index = frame.CommandBindings.Count - 1; index >= 0; index--)
        {
            if (
                frame.CommandBindings[index].Command == NavigationCommands.BrowseBack
                || frame.CommandBindings[index].Command == NavigationCommands.BrowseForward
                || frame.CommandBindings[index].Command == NavigationCommands.NavigateJournal
                || frame.CommandBindings[index].Command == NavigationCommands.Refresh
                || frame.CommandBindings[index].Command == NavigationCommands.BrowseStop
            )
            {
                frame.CommandBindings.Remove(frame.CommandBindings[index]);
            }
        }
    }

    /// <summary>
    /// Commits the successful navigation lifecycle, applies the visual transition, and discards
    /// WPF's journal entry.
    /// </summary>
    /// <remarks>
    /// Selection is projected by <see cref="NavigationView"/>, history lives in its journal, and
    /// semantic hierarchy is resolved independently through <see cref="Wpf.Zen.Ui.NavigationMap"/>.
    /// Retaining the frame entry would create a second, independently navigable history.
    /// </remarks>
    private void OnNavigationFrameNavigated(object sender, NavigationEventArgs eventArgs)
    {
        _ = _navigationFrame.RemoveBackEntry();

        UpdateNavigationFrameScrollState(eventArgs.Content);

        if (eventArgs.Content is not null)
        {
            ApplyTransitionEffectToNavigationFrame();
        }

        _ = CancelCurrentNavigationEnter();
        AbortPendingNavigationTransition();

        var exitedContent = _navigationAwareContent;
        var exitedTarget  = _navigationAwareTarget;
        var enteredTarget = (eventArgs.ExtraData as NavigationFrameState)?.NavigationTarget;

        _navigationAwareContent = eventArgs.Content;
        _navigationAwareTarget  = enteredTarget;

        var enterOperation = eventArgs.Content is null
            ? null
            : new NavigationEnterOperation(eventArgs.Content, enteredTarget);

        _navigationEnterOperation = enterOperation;

        // Frame.Navigated is the commit point for object navigation. Failed, stopped, and
        // superseded requests never reach this handler and therefore never enter the lifecycle.
        // Resolve targets at Loaded priority because WPF attaches the frame presenter after raising
        // Navigated; a page may establish its DataContext during that attachment.
        var dispatchOperation = _navigationFrame.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    _navigationTransitionOperation = null;
                    NotifyNavigationTransitionAfterAttachment(
                        exitedContent,
                        exitedTarget,
                        eventArgs.Content,
                        enteredTarget,
                        enterOperation
                    );
                }
            )
        );
        _navigationTransitionOperation = dispatchOperation;

        if (enterOperation is not null)
        {
            // A dispatcher can abort pending work during shutdown. Release the operation's initial
            // dispatch lease in that path so a cancelled token source is not retained forever.
            dispatchOperation.Aborted += (_, _) =>
            {
                if (ReferenceEquals(_navigationTransitionOperation, dispatchOperation))
                {
                    _navigationTransitionOperation = null;
                }

                enterOperation.CompleteDispatch();
            };
        }
    }

    /// <summary>
    /// Notifies the page that lost current-navigation status before the page that gained it.
    /// </summary>
    private void NotifyNavigationTransitionAfterAttachment(
        object? exitedContent,
        NavigationTarget? exitedTarget,
        object? enteredContent,
        NavigationTarget? enteredTarget,
        NavigationEnterOperation? enterOperation
    )
    {
        try
        {
            var failures = new List<NavigationCallbackException>();
            NotifyContentAboutNavigationExited(exitedContent, exitedTarget, failures);
            StartNavigationEntered(
                enteredContent,
                enteredTarget,
                enterOperation,
                failures
            );

            // Both sides are invoked before an unhandled synchronous failure is rethrown. This is
            // essential after WPF has committed the destination: one faulty old view must not leave
            // the new view model unaware that it is current.
            ReportNavigationCallbackFailures(failures);
        }
        finally
        {
            enterOperation?.CompleteDispatch();
        }
    }

    /// <summary>Synchronizes the template-facing scrolling state with a navigation root.</summary>
    /// <remarks>
    /// The destination is inspected before <see cref="Frame.Navigate(object)"/> whenever the
    /// NavigationView initiates navigation, preventing a one-frame flash of the previous host
    /// template. The Navigated handler calls this method again as the commit-time safeguard for
    /// null navigation and any future navigation path that targets the private frame directly.
    /// </remarks>
    private void UpdateNavigationFrameScrollState(object? content)
    {
        bool isActive = content is DependencyObject dependencyObject
            && GetIsNavigationFrameScrollEnabled(dependencyObject);

        SetValue(IsNavigationFrameScrollActivePropertyKey, isActive);
    }

    /// <summary>
    /// Notifies the current page when the complete navigation view leaves a loaded visual tree.
    /// </summary>
    /// <remarks>
    /// Changing <see cref="PaneDisplayMode"/> moves the same frame between template hosts and
    /// transiently raises <see cref="FrameworkElement.Unloaded"/>. Deferring until context-idle
    /// distinguishes that move from a genuine window, tab, or application detach without coupling
    /// the frame to any one built-in template.
    /// </remarks>
    private void OnNavigationFrameUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        _ = _navigationFrame.Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(NotifyNavigationFrameDetached)
        );
    }

    /// <summary>Completes a deferred unload after ruling out template-driven reparenting.</summary>
    private void NotifyNavigationFrameDetached()
    {
        if (_navigationFrame.IsLoaded)
        {
            return;
        }

        _ = CancelCurrentNavigationEnter();
        AbortPendingNavigationTransition();

        var content = _navigationAwareContent;
        var target  = _navigationAwareTarget;

        _navigationAwareContent = null;
        _navigationAwareTarget = null;

        var failures = new List<NavigationCallbackException>();

        NotifyContentAboutNavigationExited(content, target, failures);
        ReportNavigationCallbackFailures(failures);
    }

    /// <summary>
    /// Applies the configured transition to the control-owned frame.
    /// </summary>
    /// <remarks>
    /// Animating the page itself would replace or hold animation clocks on consumer-owned
    /// <see cref="UIElement.Opacity"/> and <see cref="UIElement.RenderTransform"/> properties. The
    /// stable frame is an implementation detail, so animating it produces the same visual result
    /// without mutating the navigated page.
    /// </remarks>
    private void ApplyTransitionEffectToNavigationFrame()
    {
        if (TransitionDuration < 1)
        {
            return;
        }

        _ = TransitionAnimationProvider.ApplyTransition(_navigationFrame, Transition, TransitionDuration);
    }

    /// <summary>
    /// Invokes every synchronous entry callback and starts every asynchronous entry callback.
    /// </summary>
    /// <remarks>
    /// All callbacks run on the dispatcher after visual attachment. Asynchronous methods preserve
    /// the normal WPF synchronization context up to their first incomplete await, then their
    /// completions are observed independently so a slow page cannot block a later destination.
    /// A target that deliberately implements both entry interfaces receives both callbacks, with
    /// the synchronous callback first.
    /// </remarks>
    private void StartNavigationEntered(
        object? content,
        NavigationTarget? navigationTarget,
        NavigationEnterOperation? operation,
        List<NavigationCallbackException> synchronousFailures
    )
    {
        if (content is null)
        {
            return;
        }

        foreach (INavigationExited callbackTarget in GetNavigationLifecycleTargets(content))
        {
            if (callbackTarget is INavigationAware synchronousCallbackTarget)
            {
                try
                {
                    synchronousCallbackTarget.OnNavigationEntered();
                }
                catch (Exception exception)
                {
                    AddNavigationCallbackFailure(
                        synchronousFailures,
                        NavigationCallbackStage.Entered,
                        content,
                        synchronousCallbackTarget,
                        navigationTarget,
                        exception,
                        CancellationToken.None
                    );
                }
            }

            if (callbackTarget is not IAsyncNavigationAware asynchronousCallbackTarget
                || operation is null)
            {
                continue;
            }

            StartAsyncNavigationEntered(
                content,
                asynchronousCallbackTarget,
                navigationTarget,
                operation,
                synchronousFailures
            );
        }
    }

    /// <summary>Starts and observes one asynchronous entry callback.</summary>
    private void StartAsyncNavigationEntered(
        object content,
        IAsyncNavigationAware callbackTarget,
        NavigationTarget? navigationTarget,
        NavigationEnterOperation operation,
        List<NavigationCallbackException> synchronousFailures
    )
    {
        operation.RememberCallbackTarget(callbackTarget);
        Task task;
        try
        {
            task = callbackTarget.OnNavigationEnteredAsync(operation.Token)
                ?? throw new InvalidOperationException(
                    $"{callbackTarget.GetType().FullName}.{nameof(IAsyncNavigationAware.OnNavigationEnteredAsync)} returned null."
                );
        }
        catch (Exception exception)
        {
            AddNavigationCallbackFailure(
                synchronousFailures,
                NavigationCallbackStage.Entered,
                content,
                callbackTarget,
                navigationTarget,
                exception,
                operation.Token
            );
            return;
        }

        if (task.IsCompleted)
        {
            ObserveCompletedNavigationEnter(
                task,
                content,
                callbackTarget,
                navigationTarget,
                operation.Token,
                synchronousFailures
            );
        }
        else
        {
            operation.Track(
                task,
                completedTask => ObserveCompletedNavigationEnter(
                    completedTask,
                    content,
                    callbackTarget,
                    navigationTarget,
                    operation.Token
                )
            );
        }
    }

    /// <summary>Invokes synchronous exit callbacks while isolating view and view-model failures.</summary>
    private static void NotifyContentAboutNavigationExited(
        object? content,
        NavigationTarget? navigationTarget,
        List<NavigationCallbackException> failures
    )
    {
        if (content is null)
        {
            return;
        }

        foreach (INavigationExited callbackTarget in GetNavigationLifecycleTargets(content))
        {
            try
            {
                callbackTarget.OnNavigationExited();
            }
            catch (Exception exception)
            {
                failures.Add(
                    new NavigationCallbackException(
                        NavigationCallbackStage.Exited,
                        content,
                        callbackTarget,
                        navigationTarget,
                        exception
                    )
                );
            }
        }
    }

    /// <summary>Resolves the view and distinct view-model participants for one content root.</summary>
    /// <remarks>
    /// Resolution occurs at Loaded priority because a page may establish DataContext while WPF
    /// attaches the frame presenter. The returned snapshot is safe to use after an asynchronous
    /// callback continues away from the dispatcher.
    /// </remarks>
    private static IReadOnlyList<INavigationExited> GetNavigationLifecycleTargets(object content)
    {
        if (content is INavigationExited aware)
        {
            if (aware is FrameworkElement frameworkElement
                && !ReferenceEquals(frameworkElement.DataContext, aware)
                && frameworkElement.DataContext is INavigationExited viewModel)
            {
                return [aware, viewModel];
            }

            return [aware];
        }

        if (content is INavigableView<object> { ViewModel: INavigationExited navigableViewModel })
        {
            return [navigableViewModel];
        }

        return content is FrameworkElement { DataContext: INavigationExited dataContext }
            ? [dataContext]
            : [];
    }

    /// <summary>Observes an entry task that completed during its initial dispatcher invocation.</summary>
    private static void ObserveCompletedNavigationEnter(
        Task task,
        object content,
        IAsyncNavigationAware callbackTarget,
        NavigationTarget? navigationTarget,
        CancellationToken cancellationToken,
        List<NavigationCallbackException> failures
    )
    {
        try
        {
            task.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            AddNavigationCallbackFailure(
                failures,
                NavigationCallbackStage.Entered,
                content,
                callbackTarget,
                navigationTarget,
                exception,
                cancellationToken
            );
        }
    }

    /// <summary>Observes a later entry completion and publishes a failure on the UI dispatcher.</summary>
    private void ObserveCompletedNavigationEnter(
        Task task,
        object content,
        IAsyncNavigationAware callbackTarget,
        NavigationTarget? navigationTarget,
        CancellationToken cancellationToken
    )
    {
        var failures = new List<NavigationCallbackException>(1);
        ObserveCompletedNavigationEnter(
            task,
            content,
            callbackTarget,
            navigationTarget,
            cancellationToken,
            failures
        );

        if (failures.Count > 0)
        {
            QueueNavigationCallbackFailures(failures);
        }
    }

    private static void AddNavigationCallbackFailure(
        List<NavigationCallbackException> failures,
        NavigationCallbackStage stage,
        object content,
        INavigationExited callbackTarget,
        NavigationTarget? navigationTarget,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return;
        }

        failures.Add(
            new NavigationCallbackException(
                stage,
                content,
                callbackTarget,
                navigationTarget,
                exception
            )
        );
    }

    /// <summary>Cancels the entry work associated with the previous committed occurrence.</summary>
    private Task CancelCurrentNavigationEnter()
    {
        var operation = _navigationEnterOperation;

        _navigationEnterOperation = null;

        if (operation is null)
        {
            return Task.CompletedTask;
        }

        var cancellationException = operation.Cancel();
        if (cancellationException is not null && operation.CallbackTarget is not null)
        {
            QueueNavigationCallbackFailures(
                [
                    new NavigationCallbackException(
                        NavigationCallbackStage.Entered,
                        operation.Content,
                        operation.CallbackTarget,
                        operation.NavigationTarget,
                        cancellationException
                    ),
                ]
            );
        }

        return operation.Completion;
    }

    /// <summary>Aborts lifecycle delivery that has not begun on the owning dispatcher.</summary>
    private void AbortPendingNavigationTransition()
    {
        DispatcherOperation? operation = _navigationTransitionOperation;
        _navigationTransitionOperation = null;

        if (operation is { Status: DispatcherOperationStatus.Pending })
        {
            operation.Abort();
        }
    }

    /// <summary>Queues asynchronous failures through WPF's fail-fast dispatcher boundary.</summary>
    /// <remarks>
    /// Dispatcher.InvokeAsync captures exceptions into an unobserved task. The legacy BeginInvoke
    /// delegate path instead routes an unhandled exception through DispatcherUnhandledException,
    /// which preserves the control's documented fail-fast fallback after the public event runs.
    /// </remarks>
    private void QueueNavigationCallbackFailures(
        IReadOnlyList<NavigationCallbackException> failures
    )
    {
        if (_navigationFrame.Dispatcher.HasShutdownStarted
            || _navigationFrame.Dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = _navigationFrame.Dispatcher.BeginInvoke(
            DispatcherPriority.Normal,
            new Action(() => ReportNavigationCallbackFailures(failures))
        );
    }

    /// <summary>Raises every failure event before applying the unhandled fallback.</summary>
    private void ReportNavigationCallbackFailures(
        IReadOnlyList<NavigationCallbackException> failures
    )
    {
        if (failures.Count == 0)
        {
            return;
        }

        var unhandledExceptions = new List<Exception>(failures.Count);
        foreach (NavigationCallbackException failure in failures)
        {
            try
            {
                OnNavigationCallbackFailed(new NavigationCallbackFailedEventArgs(failure));
            }
            catch (Exception exception)
            {
                unhandledExceptions.Add(exception);
            }
        }

        if (unhandledExceptions.Count == 1)
        {
            throw unhandledExceptions[0];
        }

        if (unhandledExceptions.Count > 1)
        {
            throw new AggregateException(
                "Multiple navigation-aware callbacks failed.",
                unhandledExceptions
            );
        }
    }

    /// <summary>Carries semantic context through WPF's asynchronous object-navigation queue.</summary>
    private sealed class NavigationFrameState(NavigationTarget? navigationTarget)
    {
        internal NavigationTarget? NavigationTarget { get; } = navigationTarget;
    }

    /// <summary>
    /// Keeps one navigation cancellation source alive until every callback using its token ends.
    /// </summary>
    /// <remarks>
    /// The initial pending lease represents the Loaded-priority dispatch. A cancelled operation is
    /// disposed only after that dispatch and all incomplete entry tasks release their leases. This
    /// lets implementations register callbacks against the supplied token for the complete lifetime
    /// of the Task they returned, including when a newer navigation cancels it before dispatch.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1001:Types that own disposable fields should be disposable",
        Justification = "Cancellation is reference-counted across the dispatch and callback tasks; exposing unconditional disposal would violate the token lifetime contract."
    )]
    private sealed class NavigationEnterOperation(
        object content,
        NavigationTarget? navigationTarget
    )
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly TaskCompletionSource<bool> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private int _dispatchCompleted;
        private int _isCancellationRequested;
        private int _isDisposed;
        private int _pendingLeases = 1;

        internal CancellationToken Token => _cancellationTokenSource.Token;

        /// <summary>
        /// Gets a task that completes after the Loaded-priority dispatch and every asynchronous
        /// callback lease end.
        /// </summary>
        internal Task Completion => _completion.Task;

        internal object Content { get; } = content;

        internal NavigationTarget? NavigationTarget { get; } = navigationTarget;

        internal IAsyncNavigationAware? CallbackTarget { get; private set; }

        internal void RememberCallbackTarget(IAsyncNavigationAware callbackTarget)
        {
            CallbackTarget ??= callbackTarget;
        }

        internal void Track(Task task, Action<Task> observer)
        {
            _ = Interlocked.Increment(ref _pendingLeases);
            _ = task.ContinueWith(
                completedTask =>
                {
                    try
                    {
                        observer(completedTask);
                    }
                    finally
                    {
                        ReleaseLease();
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
        }

        internal void CompleteDispatch()
        {
            if (Interlocked.Exchange(ref _dispatchCompleted, 1) == 0)
            {
                ReleaseLease();
            }
        }

        internal Exception? Cancel()
        {
            Exception? cancellationException = null;
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (Exception exception)
            {
                cancellationException = exception;
            }
            finally
            {
                Volatile.Write(ref _isCancellationRequested, 1);
                TryDispose();
            }

            return cancellationException;
        }

        private void ReleaseLease()
        {
            if (Interlocked.Decrement(ref _pendingLeases) == 0)
            {
                _completion.TrySetResult(true);
                TryDispose();
            }
        }

        private void TryDispose()
        {
            if (Volatile.Read(ref _isCancellationRequested) != 0
                && Volatile.Read(ref _pendingLeases) == 0
                && Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                _cancellationTokenSource.Dispose();
            }
        }
    }
}
