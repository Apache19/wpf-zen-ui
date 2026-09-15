// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Exposes an input method's platform mode command independently from interpreted mode state.
/// </summary>
/// <remarks>
/// Implement this optional session capability when the platform supplies a command presentation
/// and invocation contract without supplying a reliable semantic mode. Calls run on the owning
/// Dispatcher and must return promptly.
/// </remarks>
public interface ISoftKeyboardInputModeButtonSession
{
    /// <summary>
    /// Reads the active profile's current mode-command presentation.
    /// </summary>
    /// <param name="inputMethod">The active profile obtained from the current snapshot.</param>
    /// <returns>
    /// A presentation descriptor, or null when either the command or content is absent.
    /// </returns>
    SoftKeyboardInputModeButton? GetInputModeButton(SoftKeyboardInputMethod inputMethod);

    /// <summary>
    /// Invokes the active profile's platform mode command.
    /// </summary>
    /// <param name="inputMethod">The active profile obtained from the current snapshot.</param>
    /// <returns>True when the platform command accepted the invocation; otherwise false.</returns>
    bool TryInvokeInputModeButton(SoftKeyboardInputMethod inputMethod);
}
