// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;

namespace Wpf.Ui.Extensions;

[MarkupExtensionReturnType(typeof(object))]
public sealed class LibraryInitializerExtension : MarkupExtension
{
    static LibraryInitializerExtension()
    {
        LibraryInitializer.EnsureInitialized();
    }

    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        return null;
    }
}
