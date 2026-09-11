// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>Optional literal repertoire for the built-in compact letter and symbol renderers.</summary>
/// <remarks>This data has no matching or fallback responsibilities. A language provider may choose
/// these renderers or generate completely different layouts without constructing a repertoire.</remarks>
internal sealed class CompactKeyboardRepertoire
{
    internal string Comma { get; init; } = ",";
    internal string Period { get; init; } = ".";
    internal string SymbolsTail { get; init; } = "?";
    internal string MoreSymbolsTail { get; init; } = "\\";
    internal SoftKeyboardKey? ExtraLetterKey { get; init; }
    internal string[] SymbolsSecond { get; init; } = ["!", "@", "#", "$", "%", "&", "_", "-", "=", "+"];
    internal string[] SymbolsThird { get; init; } = [";", ":", "(", ")", "/", "'", "\""];
    internal string[] MoreSymbolsSecond { get; init; } = ["%", "[", "]", "{", "}", "<", ">", "€", "£", "¥"];
    internal string[] MoreSymbolsThird { get; init; } = ["*", "`", "°", "×", "÷", "~", "|"];

    internal static CompactKeyboardRepertoire General { get; } = new();
}
