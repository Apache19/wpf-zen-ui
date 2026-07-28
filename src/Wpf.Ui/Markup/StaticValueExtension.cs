// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.Markup;

/// <summary>
/// Resolves a strongly typed <see cref="ValueToken"/> once by using WPF static resource lookup.
/// </summary>
[ContentProperty(nameof(Token))]
[MarkupExtensionReturnType(typeof(object))]
public sealed class StaticValueExtension : StaticResourceExtension
{
    /// <summary>Gets or sets the logical theme value token resolved by this reference.</summary>
    [ConstructorArgument("token")]
    public ValueToken Token
    {
        get;
        set
        {
            var resourceKey = ValueTokenResourceKey.Get(value);
            field       = value;
            ResourceKey = resourceKey;
        }
    }

    /// <summary>Initializes a new instance of the <see cref="StaticValueExtension"/> class for the specified token.</summary>
    /// <param name="token">The theme value role to resolve from the current resource scope.</param>
    public StaticValueExtension(ValueToken token)
    {
        Token = token;
    }
}
