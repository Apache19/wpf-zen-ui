// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;

namespace Wpf.Ui.Markup;

[ContentProperty(nameof(Key))]
[MarkupExtensionReturnType(typeof(object))]
public class UIResourceStaticExtension : MarkupExtension
{
    [ConstructorArgument("key")]
    public ThemeResource Key { get; }

    public UIResourceStaticExtension(ThemeResource key)
    {
        Key = key;
    }

    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        var key = UIResourceExtension.KeyMapping(Key);
        return new StaticResourceExtension(key).ProvideValue(serviceProvider);
    }
}
