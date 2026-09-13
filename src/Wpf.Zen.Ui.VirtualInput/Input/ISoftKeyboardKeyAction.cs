// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Implements a language or application key function without adding a built-in key kind.
/// </summary>
/// <remarks>
/// Called synchronously on the keyboard dispatcher after target eligibility and focus validation.
/// Use the supplied context for input and navigation; it expires on return and must not be retained
/// for asynchronous work. Actions may be shared by layouts and must not retain an editor reference.
/// Native conversion-mode changes belong in ISoftKeyboardInputStateAdapter; layout construction
/// must not mutate IME state. Exceptions from application action code propagate to the caller.
/// </remarks>
public interface ISoftKeyboardKeyAction : ISoftKeyboardAction
{
    /// <summary>
    /// Executes the function using the current keyboard's guarded services.
    /// </summary>
    /// <param name="context">
    /// Services valid only for the duration of this call.
    /// </param>
    void Execute(SoftKeyboardActionContext context);
}
