// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Candidates;

/// <summary>
/// Describes a TSF behavior extension's candidate presentation decision.
/// </summary>
public enum SoftKeyboardTsfCandidatePresentation
{
    /// <summary>
    /// Uses the core policy and the keyboard's CandidateMode.
    /// </summary>
    Default,

    /// <summary>
    /// Requests candidate presentation inside the soft keyboard.
    /// </summary>
    Embedded,

    /// <summary>
    /// Keeps the input method's native candidate surface visible.
    /// </summary>
    Native,
}
