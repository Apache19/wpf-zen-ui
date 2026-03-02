// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.Markup;


[TypeConverter(typeof(TestKeyConverter))]
[MarkupExtensionReturnType(typeof(object))]
public class TestAKeyExtension : MarkupExtension
{
    [TypeConverter(typeof(TestKeyConverter))]
    public TestAKeyExtension()
    {
    }

    [TypeConverter(typeof(TestKeyConverter))]
    public TestAKeyExtension(TestKey key)
    {
        Key = key;
    }

    [TypeConverter(typeof(TestKeyConverter))]
    [ConstructorArgument("key")]
    public TestKey Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}