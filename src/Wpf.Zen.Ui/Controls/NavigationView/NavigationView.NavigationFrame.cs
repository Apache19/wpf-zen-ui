// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.Controls;

/// <content>
/// Converts Frame commits and visual attachments into independent page activations within a
/// session.
/// </content>
public partial class NavigationView
{
    private NavigationActivation? _activation;
    private DispatcherOperation?  _navigationTransitionOperation;
    private DispatcherOperation?  _visualDetachOperation;
    private bool                  _frameDetached;

    private static Frame CreateNavigationFrame()
    {
        var frame = new Frame
        {
            Focusable              = false,
            FocusVisualStyle       = null,
            IsTabStop              = false,
            JournalOwnership       = JournalOwnership.OwnsJournal,
            NavigationUIVisibility = NavigationUIVisibility.Hidden,
            SnapsToDevicePixels    = true,
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
            if (frame.CommandBindings[index].Command == NavigationCommands.BrowseBack
                || frame.CommandBindings[index].Command == NavigationCommands.BrowseForward
                || frame.CommandBindings[index].Command == NavigationCommands.NavigateJournal
                || frame.CommandBindings[index].Command == NavigationCommands.Refresh
                || frame.CommandBindings[index].Command == NavigationCommands.BrowseStop)
            {
                frame.CommandBindings.Remove(frame.CommandBindings[index]);
            }
        }
    }


    /// <summary>
    /// Creates activations only for actual Frame commits in the current session; invalidated
    /// requests receive no entry.
    /// </summary>
    private void OnNavigationFrameNavigated(object sender, NavigationEventArgs eventArgs)
    {
        _ = _navigationFrame.RemoveBackEntry();

        if (eventArgs.ExtraData is not NavigationTransaction transaction
            || !IsCurrentRequest(transaction))
        {
            return;
        }

        transaction.Execute(() =>
        {
            var failures = new NavigationFailures();
            failures.Try(() => CommitNavigationRequest(transaction));

            void Publish(Action action)
            {
                if (IsCurrentRequest(transaction))
                {
                    failures.Try(action);
                }
            }

            Publish(() => UpdateNavigationFrameScrollState(eventArgs.Content));

            Publish(() =>
            {
                var settings = GetNavigationTransitionSettings(eventArgs.Content, transaction.TransitionSettings);
                ApplyTransitionEffectToNavigationFrame(settings);
            });

            Publish(() => UpdatePageHeader(eventArgs.Content as FrameworkElement));

            if (eventArgs.Content is not null && !_frameDetached)
            {
                Publish(() => ScheduleActivation(
                    transaction.Session, transaction.Instance!, transaction.Request?.Target));
            }

            Exception? failure = failures.Exception;

            var result = failure is null
                ? NavigationResult.Success
                : NavigationResult.FromFailure(failure, transaction.IsCommitted);

            transaction.Finish(result);
        });
    }

    /// <summary>
    /// Completes cancellation when Frame stops an uncommitted request, preserving existing
    /// projections.
    /// </summary>
    private void OnNavigationFrameStopped(object sender, NavigationEventArgs eventArgs)
    {
        if (eventArgs.ExtraData is NavigationTransaction transaction && !transaction.IsCommitted)
        {
            transaction.Finish(new NavigationResult(NavigationResultStatus.Cancelled));
        }
    }

    /// <summary>
    /// Transfers asynchronous Frame failures to request completion so navigation tasks remain
    /// observed.
    /// </summary>
    private void OnNavigationFrameFailed(object sender, NavigationFailedEventArgs eventArgs)
    {
        if (eventArgs.ExtraData is NavigationTransaction transaction)
        {
            eventArgs.Handled = true;
            transaction.Finish(NavigationResult.FromFailure(eventArgs.Exception, transaction.IsCommitted));
        }
    }

