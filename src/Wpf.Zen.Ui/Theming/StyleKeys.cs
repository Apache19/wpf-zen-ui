// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Theming;

/// <summary>
/// Provides assembly-scoped keys for reusable styles published by WPF Zen UI.
/// </summary>
/// <remarks>
/// XAML consumers should normally use <c>DynamicStyle</c> or <c>StaticStyle</c>. Each property is initialized
/// explicitly so adding a key does not depend on reflection or a writable static property.
/// </remarks>
public static class StyleKeys
{
    public static ComponentResourceKey MenuStyle                       { get; } = Create(nameof(MenuStyle));
    public static ComponentResourceKey UiMenuStyle                     { get; } = Create(nameof(UiMenuStyle));
    public static ComponentResourceKey MenuItemStyle                   { get; } = Create(nameof(MenuItemStyle));
    public static ComponentResourceKey UiMenuItemStyle                 { get; } = Create(nameof(UiMenuItemStyle));
    public static ComponentResourceKey MenuItemSeparatorStyle          { get; } = Create(nameof(MenuItemSeparatorStyle));
    public static ComponentResourceKey DefaultCheckBoxStyle            { get; } = Create(nameof(DefaultCheckBoxStyle));
    public static ComponentResourceKey DefaultRadioButtonStyle         { get; } = Create(nameof(DefaultRadioButtonStyle));
    public static ComponentResourceKey DefaultComboBoxStyle            { get; } = Create(nameof(DefaultComboBoxStyle));
    public static ComponentResourceKey DefaultComboBoxItemStyle        { get; } = Create(nameof(DefaultComboBoxItemStyle));
    public static ComponentResourceKey UiComboBoxStyle                 { get; } = Create(nameof(UiComboBoxStyle));
    public static ComponentResourceKey DefaultTextBoxStyle             { get; } = Create(nameof(DefaultTextBoxStyle));
    public static ComponentResourceKey DefaultUiTextBoxStyle           { get; } = Create(nameof(DefaultUiTextBoxStyle));
    public static ComponentResourceKey DefaultRichTextBoxStyle         { get; } = Create(nameof(DefaultRichTextBoxStyle));
    public static ComponentResourceKey UiRichTextBoxStyle              { get; } = Create(nameof(UiRichTextBoxStyle));
    public static ComponentResourceKey DefaultControlFocusVisualStyle  { get; } = Create(nameof(DefaultControlFocusVisualStyle));
    public static ComponentResourceKey DefaultScrollViewerStyle        { get; } = Create(nameof(DefaultScrollViewerStyle));
    public static ComponentResourceKey UiDynamicScrollViewerStyle      { get; } = Create(nameof(UiDynamicScrollViewerStyle));
    public static ComponentResourceKey IconInfoBadgeStyle              { get; } = Create(nameof(IconInfoBadgeStyle));
    public static ComponentResourceKey ValueInfoBadgeStyle             { get; } = Create(nameof(ValueInfoBadgeStyle));
    public static ComponentResourceKey DotInfoBadgeStyle               { get; } = Create(nameof(DotInfoBadgeStyle));
    public static ComponentResourceKey DefaultCalendarStyle            { get; } = Create(nameof(DefaultCalendarStyle));
    public static ComponentResourceKey DefaultDatePickerStyle          { get; } = Create(nameof(DefaultDatePickerStyle));
    public static ComponentResourceKey UiSplitButtonStyle              { get; } = Create(nameof(UiSplitButtonStyle));
    public static ComponentResourceKey UiAutoSuggestBoxStyle           { get; } = Create(nameof(UiAutoSuggestBoxStyle));
    public static ComponentResourceKey UiBadgeStyle                    { get; } = Create(nameof(UiBadgeStyle));
    public static ComponentResourceKey DefaultProgressBarStyle         { get; } = Create(nameof(DefaultProgressBarStyle));

    /// <summary>
    /// Gets the theme-routing key for the ListView style used while a WPF Zen UI GridView is active.
    /// </summary>
    public static ComponentResourceKey GridViewListViewStyle           { get; } = Create(nameof(GridViewListViewStyle));

    /// <summary>
    /// Gets the theme-routing key for item containers generated while a WPF Zen UI GridView is active.
    /// </summary>
    public static ComponentResourceKey GridViewItemContainerStyle      { get; } = Create(nameof(GridViewItemContainerStyle));

    /// <summary>
    /// Gets the reusable Fluent column-header style key for a WPF Zen UI GridView.
    /// </summary>
    /// <remarks>
    /// Use <c>StaticStyle GridViewColumnHeader</c> as a <see cref="Style.BasedOn"/> value when a
    /// custom header should preserve the default template, resize gripper, and role-specific behavior.
    /// </remarks>
    public static ComponentResourceKey GridViewColumnHeaderStyle       { get; } = Create(nameof(GridViewColumnHeaderStyle));

    /// <summary>
    /// Gets the reusable, self-contained style key for the inner <see cref="Controls.PassiveScrollViewer"/>
    /// inside a WPF Zen UI GridView template. The style bundles <c>GridViewScrollViewerTemplate</c>, intrinsic
    /// constants (<c>Focusable</c>, <c>HorizontalScrollBarVisibilityThreshold</c>, selection-indicator
    /// gutter), and templated-parent routing (<c>Padding</c>, scroll-bar visibility, <c>CanContentScroll</c>,
    /// <c>ScrollSpillMode</c>, column definitions) so that a custom <c>GridViewTemplate</c> only needs to
    /// set this one <c>Style</c> without any local property duplication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference with <c>{m:StaticStyle GridViewScrollViewer}</c>. Apply on a
    /// <see cref="Controls.PassiveScrollViewer"/> placed inside a <c>GridViewTemplate</c>. The style is
    /// self-contained: no additional attributes are required on the control instance.
    /// </para>
    /// <para>
    /// <b>This style is scoped exclusively to the GridView template's inner scroll viewer.</b>
    /// Its <c>TemplatedParent</c> bindings resolve against the hosting <see cref="System.Windows.Controls.ListView"/>;
    /// using it on a stand-alone <see cref="Controls.PassiveScrollViewer"/> or inside any other template
    /// will produce silent binding failures and is not supported. Prefer <c>DefaultScrollBarStyle</c> or a
    /// local style for general-purpose scroll-viewer customization.
    /// </para>
    /// </remarks>
    public static ComponentResourceKey GridViewScrollViewerStyle       { get; } = Create(nameof(GridViewScrollViewerStyle));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}
