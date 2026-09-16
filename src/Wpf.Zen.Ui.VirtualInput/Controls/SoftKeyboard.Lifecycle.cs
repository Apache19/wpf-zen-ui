// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Coordinates dispatcher, window, and platform-service ownership across load and visibility
/// changes.
/// </summary>
public partial class SoftKeyboard
{
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _session = SoftKeyboardDispatcherSession.GetForCurrentDispatcher();
        _session.Register(this);

        _ = EnsureCandidateSession();

        // todo: 在没有 Window 的环境下（例如 WinForms/HostElement），如何让此机制生效？
        _window = Window.GetWindow(this);

        if (_window is not null)
        {
            _window.Activated += OnWindowActivated;
            _window.Deactivated += OnWindowDeactivated;
            _window.SourceInitialized += OnWindowSourceInitialized;
            AttachWindowMessageHook();
        }

        ResolveTarget();
        RefreshInputMode();

        if (IsOpen)
        {
            RequestSystemKeyboardHide();
            RequestCandidateRefresh();
            QueueCandidateBridgeRefresh();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _inputOperationVersion++;

        // Unloading commonly means the Window/Page itself is going away. Showing a system keyboard
        // for a disappearing HWND would be intrusive and can target the next foreground window, so
        // only discard this control's suppression ownership here.

        _ = TryCancelActiveComposition();

        ReleaseSystemKeyboardSuppression(tryRestore: false);

        _session?.Unregister(this);

        ResetCandidateSession();
        ResetInputMethodSession();

        _session = null;

        if (_window is not null)
        {
            _window.Activated -= OnWindowActivated;
            _window.Deactivated -= OnWindowDeactivated;
            _window.SourceInitialized -= OnWindowSourceInitialized;
            DetachWindowMessageHook();
            _window = null;
        }

        CancelKeyInteraction();
        CloseFlyouts();
        ReplaceCandidates([], SoftKeyboardCandidatePresentationState.None);
        ClearActiveComposition();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (!IsVisible)
        {
            _inputOperationVersion++;

            _ = TryCancelActiveComposition();

            CancelKeyInteraction();
            CloseFlyouts();

            ResetCandidateSession();

            ReleaseSystemKeyboardSuppression(tryRestore: true);

            return;
        }

        // IsVisible includes ancestor visibility, unlike the local Visibility value. Reacquire the
        // still-active TSF element when a Page/dialog region becomes visible without requiring the
        // user to type another character.
        if (IsLoaded && IsOpen && ActiveTarget is not null)
        {
            RequestCandidateRefresh();
            RequestSystemKeyboardHide();
        }
    }

    private void OnWindowActivated(object? sender, EventArgs args)
    {
        if (IsOpen)
        {
            // Deactivation occurs after the HWND has lost foreground ownership, when SendInput is
            // no longer safe. Cancel any composition that survived the app switch now, then let the
            // immediate refresh either reacquire a still-valid TSF element or observe its
            // cancellation.
            _ = TryCancelActiveComposition();

            RefreshInputMethodState();
            RequestSystemKeyboardHide();
            QueueCandidateBridgeRefresh();
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs args)
    {
        _inputOperationVersion++;

        // SendInput targets the foreground thread. Ending every pending gesture here prevents a
        // held key from continuing into another process during an Alt+Tab or tablet app switch.
        CancelKeyInteraction();
        CloseFlyouts();
        ReleaseSystemKeyboardSuppression(tryRestore: false);

        ResetCandidateSession();
    }

    private void OnWindowSourceInitialized(object? sender, EventArgs args)
    {
        AttachWindowMessageHook();
    }

    /// <summary>
    /// Observes exact HKL changes and legacy IMM mode notifications for this HWND.
    /// </summary>
    private void AttachWindowMessageHook()
    {
        if (_windowSource is not null || _window is null)
        {
            return;
        }

        _windowSource = PresentationSource.FromVisual(_window) as HwndSource;
        _windowSource?.AddHook(OnWindowMessage);
    }

    private void DetachWindowMessageHook()
    {
        _windowSource?.RemoveHook(OnWindowMessage);
        _windowSource = null;
    }

    private IntPtr OnWindowMessage(
        IntPtr window,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        const int WmInputLanguageChange = 0x0051;
        const int WmImeNotify           = 0x0282;
        const int ImnSetConversionMode  = 0x0006;
        const int ImnSetOpenStatus      = 0x0008;

        if (message == WmInputLanguageChange)
        {
            OnSessionInputMethodChanged(
                new InputMethodChange(
                    InputMethodChangeKind.InputLanguage,
                    KeyboardLayout: lParam
                )
            );
        }
        else if (message == WmImeNotify && wParam.ToInt32() == ImnSetOpenStatus)
        {
            if (ImmInputContextState.TryReadOpen(window, out var open))
            {
                OnSessionInputMethodChanged(
                    new InputMethodChange(
                        InputMethodChangeKind.ImeOpen,
                        StateValue: open
                    )
                );
            }
        }
        else if (message == WmImeNotify && wParam.ToInt32() == ImnSetConversionMode)
        {
            if (ImmInputContextState.TryReadConversion(window, out var conversion))
            {
                OnSessionInputMethodChanged(
                    new InputMethodChange(
                        InputMethodChangeKind.ImeConversion,
                        StateValue: conversion
                    )
                );
            }
        }

        return IntPtr.Zero;
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (!IsEnabled)
        {
            _inputOperationVersion++;

            _ = TryCancelActiveComposition();

            CancelKeyInteraction();
            CloseFlyouts();

            ResetCandidateSession();

            ReleaseSystemKeyboardSuppression(tryRestore: true);
        }
        else if (IsLoaded && IsOpen)
        {
            RequestCandidateRefresh();
            RequestSystemKeyboardHide();
        }
    }
}
