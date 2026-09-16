// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Contributes independently resolvable mode capabilities for a particular text service.
/// </summary>
/// <remarks>
/// TSF standardizes the compartments that carry open and conversion values, but a text service can
/// use additional values or expose service-specific transitions. The adapter owns that
/// interpretation. Each state, presentation, and transition facet can either stop resolution or
/// allow the platform default to supply that facet. Calls run on the keyboard Dispatcher, must not
/// block, and must not activate another profile or change focus.
/// </remarks>
public interface ISoftKeyboardInputModeAdapter
{
    /// <summary>
    /// Reads one coherent native snapshot and contributes any facets understood by the adapter.
    /// </summary>
    /// <param name="inputMethod">The active input-method profile.</param>
    /// <returns>
    /// Independently handled state, presentation, and transition facets. Unhandled facets continue
    /// through the platform-default resolver.
    /// </returns>
    SoftKeyboardInputModeContribution GetContribution(SoftKeyboardInputMethod inputMethod);

    /// <summary>
    /// Requests a transition using the text service's mode-switch contract.
    /// </summary>
    /// <param name="inputMethod">The active input-method profile.</param>
    /// <param name="transition">The transition descriptor returned by this adapter.</param>
    /// <returns>Whether the mode-switch request was accepted.</returns>
    /// <remarks>
    /// The displayed state is always read back after the request. The method is called only for a
    /// transition facet returned by the same adapter and published for the same active profile.
    /// </remarks>
    bool TrySwitchMode(
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardInputModeTransition transition);
}
