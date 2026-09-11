// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Describes how the current input method's candidate UI is being presented.
/// </summary>
public enum SoftKeyboardCandidatePresentationState
{
    /// <summary>
    /// No candidate list is active.
    /// </summary>
    None,

    /// <summary>
    /// The candidate list is rendered inside the soft keyboard.
    /// </summary>
    Embedded,

    /// <summary>
    /// The input method cannot be controlled safely, so its native candidate window remains visible.
    /// </summary>
    NativeFallback,

    /// <summary>
    /// TSF candidate integration is unavailable on the current runtime.
    /// </summary>
    Unavailable,
}
