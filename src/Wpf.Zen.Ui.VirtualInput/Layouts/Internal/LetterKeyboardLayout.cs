// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using static Wpf.Zen.Ui.VirtualInput.Layouts.Internal.KeyboardLayoutParts;

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Internal;

/// <summary>
/// Builds a complete compact text page without interpreting address input scopes.
/// </summary>
internal static class LetterKeyboardLayout
{
    internal static SoftKeyboardLayout Create(SoftKeyboardLayoutContext context, CompactKeyboardRepertoire repertoire)
    {
        var rows = PhysicalKeyboardKeys.CreateRows(context);

        IReadOnlyList<SoftKeyboardKey> thirdRow =
        [
            new(SoftKeyboardKeyKind.Shift, GetShiftContent(context.ShiftState), automationName: "Shift"),

            .. rows[2],

            new(SoftKeyboardKeyKind.VirtualKey, BackspaceIcon, "Backspace", Key.Back, isRepeatEnabled: true),
        ];

        // 第四排
        SoftKeyboardKey[] bottom =
        [
            // 数字和符号面板切换键
            new(SoftKeyboardKeyKind.ChangePage, "123", "Numbers and symbols", page: SoftKeyboardPages.Symbols),

            // 输入法开关
            CreateImeKey(context),
            CreateTextKey(repertoire.Comma),

            new(SoftKeyboardKeyKind.VirtualKey, MoveLeftIcon, "Move cursor left", Key.Left, isRepeatEnabled: true),

            new(SoftKeyboardKeyKind.VirtualKey, SpaceIcon, "Space", Key.Space),

            new(SoftKeyboardKeyKind.VirtualKey, MoveRightIcon, "Move cursor right", Key.Right, isRepeatEnabled: true),

            CreateTextKey(repertoire.Period),

            // 输入法选择键
            new(SoftKeyboardKeyKind.InputMethods, InputMethodsIcon, "Input methods"),

            // 回车键
            new(SoftKeyboardKeyKind.VirtualKey, EnterIcon, "Enter", Key.Enter),
        ];

        var builder = new SoftKeyboardLayoutBuilder(DefaultColumnCount);

        builder.AddRow(0, rows[0], 2);

        if (repertoire.ExtraLetterKey is not null)
        {
            builder.AddRow(1, [.. rows[1], repertoire.ExtraLetterKey], startColumn: 0, columnSpan: 2);
        }
        else
        {
            builder.AddRow(1, rows[1], 2, startColumn: 1);
        }

        builder.AddRow(2, thirdRow, columnSpans: [2, 2, 2, 2, 2, 2, 2, 2, 4]);

        builder.AddRow(3, bottom, columnSpans: [2, 2, 2, 2, 4, 2, 2, 2, 2]);

        return builder.Build();
    }
}
