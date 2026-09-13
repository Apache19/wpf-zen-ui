// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

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
                else if (button.Tag is SoftKeyboardKeyKind.ToggleIme)
                {
                    ToggleIme();
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
                else if (TrySendText(text) && ShiftState == SoftKeyboardShiftState.Once)
                {
                    SetValue(ShiftStatePropertyKey, SoftKeyboardShiftState.Off);
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
                if (TrySendText(key.Text ?? string.Empty) && ShiftState == SoftKeyboardShiftState.Once)
                {
                    SetValue(ShiftStatePropertyKey, SoftKeyboardShiftState.Off);
                }

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

            case SoftKeyboardKeyKind.ToggleIme:
                ToggleIme();
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

    private void ToggleIme()
    {
        RunAfterActiveCompositionCancellation(ToggleImeCore);
    }

    /// <summary>
    /// Writes the opposite of the last observed input mode through exactly one state mechanism.
    /// </summary>
    private void ToggleImeCore()
    {
        if (!PrepareTargetForInput())
        {
            return;
        }

        var inputMethod  = ActiveInputMethod;
        var currentState = ImeState;

        if (inputMethod?.SupportsImeToggle != true
            || currentState is not (InputMethodState.On or InputMethodState.Off))
        {
            return;
        }

        var requestedState = currentState == InputMethodState.On
            ? InputMethodState.Off
            : InputMethodState.On;

        _ = RunInputMethodCommand((session, isCurrent) =>
        {
            if (!isCurrent())
            {
                return false;
            }

            var adapter  = ResolveInputStateAdapter();
            var accepted = adapter?.TrySetState(inputMethod, requestedState)
                ?? session.TrySetState(requestedState);

            return accepted && isCurrent();
        });

        QueueInputMethodStateRefresh();
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

    private bool TrySendText(string text)
    {
        if (!PrepareTargetForInput())
        {
            return false;
        }

        // KEYEVENTF_UNICODE bypasses the active TIP. Escape is queued first so mutable pre-edit
        // text cannot be interleaved with symbols or long-press alternatives.
        if (!TryCancelActiveComposition())
        {
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
        var window = _window ?? Window.GetWindow(this);
        if (window is null)
        {
            return false;
        }

        var host = new WindowInteropHelper(window).Handle;

        return _platform.Input.IsHostForeground(host);
    }
}