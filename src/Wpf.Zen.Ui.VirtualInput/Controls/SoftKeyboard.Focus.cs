// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Tracks eligible editor focus and resolves custom target adapters within the configured scope.
/// </summary>
public partial class SoftKeyboard
{
    internal void OnSessionFocusChanged(IInputElement? focusedElement, bool touchOrPenInitiated)
    {
        if (_inputMethodMenu?.IsOpen == true || SettingsMenu?.IsOpen == true)
        {
            return;
        }

        if (!IsLoaded)
        {
            return;
        }

        if (focusedElement is DependencyObject d && IsWithinEffectiveScope(d))
        {
            var focusedAdapter   = GetAdapter(focusedElement);
            var isEditableTarget = focusedAdapter?.CanReceiveInput(focusedElement) == true;

            if (isEditableTarget && ReferenceEquals(focusedElement, _compositionTarget))
            {
                // A composition can survive temporary application deactivation or an external
                // focus move. GotKeyboardFocus is the first point at which Escape can again be
                // routed safely to its original editor.
                _ = TryCancelActiveComposition(focusedElement);
            }

            if (Target is null && isEditableTarget)
            {
                SetValue(ActiveTargetPropertyKey, focusedElement);
            }

            var shouldOpen = AutoShowMode == SoftKeyboardAutoShowMode.AnyFocus
                || (AutoShowMode == SoftKeyboardAutoShowMode.TouchAndPen && touchOrPenInitiated);

            if (shouldOpen && isEditableTarget)
            {
                SetCurrentValue(IsOpenProperty, true);
            }

            if (IsOpen)
            {
                RefreshInputMethodState();

                RequestSystemKeyboardHide();

                RequestCandidateRefresh();

                QueueCandidateBridgeRefresh();
            }

            return;
        }

        if (AutoHideMode == SoftKeyboardAutoHideMode.FocusLeavesScope)
        {
            SetCurrentValue(IsOpenProperty, false);
        }

        // Focus outside the effective scope cannot be a valid target for this keyboard. Release the
        // local suppression marker immediately, but do not request TryShow for another window or
        // scope—the new focus owner and Windows input policy must decide what UI it needs.
        ReleaseSystemKeyboardSuppression(tryRestore: false);
        ResetCandidateSession();
    }

    /// <summary>
    /// Applies one typed WPF or TSF notification to the input-method state.
    /// </summary>
    internal void OnSessionInputMethodChanged(InputMethodChange change)
    {
        VerifyAccess();
        ApplyInputMethodChange(change);
    }

