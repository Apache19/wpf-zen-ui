// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Identifies one input-method refresh cause without combining unrelated state.
/// </summary>
internal enum InputMethodChangeKind
{
    /// <summary>
    /// Requests discovery and presentation refresh without supplying a native state value.
    /// </summary>
    RefreshAll,
    /// <summary>
    /// Reports profile activation or deactivation and establishes observation ownership.
    /// </summary>
    Profile,
    /// <summary>
    /// Carries both state values read in response to an open notification.
    /// </summary>
    ImeOpen,
    /// <summary>
    /// Carries both state values read in response to a conversion notification.
    /// </summary>
    ImeConversion,
    /// <summary>
    /// Requests language-bar presentation refresh after its short icon readiness delay.
    /// </summary>
    InputModePresentation,
}

/// <summary>
/// Carries one typed input-method invalidation cause.
/// </summary>
/// <param name="Kind">The notification category used for observation and scheduling.</param>
/// <param name="Profile">The identity reported by a profile notification.</param>
/// <param name="State">
/// The open/conversion sample captured from the notifying subsystem. Both fields enter the
/// observation pipeline together; the kind identifies the trigger, not the only field read.
/// </param>
internal readonly record struct InputMethodChange(
    InputMethodChangeKind Kind,
    InputProcessorProfileChange Profile = default,
    SoftKeyboardInputObservation? State = null
);

/// <summary>
/// Carries the identity supplied by <c>ITfInputProcessorProfileActivationSink</c>.
/// </summary>
internal readonly record struct InputProcessorProfileChange(
    uint ProfileType,
    ushort LanguageId,
    Guid ClassId,
    Guid CategoryId,
    Guid ProfileId,
    IntPtr KeyboardLayout,
    bool IsActive
);
