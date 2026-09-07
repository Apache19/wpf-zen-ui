// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Specifies which horizontal side of an editable text area contains its icon.
/// </summary>
/// <remarks>
/// Text controls reserve their vertical space for the editor, placeholder, and accessory buttons,
/// so their icon contract intentionally exposes only the two positions supported by those templates.
/// </remarks>
public enum TextBoxIconPlacement
{
    /// <summary>
    /// Places the icon before the editable text area.
    /// </summary>
    Left,

    /// <summary>
    /// Places the icon after the editable text area and before any accessory buttons.
    /// </summary>
    Right,
}
