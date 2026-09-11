// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Specifies how a <see cref="SoftKeyboard"/> reacts to editor focus.
/// </summary>
public enum SoftKeyboardAutoShowMode
{
    /// <summary>
    /// The application controls <see cref="SoftKeyboard.IsOpen"/> explicitly.
    /// </summary>
    Disabled,

    /// <summary>
    /// The keyboard opens when touch or pen input gives focus to a supported editor.
    /// </summary>
    TouchAndPen,

    /// <summary>
    /// The keyboard opens whenever a supported editor receives keyboard focus.
    /// </summary>
    AnyFocus,
}
