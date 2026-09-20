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
    /// Defers to the next candidate behavior. If no extension makes an explicit decision,
    /// the input method keeps its native candidate surface.
    /// </summary>
    Default,

    /// <summary>
    /// Explicitly permits candidate presentation inside the soft keyboard for this input method.
    /// The core still requires controllable candidates belonging to the focused TSF document.
    /// </summary>
    Embedded,

    /// <summary>
    /// Keeps the input method's native candidate surface visible.
    /// </summary>
    Native,
}
