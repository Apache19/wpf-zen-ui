// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Captures platform values used by a text service to describe its current keyboard mode.
/// </summary>
/// <remarks>
/// Values retain the original TSF integers; unavailable compartments remain null. Extensions
/// interpret these opaque values according to a known text service contract.
/// </remarks>
public sealed class SoftKeyboardTsfInputModeState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardTsfInputModeState"/> class.
    /// </summary>
    /// <param name="openValue">The raw keyboard open/close compartment value.</param>
    /// <param name="conversionValue">The raw TSF conversion flags, or null when unavailable.</param>
    /// <param name="canSwitch">
    /// Whether the active service owns an enabled standard TSF input-mode command.
    /// </param>
    public SoftKeyboardTsfInputModeState(int? openValue, int? conversionValue, bool canSwitch)
    {
        OpenValue       = openValue;
        ConversionValue = conversionValue;
        CanSwitch       = canSwitch;
    }

    /// <summary>
    /// Gets the raw value of <c>GUID_COMPARTMENT_KEYBOARD_OPENCLOSE</c>, or null when unavailable.
    /// </summary>
    public int? OpenValue { get; }

    /// <summary>
    /// Gets the raw TSF conversion bit field, or null when unavailable.
    /// </summary>
    public int? ConversionValue { get; }

    /// <summary>
    /// Gets whether <c>GUID_LBI_INPUTMODE</c> is present, enabled, and owned by the expected service.
    /// </summary>
    public bool CanSwitch { get; }
}
