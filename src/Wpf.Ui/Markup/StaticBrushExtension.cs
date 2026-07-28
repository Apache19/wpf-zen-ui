// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.Markup;

/// <summary>
/// Resolves a strongly typed <see cref="BrushToken"/> once by using WPF static resource lookup.
/// </summary>
/// <remarks>
/// Choose this extension only when the brush object should be captured at load time or when the target cannot
/// accept a dynamic resource expression. Theme and accent dictionary replacement will not replace that captured brush.
/// </remarks>
[ContentProperty(nameof(Token))]
[MarkupExtensionReturnType(typeof(Brush))]
public sealed class StaticBrushExtension : StaticResourceExtension
{
    /// <summary>Gets or sets the logical brush token resolved by this reference.</summary>
    [ConstructorArgument("token")]
    public BrushToken Token
    {
        get;
        set
        {
            var resourceKey = BrushTokenResourceKey.Get(value);
            field       = value;
            ResourceKey = resourceKey;
        }
    }

    /// <summary>Initializes a new instance of the <see cref="StaticBrushExtension"/> class for the specified token.</summary>
    /// <param name="token">The brush role to resolve from the current resource scope.</param>
    public StaticBrushExtension(BrushToken token)
    {
        Token = token;
    }
}
