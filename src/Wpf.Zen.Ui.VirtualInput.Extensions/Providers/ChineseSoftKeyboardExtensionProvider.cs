// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Extensions.Routing;

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Registers the Chinese input-method adapter for matching profile or culture contexts.
/// </summary>
public sealed class ChineseSoftKeyboardExtensionProvider : ISoftKeyboardExtensionProvider
{
    /// <summary>
    /// Gets the shared Chinese extension provider.
    /// </summary>
    public static ChineseSoftKeyboardExtensionProvider Default { get; } = new();

    /// <inheritdoc />
    public IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return ProfileLanguage.IsChinese(context.Layout)
            ? [new SoftKeyboardExtension(inputModeAdapter: ChineseInputModeAdapter.Default)]
            : [];
    }
}
