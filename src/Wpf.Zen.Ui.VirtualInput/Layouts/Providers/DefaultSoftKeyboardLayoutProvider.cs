// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

/// <summary>
/// Provides the language-neutral URL, email, number, letter, and symbol layouts.
/// </summary>
/// <remarks>
/// Language and input-method-specific routes belong to explicitly configured extensions. This
/// provider is the final core fallback and does not inspect profile identities or language families.
/// </remarks>
public sealed class DefaultSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    /// <summary>
    /// Gets the shared, stateless default composition.
    /// </summary>
    public static DefaultSoftKeyboardLayoutProvider Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        return StandardSoftKeyboardLayoutProvider.Default.CreateLayout(context);
    }
}
