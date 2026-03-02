// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.Markup;

/// <summary>
/// Provides a strongly-typed wrapper over <see cref="StaticResourceExtension"/>
/// for resolving style resources by <see cref="StyleId"/>.
/// </summary>
/// <remarks>
/// ReSharper may not recognize this custom extension correctly and can
/// misinterpret it as a <c>DynamicResourceExtension</c> during static analysis.
///
/// This does not affect runtime behavior.
///
/// If necessary, you may suppress the related inspection in ReSharper.
/// Alternatively, reference the resource key directly using a static expression,
/// for example:
/// <c>"{StaticResource {x:Static th:StyleKeys.ButtonStyle}}"</c>
/// </remarks>
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
