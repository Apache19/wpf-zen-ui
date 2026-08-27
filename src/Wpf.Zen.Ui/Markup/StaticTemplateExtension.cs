// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.Markup;

/// <summary>
/// Resolves a strongly typed <see cref="TemplateToken"/> once by using WPF static resource lookup.
/// </summary>
[ContentProperty(nameof(Token))]
[MarkupExtensionReturnType(typeof(FrameworkTemplate))]
public sealed class StaticTemplateExtension : StaticResourceExtension
{
    /// <summary>Gets or sets the logical template token resolved by this reference.</summary>
    [ConstructorArgument("token")]
    public TemplateToken Token
    {
        get;
        set
        {
            var resourceKey = TemplateTokenResourceKey.Get(value);
            field       = value;
            ResourceKey = resourceKey;
        }
    }

    public StaticTemplateExtension()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StaticTemplateExtension"/> class for the specified token.</summary>
    /// <param name="token">The template role to resolve from the current resource scope.</param>
    public StaticTemplateExtension(TemplateToken token)
    {
        Token = token;
    }
}
