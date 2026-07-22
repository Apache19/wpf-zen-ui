// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。

namespace Wpf.Ui.Theming;

public static class StyleKeys
{
    public static ComponentResourceKey MenuStyle                       { get; private set; }
    public static ComponentResourceKey UiMenuStyle                     { get; private set; }
    public static ComponentResourceKey MenuItemStyle                   { get; private set; }
    public static ComponentResourceKey UiMenuItemStyle                 { get; private set; }
    public static ComponentResourceKey MenuItemSeparatorStyle          { get; private set; }
    public static ComponentResourceKey DefaultCheckBoxStyle            { get; private set; }
    public static ComponentResourceKey DefaultUiCheckBoxStyle          { get; private set; }
    public static ComponentResourceKey DefaultComboBoxStyle            { get; private set; }
    public static ComponentResourceKey DefaultUiComboBoxStyle          { get; private set; }
    public static ComponentResourceKey DefaultTextBoxStyle             { get; private set; }
    public static ComponentResourceKey DefaultUiTextBoxStyle           { get; private set; }
    public static ComponentResourceKey DefaultTextBoxScrollViewerStyle { get; private set; }
    public static ComponentResourceKey DefaultControlFocusVisualStyle  { get; private set; }
    public static ComponentResourceKey DefaultScrollBarStyle           { get; private set; }

    /// <summary>
    /// Gets the assembly-scoped key for the opt-in dynamic scrolling <see cref="System.Windows.Controls.Page"/> style.
    /// </summary>
    /// <remarks>
    /// A <see cref="ComponentResourceKey"/> is required here because pages can be created without an
    /// <see cref="Application"/> resource scope, for example when WPF content is hosted by WinForms.
    /// WPF can resolve this key from this assembly's theme dictionary instead of relying on an
    /// application-level string resource.
    /// </remarks>
    public static ComponentResourceKey DynamicScrollPageStyle { get; private set; }

    public static ComponentResourceKey NavigationViewItemDataTemplate { get; private set; }

    public static ComponentResourceKey IconInfoBadgeStyle        { get; private set; }
    public static ComponentResourceKey ValueInfoBadgeStyle       { get; private set; }
    public static ComponentResourceKey DotInfoBadgeStyle         { get; private set; }
    public static ComponentResourceKey DefaultCalendarStyle      { get; private set; }
    public static ComponentResourceKey DefaultDatePickerStyle    { get; private set; }
    public static ComponentResourceKey DefaultValidationTemplate { get; private set; }

    static StyleKeys()
    {
        // 使用反射统一设置每个属性值
        var type       = typeof(StyleKeys);
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