// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Chooses who presents newly started IME candidate UI. This does not change the input method's own settings.
/// </summary>
public enum SoftKeyboardCandidateMode
{
    /// <summary>
    /// Embeds supported candidate elements, preserving native UI for known incomplete integrations such as Weasel.
    /// </summary>
    Automatic,

    /// <summary>
    /// Leaves candidate and reading UI to the input method, including its pointer interaction and composition preview.
    /// </summary>
    Native,

    /// <summary>
    /// Attempts TSF candidate integration even for known incomplete implementations.
    /// </summary>
    /// <remarks>
    /// Does not create missing reading text or force the IME to publish candidates. For Weasel,
    /// enable inline_preedit in the IME configuration before choosing this mode to keep pre-edit visible.
    /// </remarks>
    Embedded,
}
