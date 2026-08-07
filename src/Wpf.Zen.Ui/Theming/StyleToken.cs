// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Theming;

/// <summary>
/// Identifies the public style roles exposed by the WPF Zen UI theme dictionaries.
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
    DefaultScrollViewer,
    UiDynamicScrollViewer,
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
    /// Identifies the ListView style selected while a WPF Zen UI GridView is active.
    /// </summary>
    GridViewListView,

    /// <summary>
    /// Identifies the item-container style selected for rows in a WPF Zen UI GridView.
    /// </summary>
    GridViewItemContainer,

    /// <summary>
    /// Identifies the default Fluent style for column-header containers in a WPF Zen UI GridView.
    /// </summary>
    GridViewColumnHeader,

    /// <summary>
    /// Identifies the self-contained default style for the inner <see cref="Controls.PassiveScrollViewer"/>
    /// used by a WPF Zen UI GridView template. The style bundles the scroll-viewer template, intrinsic
    /// constants, and templated-parent routing so developers can replace only the outer chrome of
    /// <c>GridViewTemplate</c> (for example, to add rounded-corners clipping) while referencing the
    /// library's complete header, content, and scroll-bar layout with a single style reference.
    /// </summary>
    /// <remarks>
    /// This token is scoped exclusively to the <see cref="Controls.PassiveScrollViewer"/> inside a
    /// <c>GridViewTemplate</c>. The style's <c>TemplatedParent</c> bindings resolve against the
    /// hosting <see cref="System.Windows.Controls.ListView"/>; applying it elsewhere will cause
    /// silent binding failures. For general-purpose scroll-viewer customization, use a local style instead.
    /// </remarks>
    GridViewScrollViewer,
}
