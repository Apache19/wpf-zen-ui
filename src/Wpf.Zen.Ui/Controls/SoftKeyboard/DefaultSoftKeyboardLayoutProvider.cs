// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Text;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

// todo: 应增加适合输入网址和电子邮件地址的键盘布局

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
    private static readonly FluentIconSource InputMethodsIcon = new(SymbolRegular.Globe24);
    private static readonly FluentIconSource MoveLeftIcon = new(SymbolRegular.ArrowLeft24);
    private static readonly FluentIconSource MoveRightIcon = new(SymbolRegular.ArrowRight24);
    private static readonly FluentIconSource PreviousSymbolsIcon = new(SymbolRegular.CaretLeft24, filled: true);
    private static readonly FluentIconSource MoreSymbolsIcon = new(SymbolRegular.CaretRight24, filled: true);
    private static readonly FluentIconSource SpaceIcon = new(SymbolRegular.Spacebar24);
    private static readonly FluentIconSource EnterIcon = new(SymbolRegular.ArrowEnterLeft24);

    private static readonly Key[][] LetterRows =
    [
        [Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P],
        [Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L],
        [Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M],
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
            return CreateNumberLayout(context);
        }

        if (context.DisplayMode is SoftKeyboardDisplayMode.Symbols or SoftKeyboardDisplayMode.MoreSymbols)
        {
            return CreateSymbolLayout(context);
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
            new (SoftKeyboardKeyKind.Shift, GetShiftContent(context.ShiftState), "Shift"),
            .. rows[2],
            new (SoftKeyboardKeyKind.VirtualKey, BackspaceIcon, "Backspace", Key.Back, isRepeatEnabled: true),
        ];

        bool native = context.ImeState == InputMethodState.On;

        // 第四排
        IReadOnlyList<SoftKeyboardKey> bottom =
        [
            // 数字和符号面板切换键
            new(SoftKeyboardKeyKind.ChangeDisplayMode, "123", "Numbers and symbols", displayMode: SoftKeyboardDisplayMode.Symbols),

            // 输入法开关
            CreateImeKey(context),
            CreateTextKey(native ? "，" : ","),

            new(SoftKeyboardKeyKind.VirtualKey, MoveLeftIcon, "Move cursor left", Key.Left, isRepeatEnabled: true),
            new(SoftKeyboardKeyKind.VirtualKey, SpaceIcon, "Space", Key.Space),
            new(SoftKeyboardKeyKind.VirtualKey, MoveRightIcon, "Move cursor right", Key.Right, isRepeatEnabled: true),

            CreateTextKey(native ? "。" : "."),

            // 输入法选择键（地球）
            new(SoftKeyboardKeyKind.InputMethods, InputMethodsIcon, "Input methods"),

            // 回车键
            new(SoftKeyboardKeyKind.VirtualKey, EnterIcon, "Enter", Key.Enter),
        ];

        var positionedKeys = new List<SoftKeyboardKey>();

        AddPositionedRow(positionedKeys, rows[0], 0, 0, 2);

        if (native && context.Culture.TwoLetterISOLanguageName == "ja")
        {
            AddPositionedRow(
                positionedKeys,
                [
                    ..
                    rows[1],
                    new SoftKeyboardKey(
                        SoftKeyboardKeyKind.Text,
                        "ー・",
                        "Long vowel mark and middle dot",
                        text: "ー",
                        alternates: ["・"]
                    )
                ],
                row: 1,
                startColumn: 0,
                columnSpan: 2
            );
        }
        else
        {
            AddPositionedRow(positionedKeys, rows[1], 1, 1, 2);
        }

        AddPositionedRow(positionedKeys, thirdRow, 2, 0, [2, 2, 2, 2, 2, 2, 2, 2, 4]);
        AddPositionedRow(positionedKeys, bottom, 3, 0, [2, 2, 2, 2, 4, 2, 2, 2, 2]);

        return new SoftKeyboardLayout(positionedKeys, DefaultColumnCount);
    }

    private static SoftKeyboardLayout CreateNumberLayout(SoftKeyboardLayoutContext context)
    {
        var scope = context.InputScope;
        string[][] values = IsTelephoneScope(scope)
            ? [["1", "2", "3"], ["4", "5", "6"], ["7", "8", "9"], ["+", "0", "#"]]
            : [["7", "8", "9"], ["4", "5", "6"], ["1", "2", "3"], ["-", "0", context.Culture.NumberFormat.NumberDecimalSeparator]];
        var keys = new List<SoftKeyboardKey>();
        for (int row = 0; row < 4; row++)
        {
            AddPositionedRow(keys, values[row].Select(CreateTextKey).ToArray(), row, 0, 5);
        }

        // Numeric input uses the same four tracks as letters and symbols. Commands occupy a
        // fourth column so input-scope changes cannot add a fifth row to the keyboard.
        keys.Add(new(SoftKeyboardKeyKind.VirtualKey, BackspaceIcon, "Backspace", Key.Back, row: 0, column: 15, columnSpan: 5, isRepeatEnabled: true));
        keys.Add(CreateImeKey(context).WithPlacement(1, 15, 5));
        keys.Add(new(SoftKeyboardKeyKind.InputMethods, InputMethodsIcon, "Input methods", row: 2, column: 15, columnSpan: 5));
        keys.Add(new(SoftKeyboardKeyKind.ChangeDisplayMode, "ABC", "Letters", displayMode: SoftKeyboardDisplayMode.Letters, row: 3, column: 15, columnSpan: 2));
        keys.Add(new(SoftKeyboardKeyKind.VirtualKey, EnterIcon, "Enter", Key.Enter, row: 3, column: 17, columnSpan: 3));
        return new SoftKeyboardLayout(keys, DefaultColumnCount);
    }

    private static SoftKeyboardLayout CreateSymbolLayout(SoftKeyboardLayoutContext context)
    {
        bool native = context.ImeState == InputMethodState.On;
        bool more = context.DisplayMode == SoftKeyboardDisplayMode.MoreSymbols;
        string[] second = (native, more) switch
        {
            (false, false) => ["!", "@", "#", "$", "%", "&", "_", "-", "=", "+"],
            (false, true) => ["%", "[", "]", "{", "}", "<", ">", "€", "£", "¥"],
            (true, false) => ["！", "@", "#", "￥", "%", "&", "_", "-", "=", "+"],
            (true, true) => ["、", "【", "】", "｛", "｝", "〈", "〉", "*", "…", "•"],
        };
        string[] third = (native, more) switch
        {
            (false, false) => [";", ":", "(", ")", "/", "'", "\""],
            (false, true) => ["*", "`", "°", "×", "÷", "~", "|"],
            (true, false) => ["/", "；", "：", "“", "”", "（", "）"],
            (true, true) => ["µ", "½", "｜", "©", "¶", "°", "~"],
        };
        IReadOnlyList<SoftKeyboardKey> thirdRow =
        [
            new(SoftKeyboardKeyKind.ChangeDisplayMode, more ? PreviousSymbolsIcon : MoreSymbolsIcon, more ? "Previous symbols" : "More symbols", displayMode: more ? SoftKeyboardDisplayMode.Symbols : SoftKeyboardDisplayMode.MoreSymbols),
            .. third.Select(CreateTextKey),
            new(SoftKeyboardKeyKind.VirtualKey, BackspaceIcon, "Backspace", Key.Back, isRepeatEnabled: true),
        ];
        IReadOnlyList<SoftKeyboardKey> bottom =
        [
            new(SoftKeyboardKeyKind.ChangeDisplayMode, "ABC", "Letters", displayMode: SoftKeyboardDisplayMode.Letters),
            CreateImeKey(context),
            CreateTextKey(native ? "，" : ","),
            new(SoftKeyboardKeyKind.VirtualKey, SpaceIcon, "Space", Key.Space),
            CreateTextKey(native ? "。" : "."),
            CreateTextKey(more ? (native ? "^" : "\\") : (native ? "？" : "?")),
            new(SoftKeyboardKeyKind.InputMethods, InputMethodsIcon, "Input methods"),
            new(SoftKeyboardKeyKind.VirtualKey, EnterIcon, "Enter", Key.Enter),
        ];
        var keys = new List<SoftKeyboardKey>();
        AddPositionedRow(keys, "1234567890".Select(c => CreateTextKey(c.ToString())).ToArray(), 0, 0, 2);
        AddPositionedRow(keys, second.Select(CreateTextKey).ToArray(), 1, 0, 2);
        AddPositionedRow(keys, thirdRow, 2, 0, [3, 2, 2, 2, 2, 2, 2, 2, 3]);
        AddPositionedRow(keys, bottom, 3, 0, [2, 2, 2, 6, 2, 2, 2, 2]);
        return new SoftKeyboardLayout(keys, DefaultColumnCount);
    }

    // The cap reports the current state. Latin A is culture-neutral; native modes use familiar
    // language abbreviations while unknown services retain the same reliable toggle action.
    private static SoftKeyboardKey CreateImeKey(SoftKeyboardLayoutContext context) => new(
        SoftKeyboardKeyKind.ToggleIme,
        context.ImeState != InputMethodState.On ? "A" : context.Culture.TwoLetterISOLanguageName switch
        {
            "zh" => "中",
            "ja" => "日",
            "ko" => "한",
            _ => "文",
        },
        context.ImeState == InputMethodState.On ? "Native input; switch to direct input" : "Direct input; enable input method");

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
            // Flag 4 (Windows 10 1607+) preserves the keyboard buffer: painting a cap must
            // never consume a hardware dead key or manufacture a composition transition.
            int length = SoftKeyboardNativeMethods.ToUnicodeEx(
                (uint)virtualKey,
                scanCode,
                state,
                buffer,
                buffer.Capacity,
                4,
                layout
            );

            if (length != 1 || buffer.Length == 0 || char.IsControl(buffer[0]))
            {
                string fallback = key.ToString();
                return shifted ? fallback.ToUpperInvariant() : fallback.ToLowerInvariant();
            }

            return buffer[0].ToString();
        }
    }
}
