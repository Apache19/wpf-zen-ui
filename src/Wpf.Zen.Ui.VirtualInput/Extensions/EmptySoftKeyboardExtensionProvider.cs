// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Supplies no input-method-specific capabilities.
/// </summary>
public sealed class EmptySoftKeyboardExtensionProvider : ISoftKeyboardExtensionProvider
{
    /// <summary>
    /// Gets the shared empty provider used by the core keyboard.
    /// </summary>
    public static EmptySoftKeyboardExtensionProvider Default { get; } = new();

    /// <inheritdoc />
    public IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return [];
    }
}