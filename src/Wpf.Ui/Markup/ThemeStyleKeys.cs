// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。

namespace Wpf.Ui.Markup;

public static class ThemeStyleKeys
{
    public static ComponentResourceKey DefaultCheckBoxStyle            { get; private set; }
    public static ComponentResourceKey DefaultUiCheckBoxStyle          { get; private set; }

    public static ComponentResourceKey DefaultUiButtonStyle            { get; private set; }
    public static ComponentResourceKey DefaultTextBoxScrollViewerStyle { get; private set; }
    public static ComponentResourceKey DefaultControlContextMenu       { get; private set; }
    public static ComponentResourceKey DefaultControlFocusVisualStyle  { get; private set; }

    static ThemeStyleKeys()
    {
        // 使用反射统一设置每个属性值
        var type       = typeof(ThemeStyleKeys);
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