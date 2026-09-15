// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using static Wpf.Zen.Ui.VirtualInput.Layouts.Internal.KeyboardLayoutParts;

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Internal;

/// <summary>
/// Arranges the built-in two-page symbol grid from language-specific literal data.
/// </summary>
internal static class SymbolKeyboardLayout
{
    internal static SoftKeyboardLayout Create(SoftKeyboardLayoutContext context, CompactKeyboardRepertoire repertoire)
    {
        var more = context.Page == SoftKeyboardPages.MoreSymbols;

        var second = more ? repertoire.MoreSymbolsSecond : repertoire.SymbolsSecond;
        var third  = more ? repertoire.MoreSymbolsThird  : repertoire.SymbolsThird;

        IReadOnlyList<SoftKeyboardKey> thirdRow =
        [
            new(
                kind:           SoftKeyboardKeyKind.ChangePage,
                content:        more ? PrevSymbolsIcon : MoreSymbolsIcon,
                automationName: more ? "Previous symbols" : "More symbols",
                page:    more ? SoftKeyboardPages.Symbols : SoftKeyboardPages.MoreSymbols
            ),

            .. third.Select(selector: CreateTextKey),

            new(
                kind:            SoftKeyboardKeyKind.VirtualKey,
                content:         BackspaceIcon,
                automationName:  "Backspace",
                key:             Key.Back,
                isRepeatEnabled: true
            ),
        ];

        IReadOnlyList<SoftKeyboardKey> bottom =
        [
            new(
                kind:           SoftKeyboardKeyKind.ChangePage,
                content:        "ABC",
                automationName: "Letters",
                page:    SoftKeyboardPages.Letters
            ),

            ..

            context.CanSwitchInputMode
                ? new[] { CreateInputModeKey(context) }
                : Array.Empty<SoftKeyboardKey>(),

            CreateTextKey(value: repertoire.Comma),

            new(
                kind:           SoftKeyboardKeyKind.VirtualKey,
                content:        SpaceIcon,
                automationName: "Space",
                key:            Key.Space
            ),

            CreateTextKey(value: repertoire.Period),

            CreateTextKey(
                value: more ? repertoire.MoreSymbolsTail : repertoire.SymbolsTail
            ),

            new(
                kind:           SoftKeyboardKeyKind.InputMethods,
                content:        InputMethodsIcon,
                automationName: "Input methods"
            ),

            new(
                kind:           SoftKeyboardKeyKind.VirtualKey,
                content:        EnterIcon,
                automationName: "Enter",
                key:            Key.Enter
            ),
        ];

        var builder = new SoftKeyboardLayoutBuilder(DefaultColumnCount);

        builder.AddRow(
            keys: [.. "1234567890".Select(selector: c => CreateTextKey(value: c.ToString()))],
            row: 0,
            startColumn: 0,
            columnSpan: 2
        );

        builder.AddRow(
            keys: [.. second.Select(selector: CreateTextKey)],
            row: 1,
            startColumn: 0,
            columnSpan: 2
        );

        builder.AddRow(
            keys: thirdRow,
            row: 2,
            startColumn: 0,
            columnSpans: [3, 2, 2, 2, 2, 2, 2, 2, 3]
        );

        builder.AddRow(
            keys: bottom,
            row: 3,
            startColumn: 0,
            columnSpans: context.CanSwitchInputMode
                ? [2, 2, 2, 6, 2, 2, 2, 2]
                : [2, 2, 8, 2, 2, 2, 2]
        );

        return builder.Build();
    }
}
