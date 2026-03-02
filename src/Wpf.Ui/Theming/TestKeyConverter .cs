// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;

namespace Wpf.Ui.Theming;

public class TestKeyConverter : TypeConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
    {
        // 这里先硬编码，也可以反射扫描 TestKey 静态属性
        var values = new[]
        {
            "Alpha",
            "Beta",
            "Gamma"
        };

        return new StandardValuesCollection(values);
    }

}
