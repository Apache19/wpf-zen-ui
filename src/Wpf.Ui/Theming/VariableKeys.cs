// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;

// ReSharper disable UnassignedGetOnlyAutoProperty

namespace Wpf.Ui.Theming;

#pragma warning disable CS8618
#pragma warning disable SA1516 // Elements should be separated by blank line

/// <summary>
/// 定义在 Variables.xaml 里的资源键
/// </summary>
public static class VariableKeys
{
    public static ComponentResourceKey DefaultIconFontSize                      { get; private set; }
    public static ComponentResourceKey ControlContentThemeFontSize              { get; private set; }
    public static ComponentResourceKey ControlCornerRadius                      { get; private set; }
    public static ComponentResourceKey OverlayCornerRadius                      { get; private set; }
    public static ComponentResourceKey PopupCornerRadius                        { get; private set; }
    public static ComponentResourceKey TextControlBorderThemeThickness          { get; private set; }
    public static ComponentResourceKey TextControlBorderThemeThicknessFocused   { get; private set; }
    public static ComponentResourceKey TextControlThemePadding                  { get; private set; }
    public static ComponentResourceKey ButtonThemePadding                       { get; private set; }
    public static ComponentResourceKey ContentControlFontSize                   { get; private set; }
    public static ComponentResourceKey TextControlThemeMinHeight                { get; private set; }
    public static ComponentResourceKey TextControlThemeMinWidth                 { get; private set; }
    public static ComponentResourceKey ListViewItemMinHeight                    { get; private set; }
    public static ComponentResourceKey TreeViewItemMinHeight                    { get; private set; }
    public static ComponentResourceKey TreeViewItemMultiSelectCheckBoxMinHeight { get; private set; }
    public static ComponentResourceKey TreeViewItemPresenterMargin              { get; private set; }
    public static ComponentResourceKey TreeViewItemPresenterPadding             { get; private set; }
    public static ComponentResourceKey TimePickerHostPadding                    { get; private set; }
    public static ComponentResourceKey DatePickerHostPadding                    { get; private set; }
    public static ComponentResourceKey DatePickerHostMonthPadding               { get; private set; }
    public static ComponentResourceKey ComboBoxMinHeight                        { get; private set; }
    public static ComponentResourceKey NavigationViewItemOnLeftMinHeight        { get; private set; }

    static VariableKeys()
    {
        // 使用反射统一设置每个属性值
        var type       = typeof(VariableKeys);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static);
        foreach (var property in properties)
        {
            if (property.PropertyType == typeof(ComponentResourceKey))
            {
                var key = Create(property.Name);
                property.SetValue(null, key);
            }
        }
    }

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}

#pragma warning restore SA1516 // Elements should be separated by blank line
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。