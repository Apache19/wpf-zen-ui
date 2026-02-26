// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.Markup;

[ContentProperty(nameof(Id))]
[MarkupExtensionReturnType(typeof(Style))]
public class StyleStaticResourceExtension : StaticResourceExtension
{
    [ConstructorArgument("id")]
    public StyleId Id
    {
        get;
        set
        {
            field       = value;
            ResourceKey = StyleResourceExtension.KeyMapping(value);
        }
    }

    public StyleStaticResourceExtension(StyleId id)
    {
        Id = id;
    }
}
