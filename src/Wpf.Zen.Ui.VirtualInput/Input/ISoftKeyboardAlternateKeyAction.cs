// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Lets a custom key route its alternative values through the IME instead of literal Unicode insertion.
/// </summary>
/// <remarks>
/// Alternatives remain presentation strings on the immutable key.
/// </remarks>
public interface ISoftKeyboardAlternateKeyAction : ISoftKeyboardKeyAction
{
    /// <summary>
    /// Executes a selected alternative through the target-validated context.
    /// </summary>
    /// <param name="context">Dispatcher-bound input services; must not be retained.</param>
    /// <param name="alternative">A value from the invoked key's Alternates collection.</param>
    void ExecuteAlternate(SoftKeyboardActionContext context, string alternative);
}
