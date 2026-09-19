// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Interprets observed data for explicitly supported input services.
/// </summary>
/// <remarks>
/// Calls run on the keyboard Dispatcher and must return promptly without changing focus or profile.
/// Missing data must remain unknown. Interpretation must not read compartments to fill missing
/// data.
/// </remarks>
public interface ISoftKeyboardInputModeAdapter
{
    /// <summary>
    /// Produces presentation and layout capabilities from the current generation's observations.
    /// </summary>
    /// <param name="inputMethod">The profile associated with the observation.</param>
    /// <param name="observation">
    /// Raw notification or settled fallback data, with missing values explicitly represented.
    /// Unsupported combinations should contribute no capabilities. A decodable combination
    /// does not guarantee freshness when the service supplies stale but otherwise legal values.
    /// </param>
    /// <returns>The optional capabilities understood by this adapter.</returns>
    SoftKeyboardInputModeContribution GetContribution(
        SoftKeyboardInputMethod inputMethod, SoftKeyboardInputObservation observation);

    /// <summary>
    /// Executes a freshly resolved fallback only when no platform command is available.
    /// </summary>
    /// <param name="inputMethod">The still-current profile after editor preparation.</param>
    /// <param name="observation">The latest observation used to resolve the command.</param>
    /// <param name="transition">The command returned by this adapter during this click.</param>
    /// <returns>Whether the request was accepted; this does not confirm a mode change.</returns>
    bool TrySwitchMode(
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardInputObservation observation,
        SoftKeyboardInputModeTransition transition);
}
