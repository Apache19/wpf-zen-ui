// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Coordinates the template-owned visual states with the dialog lifecycle.</summary>
public partial class ContentDialogHost
{
    private const string DialogStatesGroupName = "DialogStates";
    private const string ClosedStateName = "Closed";
    private const string OpenedStateName = "Opened";

    /// <summary>
    /// Enters a dialog state and, when transitions are enabled, waits for the template's
    /// <see cref="VisualTransition"/> to finish.
    /// </summary>
    /// <remarks>
    /// WPF normally raises <see cref="VisualStateGroup.CurrentStateChanged"/> after both the generated
    /// and explicit transition storyboards complete. An explicit storyboard with no generated
    /// duration is also observed directly because its clock can visibly finish before WPF delivers
    /// the group notification. Templates may omit the state group; such templates retain all host
    /// behavior and transition immediately. State storyboards should describe stable poses, while
    /// time-based entrance and exit animation belongs in transitions between <c>Closed</c> and
    /// <c>Opened</c>.
    /// </remarks>
    private Task GoToDialogStateAsync(string stateName)
    {
        FinishTransition();

        VisualStateGroup? group = GetDialogStateGroup();
        VisualState? targetState = GetState(group, stateName);
        bool useTransitions = IsAnimationEnabled
            && SystemParameters.ClientAreaAnimation
            && IsLoaded
            && IsVisible;

        if (group is null || targetState is null || !useTransitions
            || ReferenceEquals(group.CurrentState, targetState))
        {
            _ = VisualStateManager.GoToState(this, stateName, false);
            return Task.CompletedTask;
        }

        VisualState? previousState = group.CurrentState;
        VisualTransition? transition = GetTransition(group, previousState, targetState);
        Storyboard? explicitStoryboard = transition?.Storyboard;
        // When GeneratedDuration is zero, WPF's dynamic transition contains no independently timed
        // visual work. Completion of the explicit storyboard therefore describes the visible end
        // of the whole transition and is a safe fallback if the group notification is lost.
        bool explicitStoryboardCompletesTransition = explicitStoryboard is not null
            && transition!.GeneratedDuration.HasTimeSpan
            && transition.GeneratedDuration.TimeSpan == TimeSpan.Zero;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool completed = false;
        bool completionScheduled = false;
        bool explicitStoryboardCompleted = false;
        bool stateChangeConfirmed = false;

        void Complete()
        {
            if (completed)
                return;

            completed = true;
            group.CurrentStateChanged -= OnCurrentStateChanged;
            if (explicitStoryboard is not null)
                explicitStoryboard.Completed -= OnExplicitStoryboardCompleted;
            _finishTransition = null;
            completion.TrySetResult(true);
        }

        void ScheduleCompletionBoundary()
        {
            if (completed || completionScheduled)
                return;

            completionScheduled = true;
            // Storyboard and VisualStateManager completion callbacks run during a timing-manager
            // tick. Loaded is the first dispatcher priority after layout/render and before input,
            // giving WPF one deterministic boundary to settle its controllable clocks without
            // allowing mouse traffic to starve mandatory dialog cleanup.
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                completionScheduled = false;
                if (completed)
                    return;

                if (explicitStoryboardCompleted && !stateChangeConfirmed)
                    Finish();
                else
                    Complete();
            }));
        }

        void OnCurrentStateChanged(object? sender, VisualStateChangedEventArgs e)
        {
            if (ReferenceEquals(e.NewState, targetState))
            {
                stateChangeConfirmed = true;
                group.CurrentStateChanged -= OnCurrentStateChanged;
                ScheduleCompletionBoundary();
            }
        }

        void OnExplicitStoryboardCompleted(object? sender, EventArgs e)
        {
            explicitStoryboardCompleted = true;
            explicitStoryboard!.Completed -= OnExplicitStoryboardCompleted;
            ScheduleCompletionBoundary();
        }

        void Finish()
        {
            if (completed)
                return;

            // VisualStateManager records the destination as CurrentState as soon as a transition
            // starts. Asking for that state again therefore does not stop its clocks. Briefly enter
            // the previous stable state without transitions, then snap to the destination. Both
            // changes occur in one dispatcher turn, so no intermediate frame is rendered.
            if (previousState is not null && ReferenceEquals(group.CurrentState, targetState))
                _ = VisualStateManager.GoToState(this, previousState.Name, false);

            _ = VisualStateManager.GoToState(this, stateName, false);
            Complete();
        }

        group.CurrentStateChanged += OnCurrentStateChanged;
        if (explicitStoryboardCompletesTransition)
            explicitStoryboard!.Completed += OnExplicitStoryboardCompleted;
        _finishTransition = Finish;
        try
        {
            if (!VisualStateManager.GoToState(this, stateName, true))
                Complete();
        }
        catch
        {
            Complete();
            throw;
        }

        return completion.Task;
    }

    /// <summary>
    /// Locates the visual-state group on the template root, matching WPF's Control.StateGroupsRoot
    /// convention without depending on any particular named template part.
    /// </summary>
    private VisualStateGroup? GetDialogStateGroup()
    {
        if (VisualTreeHelper.GetChildrenCount(this) == 0
            || VisualTreeHelper.GetChild(this, 0) is not FrameworkElement stateGroupsRoot)
            return null;

        foreach (object item in VisualStateManager.GetVisualStateGroups(stateGroupsRoot))
        {
            if (item is VisualStateGroup { Name: DialogStatesGroupName } group)
                return group;
        }

        return null;
    }

    private static VisualState? GetState(VisualStateGroup? group, string? stateName)
    {
        if (group is null)
            return null;

        foreach (object item in group.States)
        {
            if (item is VisualState { Name: var name } state && name == stateName)
                return state;
        }

        return null;
    }

    /// <summary>
    /// Reproduces WPF's public-state transition selection so the host observes the same explicit
    /// storyboard that <see cref="VisualStateManager.GoToState(FrameworkElement, string, bool)"/>
    /// starts. WPF exposes the transition collection but not its selection helper.
    /// </summary>
    private static VisualTransition? GetTransition(
        VisualStateGroup group,
        VisualState? fromState,
        VisualState toState
    )
    {
        VisualTransition? best = null;
        VisualTransition? defaultTransition = null;
        int bestScore = -1;

        foreach (object item in group.Transitions)
        {
            if (item is not VisualTransition transition)
                continue;
            if (transition.From is null && transition.To is null)
            {
                defaultTransition ??= transition;
                continue;
            }

            int score = -1;
            VisualState? transitionFromState = GetState(group, transition.From);
            VisualState? transitionToState = GetState(group, transition.To);
            if (ReferenceEquals(fromState, transitionFromState))
                score++;
            else if (transitionFromState is not null)
                continue;

            if (ReferenceEquals(toState, transitionToState))
                score += 2;
            else if (transitionToState is not null)
                continue;

            if (score > bestScore)
            {
                bestScore = score;
                best = transition;
            }
        }

        return best ?? defaultTransition;
    }

    private void FinishTransition() => _finishTransition?.Invoke();

    private static void OnIsAnimationEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ContentDialogHost)d).FinishTransition();
}
