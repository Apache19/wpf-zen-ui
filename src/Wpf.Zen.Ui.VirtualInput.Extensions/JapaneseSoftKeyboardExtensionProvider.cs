// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Contributes Japanese language routing, localized punctuation, and input-mode interpretation.
/// </summary>
public sealed class JapaneseSoftKeyboardExtensionProvider : ISoftKeyboardExtensionProvider
{
    private static readonly CompactSoftKeyboardLayoutProvider CompactProvider = new(
        new SoftKeyboardCompactRepertoire
        {
            Comma = "、",
            Period = "。",
            SymbolsTail = "？",
            MoreSymbolsTail = "〜",
            ExtraLetterKey = new SoftKeyboardKey(
                SoftKeyboardKeyKind.Text,
                "ー",
                "长音符和中点",
                text: "ー",
                alternates: ["・"],
                alternateHint: "・"
            ),
            SymbolsSecond = ["！", "？", "＠", "＃", "￥", "％", "＆", "＊", "ー", "＝"],
            SymbolsThird = ["「", "」", "（", "）", "・", "：", "；"],
            MoreSymbolsSecond = ["『", "』", "【", "】", "〈", "〉", "《", "》", "〔", "〕"],
            MoreSymbolsThird = ["［", "］", "｛", "｝", "…", "※", "〒"],
        }
    );

    private static readonly ISoftKeyboardLayoutProvider LanguageProvider = new JapaneseLanguageProvider();

    /// <summary>
    /// Gets the shared Japanese extension provider.
    /// </summary>
    public static JapaneseSoftKeyboardExtensionProvider Default { get; } = new();

    /// <inheritdoc />
    public IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return SupportsJapanese(context.Layout)
            ?
            [
                new SoftKeyboardExtension(
                    layoutProvider: LanguageProvider,
                    inputModeAdapter: JapaneseInputModeAdapter.Default
                )
            ]
            : [];
    }

    private sealed class JapaneseLanguageProvider : ISoftKeyboardLayoutProvider
    {
        public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
        {
            if (!SupportsJapanese(context))
            {
                return null;
            }

            return UrlSoftKeyboardLayoutProvider.Default.CreateLayout(context)
                ?? EmailSoftKeyboardLayoutProvider.Default.CreateLayout(context)
                ?? NumberSoftKeyboardLayoutProvider.Default.CreateLayout(context)
                ?? (context.InputMode?.IsNativeInput == true ? CompactProvider.CreateLayout(context) : null);
        }
    }

    /// <summary>
    /// Uses the verified active profile for routing and falls back to culture without a profile.
    /// </summary>
    private static bool SupportsJapanese(SoftKeyboardLayoutContext context)
    {
        return context.InputMethod is { } inputMethod
            ? (inputMethod.LanguageId & 0x03FF) == 0x11
            : context.Culture.TwoLetterISOLanguageName == "ja";
    }
}
