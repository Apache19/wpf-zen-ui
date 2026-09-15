// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Owns suppression and safe restoration of the window-scoped Windows input pane.
/// </summary>
public partial class SoftKeyboard
{
    private void RequestSystemKeyboardHide()
    {
        if (!IsSystemKeyboardSuppressionRequired)
        {
            ReleaseSystemKeyboardSuppression(tryRestore: true);
            return;
        }

        // UIElement.Focus calls TipTsfHelper.Show AFTER Keyboard.Focus has raised GotKeyboardFocus.
        // A hide inside that event alone loses to WPF's subsequent show. Background also runs after
        // the Input-priority operation that TipTsfHelper queues when dispatcher processing is disabled.
        // Coalesce requests, but never throttle away this final hide when editors change quickly.
        if (_inputPaneHideOperation is null)
        {
            _inputPaneHideOperation = Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(
                    () =>
                    {
                        _inputPaneHideOperation = null;
                        HideSystemKeyboardIfRequired();
                    }
                )
            );
        }

        // Windows can finish its own focus tracking asynchronously, after WPF's dispatcher work.
        // Keep a low-frequency guard only for a live foreground editor. TryHide is advisory, so a
        // successful request is not a permanent policy and must not disable subsequent checks.
        _inputPaneHideTimer ??= new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            OnInputPaneHideTimerTick,
            Dispatcher
        );

        _inputPaneHideTimer.Start();

        var now     = Environment.TickCount;
        var elapsed = unchecked(now - _lastInputPaneHideTick);

        if (_lastInputPaneHideTick != 0 && elapsed is >= 0 and < 250)
        {
            return;
        }

        _lastInputPaneHideTick = now;

        HideSystemKeyboardIfRequired();
    }

    /// <summary>
    /// Rechecks ownership at execution time so deferred work cannot hide another editor's input UI.
    /// No global Windows setting, WPF AppContext switch, or input-method state is changed.
    /// </summary>
    private void HideSystemKeyboardIfRequired()
    {
        if (!IsSystemKeyboardSuppressionRequired)
        {
            ReleaseSystemKeyboardSuppression(tryRestore: false);
            return;
        }

        var window = _window ?? Window.GetWindow(this);
        var handle = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
        var result = _platform.InputPane.TryHide(handle);

        if (result == SoftKeyboardSystemKeyboardSuppressionState.Hidden)
        {
            _systemKeyboardHiddenByThisControl = true;
        }

        SetValue(SystemKeyboardSuppressionStatePropertyKey, result);

        if (result == SoftKeyboardSystemKeyboardSuppressionState.Unavailable)
        {
            // Unsupported WinRT platforms should not repeatedly attempt activation. A later focus
            // or visibility transition may retry; ordinary Windows rejection remains recoverable.
            _inputPaneHideTimer?.Stop();
        }
    }

    private void OnInputPaneHideTimerTick(object? sender, EventArgs args)
    {
        HideSystemKeyboardIfRequired();
    }

    /// <summary>
    /// Gets a value indicating whether this control currently has enough live context to suppress
    /// the HWND input pane.
    /// </summary>
    /// <remarks>
    /// IsVisible includes ancestor visibility. Requiring the target to remain keyboard-focused avoids
    /// hiding Windows input UI while focus is on a command, another form, or another application; a
    /// soft-key invocation can still restore the remembered editor before requesting suppression.
    /// </remarks>
    internal bool IsSystemKeyboardSuppressionRequired
    {
        get
        {
            if (!IsLoaded
                || !IsEnabled
                || !IsVisible
                || !IsOpen
                || ActiveTarget is not DependencyObject targetObject
                || !ReferenceEquals(Keyboard.FocusedElement, ActiveTarget)
                || !IsWithinEffectiveScope(targetObject)
                || GetAdapter(ActiveTarget)?.CanReceiveInput(ActiveTarget) != true)
            {
                return false;
            }

            return IsWindowForeground();
        }
    }

    /// <summary>
    /// Ends this control's ownership of a successful InputPane hide request and optionally lets
    /// Windows restore the pane for the still-focused editor.
    /// </summary>
    private void ReleaseSystemKeyboardSuppression(bool tryRestore)
    {
        _inputPaneHideOperation?.Abort();
        _inputPaneHideOperation = null;
        _inputPaneHideTimer?.Stop();

        var shouldRestore = tryRestore
            && _systemKeyboardHiddenByThisControl
            && (_session?.IsSystemKeyboardSuppressionRequiredByOther(this) != true)
            && CanRestoreSystemKeyboard();

        _systemKeyboardHiddenByThisControl = false;
        _lastInputPaneHideTick = 0;

        SetValue(
            SystemKeyboardSuppressionStatePropertyKey,
            SoftKeyboardSystemKeyboardSuppressionState.NotRequested
        );

        if (!shouldRestore)
        {
            return;
        }

        var window = _window ?? Window.GetWindow(this);
        var handle = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;

        _ = _platform.InputPane.TryShow(handle);
    }

    /// <summary>
    /// Validates the conservative subset of WPF's own TipTsfHelper.Show preconditions available to
    /// the control without reflecting private stylus policy or cached WinRT state.
    /// </summary>
    private bool CanRestoreSystemKeyboard()
    {
        if (ActiveTarget is not DependencyObject targetObject
            || !ReferenceEquals(Keyboard.FocusedElement, ActiveTarget)
            || !IsWithinEffectiveScope(targetObject)
            || GetAdapter(ActiveTarget)?.CanReceiveInput(ActiveTarget) != true)
        {
            return false;
        }

        return IsWindowForeground();
    }

    /// <summary>
    /// Gets a value indicating whether the live editor and visible surface can own candidates.
    /// Remembered targets may restore focus for a key, but must never capture another editor's IME UI.
    /// </summary>
    internal bool CanPresentCandidates
    {
        get =>
            InputMode?.IsNativeInput == true
            && ActiveInputMethod is not null
            && IsSystemKeyboardSuppressionRequired;
    }
}
