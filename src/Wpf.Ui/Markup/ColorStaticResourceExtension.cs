// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

#pragma warning disable WPF0081 // This markup must be used only on the Color.

namespace Wpf.Ui.Markup;

[ContentProperty(nameof(Id))]
[MarkupExtensionReturnType(typeof(Color))]
public class ColorStaticResourceExtension : StaticResourceExtension
{
    [ConstructorArgument("id")]
    public ColorId Id
    {
        get;
        set
        {
            field       = value;
            ResourceKey = ColorResourceExtension.KeyMapping(value);
        }
    }

    public ColorStaticResourceExtension(ColorId id)
    {
        Id = id;
    }
}