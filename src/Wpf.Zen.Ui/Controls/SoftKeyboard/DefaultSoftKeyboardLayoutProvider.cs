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
    // Two logical columns form the nominal width of an ordinary character key. The half-key
    // granularity makes physical-row staggering and 1.5-key command caps exactly representable.
    private const int DefaultColumnCount = 20;

    // Icon sources are immutable presentation data. Sharing them avoids rebuilding identical
    // descriptors whenever Shift or the requested surface regenerates the layout; the implicit
    // FluentIconSource data template still creates an independent visual for every key button.
    private static readonly FluentIconSource ShiftIcon = new(SymbolRegular.KeyboardShift24);
    private static readonly FluentIconSource ShiftOnceIcon = new(SymbolRegular.KeyboardShiftUppercase24);
    private static readonly FluentIconSource ShiftLockedIcon = new(SymbolRegular.KeyboardShiftUppercase24, filled: true);
    private static readonly FluentIconSource BackspaceIcon = new(SymbolRegular.Backspace24);
    private static readonly FluentIconSource EmojiIcon = new(SymbolRegular.Emoji24);
    private static readonly FluentIconSource InputMethodsIcon = new(SymbolRegular.Keyboard24);
    private static readonly FluentIconSource MoveLeftIcon = new(SymbolRegular.ArrowLeft24);
    private static readonly FluentIconSource MoveRightIcon = new(SymbolRegular.ArrowRight24);
    private static readonly FluentIconSource SpaceIcon = new(SymbolRegular.Spacebar24);
    private static readonly FluentIconSource EnterIcon = new(SymbolRegular.ArrowEnterLeft24);
    private static readonly FluentIconSource CloseIcon = new(SymbolRegular.ChevronDown24);

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
        var rows = new List<IReadOnlyList<SoftKeyboardKey>>();

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
            rows.Add(keys.ToArray());
        }

        IReadOnlyList<SoftKeyboardKey> thirdRow =
        [
            new SoftKeyboardKey(SoftKeyboardKeyKind.Shift, GetShiftContent(context.ShiftState), "Shift"),
            .. rows[2],
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, BackspaceIcon, "Backspace", Key.Back),
        ];

        var bottom = new List<SoftKeyboardKey>
        {
            new(SoftKeyboardKeyKind.ChangeDisplayMode, "&123", "Numbers and symbols", displayMode: SoftKeyboardDisplayMode.Symbols),
            new(SoftKeyboardKeyKind.ChangeDisplayMode, EmojiIcon, "Emoji", displayMode: SoftKeyboardDisplayMode.Emoji),
            new(SoftKeyboardKeyKind.InputMethods, InputMethodsIcon, "Input methods"),
            new(SoftKeyboardKeyKind.VirtualKey, MoveLeftIcon, "Move cursor left", Key.Left),
            new(SoftKeyboardKeyKind.VirtualKey, SpaceIcon, "Space", Key.Space),
            new(SoftKeyboardKeyKind.VirtualKey, MoveRightIcon, "Move cursor right", Key.Right),
        };

        AddInputScopeKey(bottom, context.InputScope);
        bottom.Add(new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, EnterIcon, "Enter", Key.Enter));
        bottom.Add(new SoftKeyboardKey(SoftKeyboardKeyKind.Close, CloseIcon, "Close keyboard"));

        var positionedKeys = new List<SoftKeyboardKey>();
        AddPositionedRow(positionedKeys, rows[0], 0, 0, 2);
        AddPositionedRow(positionedKeys, rows[1], 1, 1, 2);
        AddPositionedRow(positionedKeys, thirdRow, 2, 0, [2, 2, 2, 2, 2, 2, 2, 2, 4]);
        AddPositionedRow(
            positionedKeys,
            bottom,
            3,
            0,
            bottom.Count == 9
                ? [2, 2, 2, 2, 4, 2, 2, 2, 2]
                : [2, 2, 2, 2, 2, 2, 2, 2, 2, 2]
        );
        return new SoftKeyboardLayout(positionedKeys, DefaultColumnCount);
    }

    private static SoftKeyboardLayout CreateNumberLayout(InputScopeNameValue scope)
    {
        string[][] values = IsTelephoneScope(scope)
            ? [["1", "2", "3"], ["4", "5", "6"], ["7", "8", "9"], ["+", "0", "#"]]
            : [["7", "8", "9"], ["4", "5", "6"], ["1", "2", "3"], ["-", "0", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator]];

        IReadOnlyList<IReadOnlyList<SoftKeyboardKey>> rows = values
            .Select(row => (IReadOnlyList<SoftKeyboardKey>)row.Select(CreateTextKey).ToArray())
            .ToList();
        IReadOnlyList<SoftKeyboardKey> commandRow =
        [
            new SoftKeyboardKey(SoftKeyboardKeyKind.ChangeDisplayMode, "ABC", "Letters", displayMode: SoftKeyboardDisplayMode.Letters),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, BackspaceIcon, "Backspace", Key.Back),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, MoveLeftIcon, "Move cursor left", Key.Left),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, MoveRightIcon, "Move cursor right", Key.Right),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, EnterIcon, "Enter", Key.Enter),
            new SoftKeyboardKey(SoftKeyboardKeyKind.Close, CloseIcon, "Close keyboard"),
        ];

        var keys = new List<SoftKeyboardKey>();
        for (var row = 0; row < rows.Count; row++)
        {
            // Eighteen occupied columns centered inside twenty columns keep the three-key number pad
            // broad without introducing a separate panel sizing mode.
            AddPositionedRow(keys, rows[row], row, 1, 6);
        }

        AddPositionedRow(keys, commandRow, rows.Count, 0, [3, 3, 3, 3, 4, 4]);
        return new SoftKeyboardLayout(keys, DefaultColumnCount);
    }

    private static SoftKeyboardLayout CreateSymbolLayout()
    {
        IReadOnlyList<IReadOnlyList<SoftKeyboardKey>> rows = SymbolRows
            .Select(row => (IReadOnlyList<SoftKeyboardKey>)row.Select(CreateTextKey).ToArray())
            .ToList();
        IReadOnlyList<SoftKeyboardKey> commandRow =
        [
            new SoftKeyboardKey(SoftKeyboardKeyKind.ChangeDisplayMode, "ABC", "Letters", displayMode: SoftKeyboardDisplayMode.Letters),
            new SoftKeyboardKey(SoftKeyboardKeyKind.ChangeDisplayMode, EmojiIcon, "Emoji", displayMode: SoftKeyboardDisplayMode.Emoji),
            new SoftKeyboardKey(SoftKeyboardKeyKind.InputMethods, InputMethodsIcon, "Input methods"),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, MoveLeftIcon, "Move cursor left", Key.Left),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, SpaceIcon, "Space", Key.Space),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, MoveRightIcon, "Move cursor right", Key.Right),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, BackspaceIcon, "Backspace", Key.Back),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, EnterIcon, "Enter", Key.Enter),
            new SoftKeyboardKey(SoftKeyboardKeyKind.Close, CloseIcon, "Close keyboard"),
        ];

        var keys = new List<SoftKeyboardKey>();
        AddPositionedRow(keys, rows[0], 0, 0, 2);
        AddPositionedRow(keys, rows[1], 1, 1, 2);
        AddPositionedRow(keys, rows[2], 2, 2, 2);
        AddPositionedRow(keys, commandRow, 3, 0, [3, 2, 2, 2, 3, 2, 2, 2, 2]);
        return new SoftKeyboardLayout(keys, DefaultColumnCount);
    }

    /// <summary>
    /// Selects a key-cap description that distinguishes inactive, one-shot, and locked Shift
    /// without placing stateful visual elements in the immutable layout model.
    /// </summary>
    private static FluentIconSource GetShiftContent(SoftKeyboardShiftState shiftState) => shiftState switch
    {
        SoftKeyboardShiftState.Off    => ShiftIcon,
        SoftKeyboardShiftState.Once   => ShiftOnceIcon,
        SoftKeyboardShiftState.Locked => ShiftLockedIcon,
        _ => throw new ArgumentOutOfRangeException(nameof(shiftState)),
    };

    private static SoftKeyboardKey CreateTextKey(string value) =>
        new(SoftKeyboardKeyKind.Text, value, value, text: value);

    /// <summary>
    /// Places equal-width keys consecutively while retaining row literals as the readable source of
    /// the default layout.
    /// </summary>
    private static void AddPositionedRow(
        List<SoftKeyboardKey> destination,
        IReadOnlyList<SoftKeyboardKey> keys,
        int row,
        int startColumn,
        int columnSpan
    )
    {
        AddPositionedRow(destination, keys, row, startColumn, Enumerable.Repeat(columnSpan, keys.Count));
    }

    /// <summary>
    /// Places a row with explicit spans. Keeping this conversion at the provider boundary leaves the
    /// public model flat while making wide command caps and half-key offsets exact layout data.
    /// </summary>
    private static void AddPositionedRow(
        List<SoftKeyboardKey> destination,
        IReadOnlyList<SoftKeyboardKey> keys,
        int row,
        int startColumn,
        IEnumerable<int> columnSpans
    )
    {
        int column = startColumn;
        using IEnumerator<int> spans = columnSpans.GetEnumerator();
        foreach (SoftKeyboardKey key in keys)
        {
            if (!spans.MoveNext())
            {
                throw new ArgumentException("Every key in a row requires a column span.", nameof(columnSpans));
            }

            int columnSpan = spans.Current;
            destination.Add(key.WithPlacement(row, column, columnSpan));
            column = checked(column + columnSpan);
        }

        if (spans.MoveNext())
        {
            throw new ArgumentException("A row cannot contain more column spans than keys.", nameof(columnSpans));
        }
    }

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
