// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Theming;

// todo 参考系统ThemeMode，主题资源key似乎可以摆脱枚举类。

public enum StyleId
{
    Unknown,

    ButtonStyle,
    UiButtonStyle,

    RepeatButtonStyle,

    MenuStyle,
    UiMenuStyle,
    MenuItemStyle,
    UiMenuItemStyle,
    MenuItemSeparatorStyle,

    DefaultCheckBoxStyle,
    DefaultUiCheckBoxStyle,

    DefaultComboBoxStyle,
    DefaultUiComboBoxStyle,

    DefaultTextBoxStyle,
    DefaultUiTextBoxStyle,

    DefaultTextBoxScrollViewerStyle,
    DefaultControlFocusVisualStyle,

    DefaultScrollBarStyle,

    NavigationViewItemDataTemplate,

    IconInfoBadgeStyle,
    ValueInfoBadgeStyle,
    DotInfoBadgeStyle,
}