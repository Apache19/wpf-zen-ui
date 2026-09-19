// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.VirtualInput.Extensions.Routing;

namespace Wpf.Zen.Ui.VirtualInput.Extensions.Layouts;

/// <summary>
/// Defines the Chinese compact keyboard's punctuation and symbol repertoire.
/// The input-method adapter selects this provider only for recognized observations.
/// </summary>
internal sealed class ChineseCompactLayoutProvider : LanguageCompactLayoutProvider
{
    /// <summary>
    /// Gets the shared layout provider, retaining identity across observation refreshes.
    /// </summary>
    internal static ChineseCompactLayoutProvider Default { get; } = new();

    private ChineseCompactLayoutProvider()
        : base(new SoftKeyboardCompactRepertoire
        {
            Comma = "，",
            Period = "。",
            SymbolsTail = "？",
            MoreSymbolsTail = "^",
            SymbolsSecond = ["！", "@", "#", "￥", "%", "&", "_", "-", "=", "+"],
            SymbolsThird = ["/", "；", "：", "“", "”", "（", "）"],
            MoreSymbolsSecond = ["、", "【", "】", "｛", "｝", "〈", "〉", "*", "…", "•"],
            MoreSymbolsThird = ["µ", "½", "｜", "©", "¶", "°", "~"],
        })
    {
    }

    /// <inheritdoc />
    protected override bool Supports(SoftKeyboardLayoutContext context)
    {
        return ProfileLanguage.IsChinese(context);
    }
}
