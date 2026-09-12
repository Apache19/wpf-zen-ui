// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

/// <summary>
/// Owns the complete built-in japanese input-environment composition.
/// </summary>
/// <remarks>
/// Matches the input language in both direct and native modes. Scope providers are explicitly chosen
/// here, so addresses and numbers are not intercepted by an outer default router. The built-in policy
/// reuses standard address and number pages, selects native text/symbol pages while IME is on, and
/// delegates remaining contexts to the standard composition. Applications may prepend profile,
/// scope or page overrides and reuse this provider as the fallback for only this language.
/// </remarks>
public sealed class JapaneseSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    private readonly CompositeSoftKeyboardLayoutProvider _layouts = new(
        UrlSoftKeyboardLayoutProvider.Default,
        EmailSoftKeyboardLayoutProvider.Default,
        NumberSoftKeyboardLayoutProvider.Default,
        SupplementalSoftKeyboardLayoutProvider.Default,
        new SoftKeyboardLayoutRule(
            context => context.ImeState == System.Windows.Input.InputMethodState.On
                && context.Page is SoftKeyboardPages.Automatic or SoftKeyboardPages.Letters,
            context => LetterKeyboardLayout.Create(context, JapaneseKeyboardRepertoire.Value)
        ),
        new SoftKeyboardLayoutRule(
            context => context.ImeState == System.Windows.Input.InputMethodState.On
                && context.Page is SoftKeyboardPages.Symbols or SoftKeyboardPages.MoreSymbols,
            context => SymbolKeyboardLayout.Create(context, JapaneseKeyboardRepertoire.Value)
        ),
        StandardSoftKeyboardLayoutProvider.Default
    );

    /// <summary>
    /// Gets the shared, stateless japanese composition.
    /// </summary>
    public static JapaneseSoftKeyboardLayoutProvider Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return context.Culture.TwoLetterISOLanguageName == "ja"
            ? _layouts.CreateLayout(context)
            : null;
    }
}
