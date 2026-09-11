// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Composes complete built-in address, numeric and general-text layouts.
/// </summary>
/// <remarks>
/// This provider is a reusable language-neutral fallback, not a base class for language implementations.
/// Each scope provider decides which pages it supports; explicit pages remain reachable in numeric
/// and address editors. Unrecognized pages are declined. A language provider can replace any branch,
/// including the address and number branches, before choosing to delegate to this composition.
/// </remarks>
public sealed class StandardSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    private readonly CompositeSoftKeyboardLayoutProvider _layouts = new(
        UrlSoftKeyboardLayoutProvider.Default,
        EmailSoftKeyboardLayoutProvider.Default,
        NumberSoftKeyboardLayoutProvider.Default,
        new SoftKeyboardLayoutRule(
            context => context.Page is SoftKeyboardPages.Automatic or SoftKeyboardPages.Letters,
            context => LetterKeyboardLayout.Create(context, CompactKeyboardRepertoire.General)
        ),
        new SoftKeyboardLayoutRule(
            context => context.Page is SoftKeyboardPages.Symbols or SoftKeyboardPages.MoreSymbols,
            context => SymbolKeyboardLayout.Create(context, CompactKeyboardRepertoire.General)
        )
    );

    /// <summary>
    /// Gets the shared, stateless standard composition.
    /// </summary>
    public static StandardSoftKeyboardLayoutProvider Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        return _layouts.CreateLayout(context);
    }
}
