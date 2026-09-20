// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Maintains composition cancellation as a guarded asynchronous input boundary.
/// </summary>
public partial class SoftKeyboard
{
    private int _compositionStartVersion;

    /// <summary>
    /// Identifies the current observed composition stream for candidate lifetime checks. A new
    /// start changes this token even when the TIP reuses its native candidate element and text.
    /// </summary>
    internal object CompositionIdentity { get; private set; } = new();

    /// <summary>
    /// Asks the active TIP to discard its mutable pre-edit transaction by sending Escape to the
    /// editor that originated the matching WPF composition.
    /// </summary>
    /// <remarks>
    /// TSF TerminateComposition only removes the composing property and may preserve or commit the
    /// covered text. Escape retains the input method's own cancellation semantics (for example,
    /// Microsoft Pinyin removes its unconfirmed phonetic text). The request is sent only while the
    /// originating editor still owns keyboard focus and its Window owns the foreground; otherwise
    /// SendInput could affect another target.
    /// </remarks>
    private bool TryCancelActiveComposition(IInputElement? expectedTarget = null)
    {
        if (_activeTextComposition is null || _compositionTarget is null)
        {
            return true;
        }

        if (_compositionCancellationRequested)
        {
            // A cancellation request is already pending for the observed composition.
            return true;
        }

        var target = expectedTarget ?? _compositionTarget;
        if (!ReferenceEquals(target, _compositionTarget))
        {
            return true;
        }

        if (!ReferenceEquals(Keyboard.FocusedElement, target)
            || !IsWindowForeground()
            || !_platform.Input.SendVirtualKey(Key.Escape, ModifierKeys.None))
        {
            return false;
        }

        // Retain the WPF composition until its final event. Callers that send literal text or
        // change input-method state must wait for that acknowledgement. If the TIP ignores Escape,
        // reset the request guard after input processing so the next boundary can retry.
        _compositionCancellationRequested = true;

        var requestedComposition = _activeTextComposition;

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(
                () =>
                {
                    if (ReferenceEquals(_activeTextComposition, requestedComposition))
                    {
                        _compositionCancellationRequested = false;
                    }
                }
            )
        );

        return true;
    }

    /// <summary>
    /// Defers an operation that mutates TSF input-method state until WPF has observed the requested
    /// cancellation completing.
    /// </summary>
    /// <remarks>
    /// SendInput only places Escape in the foreground thread's input queue; it does not process the
    /// key before returning. Changing the active TIP or its open compartment in the same callback
    /// could therefore run ahead of cancellation and cause the outgoing TIP to preserve raw text.
    /// ContextIdle runs after ordinary input dispatch. If no final composition event arrives, the
    /// operation is deliberately abandoned instead of converting uncertain pre-edit text.
    /// </remarks>
    private void RunAfterActiveCompositionCancellation(Action operation)
    {
        if (_activeTextComposition is null)
        {
            operation();
            return;
        }

        var isInputCurrent     = CaptureInputLifetime();
        var compositionVersion = _compositionStartVersion;
        var operationTarget    = ActiveTarget;

        if (!TryCancelActiveComposition())
        {
            return;
        }

        // Completion belongs to the profile, editor and composition captured before Escape.
        // A new composition may both start and finish before this idle callback runs.

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(
                () =>
                {
                    if (!isInputCurrent() || compositionVersion != _compositionStartVersion
                        || !ReferenceEquals(operationTarget, Keyboard.FocusedElement))
                    {
                        return;
                    }

                    // A still-active composition means either the TIP ignored Escape or a fresh
                    // composition has already started. Neither case is safe for changing TSF state.
                    if (_activeTextComposition is not null)
                    {
                        return;
                    }

                    operation();
                }
            )
        );
    }
}
