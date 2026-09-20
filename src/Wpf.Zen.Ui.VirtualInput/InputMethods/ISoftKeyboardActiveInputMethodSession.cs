// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Supplies current profile identity without enumerating the input-method catalog.
/// </summary>
/// <remarks>
/// Sessions implementing this capability serve routine observations and input validation through
/// this method. GetSnapshot is reserved for explicit catalog requests. Sessions without this
/// capability supply both through GetSnapshot. Calls run on the owning Dispatcher.
/// </remarks>
public interface ISoftKeyboardActiveInputMethodSession : ISoftKeyboardInputMethodSession
{
    /// <summary>
    /// Reads the currently active profile, or null if its identity is unavailable.
    /// </summary>
    /// <returns>A current profile description, independent of the last enumerated catalog.</returns>
    SoftKeyboardInputMethod? GetActiveInputMethod();
}
