// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Dispatches key-cap semantics through the foreground-validated native input path.
/// </summary>
public partial class SoftKeyboard
{
    private void OnButtonClick(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is not ButtonBase button)
        {
            return;
        }

        CandidateInteractionVersion++;

        switch (button.DataContext)
        {
            default:
                if (button.Name == SettingsButtonPartName)
                {
                    // DropDownButton opens its Flyout after raising Click. Prepare the data while
                    // editor focus still belongs to the text store; the button owns popup state.
                    CloseFlyouts();
                    RefreshSettings();
                }
                else if (button.Tag is SoftKeyboardKey { Kind: SoftKeyboardKeyKind.ChangePage } navigation)
                {
                    ShowPage(navigation.Page);
                }
                else if (button.Tag is SoftKeyboardKeyKind.Close)
                {
                    CloseKeyboard();
                }
                else if (button.Tag is SoftKeyboardKeyKind.SwitchInputMode)
                {
                    SwitchInputMode();
                }
                else
                {
                    // Replacement templates may contain application commands. Only consume
                    // actions owned by the keyboard so those Click events can reach the host.
                    return;
                }

                break;
        }

        args.Handled = true;
    }

    private void MoveCandidatePage(int delta)
    {
        if (!CanPresentCandidates
            || (delta < 0 && !CanGoToPreviousCandidatePage)
            || (delta > 0 && !CanGoToNextCandidatePage)
            || !PrepareTargetForInput())
        {
            return;
        }

        _ = MoveCandidatePageAsync(delta);

        RequestSystemKeyboardHide();
    }

    private void OnKeyInteractionStarted(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is not SoftKeyboardKeyControl keyControl)
        {
            return;
        }

        args.Handled = true;

        CandidateInteractionVersion++;

        if (!ReferenceEquals(_activeKeyControl, keyControl))
        {
            CancelKeyInteraction();
        }

        _activeKeyControl = keyControl;

        _inputMethodMenuRequestVersion++;

        _inputMethodMenu?.SetCurrentValue(ContextMenu.IsOpenProperty, false);

        SettingsMenu?.SetCurrentValue(ContextMenu.IsOpenProperty, false);

        if (!IsOpen || !IsEnabled || !IsVisible)
        {
            CancelKeyInteraction();
        }
    }

    private void OnKeyInputRequested(object? sender, SoftKeyboardInputRequestedEventArgs args)
    {
        args.Handled = true;

        if (args.OriginalSource is not SoftKeyboardKeyControl keyControl
            || !ReferenceEquals(_activeKeyControl, keyControl)
            || !ReferenceEquals(keyControl.Key, args.Key)
            || !IsOpen || !IsEnabled || !IsVisible)
        {
            return;
        }

        // The key owns the gesture, but each request still crosses the keyboard's normal
        // target/focus/IME validation boundary. No editor reference is stored on a key cap.

        _inputMethodMenuAnchor = keyControl;

        try
        {
            if (args.AlternateText is { } text)
            {
                if (args.Key.Action is ISoftKeyboardAlternateKeyAction or IAsyncSoftKeyboardAlternateKeyAction)
                {
                    ExecuteCustomAction(args.Key.Action, text);
                }
                else
                {
                    _ = SendActionTextAsync(text);
                }
            }
            else
            {
                InvokeKey(args.Key);
            }
        }
        finally
        {
            _inputMethodMenuAnchor = null;
        }
    }

    private void InvokeKey(SoftKeyboardKey key)
    {
        switch (key.Kind)
        {
            case SoftKeyboardKeyKind.VirtualKey:
                // Backspace edits the TIP's pre-edit text as well as committed editor text.
                // Microsoft Pinyin does not handle Shift+Backspace like plain Backspace during
                // composition, so the surface's latched Shift must not modify this key. Keep
                // the latch intact so the next character still uses the requested casing.
                var shifted = key.Key != Key.Back
                    && ShiftState is SoftKeyboardShiftState.Once or SoftKeyboardShiftState.Locked;

                if (TrySendVirtualKey(key.Key, shifted)
                    && ShiftState == SoftKeyboardShiftState.Once
                    && IsCharacterKey(key.Key))
                {
                    SetValue(ShiftStatePropertyKey, SoftKeyboardShiftState.Off);
                }

                break;

            case SoftKeyboardKeyKind.Text:
                _ = SendActionTextAsync(key.Text ?? string.Empty);
                break;

            case SoftKeyboardKeyKind.Shift:
                ToggleShift();
                break;

            case SoftKeyboardKeyKind.ChangePage:
                ShowPage(key.Page);
                break;

            case SoftKeyboardKeyKind.InputMethods:
                ShowInputMethodMenu();
                break;

            case SoftKeyboardKeyKind.SwitchInputMode:
                SwitchInputMode();
                break;

            case SoftKeyboardKeyKind.Close:
                CloseKeyboard();
                break;

            case SoftKeyboardKeyKind.Custom:
                // todo: 这里能这么笃定地确定 Action 一定非空？
                ExecuteCustomAction(key.Action!);
                break;
        }
    }

    private void ToggleShift()
    {
        var now     = Environment.TickCount;
        var elapsed = unchecked(now - _lastShiftClickTick);

        _lastShiftClickTick = now;

        var next = ShiftState == SoftKeyboardShiftState.Once && elapsed is >= 0 and <= 500
            ? SoftKeyboardShiftState.Locked
            : ShiftState switch
            {
                SoftKeyboardShiftState.Off => SoftKeyboardShiftState.Once,
                _                          => SoftKeyboardShiftState.Off,
            };

        SetValue(ShiftStatePropertyKey, next);
    }

    /// <summary>
    /// Captures the clicked key's physical anchor before asynchronously canceling composition.
    /// </summary>
    private void SwitchInputMode()
    {
        if (_inputObservation.IsObserving)
        {
            Debug.WriteLine("[SoftKeyboard.InputMode.Command] Click ignored; reason=ActivationWindow.");
            return;
        }

        var anchor = _inputMethodMenuAnchor as FrameworkElement ?? this;
        var bounds = new Rect();

        if (PresentationSource.FromVisual(anchor) is not null)
        {
            bounds = new Rect(
                anchor.PointToScreen(new Point()),
                anchor.PointToScreen(new Point(anchor.ActualWidth, anchor.ActualHeight))
            );
        }

        RunAfterActiveCompositionCancellation(() => SwitchInputModeCore(bounds));
    }

    /// <summary>
    /// Rediscovers the current command after focus preparation; never invokes two command routes.
    /// </summary>
    private void SwitchInputModeCore(Rect screenBounds)
    {
        if (_inputObservation.IsObserving || !PrepareTargetForInput()
            || _inputObservation.IsObserving || InputModeButton is null)
        {
            return;
        }

        var attempted      = false;
        var sessionVersion = _inputMethodSessionVersion;
        var inputVersion   = _inputOperationVersion;
        try
        {
            RunInputMethodCommand((session, isCurrent) =>
            {
                var actual = session.GetSnapshot().ActiveInputMethod;

                if (!isCurrent() || !HaveSameInputMethod(actual, _observedModeProfile)
                    || !HaveSameInputMethod(actual, ActiveInputMethod))
                {
                    if (isCurrent())
                    {
                        BeginInputObservation(actual);
                    }

                    return false;
                }

                if (actual is null || _inputObservation.IsObserving)
                {
                    return false;
                }

                attempted = true;

                var result = session is ISoftKeyboardInputModeButtonSession platform
                    ? platform.TryInvokeInputModeButton(actual, screenBounds)
                    : SoftKeyboardInputModeCommandResult.Unavailable;

                Debug.WriteLine(
                    "[SoftKeyboard.InputMode.Command] PLATFORM returned; "
                    + $"profileId={actual.ProfileId}, result={result}."
                );

                if (result != SoftKeyboardInputModeCommandResult.Unavailable || !isCurrent()
                    || _inputObservation.IsObserving)
                {
                    return result == SoftKeyboardInputModeCommandResult.Accepted;
                }

                var adapter = ResolveObservedInputModeAdapter(
                    actual,
                    _observedInputCulture ?? InputLanguageManager.Current.CurrentInputLanguage,
                    ActiveTarget
                );

                var observation  = _inputObservation.Observation;
                var contribution = adapter?.GetContribution(actual, observation);

                if (!isCurrent() || _inputObservation.IsObserving
                    || contribution?.Transition is not { } transition)
                {
                    return false;
                }

                var accepted = adapter!.TrySwitchMode(actual, observation, transition);

                Debug.WriteLine(
                    "[SoftKeyboard.InputMode.Command] EXTENSION returned; "
                    + $"profileId={actual.ProfileId}, transition={transition.Id}, accepted={accepted}."
                );

                return accepted;
            });
        }
        finally
        {
            if (attempted && sessionVersion == _inputMethodSessionVersion
                && inputVersion == _inputOperationVersion && _observationInitialized)
            {
                _inputObservation.AfterClick();
            }
        }

        RequestSystemKeyboardHide();
    }

    private bool TrySendVirtualKey(Key key, bool shift)
    {
        return TrySendVirtualKey(key, shift ? ModifierKeys.Shift : ModifierKeys.None);
    }

    private bool TrySendVirtualKey(Key key, ModifierKeys modifiers)
    {
        if (!PrepareTargetForInput())
        {
            return false;
        }

        var sent = _platform.Input.SendVirtualKey(key, modifiers);
        if (sent)
        {
            QueueCandidateBridgeRefresh();
        }

        RequestSystemKeyboardHide();
        return sent;
    }

    /// <summary>
    /// Sends literal text only when the validated target has no active composition. A pending
    /// composition requests cancellation and returns false so callers can observe actual delivery.
    /// </summary>
    private bool TrySendText(string text)
    {
        if (!PrepareTargetForInput())
        {
            return false;
        }

        // A synchronous caller cannot wait for TSF to process Escape. Request cancellation but
        // report that no text was sent; asynchronous callers use SendActionTextAsync to wait.
        if (_activeTextComposition is not null)
        {
            _ = TryCancelActiveComposition();
            return false;
        }

        var sent = _platform.Input.SendUnicode(text);
        if (sent)
        {
            QueueCandidateBridgeRefresh();
        }

        RequestSystemKeyboardHide();
        return sent;
    }

    private bool PrepareTargetForInput()
    {
        var target  = Target ?? ActiveTarget;
        var adapter = target is null ? null : GetAdapter(target);

        if (!IsLoaded || !IsEnabled || !IsVisible || !IsOpen
            || target is not DependencyObject targetObject
            || adapter is null
            || !IsWithinEffectiveScope(targetObject)
            || !adapter.CanReceiveInput(target)
            || !IsWindowForeground())
        {
            return false;
        }

        if (!adapter.TryFocus(target) || !ReferenceEquals(Keyboard.FocusedElement, target))
        {
            return false;
        }

        SetValue(ActiveTargetPropertyKey, target);

        // The candidate sink must be advised before the injected key can open a TSF candidate UI.
        // This also retries the normal focus path, where WPF may have created TextServicesHost only
        // after the GotKeyboardFocus preprocessing callback completed.
        RequestCandidateRefresh();

        // Focus callbacks can disable, hide, or invalidate the editor or keyboard synchronously.
        return IsSystemKeyboardSuppressionRequired;
    }

    private bool IsWindowForeground()
    {
        return _platform.Input.IsHostForeground(GetHostWindowHandle());
    }

    /// <summary>
    /// Resolves the HWND that owns the current input target without requiring a WPF
    /// <see cref="Window"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="PresentationSource.FromDependencyObject(DependencyObject)"/> covers ordinary
    /// windows, WinForms <see cref="HwndSource"/> islands created by ElementHost, and standalone
    /// HwndSource roots. The target is preferred because injected input and InputPane requests must
    /// be guarded against the native host that actually owns the editor.
    /// </remarks>
    private IntPtr GetHostWindowHandle()
    {
        return GetHostHwndSource()?.Handle ?? IntPtr.Zero;
    }

    /// <summary>
    /// Gets the connected HWND presentation source for the target or keyboard surface.
    /// </summary>
    private HwndSource? GetHostHwndSource()
    {
        if ((Target ?? ActiveTarget) is DependencyObject targetObject
            && PresentationSource.FromDependencyObject(targetObject) is HwndSource targetSource)
        {
            return targetSource;
        }

        if (TargetScope is not null
            && PresentationSource.FromDependencyObject(TargetScope) is HwndSource scopeSource)
        {
            return scopeSource;
        }

        return PresentationSource.FromDependencyObject(this) as HwndSource;
    }
}
