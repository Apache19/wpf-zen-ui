// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;

namespace Wpf.Ui.Markup;

public static class ThemeKeys
{
    static ThemeKeys()
    {
        // 使用反射统一设置每个属性值
        var type       = typeof(ThemeKeys);
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
