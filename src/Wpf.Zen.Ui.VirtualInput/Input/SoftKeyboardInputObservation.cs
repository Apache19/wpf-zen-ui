// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Contains raw input-mode observations for one profile and editor generation.
/// </summary>
/// <remarks>
/// TSF compartment and IMM window notifications update the same fields in arrival order.
/// Either notification samples both fields through its own subsystem. A settled fallback may
/// supplement missing data or replace an uninterpretable combination once per generation.
/// Presence flags describe attempted samples, not receipt of a particular notification.
/// Missing and failed reads never imply zero or a mode. Adapters interpret the raw combination.
/// </remarks>
public sealed class SoftKeyboardInputObservation
{
    /// <summary>
    /// Creates an immutable observation, including sample-presence flags.
    /// </summary>
    /// <param name="openObserved">Whether reading the open field has been attempted.</param>
    /// <param name="openValue">Its last integer, or null when that read failed.</param>
    /// <param name="conversionObserved">
    /// Whether reading the conversion field has been attempted.
    /// </param>
    /// <param name="conversionValue">Its last integer, or null when that read failed.</param>
    public SoftKeyboardInputObservation(
        bool openObserved       = false,
        int? openValue          = null,
        bool conversionObserved = false,
        int? conversionValue    = null)
    {
        OpenObserved            = openObserved;
        OpenValue               = openObserved ? openValue : null;
        ConversionObserved      = conversionObserved;
        ConversionValue         = conversionObserved ? conversionValue : null;
    }

    /// <summary>
    /// Gets an observation with no state samples attempted.
    /// </summary>
    public static SoftKeyboardInputObservation Empty { get; } = new();

    /// <summary>
    /// Gets whether the open field was sampled, even if reading its value failed.
    /// </summary>
    public bool OpenObserved { get; }

    /// <summary>
    /// Gets the last observed open integer, or null when absent or unreadable.
    /// </summary>
    public int? OpenValue { get; }

    /// <summary>
    /// Gets whether the conversion field was sampled, even if reading its value failed.
    /// </summary>
    public bool ConversionObserved { get; }

    /// <summary>
    /// Gets the last observed conversion integer, or null when absent or unreadable.
    /// </summary>
    public int? ConversionValue { get; }
}