// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.Markup;

/// <summary>
/// Resolves a strongly typed <see cref="ColorToken"/> once by using WPF static resource lookup.
/// </summary>
[ContentProperty(nameof(Token))]
[MarkupExtensionReturnType(typeof(Color))]
public sealed class StaticColorExtension : StaticResourceExtension
{
    /// <summary>Gets or sets the logical color token resolved by this reference.</summary>
    [ConstructorArgument("token")]
    public ColorToken Token
    {
        get;
        set
        {
            var resourceKey = ColorTokenResourceKey.Get(value);
            field       = value;
            ResourceKey = resourceKey;
        }
    }

    public StaticColorExtension()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StaticColorExtension"/> class for the specified token.</summary>
    /// <param name="token">The color role to resolve from the current resource scope.</param>
    public StaticColorExtension(ColorToken token)
    {
        Token = token;
    }
}
