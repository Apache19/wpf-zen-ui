// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Theming;

/// <summary>
/// Identifies the public style roles exposed by the WPF UI theme dictionaries.
/// </summary>
/// <remarks>
/// The token is intentionally independent from resource lookup lifetime. Use it with either
/// <c>DynamicStyle</c> or <c>StaticStyle</c>; keeping the constructor argument enum-typed lets
/// XAML editors offer the complete token list through IntelliSense.
/// </remarks>
public enum StyleToken
{
    None,
    Menu,
    UiMenu,
    MenuItem,
    UiMenuItem,
    MenuItemSeparator,
    DefaultCheckBox,
    DefaultRadioButton,
    DefaultComboBox,
    DefaultComboBoxItem,
    UiComboBox,
    DefaultTextBox,
    DefaultUiTextBox,
    DefaultRichTextBox,
    UiRichTextBox,
    DefaultTextBoxScrollViewer,
    DefaultControlFocusVisual,
    DefaultScrollBar,
    DynamicScrollPage,
    IconInfoBadge,
    ValueInfoBadge,
    DotInfoBadge,
    DefaultCalendar,
    DefaultDatePicker,
    UiSplitButton,
    UiAutoSuggestBox,
    UiBadge,
    DefaultProgressBar,

    /// <summary>
    /// Identifies the ListView style selected while a WPF UI GridView is active.
    /// </summary>
    GridViewListView,

    /// <summary>
    /// Identifies the item-container style selected for rows in a WPF UI GridView.
    /// </summary>
    GridViewItemContainer,

    /// <summary>
    /// Identifies the default Fluent style for column-header containers in a WPF UI GridView.
    /// </summary>
    GridViewColumnHeader,
}
