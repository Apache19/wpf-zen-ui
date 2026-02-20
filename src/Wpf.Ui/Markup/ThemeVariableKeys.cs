// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Markup;

#pragma warning disable SA1516 // Elements should be separated by blank line

/// <summary>
/// 定义在 Variables.xaml 里的资源键
/// </summary>
public static class ThemeVariableKeys
{
    public static ComponentResourceKey DefaultIconFontSize                      { get; } = Create(nameof(DefaultIconFontSize));
    public static ComponentResourceKey ControlContentThemeFontSize              { get; } = Create(nameof(ControlContentThemeFontSize));
    public static ComponentResourceKey ControlCornerRadius                      { get; } = Create(nameof(ControlCornerRadius));
    public static ComponentResourceKey OverlayCornerRadius                      { get; } = Create(nameof(OverlayCornerRadius));
    public static ComponentResourceKey PopupCornerRadius                        { get; } = Create(nameof(PopupCornerRadius));
    public static ComponentResourceKey TextControlBorderThemeThickness          { get; } = Create(nameof(TextControlBorderThemeThickness));
    public static ComponentResourceKey TextControlBorderThemeThicknessFocused   { get; } = Create(nameof(TextControlBorderThemeThicknessFocused));
    public static ComponentResourceKey TextControlThemePadding                  { get; } = Create(nameof(TextControlThemePadding));
    public static ComponentResourceKey ContentControlFontSize                   { get; } = Create(nameof(ContentControlFontSize));
    public static ComponentResourceKey TextControlThemeMinHeight                { get; } = Create(nameof(TextControlThemeMinHeight));
    public static ComponentResourceKey TextControlThemeMinWidth                 { get; } = Create(nameof(TextControlThemeMinWidth));
    public static ComponentResourceKey ListViewItemMinHeight                    { get; } = Create(nameof(ListViewItemMinHeight));
    public static ComponentResourceKey TreeViewItemMinHeight                    { get; } = Create(nameof(TreeViewItemMinHeight));
    public static ComponentResourceKey TreeViewItemMultiSelectCheckBoxMinHeight { get; } = Create(nameof(TreeViewItemMultiSelectCheckBoxMinHeight));
    public static ComponentResourceKey TreeViewItemPresenterMargin              { get; } = Create(nameof(TreeViewItemPresenterMargin));
    public static ComponentResourceKey TreeViewItemPresenterPadding             { get; } = Create(nameof(TreeViewItemPresenterPadding));
    public static ComponentResourceKey TimePickerHostPadding                    { get; } = Create(nameof(TimePickerHostPadding));
    public static ComponentResourceKey DatePickerHostPadding                    { get; } = Create(nameof(DatePickerHostPadding));
    public static ComponentResourceKey DatePickerHostMonthPadding               { get; } = Create(nameof(DatePickerHostMonthPadding));
    public static ComponentResourceKey ComboBoxEditableTextPadding              { get; } = Create(nameof(ComboBoxEditableTextPadding));
    public static ComponentResourceKey ComboBoxMinHeight                        { get; } = Create(nameof(ComboBoxMinHeight));
    public static ComponentResourceKey ComboBoxPadding                          { get; } = Create(nameof(ComboBoxPadding));
    public static ComponentResourceKey NavigationViewItemOnLeftMinHeight        { get; } = Create(nameof(NavigationViewItemOnLeftMinHeight));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}

#pragma warning restore SA1516 // Elements should be separated by blank line