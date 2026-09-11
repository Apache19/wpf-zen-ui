// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>Supplies the Chinese repertoire for the built-in compact keyboard geometry.</summary>
internal static class ChineseKeyboardRepertoire
{
    internal static CompactKeyboardRepertoire Value { get; } = new()
    {
        Comma = "，",
        Period = "。",
        SymbolsTail = "？",
        MoreSymbolsTail = "^",
        SymbolsSecond = ["！", "@", "#", "￥", "%", "&", "_", "-", "=", "+"],
        SymbolsThird = ["/", "；", "：", "“", "”", "（", "）"],
        MoreSymbolsSecond = ["、", "【", "】", "｛", "｝", "〈", "〉", "*", "…", "•"],
        MoreSymbolsThird = ["µ", "½", "｜", "©", "¶", "°", "~"],
    };
}
