// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>
/// Describes the virtual Shift modifier maintained by a <see cref="SoftKeyboard"/> .
/// </summary>
public enum SoftKeyboardShiftState
{
    /// <summary>
    /// Shift is not active.
    /// </summary>
    Off,

    /// <summary>
    /// Shift applies to the next character key and is then cleared.
    /// </summary>
    Once,

    /// <summary>
    /// Shift remains active until the user toggles it off.
    /// </summary>
    Locked,
}
