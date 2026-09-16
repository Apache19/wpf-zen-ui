// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Captures platform values used by a text service to describe its current keyboard mode.
/// </summary>
/// <remarks>
/// Values are not reduced to an On/Off state. Open remains the original TSF value, while conversion
/// may be normalized from WPF's IMM32 compatibility path into TSF flag values. Adapters must still
/// interpret both values for a known text service.
/// </remarks>
public sealed class SoftKeyboardTsfInputModeState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardTsfInputModeState"/> class.
    /// </summary>
    /// <param name="openValue">The raw keyboard open/close compartment value.</param>
    /// <param name="conversionValue">The TSF conversion flags, normalized when necessary.</param>
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
    /// Gets the TSF conversion bit field, normalized from WPF's compatibility path when necessary.
    /// </summary>
    public int? ConversionValue { get; }

    /// <summary>
    /// Gets whether <c>GUID_LBI_INPUTMODE</c> is present, enabled, and owned by the expected service.
    /// </summary>
    public bool CanSwitch { get; }
}
