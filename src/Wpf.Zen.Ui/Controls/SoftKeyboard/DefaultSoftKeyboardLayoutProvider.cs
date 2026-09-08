// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Text;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Creates the standard touch-oriented layout used by <see cref="SoftKeyboard"/>.
/// </summary>
/// <remarks>
/// Letter rows describe physical Windows virtual-key positions rather than hard-coded Latin text.
/// Labels are resolved through the active HKL, so QWERTY, QWERTZ, and AZERTY layouts remain aligned
/// with the actual keystrokes delivered to TSF. IME profiles commonly retain Latin labels for their
/// composition keys, which is also the behavior reported by the associated Windows layout.
/// </remarks>
public sealed class DefaultSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    private static readonly Key[][] LetterRows =
    [
        [Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P],
        [Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L],
        [Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M],
    ];

    private static readonly string[][] SymbolRows =
    [
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"],
        ["@", "#", "$", "%", "&", "-", "+", "(", ")"],
        ["*", "\"", "'", ":", ";", "!", "?", "/"],
    ];

    private static readonly IReadOnlyDictionary<Key, string[]> Alternates =
        new Dictionary<Key, string[]>
        {
            [Key.A] = ["á", "à", "â", "ä", "ã", "å", "æ"],
            [Key.C] = ["ç"],
            [Key.E] = ["é", "è", "ê", "ë"],
            [Key.I] = ["í", "ì", "î", "ï"],
            [Key.N] = ["ñ"],
            [Key.O] = ["ó", "ò", "ô", "ö", "õ", "ø", "œ"],
            [Key.S] = ["ß", "š"],
            [Key.U] = ["ú", "ù", "û", "ü"],
            [Key.Y] = ["ý", "ÿ"],
            [Key.Z] = ["ž"],
        };

    /// <summary>Gets the shared default provider.</summary>
    public static DefaultSoftKeyboardLayoutProvider Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardLayout CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // InputScope supplies the initial layout only. Explicit Letters/Symbols requests must
        // remain reachable even when the editor advertises numeric input.
        if (context.DisplayMode == SoftKeyboardDisplayMode.Numbers
            || (context.DisplayMode == SoftKeyboardDisplayMode.Automatic && IsNumericScope(context.InputScope)))
        {
            return CreateNumberLayout(context.InputScope);
        }

        if (context.DisplayMode == SoftKeyboardDisplayMode.Symbols)
        {
            return CreateSymbolLayout();
        }

        return CreateLetterLayout(context);
    }

    private static SoftKeyboardLayout CreateLetterLayout(SoftKeyboardLayoutContext context)
    {
        var shifted = context.ShiftState is SoftKeyboardShiftState.Once or SoftKeyboardShiftState.Locked;
        var rows = new List<SoftKeyboardRow>();

        foreach (var keyRow in LetterRows)
        {
            var keys = keyRow.Select(key =>
            {
                string label = KeyboardLayoutLabels.GetLabel(key, shifted);
                IEnumerable<string>? alternates = null;
                if (Alternates.TryGetValue(key, out var variants))
                {
                    alternates = shifted
                        ? variants.Select(static value => value.ToUpperInvariant())
                        : variants;
                }

                return new SoftKeyboardKey(
                    SoftKeyboardKeyKind.VirtualKey,
                    label,
                    label,
                    key,
                    alternates: alternates
                );
            });
            rows.Add(new SoftKeyboardRow(keys));
        }

        rows.Insert(
            2,
            new SoftKeyboardRow(
            [
                new SoftKeyboardKey(SoftKeyboardKeyKind.Shift, "⇧", "Shift", width: 1.35d),
                .. rows[2].Keys,
                new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "⌫", "Backspace", Key.Back, width: 1.35d),
            ])
        );
        rows.RemoveAt(3);

        var bottom = new List<SoftKeyboardKey>
        {
            new(SoftKeyboardKeyKind.ChangeDisplayMode, "&123", "Numbers and symbols", displayMode: SoftKeyboardDisplayMode.Symbols, width: 1.3d),
            new(SoftKeyboardKeyKind.ChangeDisplayMode, "☺", "Emoji", displayMode: SoftKeyboardDisplayMode.Emoji),
            new(SoftKeyboardKeyKind.InputMethods, "⌨", "Input methods"),
            new(SoftKeyboardKeyKind.VirtualKey, "←", "Move cursor left", Key.Left),
            new(SoftKeyboardKeyKind.VirtualKey, "space", "Space", Key.Space, width: 3d),
            new(SoftKeyboardKeyKind.VirtualKey, "→", "Move cursor right", Key.Right),
        };

        AddInputScopeKey(bottom, context.InputScope);
        bottom.Add(new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "↵", "Enter", Key.Enter, width: 1.4d));
        bottom.Add(new SoftKeyboardKey(SoftKeyboardKeyKind.Close, "⌄", "Close keyboard"));
        rows.Add(new SoftKeyboardRow(bottom));

        return new SoftKeyboardLayout(rows);
    }

    private static SoftKeyboardLayout CreateNumberLayout(InputScopeNameValue scope)
    {
        string[][] values = IsTelephoneScope(scope)
            ? [["1", "2", "3"], ["4", "5", "6"], ["7", "8", "9"], ["+", "0", "#"]]
            : [["7", "8", "9"], ["4", "5", "6"], ["1", "2", "3"], ["-", "0", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator]];

        var rows = values
            .Select(row => new SoftKeyboardRow(row.Select(CreateTextKey)))
            .ToList();
        rows.Add(
            new SoftKeyboardRow(
            [
                new SoftKeyboardKey(SoftKeyboardKeyKind.ChangeDisplayMode, "ABC", "Letters", displayMode: SoftKeyboardDisplayMode.Letters, width: 1.4d),
                new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "⌫", "Backspace", Key.Back, width: 1.4d),
                new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "←", "Move cursor left", Key.Left),
                new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "→", "Move cursor right", Key.Right),
                new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "↵", "Enter", Key.Enter, width: 1.4d),
                new SoftKeyboardKey(SoftKeyboardKeyKind.Close, "⌄", "Close keyboard"),
            ])
        );
        return new SoftKeyboardLayout(rows);
    }

    private static SoftKeyboardLayout CreateSymbolLayout()
    {
        var rows = SymbolRows
            .Select(row => new SoftKeyboardRow(row.Select(CreateTextKey)))
            .ToList();
        rows.Add(
            new SoftKeyboardRow(
            [
                new SoftKeyboardKey(SoftKeyboardKeyKind.ChangeDisplayMode, "ABC", "Letters", displayMode: SoftKeyboardDisplayMode.Letters, width: 1.4d),
                new SoftKeyboardKey(SoftKeyboardKeyKind.ChangeDisplayMode, "☺", "Emoji", displayMode: SoftKeyboardDisplayMode.Emoji),
                new SoftKeyboardKey(SoftKeyboardKeyKind.InputMethods, "⌨", "Input methods"),
                new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "←", "Move cursor left", Key.Left),
                new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "space", "Space", Key.Space, width: 2.5d),
                new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "→", "Move cursor right", Key.Right),
                new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "⌫", "Backspace", Key.Back, width: 1.4d),
                new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "↵", "Enter", Key.Enter, width: 1.4d),
                new SoftKeyboardKey(SoftKeyboardKeyKind.Close, "⌄", "Close keyboard"),
            ])
        );
        return new SoftKeyboardLayout(rows);
    }

    private static SoftKeyboardKey CreateTextKey(string value) =>
        new(SoftKeyboardKeyKind.Text, value, value, text: value);

    private static bool IsNumericScope(InputScopeNameValue scope) =>
        scope is InputScopeNameValue.Number
            or InputScopeNameValue.Digits
            or InputScopeNameValue.NumberFullWidth
            or InputScopeNameValue.CurrencyAmount
            or InputScopeNameValue.CurrencyAmountAndSymbol
            || IsTelephoneScope(scope);

    private static bool IsTelephoneScope(InputScopeNameValue scope) =>
        scope is InputScopeNameValue.TelephoneNumber
            or InputScopeNameValue.TelephoneCountryCode
            or InputScopeNameValue.TelephoneAreaCode
            or InputScopeNameValue.TelephoneLocalNumber;

    private static void AddInputScopeKey(List<SoftKeyboardKey> keys, InputScopeNameValue scope)
    {
        if (scope is InputScopeNameValue.EmailSmtpAddress or InputScopeNameValue.EmailUserName)
        {
            keys.Add(CreateTextKey("@"));
            keys.Add(CreateTextKey(".com"));
        }
        else if (scope == InputScopeNameValue.Url)
        {
            keys.Add(CreateTextKey("/"));
            keys.Add(CreateTextKey(".com"));
        }
        else
        {
            keys.Add(CreateTextKey("."));
        }
    }

    /// <summary>
    /// Resolves labels without changing the process input language or retaining dead-key state.
    /// </summary>
    private static class KeyboardLayoutLabels
    {
        internal static string GetLabel(Key key, bool shifted)
        {
            int virtualKey = KeyInterop.VirtualKeyFromKey(key);
            IntPtr layout = SoftKeyboardNativeMethods.GetKeyboardLayout(0);
            var state = new byte[256];
            if (shifted)
            {
                state[0x10] = 0x80;
            }

            var buffer = new StringBuilder(8);
            uint scanCode = SoftKeyboardNativeMethods.MapVirtualKeyEx(
                (uint)virtualKey,
                SoftKeyboardNativeMethods.MapVkToVsc,
                layout
            );
            int length = SoftKeyboardNativeMethods.ToUnicodeEx(
                (uint)virtualKey,
                scanCode,
                state,
                buffer,
                buffer.Capacity,
                0,
                layout
            );

            if (length < 0)
            {
                // ToUnicodeEx stores a dead key in thread-local keyboard state. Calling it again
                // with an empty state clears that state so label generation cannot affect typing.
                _ = SoftKeyboardNativeMethods.ToUnicodeEx(
                    (uint)virtualKey,
                    scanCode,
                    new byte[256],
                    new StringBuilder(8),
                    8,
                    0,
                    layout
                );
            }

            if (length != 1 || buffer.Length == 0 || char.IsControl(buffer[0]))
            {
                string fallback = key.ToString();
                return shifted ? fallback.ToUpperInvariant() : fallback.ToLowerInvariant();
            }

            return buffer[0].ToString();
        }
    }
}
