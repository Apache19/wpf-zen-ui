// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.Markup;

/// <summary>
/// Resolves a strongly typed <see cref="StyleToken"/> once by using WPF static resource lookup.
/// </summary>
/// <remarks>
/// Static lookup is required for non-dependency properties such as <see cref="Style.BasedOn"/>. It fixes the
/// referenced <see cref="Style"/> instance at load time, but dynamic resource references contained inside that
/// style remain dynamic and can still react to theme changes.
/// </remarks>
[ContentProperty(nameof(Token))]
[MarkupExtensionReturnType(typeof(Style))]
public sealed class StaticStyleExtension : StaticResourceExtension
{
    /// <summary>Gets or sets the logical style token resolved by this reference.</summary>
    [ConstructorArgument("token")]
    public StyleToken Token
    {
        get;
        set
        {
            var resourceKey = StyleTokenResourceKey.Get(value);
            field       = value;
            ResourceKey = resourceKey;
        }
    }

    /// <summary>Initializes a new instance of the <see cref="StaticStyleExtension"/> class for the specified token.</summary>
    /// <param name="token">The style role to resolve from the current resource scope.</param>
    public StaticStyleExtension(StyleToken token)
    {
        Token = token;
    }
}
