// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

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
        var rows    = new List<IReadOnlyList<SoftKeyboardKey>>();

        foreach (var keyRow in LetterRows)
        {
            var keys = keyRow.Select(key =>
            {
                var label = KeyboardLayoutLabels.GetLabel(key, shifted);

                IEnumerable<string>? alternates = null;

                if (Alternates.TryGetValue(key, out var variants))
                {
                    alternates = shifted
                        ? variants.Select(static value => value.ToUpperInvariant())
                        : variants;
                }

                return new SoftKeyboardKey(
                    kind:           SoftKeyboardKeyKind.VirtualKey,
                    content:        label,
                    automationName: label,
                    key:            key,
                    alternates:     alternates
                );
            });

            rows.Add([.. keys]);
        }

        IReadOnlyList<SoftKeyboardKey> thirdRow =
        [
            new(
                kind:           SoftKeyboardKeyKind.Shift,
                content:        GetShiftContent(context.ShiftState),
                automationName: "Shift"
            ),

            ..
            rows[2],

            new(
                kind:            SoftKeyboardKeyKind.VirtualKey,
                content:         BackspaceIcon,
                automationName:  "Backspace",
                key:             Key.Back,
                isRepeatEnabled: true
            ),
        ];

        var native  = context.ImeState == InputMethodState.On;
        var address = context.InputScope is InputScopeNameValue.Url or InputScopeNameValue.EmailSmtpAddress;

        // 第四排
        var bottom = address ? CreateAddressBottomRow(context) :
        [
            // 数字和符号面板切换键
            new SoftKeyboardKey(
                SoftKeyboardKeyKind.ChangeDisplayMode,
                "123",
                "Numbers and symbols",
                displayMode: SoftKeyboardDisplayMode.Symbols
            ),

            // 输入法开关
            CreateImeKey(context),
            CreateTextKey(native ? "，" : ","),

            new SoftKeyboardKey(
                SoftKeyboardKeyKind.VirtualKey,
                MoveLeftIcon,
                "Move cursor left",
                Key.Left,
                isRepeatEnabled: true
            ),

            new SoftKeyboardKey(
                SoftKeyboardKeyKind.VirtualKey,
                SpaceIcon,
                "Space",
                Key.Space
            ),

            new SoftKeyboardKey(
                SoftKeyboardKeyKind.VirtualKey,
                MoveRightIcon,
                "Move cursor right",
                Key.Right,
                isRepeatEnabled: true
            ),

            CreateTextKey(native ? "。" : "."),

            // 输入法选择键
            new SoftKeyboardKey(SoftKeyboardKeyKind.InputMethods, InputMethodsIcon, "Input methods"),

            // 回车键
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, EnterIcon, "Enter", Key.Enter),
        ];

        var positionedKeys = new List<SoftKeyboardKey>();

        AddPositionedRow(positionedKeys, rows[0], 0, 0, 2);

        if (!address && native && context.Culture.TwoLetterISOLanguageName == "ja")
        {
            AddPositionedRow(
                positionedKeys,
                [
                    ..
                    rows[1],
                    new SoftKeyboardKey(
                        SoftKeyboardKeyKind.Text,
                        "ー",
                        "Long vowel mark and middle dot",
                        text: "ー",
                        alternates: ["・"],
                        alternateHint: "・"
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

        // URL shortcuts use ten ordinary-width keys; email retains a double-width space key.
        AddPositionedRow(positionedKeys, bottom, 3, 0, context.InputScope switch
        {
            InputScopeNameValue.Url              => [2, 2, 2, 2, 2, 2, 2, 2, 2, 2],
            InputScopeNameValue.EmailSmtpAddress => [2, 2, 2, 4, 2, 2, 2, 2, 2],
            _                                    => [2, 2, 2, 2, 4, 2, 2, 2, 2],
        });

        return new SoftKeyboardLayout(positionedKeys, DefaultColumnCount);
    }

    /// <summary>
    /// Supplies literal address shortcuts while retaining the ordinary virtual letter keys and IME
    /// actions. Input scope specializes the letter surface even after returning from a symbol page.
    /// </summary>
    private static IReadOnlyList<SoftKeyboardKey> CreateAddressBottomRow(SoftKeyboardLayoutContext context)
    {
        var url = context.InputScope == InputScopeNameValue.Url;

        // Address punctuation and protocol/domain fragments are Unicode literals, independent of
        // Shift and native punctuation mode. In particular, http must not append :// implicitly.
        return
        [
            new SoftKeyboardKey(
                kind:           SoftKeyboardKeyKind.ChangeDisplayMode,
                content:        "123",
                automationName: "Numbers and symbols",
                displayMode:    SoftKeyboardDisplayMode.Symbols
            ),

            CreateImeKey(context: context),

            url
                ? new SoftKeyboardKey(
                    kind:           SoftKeyboardKeyKind.Text,
                    content:        "http",
                    automationName: "http",
                    text:           "http",
                    alternates:     ["https"],
                    alternateHint:  "https"
                )
                : CreateTextKey("@"),

            ..

            url ? new[] { CreateTextKey(":") } : Array.Empty<SoftKeyboardKey>(),

            new SoftKeyboardKey(
                kind:           SoftKeyboardKeyKind.VirtualKey,
                content:        SpaceIcon,
                automationName: "Space",
                key:            Key.Space
            ),

            new SoftKeyboardKey(
                kind:           SoftKeyboardKeyKind.Text,
                content:        ".com",
                automationName: ".com",
                text:           ".com",
                alternates:     GetDomainAlternates(context.Culture),
                alternateHint:  ".net"
            ),

            new SoftKeyboardKey(
                kind:           SoftKeyboardKeyKind.Text,
                content:        ".",
                automationName: "Period",
                text:           ".",
                alternates:     ["?"],
                alternateHint:  "?"
            ),

            CreateTextKey(url ? "/" : "-"),

            new SoftKeyboardKey(
                kind:           SoftKeyboardKeyKind.InputMethods,
                content:        InputMethodsIcon,
                automationName: "Input methods"
            ),

            new SoftKeyboardKey(
                kind:           SoftKeyboardKeyKind.VirtualKey,
                content:        EnterIcon,
                automationName: "Enter",
                key:            Key.Enter
            ),
        ];
    }

    /// <summary>
    /// Keeps common domain shortcuts and adds at most one country suffix for the active input language.
    /// </summary>
    private static IEnumerable<string> GetDomainAlternates(CultureInfo culture)
    {
        yield return ".net";
        yield return ".org";
        yield return ".edu";
        yield return ".gov";

        string countryCode;
        try
        {
            // The context follows the input language, not the application's UI or formatting culture.
            // Custom providers may supply a neutral culture; use its default specific region. An
            // invariant or unresolvable culture has no country shortcut, leaving the common ones usable.
            var regionalCulture = culture.IsNeutralCulture
                ? CultureInfo.CreateSpecificCulture(culture.Name)
                : culture;

            countryCode = new RegionInfo(regionalCulture.Name).TwoLetterISORegionName;
        }
        catch (ArgumentException)
        {
            yield break;
        }

        // Macro-regions (such as 419) are not country domains. The UK's domain uses UK rather than GB.
        if (countryCode.Length == 2)
        {
            yield return countryCode == "GB" ? ".uk" : "." + countryCode.ToLowerInvariant();
        }
    }

    private static SoftKeyboardLayout CreateNumberLayout(SoftKeyboardLayoutContext context)
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

        var keys = new List<SoftKeyboardKey>();

        for (var row = 0; row < 4; row++)
        {
            AddPositionedRow(
                destination: keys,
                keys:        values[row].Select(CreateTextKey).ToArray(),
                row:         row,
                startColumn: 0,
                columnSpan:  5
            );
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

        keys.Add(CreateImeKey(context).WithPlacement(1, 15, 5));

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
                kind:           SoftKeyboardKeyKind.ChangeDisplayMode,
                content:        "ABC",
                automationName: "Letters",
                displayMode:    SoftKeyboardDisplayMode.Letters,
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

        return new SoftKeyboardLayout(keys, DefaultColumnCount);
    }

    private static SoftKeyboardLayout CreateSymbolLayout(SoftKeyboardLayoutContext context)
    {
        var native = context.ImeState == InputMethodState.On;
        var more   = context.DisplayMode == SoftKeyboardDisplayMode.MoreSymbols;

        string[] second = (native, more) switch
        {
            (false, false) => ["!", "@", "#", "$", "%", "&", "_", "-", "=", "+"],
            (false, true ) => ["%", "[", "]", "{", "}", "<", ">", "€", "£", "¥"],
            (true,  false) => ["！", "@", "#", "￥", "%", "&", "_", "-", "=", "+"],
            (true,  true ) => ["、", "【", "】", "｛", "｝", "〈", "〉", "*", "…", "•"],
        };

        string[] third = (native, more) switch
        {
            (false, false) => [";", ":", "(", ")", "/", "'", "\""],
            (false, true ) => ["*", "`", "°", "×", "÷", "~", "|"],
            (true,  false) => ["/", "；", "：", "“", "”", "（", "）"],
            (true,  true ) => ["µ", "½", "｜", "©", "¶", "°", "~"],
        };

        IReadOnlyList<SoftKeyboardKey> thirdRow =
        [
            new(
                kind:           SoftKeyboardKeyKind.ChangeDisplayMode,
                content:        more ? PreviousSymbolsIcon : MoreSymbolsIcon,
                automationName: more ? "Previous symbols" : "More symbols",
                displayMode:    more ? SoftKeyboardDisplayMode.Symbols : SoftKeyboardDisplayMode.MoreSymbols
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
                kind:           SoftKeyboardKeyKind.ChangeDisplayMode,
                content:        "ABC",
                automationName: "Letters",
                displayMode:    SoftKeyboardDisplayMode.Letters
            ),

            CreateImeKey(context: context),
            CreateTextKey(value: native ? "，" : ","),

            new(
                kind:           SoftKeyboardKeyKind.VirtualKey,
                content:        SpaceIcon,
                automationName: "Space",
                key:            Key.Space
            ),

            CreateTextKey(value: native ? "。" : "."),

            CreateTextKey(
                value: more
                    ? native ? "^" : "\\"
                    : native ? "？" : "?"
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

        var keys = new List<SoftKeyboardKey>();

        AddPositionedRow(
            destination: keys,
            keys: [.. "1234567890".Select(selector: c => CreateTextKey(value: c.ToString()))],
            row: 0,
            startColumn: 0,
            columnSpan: 2
        );

        AddPositionedRow(
            destination: keys,
            keys: [.. second.Select(selector: CreateTextKey)],
            row: 1,
            startColumn: 0,
            columnSpan: 2
        );

        AddPositionedRow(
            destination: keys,
            keys: thirdRow,
            row: 2,
            startColumn: 0,
            columnSpans: [3, 2, 2, 2, 2, 2, 2, 2, 3]
        );

        AddPositionedRow(
            destination: keys,
            keys: bottom,
            row: 3,
            startColumn: 0,
            columnSpans: [2, 2, 2, 6, 2, 2, 2, 2]
        );

        return new SoftKeyboardLayout(keys: keys, columnCount: DefaultColumnCount);
    }

    // The cap reports the current state. Latin A is culture-neutral; native modes use familiar
    // language abbreviations while unknown services retain the same reliable toggle action.
    private static SoftKeyboardKey CreateImeKey(SoftKeyboardLayoutContext context)
    {
        return new SoftKeyboardKey(
            kind: SoftKeyboardKeyKind.ToggleIme,
            content: context.ImeState != InputMethodState.On
                ? "A"
                : context.Culture.TwoLetterISOLanguageName switch
                {
                    "zh" => "中",
                    "ja" => "日",
                    "ko" => "한",
                    _ => "文",
                },
            automationName: context.ImeState == InputMethodState.On
                ? "Native input; switch to direct input"
                : "Direct input; enable input method");
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

    private static SoftKeyboardKey CreateTextKey(string value)
    {
        return new SoftKeyboardKey(SoftKeyboardKeyKind.Text, value, value, text: value);
    }

    /// <summary>
    /// Places equal-width keys consecutively while retaining row literals as the readable source of
    /// the default layout.
    /// </summary>
    private static void AddPositionedRow(
        List<SoftKeyboardKey> destination,
        IReadOnlyList<SoftKeyboardKey> keys,
        int row,
        int startColumn,
        int columnSpan)
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
        IEnumerable<int> columnSpans)
    {
        var column = startColumn;
        using var spans = columnSpans.GetEnumerator();

        foreach (var key in keys)
        {
            if (!spans.MoveNext())
            {
                throw new ArgumentException("Every key in a row requires a column span.", nameof(columnSpans));
            }

            var columnSpan = spans.Current;
            destination.Add(key.WithPlacement(row, column, columnSpan));
            column = checked(column + columnSpan);
        }

        if (spans.MoveNext())
        {
            throw new ArgumentException("A row cannot contain more column spans than keys.", nameof(columnSpans));
        }
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

    /// <summary>
    /// Resolves labels without changing the process input language or retaining dead-key state.
    /// </summary>
    private static class KeyboardLayoutLabels
    {
        internal static unsafe string GetLabel(Key key, bool shifted)
        {
            var   virtualKey = KeyInterop.VirtualKeyFromKey(key);
            var   layout     = PInvoke.GetKeyboardLayout(0);
            byte* state      = stackalloc byte[256];

            new Span<byte>(state, 256).Clear();

            if (shifted)
            {
                state[0x10] = 0x80;
            }

            const int bufferLength = 8;
            char*     buffer       = stackalloc char[bufferLength];

            var scanCode = PInvoke.MapVirtualKeyEx(
                (uint)virtualKey,
                MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC,
                layout
            );

            // Flag 4 (Windows 10 1607+) preserves the keyboard buffer: painting a cap must
            // never consume a hardware dead key or manufacture a composition transition.
            // GetKeyboardLayout returns a borrowed HKL. The pointer overload accepts it directly
            // and keeps both buffers scoped to this call without wrapping the layout in an owning handle.
            var length = PInvoke.ToUnicodeEx(
                (uint)virtualKey,
                scanCode,
                state,
                buffer,
                bufferLength,
                4,
                layout
            );

            if (length != 1 || char.IsControl(buffer[0]))
            {
                var fallback = key.ToString();
                return shifted ? fallback.ToUpperInvariant() : fallback.ToLowerInvariant();
            }

            return buffer[0].ToString();
        }
    }
}
