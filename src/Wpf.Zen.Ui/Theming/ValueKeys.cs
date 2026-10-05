// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Theming;

/// <summary>
/// Provides assembly-scoped keys for non-color theme values defined in <c>Variables.xaml</c>.
/// </summary>
/// <remarks>
/// XAML consumers should normally use <c>DynamicValue</c> or <c>StaticValue</c>. This key surface remains
/// available for resource dictionary declarations and programmatic lookup.
/// </remarks>
public static class ValueKeys
{
    public static ComponentResourceKey DefaultControlMinWidth                   { get; } = Create(nameof(DefaultControlMinWidth));
    public static ComponentResourceKey DefaultControlMinHeight                  { get; } = Create(nameof(DefaultControlMinHeight));

    public static ComponentResourceKey DefaultIconFontSize                      { get; } = Create(nameof(DefaultIconFontSize));
    public static ComponentResourceKey ControlContentThemeFontSize              { get; } = Create(nameof(ControlContentThemeFontSize));
    public static ComponentResourceKey ControlCornerRadius                      { get; } = Create(nameof(ControlCornerRadius));
    public static ComponentResourceKey OverlayCornerRadius                      { get; } = Create(nameof(OverlayCornerRadius));
    public static ComponentResourceKey PopupCornerRadius                        { get; } = Create(nameof(PopupCornerRadius));
    public static ComponentResourceKey TextControlBorderThemeThickness          { get; } = Create(nameof(TextControlBorderThemeThickness));
    public static ComponentResourceKey TextControlBorderThemeThicknessFocused   { get; } = Create(nameof(TextControlBorderThemeThicknessFocused));
    public static ComponentResourceKey TextControlThemePadding                  { get; } = Create(nameof(TextControlThemePadding));
    public static ComponentResourceKey ButtonThemePadding                       { get; } = Create(nameof(ButtonThemePadding));
    public static ComponentResourceKey ContentControlFontSize                   { get; } = Create(nameof(ContentControlFontSize));
    public static ComponentResourceKey ListViewItemMinHeight                    { get; } = Create(nameof(ListViewItemMinHeight));
    public static ComponentResourceKey TreeViewItemMinHeight                    { get; } = Create(nameof(TreeViewItemMinHeight));
    public static ComponentResourceKey TreeViewItemMultiSelectCheckBoxMinHeight { get; } = Create(nameof(TreeViewItemMultiSelectCheckBoxMinHeight));
    public static ComponentResourceKey TreeViewItemPresenterMargin              { get; } = Create(nameof(TreeViewItemPresenterMargin));
    public static ComponentResourceKey TreeViewItemPresenterPadding             { get; } = Create(nameof(TreeViewItemPresenterPadding));
    public static ComponentResourceKey TimePickerHostPadding                    { get; } = Create(nameof(TimePickerHostPadding));
    public static ComponentResourceKey DatePickerHostPadding                    { get; } = Create(nameof(DatePickerHostPadding));
    public static ComponentResourceKey DatePickerHostMonthPadding               { get; } = Create(nameof(DatePickerHostMonthPadding));
    public static ComponentResourceKey ComboBoxMinHeight                        { get; } = Create(nameof(ComboBoxMinHeight));
    public static ComponentResourceKey NavigationViewItemOnLeftMinHeight        { get; } = Create(nameof(NavigationViewItemOnLeftMinHeight));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}