    /// <summary>
    /// Reads only the state owned by one notification type and refreshes affected properties.
    /// </summary>
    private void ApplyInputMethodChange(InputMethodChange change)
    {
        switch (change.Kind)
        {
            case InputMethodChangeKind.RefreshAll:
                RefreshInputMethodStateNow();
                return;

            case InputMethodChangeKind.Profile:
                if (!change.Profile.IsActive)
                {
                    return;
                }

                ResetObservedPlatformState();
                RefreshInputMethodStateNow();
                return;

            case InputMethodChangeKind.ImeOpen:
                _observedImeOpenValue = GetRequiredStateValue(change);
                RefreshObservedInputMode();
                RefreshCandidatesAfterInputMethodChange();
                return;

            case InputMethodChangeKind.ImeConversion:
                _observedImeConversionValue = GetRequiredStateValue(change);
                RefreshObservedInputMode();
                RefreshCandidatesAfterInputMethodChange();
                return;

            case InputMethodChangeKind.InputLanguage:
                _observedModeProfile        = null;
                _observedImeOpenValue       = 0;
                _observedImeConversionValue = 0;
                _resetObservedPlatformState = true;
                RefreshInputMethodStateNow();
                return;

            case InputMethodChangeKind.InputModePresentation:
                RefreshObservedInputMode();
                RefreshCandidatesAfterInputMethodChange();
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
    }

    /// <summary>
    /// Returns the value read by an open or conversion notification callback.
    /// </summary>
    private static int GetRequiredStateValue(InputMethodChange change)
    {
        return change.StateValue
            ?? throw new InvalidOperationException(
                $"The {change.Kind} notification did not include its observed value."
            );
    }

    /// <summary>
    /// Refreshes an application-defined source whose notification contract has no Windows type.
    /// </summary>
    internal void OnSessionInputMethodStateInvalidated()
    {
        OnSessionInputMethodChanged(
            new InputMethodChange(InputMethodChangeKind.RefreshAll)
        );
    }

    /// <summary>
    /// Invalidates input-method state for tests and compatibility with existing internal callers.
    /// </summary>
    internal void OnSessionInputLanguageChanged()
    {
        OnSessionInputMethodChanged(
            new InputMethodChange(InputMethodChangeKind.InputLanguage)
        );
    }

    /// <summary>
    /// Tracks the WPF composition associated with the active editor without taking ownership of it.
    /// </summary>
    /// <remarks>
    /// Candidate UI lifetime is not a reliable composition signal: a TIP can temporarily expose no
    /// candidates while its pre-edit range remains mutable. The matching final TextInput event ends
    /// this observation. Cancellation itself is still delegated to the TIP through Escape.
    /// </remarks>
    internal void OnSessionTextComposition(TextCompositionEventArgs args)
    {
        var composition       = args.TextComposition;
        var compositionSource = args.Source as IInputElement;

        if (args.RoutedEvent is not null
            && (args.RoutedEvent == TextCompositionManager.PreviewTextInputStartEvent
                || args.RoutedEvent == TextCompositionManager.PreviewTextInputUpdateEvent))
        {
            if (ReferenceEquals(compositionSource, ActiveTarget))
            {
                if (args.RoutedEvent == TextCompositionManager.PreviewTextInputStartEvent)
                {
                    _compositionStartVersion++;
                }

                _activeTextComposition = composition;
                _compositionTarget     = compositionSource;

                _compositionCancellationRequested = false;
            }

            return;
        }

        // TextStore.RaiseCompositionEvents creates a separate FrameworkTextComposition (including
        // FrameworkRichTextComposition) for every start/update/end record. Its final event belongs
        // to the editor's composition stream, not to the object retained from the previous record.
        // Match that stream by source and framework type; ordinary TextComposition instances still
        // require identity so unrelated direct text input cannot end a pending composition.
        if (args.RoutedEvent == TextCompositionManager.PreviewTextInputEvent
            && ReferenceEquals(compositionSource, _compositionTarget)
            && (ReferenceEquals(composition, _activeTextComposition)
                || (composition is FrameworkTextComposition
                    && _activeTextComposition is FrameworkTextComposition)))
        {
            ClearActiveComposition();
        }
    }

    private void ResolveTarget()
    {
        if (Target is not null)
        {
            var valid = Target is DependencyObject targetObject
                && IsWithinEffectiveScope(targetObject)
                && GetAdapter(Target) is { } targetAdapter
                && targetAdapter.CanReceiveInput(Target);

            SetValue(ActiveTargetPropertyKey, valid ? Target : null);
            return;
        }

        if (Keyboard.FocusedElement is { } focused and DependencyObject d
            && IsWithinEffectiveScope(d)
            && GetAdapter(focused) is { } adapter
            && adapter.CanReceiveInput(focused))
        {
            SetValue(ActiveTargetPropertyKey, focused);
        }
    }

    private ISoftKeyboardTargetAdapter? GetAdapter(IInputElement target)
    {
        if (TargetAdapter?.IsSupported(target) == true)
        {
            return TargetAdapter;
        }

        return SoftKeyboardTargetAdapter.Default.IsSupported(target)
            ? SoftKeyboardTargetAdapter.Default
            : null;
    }

    private DependencyObject? GetEffectiveScope()
    {
        return TargetScope ?? _window ?? Window.GetWindow(this);
    }

    private bool IsWithinEffectiveScope(DependencyObject element)
    {
        var scope = GetEffectiveScope();
        if (scope is null)
        {
            return false;
        }

        if (ReferenceEquals(scope, element))
        {
            return true;
        }

        for (var current = element; current is not null; current = GetParent(current))
        {
            if (ReferenceEquals(current, scope))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        if (element is Visual or Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(element);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }

        return LogicalTreeHelper.GetParent(element);
    }
}
