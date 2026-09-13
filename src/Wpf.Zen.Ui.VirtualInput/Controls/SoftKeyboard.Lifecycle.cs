// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Coordinates dispatcher, window, and platform-service ownership across load and visibility changes.
/// </summary>
public partial class SoftKeyboard
{
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _session = SoftKeyboardDispatcherSession.GetForCurrentDispatcher();
        _session.Register(this);

        _ = EnsureCandidateSession();

        // Loaded runs on this control's dispatcher; WPF creates InputMethod.Current for that dispatcher.
        if (InputMethod.Current is { } inputMethod)
        {
            inputMethod.StateChanged += OnInputMethodStateChanged;
        }

        StartInputStateObservation();

        // todo: 在没有 Window 的环境下（例如 WinForms/HostElement），如何让此机制生效？
        _window = Window.GetWindow(this);

        if (_window is not null)
        {
            _window.Activated   += OnWindowActivated;
            _window.Deactivated += OnWindowDeactivated;
        }

        ResolveTarget();
        RefreshImeState();

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

        if (InputMethod.Current is { } inputMethod)
        {
            inputMethod.StateChanged -= OnInputMethodStateChanged;
        }

        _inputStateTimer?.Stop();

        _session = null;

        if (_window is not null)
        {
            _window.Activated -= OnWindowActivated;
            _window.Deactivated -= OnWindowDeactivated;
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
            // queued refresh either reacquire a still-valid TSF element or observe its cancellation.
            _ = TryCancelActiveComposition();

            QueueInputMethodStateRefresh();
            RequestSystemKeyboardHide();
            QueueCandidateBridgeRefresh();
        }
    }

    private void OnInputMethodStateChanged(object sender, InputMethodStateChangedEventArgs args)
    {
        if (args.IsImeStateChanged || args.IsImeConversionModeChanged)
        {
            RefreshImeState();
            RequestCandidateRefresh();
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
