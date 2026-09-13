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
    /// Uses the first matching candidate behavior, then embeds candidate elements by default.
    /// </summary>
    Automatic,

    /// <summary>
    /// Leaves candidate and reading UI to the input method, including its pointer interaction and composition preview.
    /// </summary>
    Native,

    /// <summary>
    /// Requests TSF candidate integration even when an extension would otherwise prefer native UI.
    /// </summary>
    /// <remarks>
    /// Does not create missing reading or pre-edit text and does not force an input method to publish candidates.
    /// </remarks>
    Embedded,
}
