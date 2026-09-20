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
    /// <summary>
    /// Registers shared observation and discovers initial presentation when the control loads.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
#if DEBUG
        using var performance = new SoftKeyboardPerformanceScope("Loaded", this);
#endif
        _session = SoftKeyboardDispatcherSession.GetForCurrentDispatcher();
        _session.Register(this);

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

    /// <summary>
    /// Cancels pending input and observation work and releases the loaded control's sessions.
    /// </summary>
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

    /// <summary>
    /// Releases hidden input sessions and refreshes presentation when ancestor visibility returns.
    /// </summary>
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
            _session?.EnsureInputMethodMonitoring();

            ReleaseSystemKeyboardSuppression(tryRestore: true);

            return;
        }

        // IsVisible includes ancestor visibility, unlike the local Visibility value. Reacquire the
        // still-active TSF element when a Page/dialog region becomes visible without requiring the
        // user to type another character.
        if (IsLoaded)
        {
            RefreshInputMethodState();

            if (IsOpen && ActiveTarget is not null)
            {
                RequestCandidateRefresh();
                RequestSystemKeyboardHide();
            }
        }
    }

    /// <summary>
    /// Reconnects monitoring and refreshes the foreground thread's input-method presentation.
    /// </summary>
    private void OnWindowActivated(object? sender, EventArgs args)
    {
        _session?.EnsureInputMethodMonitoring();

        if (IsOpen)
        {
            // Deactivation occurs after the HWND has lost foreground ownership, when SendInput is
            // no longer safe. Cancel any composition that survived the app switch now, then let the
            // immediate refresh either reacquire a still-valid TSF element or observe its
            // cancellation.
            _ = TryCancelActiveComposition();

            RequestSystemKeyboardHide();
            QueueCandidateBridgeRefresh();
        }

        RefreshInputMethodState();
    }

    /// <summary>
    /// Invalidates pending input and observations when the host loses foreground ownership.
    /// </summary>
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

    /// <summary>
    /// Releases disabled input sessions and rediscovers presentation when the control is enabled.
    /// </summary>
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
            _session?.EnsureInputMethodMonitoring();

            ReleaseSystemKeyboardSuppression(tryRestore: true);
        }
        else if (IsLoaded)
        {
            RefreshInputMethodState();

            if (IsOpen)
            {
                RequestCandidateRefresh();
                RequestSystemKeyboardHide();
            }
        }
    }
}
