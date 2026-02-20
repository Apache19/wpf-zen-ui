// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Markup;

/// <summary>
/// 定义在 Variables.xaml 里的资源键
/// </summary>
public static class ThemeVariableKeys
{
    #pragma warning disable SA1516 // Elements should be separated by blank line

    public static ComponentResourceKey DefaultIconFontSize                      => Create(nameof(DefaultIconFontSize));
    public static ComponentResourceKey ControlContentThemeFontSize              => Create(nameof(ControlContentThemeFontSize));
    public static ComponentResourceKey ControlCornerRadius                      => Create(nameof(ControlCornerRadius));
    public static ComponentResourceKey OverlayCornerRadius                      => Create(nameof(OverlayCornerRadius));
    public static ComponentResourceKey PopupCornerRadius                        => Create(nameof(PopupCornerRadius));
    public static ComponentResourceKey TextControlBorderThemeThickness          => Create(nameof(TextControlBorderThemeThickness));
    public static ComponentResourceKey TextControlBorderThemeThicknessFocused   => Create(nameof(TextControlBorderThemeThicknessFocused));
    public static ComponentResourceKey TextControlThemePadding                  => Create(nameof(TextControlThemePadding));
    public static ComponentResourceKey ContentControlFontSize                   => Create(nameof(ContentControlFontSize));
    public static ComponentResourceKey TextControlThemeMinHeight                => Create(nameof(TextControlThemeMinHeight));
    public static ComponentResourceKey TextControlThemeMinWidth                 => Create(nameof(TextControlThemeMinWidth));
    public static ComponentResourceKey ListViewItemMinHeight                    => Create(nameof(ListViewItemMinHeight));
    public static ComponentResourceKey TreeViewItemMinHeight                    => Create(nameof(TreeViewItemMinHeight));
    public static ComponentResourceKey TreeViewItemMultiSelectCheckBoxMinHeight => Create(nameof(TreeViewItemMultiSelectCheckBoxMinHeight));
    public static ComponentResourceKey TreeViewItemPresenterMargin              => Create(nameof(TreeViewItemPresenterMargin));
    public static ComponentResourceKey TreeViewItemPresenterPadding             => Create(nameof(TreeViewItemPresenterPadding));
    public static ComponentResourceKey TimePickerHostPadding                    => Create(nameof(TimePickerHostPadding));
    public static ComponentResourceKey DatePickerHostPadding                    => Create(nameof(DatePickerHostPadding));
    public static ComponentResourceKey DatePickerHostMonthPadding               => Create(nameof(DatePickerHostMonthPadding));
    public static ComponentResourceKey ComboBoxEditableTextPadding              => Create(nameof(ComboBoxEditableTextPadding));
    public static ComponentResourceKey ComboBoxMinHeight                        => Create(nameof(ComboBoxMinHeight));
    public static ComponentResourceKey ComboBoxPadding                          => Create(nameof(ComboBoxPadding));
    public static ComponentResourceKey NavigationViewItemOnLeftMinHeight        => Create(nameof(NavigationViewItemOnLeftMinHeight));

    #pragma warning restore SA1516 // Elements should be separated by blank line

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}