// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.Markup;

/// <summary>
/// Creates a WPF dynamic resource reference for a strongly typed <see cref="TemplateToken"/>.
/// </summary>
[ContentProperty(nameof(Token))]
[MarkupExtensionReturnType(typeof(FrameworkTemplate))]
public sealed class DynamicTemplateExtension : DynamicResourceExtension
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

    public DynamicTemplateExtension()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DynamicTemplateExtension"/> class for the specified token.</summary>
    /// <param name="token">The template role to resolve from the active resource scope.</param>
    public DynamicTemplateExtension(TemplateToken token)
    {
        Token = token;
    }
}

#pragma warning disable SA1402 // File may only contain a single type
internal static class TemplateTokenResourceKey
{
    internal static ComponentResourceKey Get(TemplateToken token)
    {
        return token switch
        {
            TemplateToken.DefaultValidation      => TemplateKeys.DefaultValidationTemplate,

            _ => throw new ArgumentOutOfRangeException(nameof(token), token, "The template token is not registered."),
        };
    }
}
#pragma warning restore SA1402 // File may only contain a single type
