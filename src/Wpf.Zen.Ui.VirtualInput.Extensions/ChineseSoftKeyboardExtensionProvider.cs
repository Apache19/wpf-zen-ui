// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Contributes Chinese language routing and localized compact punctuation and symbols.
/// </summary>
public sealed class ChineseSoftKeyboardExtensionProvider : ISoftKeyboardExtensionProvider
{
    private static readonly ISoftKeyboardLayoutProvider LayoutProvider =
        new LanguageCompactLayoutProvider(
            "zh",
            new SoftKeyboardCompactRepertoire
            {
                Comma = "，",
                Period = "。",
                SymbolsTail = "？",
                MoreSymbolsTail = "^",
                SymbolsSecond = ["！", "@", "#", "￥", "%", "&", "_", "-", "=", "+"],
                SymbolsThird = ["/", "；", "：", "“", "”", "（", "）"],
                MoreSymbolsSecond = ["、", "【", "】", "｛", "｝", "〈", "〉", "*", "…", "•"],
                MoreSymbolsThird = ["µ", "½", "｜", "©", "¶", "°", "~"],
            }
        );

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

        return context.Layout.Culture.TwoLetterISOLanguageName == "zh"
            ?
            [
                new SoftKeyboardExtension(
                    layoutProvider: LayoutProvider,
                    inputModeAdapter: ChineseInputModeAdapter.Default
                )
            ]
            : [];
    }

    private sealed class LanguageCompactLayoutProvider : ISoftKeyboardLayoutProvider
    {
        private readonly string _language;
        private readonly CompactSoftKeyboardLayoutProvider _compact;

        internal LanguageCompactLayoutProvider(string language, SoftKeyboardCompactRepertoire repertoire)
        {
            _language = language;
            _compact = new CompactSoftKeyboardLayoutProvider(repertoire);
        }

        public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
        {
            if (context.Culture.TwoLetterISOLanguageName != _language)
            {
                return null;
            }

            return UrlSoftKeyboardLayoutProvider.Default.CreateLayout(context)
                ?? EmailSoftKeyboardLayoutProvider.Default.CreateLayout(context)
                ?? NumberSoftKeyboardLayoutProvider.Default.CreateLayout(context)
                ?? (context.InputMode?.IsNativeInput == true ? _compact.CreateLayout(context) : null);
        }
    }
}
