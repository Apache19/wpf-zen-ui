// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Identifies mutually exclusive input feedback independently of selection and focus.
/// </summary>
public enum InteractionState
{
    /// <summary>
    /// No active pointer or press feedback.
    /// </summary>
    Normal,

    /// <summary>
    /// The pointer is over the owning control.
    /// </summary>
    PointerOver,

    /// <summary>
    /// The owning control is pressed.
    /// </summary>
    Pressed,

    /// <summary>
    /// The owning control is unavailable for interaction.
    /// </summary>
    Disabled,
}