// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Identifies the Windows touch keyboard layout family requested by a TSF input method.
/// </summary>
public enum SoftKeyboardTouchLayoutType
{
    /// <summary>
    /// The input method has no preferred layout.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// A traditional keyboard layout.
    /// </summary>
    Classic = 1,

    /// <summary>
    /// A touch-optimized keyboard layout.
    /// </summary>
    Optimized = 2,
}
