// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Specifies when an automatically opened <see cref="SoftKeyboard"/> closes.
/// </summary>
public enum SoftKeyboardAutoHideMode
{
    /// <summary>
    /// The keyboard closes after keyboard focus leaves its configured target scope.
    /// </summary>
    FocusLeavesScope,

    /// <summary>
    /// The application or the keyboard's close key must close the keyboard.
    /// </summary>
    Manual,
}
