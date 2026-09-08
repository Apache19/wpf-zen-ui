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

    /// <summary>
    /// Identifies the rounded-rectangle presenter used inside the global keyboard-focus visual.
    /// </summary>
    /// <remarks>
    /// Use this style as the base for a <see cref="Wpf.Zen.Ui.Controls.FocusVisual.PresenterStyleProperty"/> value when only presenter
    /// properties such as margin need to change. The style targets
    /// <see cref="Wpf.Zen.Ui.Controls.FocusVisualPresenter"/>. A control whose focus outline is not a rounded rectangle
    /// may instead supply a complete <see cref="System.Windows.Controls.Control.Template"/> in its presenter style.
    /// </remarks>
    DefaultFocusVisualPresenter,

    UiButton,
    UiComboBox,
    DefaultUiTextBox,
    UiRichTextBox,
    UiDynamicScrollViewer,
    IconInfoBadge,
    ValueInfoBadge,
    DotInfoBadge,
    UiSplitButton,
    UiAutoSuggestBox,
    UiBadge,
    UiBreadcrumbBar,
    UiBreadcrumbBarItem,
    UiSeparator,

    /// <summary>Identifies the default Fluent style for the embedded soft keyboard.</summary>
    UiSoftKeyboard,

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
    /// Identifies the self-contained default style for the inner <see cref="Controls.UiScrollViewer"/>
    /// used by a WPF Zen UI GridView template. The style bundles the scroll-viewer template, intrinsic
    /// constants, and templated-parent routing so developers can replace only the outer chrome of
    /// <c>GridViewTemplate</c> (for example, to add rounded-corners clipping) while referencing the
    /// library's complete header, content, and scroll-bar layout with a single style reference.
    /// </summary>
    /// <remarks>
    /// This token is scoped exclusively to the <see cref="Controls.UiScrollViewer"/> inside a
    /// <c>GridViewTemplate</c>. The style's <c>TemplatedParent</c> bindings resolve against the
    /// hosting <see cref="System.Windows.Controls.ListView"/>; applying it elsewhere will cause
    /// silent binding failures. For general-purpose scroll-viewer customization, use a local style instead.
    /// </remarks>
    GridViewScrollViewer,
}
