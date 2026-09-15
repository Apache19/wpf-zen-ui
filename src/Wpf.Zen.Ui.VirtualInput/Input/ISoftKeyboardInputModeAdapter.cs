// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Interprets and switches the native mode contract of a particular text service.
/// </summary>
/// <remarks>
/// TSF standardizes the compartments that carry open and conversion values, but a text service can
/// use additional values or expose service-specific transitions. The adapter owns that
/// interpretation and may explicitly attach a replacement button to the returned mode. Merely
/// interpreting state leaves the platform button presentation unchanged. Calls run on the keyboard
/// Dispatcher, must not block, and must not activate another profile or change focus.
/// </remarks>
public interface ISoftKeyboardInputModeAdapter
{
    /// <summary>
    /// Reads and interprets the current mode of the active input method.
    /// </summary>
    /// <param name="inputMethod">The active input-method profile.</param>
    /// <returns>The interpreted mode, or null when no coherent state can be observed.</returns>
    SoftKeyboardInputMode? GetMode(SoftKeyboardInputMethod inputMethod);

    /// <summary>
    /// Requests a transition using the text service's mode-switch contract.
    /// </summary>
    /// <param name="inputMethod">The active input-method profile.</param>
    /// <param name="currentMode">The last mode observed for that profile.</param>
    /// <returns>Whether the mode-switch request was accepted.</returns>
    /// <remarks>
    /// This is intentionally a transition rather than an On/Off setter. Services such as Microsoft
    /// Japanese IME expose more than two modes, while vendor services may use non-Boolean state
    /// codes.
    /// The displayed mode is always read back after the request.
    /// </remarks>
    bool TrySwitchMode(SoftKeyboardInputMethod inputMethod, SoftKeyboardInputMode currentMode);
}
