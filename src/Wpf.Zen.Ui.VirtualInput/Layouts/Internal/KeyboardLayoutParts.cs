// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Internal;

/// <summary>
/// Shares stateless key presentation data among built-in surfaces.
/// </summary>
internal static class KeyboardLayoutParts
{
    // Two logical columns form the nominal width of an ordinary character key. The half-key
    // granularity makes physical-row staggering and 1.5-key command caps exactly representable.
    internal const int DefaultColumnCount = 20;

    // Icon sources are immutable presentation data. Sharing them avoids rebuilding identical
    // descriptors whenever Shift or the requested surface regenerates the layout; the implicit
    // FluentIconSource data template still creates an independent visual for every key button.
    internal static readonly FluentIconSource ShiftIcon        = new(SymbolRegular.KeyboardShift24);
    internal static readonly FluentIconSource ShiftOnceIcon    = new(SymbolRegular.KeyboardShiftUppercase24);
    internal static readonly FluentIconSource ShiftLockedIcon  = new(SymbolRegular.KeyboardShiftUppercase24, filled: true);
    internal static readonly FluentIconSource BackspaceIcon    = new(SymbolRegular.Backspace24);
    internal static readonly FluentIconSource InputMethodsIcon = new(SymbolRegular.Globe24);
    internal static readonly FluentIconSource MoveLeftIcon     = new(SymbolRegular.ArrowLeft24);
    internal static readonly FluentIconSource MoveRightIcon    = new(SymbolRegular.ArrowRight24);
    internal static readonly FluentIconSource PrevSymbolsIcon  = new(SymbolRegular.CaretLeft24, filled: true);
    internal static readonly FluentIconSource MoreSymbolsIcon  = new(SymbolRegular.CaretRight24, filled: true);
    internal static readonly FluentIconSource SpaceIcon        = new(SymbolRegular.Spacebar24);
    internal static readonly FluentIconSource EnterIcon        = new(SymbolRegular.ArrowEnterLeft24);

    // The cap reports the current state. Latin A is culture-neutral; native modes use familiar
    // language abbreviations while unknown services retain the same reliable toggle action.
    internal static SoftKeyboardKey CreateImeKey(SoftKeyboardLayoutContext context)
    {
        return new SoftKeyboardKey(
            kind: SoftKeyboardKeyKind.ToggleIme,
            content: context.ImeState != InputMethodState.On
                ? "A"
                : context.Culture.TwoLetterISOLanguageName switch
                {
                    "ja" => "あ",
                    "zh" => "中",
                    "ko" => "한",
                    _    => "文",
                },
            automationName: context.ImeState == InputMethodState.On
                ? "Native input; switch to direct input"
                : "Direct input; enable input method"
        );
    }

    /// <summary>
    /// Selects a key-cap description that distinguishes inactive, one-shot, and locked Shift
    /// without placing stateful visual elements in the immutable layout model.
    /// </summary>
    internal static FluentIconSource GetShiftContent(SoftKeyboardShiftState shiftState)
    {
        return shiftState switch
        {
            SoftKeyboardShiftState.Off    => ShiftIcon,
            SoftKeyboardShiftState.Once   => ShiftOnceIcon,
            SoftKeyboardShiftState.Locked => ShiftLockedIcon,
            _                             => throw new ArgumentOutOfRangeException(nameof(shiftState)),
        };
    }

    internal static SoftKeyboardKey CreateTextKey(string value)
    {
        return new SoftKeyboardKey(SoftKeyboardKeyKind.Text, value, value, text: value);
    }
}
