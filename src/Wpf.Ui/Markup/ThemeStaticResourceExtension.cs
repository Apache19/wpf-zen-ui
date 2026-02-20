// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;

namespace Wpf.Ui.Markup;

[MarkupExtensionReturnType(typeof(object))]
public class ThemeStaticResourceExtension : MarkupExtension
{
    [ConstructorArgument("key")]
    public ThemeResource Key { get; }

    public ThemeStaticResourceExtension(ThemeResource key)
    {
        Key = key;
    }

    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        var key = new ComponentResourceKey(typeof(ThemeResourceAnchor), Key);
        return new StaticResourceExtension(key).ProvideValue(serviceProvider);
    }
}
