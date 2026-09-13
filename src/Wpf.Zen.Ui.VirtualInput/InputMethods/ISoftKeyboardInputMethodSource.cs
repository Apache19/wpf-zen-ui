// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Creates a dispatcher-owned input-method catalog and activation session.
/// </summary>
/// <remarks>
/// Custom sessions should implement <see cref="ISoftKeyboardInputMethodObservableSession"/> when
/// their catalog or state can change externally. Other sessions use periodic recovery observation.
/// </remarks>
public interface ISoftKeyboardInputMethodSource
{
    /// <summary>
    /// Creates a session owned by one soft keyboard.
    /// </summary>
    /// <returns>A new session.</returns>
    ISoftKeyboardInputMethodSession CreateSession();
}