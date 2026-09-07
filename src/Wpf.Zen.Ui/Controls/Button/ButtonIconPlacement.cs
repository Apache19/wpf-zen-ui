// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Specifies where a <see cref="Button.Icon"/> is arranged relative to the button's content.
/// </summary>
/// <remarks>
/// This type is specific to <see cref="Button"/> because the available directions describe the
/// layout contract of that control's icon-and-content presenter. Other controls can expose their
/// own placement choices without inheriting directions that their templates cannot represent.
/// </remarks>
public enum ButtonIconPlacement
{
    /// <summary>
    /// Places the icon before the content on the horizontal axis.
    /// </summary>
    Left,

    /// <summary>
    /// Places the icon above the content.
    /// </summary>
    Top,

    /// <summary>
    /// Places the icon after the content on the horizontal axis.
    /// </summary>
    Right,

    /// <summary>
    /// Places the icon below the content.
    /// </summary>
    Bottom,
}
