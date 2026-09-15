// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Captures the raw TSF values used by a text service to describe its current keyboard mode.
/// </summary>
/// <remarks>
/// The values are deliberately not reduced to an On/Off state. Adapters must interpret them for a
/// known text service. In particular, a nonzero open value is not necessarily equivalent to native
/// input, and the conversion value is a bit field rather than an enumeration of exclusive states.
/// </remarks>
public sealed class SoftKeyboardTsfInputModeState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardTsfInputModeState"/> class.
    /// </summary>
    /// <param name="openValue">The raw keyboard open/close compartment value.</param>
    /// <param name="conversionValue">The raw keyboard conversion-mode compartment value.</param>
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
    /// Gets the raw value of <c>GUID_COMPARTMENT_KEYBOARD_INPUTMODE_CONVERSION</c>, or null when unavailable.
    /// </summary>
    public int? ConversionValue { get; }

    /// <summary>
    /// Gets whether <c>GUID_LBI_INPUTMODE</c> is present, enabled, and owned by the expected service.
    /// </summary>
    public bool CanSwitch { get; }
}
