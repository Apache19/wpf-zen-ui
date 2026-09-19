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

        _window = Window.GetWindow(this);

        if (_window is not null)
        {
            _window.Activated   += OnWindowActivated;
            _window.Deactivated += OnWindowDeactivated;
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
        ResetObservedPlatformState();

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
            _window.Activated   -= OnWindowActivated;
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
            ResetObservedPlatformState();

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
            RefreshInputMethodState();
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
        ResetObservedPlatformState();

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
            ResetObservedPlatformState();

            _ = TryCancelActiveComposition();

            CancelKeyInteraction();
            CloseFlyouts();

            ResetCandidateSession();

            ReleaseSystemKeyboardSuppression(tryRestore: true);
        }
        else if (IsLoaded && IsOpen)
        {
            RefreshInputMethodState();
            RequestCandidateRefresh();
            RequestSystemKeyboardHide();
        }
    }
}
