// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;

namespace Wpf.Ui.Markup;

[MarkupExtensionReturnType(typeof(ComponentResourceKey))]
public class ThemeResourceKeyExtension : MarkupExtension
{
    [ConstructorArgument("key")]
    private ThemeResource Key { get; }

    public ThemeResourceKeyExtension(ThemeResource key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), Key);
    }
}