    /// <summary>
    /// Reads participants at Loaded, waiting for the same instance's previous activation to drain
    /// before entry.
    /// </summary>
    private void ScheduleActivation(
        NavigationSession session, NavigationPageInstance instance, NavigationTarget? target)
    {
        var activation = new NavigationActivation(
            session, Dispatcher, instance, target,
            failure => OnNavigationCallbackFailed(new NavigationCallbackFailedEventArgs(failure))
        );

        Task previous = session.RememberActivation(instance.Page, activation.Completion);

        _activation = activation;

        var dispatch = Dispatcher.InvokeAsync(
            () =>
            {
                _navigationTransitionOperation = null;

                if (!ReferenceEquals(_activation, activation) || activation.IsStopping)
                {
                    activation.CompleteDispatch();
                    return;
                }

                _ = activation.EnterAsync(previous);
            },
            DispatcherPriority.Loaded
        );

        _navigationTransitionOperation = dispatch;

        dispatch.Aborted += (_, _) => activation.CompleteDispatch();
    }

    /// <summary>
    /// Transfers the current activation reference before aborting dispatch and delivering exit,
    /// supporting reentrant stop during exit.
    /// </summary>
    private void StopCurrentActivation()
    {
        var activation = _activation;
        _activation = null;

        var transition = _navigationTransitionOperation;
        _navigationTransitionOperation = null;

        if (transition is { Status: DispatcherOperationStatus.Pending })
        {
            transition.Abort();
        }

        activation?.Stop();
    }

    /// <summary>
    /// Coalesces visual moves within one dispatch cycle and ends activation on actual detachment.
    /// </summary>
    private void OnNavigationFrameUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        if (_visualDetachOperation is { Status: DispatcherOperationStatus.Pending })
        {
            return;
        }

        _visualDetachOperation = Dispatcher.InvokeAsync(
            () =>
            {
                _visualDetachOperation = null;
                if (_navigationFrame.IsLoaded)
                {
                    return;
                }

                _frameDetached = true;
                StopCurrentActivation();
            },
            DispatcherPriority.ContextIdle
        );
    }

    /// <summary>
    /// Reload after real unload creates a new activation; cached instances receive fresh tokens and
    /// exit obligations.
    /// </summary>
    private void OnNavigationFrameLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (!_frameDetached)
        {
            return;
        }

        _frameDetached = false;

        var session = _navigationSession;
        if (session is not null && !session.IsStopping
            && _activation is null && _currentInstance is { } instance)
        {
            ScheduleActivation(session, instance, _currentOccurrence?.Request.Target);
        }
    }

    /// <summary>
    /// Synchronizes Frame scrolling template state with the committed page.
    /// </summary>
    private void UpdateNavigationFrameScrollState(object? content)
    {
        bool active = content is DependencyObject dependencyObject
            && GetIsNavigationFrameScrollEnabled(dependencyObject);

        SetValue(IsNavigationFrameScrollActivePropertyKey, active);
    }

    /// <summary>
    /// Resolves navigation transitions from page declarations and host defaults.
    /// </summary>
    internal NavigationTransitionSettings GetNavigationTransitionSettings(
        object? content, NavigationTransitionSettings? fallback = null)
    {
        if (content is DependencyObject dependencyObject)
        {
            var transitionValueSource = DependencyPropertyHelper.GetValueSource(
                dependencyObject,
                TransitionProperty
            );

            var durationValueSource = DependencyPropertyHelper.GetValueSource(
                dependencyObject,
                TransitionDurationProperty
            );

            return new NavigationTransitionSettings(
                transitionValueSource.BaseValueSource is BaseValueSource.Default
                    ? Transition
                    : GetTransition(dependencyObject),
                durationValueSource.BaseValueSource is BaseValueSource.Default
                    ? TransitionDuration
                    : GetTransitionDuration(dependencyObject)
            );
        }

        return fallback
            ?? new NavigationTransitionSettings(Transition, TransitionDuration);
    }


    /// <summary>
    /// The transition type and duration used for one navigation commit.
    /// </summary>
    internal readonly struct NavigationTransitionSettings(NavigationTransition transition, int duration)
    {
        internal NavigationTransition Transition { get; } = transition;
        internal int                  Duration   { get; } = duration;
    }
}