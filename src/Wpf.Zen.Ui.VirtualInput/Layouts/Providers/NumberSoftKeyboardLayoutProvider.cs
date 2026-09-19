// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using static Wpf.Zen.Ui.VirtualInput.Layouts.Internal.KeyboardLayoutParts;

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

/// <summary>
/// Owns complete calculator and telephone pages, including their scope/page selection.
/// </summary>
/// <remarks>
/// Handles Numbers explicitly or Automatic for numeric input scopes. Other requests are
/// declined so a containing language or application composition can supply its own pages.
/// </remarks>
public sealed class NumberSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    /// <summary>
    /// Gets the shared, stateless numeric provider.
    /// </summary>
    public static NumberSoftKeyboardLayoutProvider Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return context.Page == SoftKeyboardPages.Numbers
               || (context.Page == SoftKeyboardPages.Automatic && IsNumericScope(context.InputScope))
            ? Create(context)
            : null;
    }

    private static SoftKeyboardLayout Create(SoftKeyboardLayoutContext context)
    {
        var scope = context.InputScope;

        string[][] values = IsTelephoneScope(scope)
            ?
            [
                ["1", "2", "3"],
                ["4", "5", "6"],
                ["7", "8", "9"],
                ["+", "0", "#"]
            ]
            :
            [
                ["7", "8", "9"],
                ["4", "5", "6"],
                ["1", "2", "3"],
                ["-", "0", context.Culture.NumberFormat.NumberDecimalSeparator]
            ];

        var keys = new SoftKeyboardLayoutBuilder(DefaultColumnCount);

        for (var row = 0; row < 4; row++)
        {
            keys.AddRow(row, values[row].Select(CreateTextKey),
                columnSpans: row == 1 && !context.HasInputModeButton ? [5, 5, 10] : [5, 5, 5]);
        }

        // Numeric input uses the same four tracks as letters and symbols. Commands occupy a
        // fourth column so input-scope changes cannot add a fifth row to the keyboard.
        keys.Add(
            new SoftKeyboardKey(
                kind:            SoftKeyboardKeyKind.VirtualKey,
                content:         BackspaceIcon,
                automationName:  "Backspace",
                key:             Key.Back,
                row:             0,
                column:          15,
                columnSpan:      5,
                isRepeatEnabled: true
            )
        );

        if (context.HasInputModeButton)
        {
            keys.Add(CreateInputModeKey(context).WithPlacement(1, 15, 5));
        }

        keys.Add(
            new SoftKeyboardKey(
                kind:           SoftKeyboardKeyKind.InputMethods,
                content:        InputMethodsIcon,
                automationName: "Input methods",
                row:            2,
                column:         15,
                columnSpan:     5
            )
        );

        keys.Add(
            new SoftKeyboardKey(
                kind:           SoftKeyboardKeyKind.ChangePage,
                content:        "ABC",
                automationName: "Letters",
                page:    SoftKeyboardPages.Letters,
                row:            3,
                column:         15,
                columnSpan:     2
            )
        );

        keys.Add(
            new SoftKeyboardKey(
                kind:           SoftKeyboardKeyKind.VirtualKey,
                content:        EnterIcon,
                automationName: "Enter",
                key:            Key.Enter,
                row:            3,
                column:         17,
                columnSpan:     3
            )
        );

        return keys.Build();
    }

    private static bool IsNumericScope(InputScopeNameValue scope)
    {
        return scope is InputScopeNameValue.Number
                   or InputScopeNameValue.Digits
                   or InputScopeNameValue.NumberFullWidth
                   or InputScopeNameValue.CurrencyAmount
                   or InputScopeNameValue.CurrencyAmountAndSymbol
               || IsTelephoneScope(scope);
    }

    private static bool IsTelephoneScope(InputScopeNameValue scope)
    {
        return scope is InputScopeNameValue.TelephoneNumber
            or InputScopeNameValue.TelephoneCountryCode
            or InputScopeNameValue.TelephoneAreaCode
            or InputScopeNameValue.TelephoneLocalNumber;
    }
}
