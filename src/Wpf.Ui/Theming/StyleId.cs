// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Theming;

public enum StyleId
{
    Unknown,

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

    /// <summary>
    /// Identifies the opt-in <see cref="System.Windows.Controls.Page"/> style that hosts page
    /// content in a <see cref="Wpf.Ui.Controls.DynamicScrollViewer"/>.
    /// </summary>
    DynamicScrollPageStyle,

    NavigationViewItemDataTemplate,

    IconInfoBadgeStyle,
    ValueInfoBadgeStyle,
    DotInfoBadgeStyle,

    DefaultCalendarStyle,
    DefaultDatePickerStyle,

    DefaultValidationTemplate,
}
