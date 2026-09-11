// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Selects a complete language composition, then falls back to the standard keyboard.
/// </summary>
/// <remarks>
/// Language selection precedes every input-scope and page decision, including numbers and addresses.
/// A language provider owns its complete routing tree and may use any geometry, page set or action.
/// No repertoire or compact-layout renderer is required by this contract. Applications can prepend
/// profile-specific or cross-language scope overrides using CompositeSoftKeyboardLayoutProvider.
/// </remarks>
public sealed class DefaultSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    private readonly CompositeSoftKeyboardLayoutProvider _layouts = new(
        JapaneseSoftKeyboardLayoutProvider.Default,
        ChineseSoftKeyboardLayoutProvider.Default,
        StandardSoftKeyboardLayoutProvider.Default
    );

    /// <summary>
    /// Gets the shared, stateless default composition.
    /// </summary>
    public static DefaultSoftKeyboardLayoutProvider Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        return _layouts.CreateLayout(context);
    }
}