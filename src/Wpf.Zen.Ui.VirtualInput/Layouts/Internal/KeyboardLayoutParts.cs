// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

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
    internal static readonly FluentIconSource ShiftIcon        = new(FluentSymbolRegular.KeyboardShift24);
    internal static readonly FluentIconSource ShiftOnceIcon    = new(FluentSymbolRegular.KeyboardShiftUppercase24);
    internal static readonly FluentIconSource ShiftLockedIcon  = new(FluentSymbolRegular.KeyboardShiftUppercase24, filled: true);
    internal static readonly FluentIconSource BackspaceIcon    = new(FluentSymbolRegular.Backspace24);
    internal static readonly FluentIconSource InputMethodsIcon = new(FluentSymbolRegular.Globe24);
    internal static readonly FluentIconSource MoveLeftIcon     = new(FluentSymbolRegular.ArrowLeft24);
    internal static readonly FluentIconSource MoveRightIcon    = new(FluentSymbolRegular.ArrowRight24);
    internal static readonly FluentIconSource PrevSymbolsIcon  = new(FluentSymbolRegular.CaretLeft24, filled: true);
    internal static readonly FluentIconSource MoreSymbolsIcon  = new(FluentSymbolRegular.CaretRight24, filled: true);
    internal static readonly FluentIconSource SpaceIcon        = new(FluentSymbolRegular.Spacebar24);
    internal static readonly FluentIconSource EnterIcon        = new(FluentSymbolRegular.ArrowEnterLeft24);

    /// <summary>
    /// Creates a mode-switch key from the independently resolved command presentation.
    /// </summary>
    internal static SoftKeyboardKey CreateInputModeKey(SoftKeyboardLayoutContext context)
    {
        var button = context.InputModeButton
            ?? throw new InvalidOperationException(
                "A mode-switch key requires an available command presentation."
            );

        return new SoftKeyboardKey(
            kind: SoftKeyboardKeyKind.SwitchInputMode,
            content: button.Content,
            automationName: button.AutomationName
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
            _ => throw new ArgumentOutOfRangeException(nameof(shiftState)),
        };
    }

    internal static SoftKeyboardKey CreateTextKey(string value)
    {
        return new SoftKeyboardKey(SoftKeyboardKeyKind.Text, value, value, text: value);
    }
}
