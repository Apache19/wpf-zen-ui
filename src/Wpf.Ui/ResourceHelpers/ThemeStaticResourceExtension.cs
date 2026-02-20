// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;

namespace Wpf.Ui.ResourceHelpers;

[MarkupExtensionReturnType(typeof(object))]
public class ThemeStaticResourceExtension : MarkupExtension
{
    public string ResourceId { get; }

    public ThemeStaticResourceExtension(string resourceId)
    {
        ResourceId = resourceId;
    }

    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        var key = new ComponentResourceKey(typeof(ThemeResourceAnchor), ResourceId);

        return new StaticResourceExtension(key).ProvideValue(serviceProvider);
    }
}
