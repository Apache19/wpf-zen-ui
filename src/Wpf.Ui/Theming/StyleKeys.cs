// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Theming;

/// <summary>
/// Provides assembly-scoped keys for reusable styles published by WPF UI.
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
    public static ComponentResourceKey DefaultTextBoxScrollViewerStyle { get; } = Create(nameof(DefaultTextBoxScrollViewerStyle));
    public static ComponentResourceKey DefaultControlFocusVisualStyle  { get; } = Create(nameof(DefaultControlFocusVisualStyle));
    public static ComponentResourceKey DefaultScrollBarStyle           { get; } = Create(nameof(DefaultScrollBarStyle));
    public static ComponentResourceKey DynamicScrollPageStyle          { get; } = Create(nameof(DynamicScrollPageStyle));
    public static ComponentResourceKey IconInfoBadgeStyle              { get; } = Create(nameof(IconInfoBadgeStyle));
    public static ComponentResourceKey ValueInfoBadgeStyle             { get; } = Create(nameof(ValueInfoBadgeStyle));
    public static ComponentResourceKey DotInfoBadgeStyle               { get; } = Create(nameof(DotInfoBadgeStyle));
    public static ComponentResourceKey DefaultCalendarStyle            { get; } = Create(nameof(DefaultCalendarStyle));
    public static ComponentResourceKey DefaultDatePickerStyle          { get; } = Create(nameof(DefaultDatePickerStyle));
    public static ComponentResourceKey UiSplitButtonStyle              { get; } = Create(nameof(UiSplitButtonStyle));
    public static ComponentResourceKey UiAutoSuggestBoxStyle           { get; } = Create(nameof(UiAutoSuggestBoxStyle));
    public static ComponentResourceKey UiBadgeStyle                    { get; } = Create(nameof(UiBadgeStyle));
    public static ComponentResourceKey DefaultProgressBarStyle         { get; } = Create(nameof(DefaultProgressBarStyle));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}