// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Provides immediate access to the input methods associated with the keyboard's dispatcher.
/// </summary>
/// <remarks>
/// All members run on the owning dispatcher and must return promptly. Sources with external data
/// must maintain their own cache and return a snapshot; they must not block waiting for background
/// work. Activation reports whether the native request was accepted, not whether future composition
/// will succeed.
/// </remarks>
public interface ISoftKeyboardInputMethodSession : IDisposable
{
    /// <summary>
    /// Reads the current catalog and active profile without changing native input state.
    /// </summary>
    /// <returns>An immutable snapshot of the currently available profiles.</returns>
    SoftKeyboardInputMethodSnapshot GetSnapshot();

    /// <summary>
    /// Requests activation of a profile from this session's catalog.
    /// </summary>
    /// <param name="inputMethod">The profile to activate.</param>
    /// <returns>True when the native activation request was accepted.</returns>
    bool TryActivate(SoftKeyboardInputMethod inputMethod);

}
