// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Adapts a text service whose native/direct mode does not follow WPF's standard IME compartments.</summary>
/// <remarks>
/// The default keyboard combines the open and Native conversion flags. Applications can supply
/// this adapter for vendor-specific state APIs without replacing key injection, TSF candidate
/// ownership or editor focus management. Calls run on the keyboard Dispatcher and must not block.
/// The keyboard observes state on WPF notifications and at one-second intervals while it owns
/// foreground editor focus. An adapter must not change focus or activate a different profile.
/// </remarks>
public interface ISoftKeyboardInputStateAdapter
{
    /// <summary>Reads the current native/direct mode for the active profile.</summary>
    /// <param name="inputMethod">The observed profile, or null when no profile is exposed.</param>
    /// <returns>On for native input, Off for direct input, or DoNotCare when state is unavailable.</returns>
    InputMethodState GetState(SoftKeyboardInputMethod? inputMethod);

    /// <summary>Requests a native/direct transition after the keyboard has cancelled composition and restored editor focus.</summary>
    /// <param name="inputMethod">The active profile; may be null when Windows exposes no identity.</param>
    /// <param name="state">On or Off; the keyboard never requests DoNotCare.</param>
    /// <returns>Whether the request was accepted. Displayed state is always read back separately.</returns>
    bool TrySetState(SoftKeyboardInputMethod? inputMethod, InputMethodState state);
}
