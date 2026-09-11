// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Adapts a custom WPF editor to the focus and input-scope contract used by <see cref="SoftKeyboard"/>.
/// </summary>
/// <remarks>
/// The keyboard still delivers ordinary keyboard input through Windows. An adapter declares eligibility,
/// read-only state, input scope, and how focus should be restored; it does not receive or store typed text.
/// </remarks>
public interface ISoftKeyboardTargetAdapter
{
    /// <summary>
    /// Returns whether this adapter recognizes <paramref name="target"/>.
    /// </summary>
    bool IsSupported(IInputElement target);

    /// <summary>
    /// Returns whether <paramref name="target"/> can currently accept user input.
    /// </summary>
    bool CanReceiveInput(IInputElement target);

    /// <summary>
    /// Gets the semantic input scope for <paramref name="target"/>.
    /// </summary>
    InputScopeNameValue GetInputScope(IInputElement target);

    /// <summary>
    /// Attempts to give keyboard focus to <paramref name="target"/>.
    /// </summary>
    bool TryFocus(IInputElement target);
}
