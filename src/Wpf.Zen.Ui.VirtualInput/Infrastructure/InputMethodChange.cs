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
    RefreshAll,
    Profile,
    ImeOpen,
    ImeConversion,
    InputModePresentation,
}

/// <summary>
/// Carries one typed input-method refresh cause without combining unrelated native state.
/// </summary>
internal readonly record struct InputMethodChange(
    InputMethodChangeKind Kind,
    InputProcessorProfileChange Profile = default,
    int? StateValue = null
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
