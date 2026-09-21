// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>
/// Identifies the semantic action represented by a <see cref="SoftKeyboardKey"/> .
/// </summary>
public enum SoftKeyboardKeyKind
{
    /// <summary>
    /// Sends a virtual key using the active Windows keyboard layout.
    /// </summary>
    VirtualKey,

    /// <summary>
    /// Inserts a Unicode string, used for symbols and key alternatives.
    /// </summary>
    Text,

    /// <summary>
    /// Changes the keyboard's Shift state.
    /// </summary>
    Shift,

    /// <summary>
    /// Changes the visible keyboard surface.
    /// </summary>
    ChangePage,

    /// <summary>
    /// Opens the input-method context menu.
    /// </summary>
    InputMethods,

    /// <summary>
    /// Invokes the active input method's mode-switch command.
    /// </summary>
    SwitchInputMode,

    /// <summary>
    /// Invokes a custom action through the keyboard's validated input services.
    /// </summary>
    Custom,
}

