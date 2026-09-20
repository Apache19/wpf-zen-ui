// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Text;
using System.Windows.Input;
using Wpf.Zen.Ui.VirtualInput.Extensions.Routing;
using Wpf.Zen.Ui.VirtualInput.Layouts.Internal;
using static Wpf.Zen.Ui.VirtualInput.Layouts.Internal.KeyboardLayoutParts;

namespace Wpf.Zen.Ui.VirtualInput.Extensions.Layouts;

/// <summary>
/// Supplies the physical character and conversion keys needed by Japanese Romaji and Kana input.
/// Address, numeric and explicit literal-symbol pages retain their specialized providers.
/// </summary>
/// <remarks>
/// Every character on the main page is a virtual key, including digits and punctuation, so the
/// IME can consume it within an existing composition. Dual caps describe the ordinary and Kana
/// mappings of the captured Windows layout, supporting both input styles on the same surface.
/// </remarks>
internal sealed class JapaneseSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    private static readonly Key[] Digits =
        [Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9, Key.D0];

    private static readonly Key[] TopLetters =
        [Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P];

    private static readonly Key[] HomeLetters =
        [Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L];

    private static readonly Key[] BottomLetters =
        [Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M];

    private static readonly CompactSoftKeyboardLayoutProvider Symbols = new(
        new SoftKeyboardCompactRepertoire
        {
            Comma             = "、",
            Period            = "。",
            SymbolsTail       = "？",
            MoreSymbolsTail   = "〜",
            SymbolsSecond     = ["！", "？", "＠", "＃", "￥", "％", "＆", "＊", "ー", "＝"],
            SymbolsThird      = ["「", "」", "（", "）", "・", "：", "；"],
            MoreSymbolsSecond = ["『", "』", "【", "】", "〈", "〉", "《", "》", "〔", "〕"],
            MoreSymbolsThird  = ["［", "］", "｛", "｝", "…", "※", "〒"],
        }
    );

    /// <summary>
    /// Gets the physical layout with ordinary and Kana labels for both input styles.
    /// </summary>
    internal static JapaneseSoftKeyboardLayoutProvider Default { get; } = new();

    private JapaneseSoftKeyboardLayoutProvider()
    {
    }

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!ProfileLanguage.IsJapanese(context))
        {
            return null;
        }

        var specialized = UrlSoftKeyboardLayoutProvider.Default.CreateLayout(context)
                          ?? EmailSoftKeyboardLayoutProvider.Default.CreateLayout(context)
                          ?? NumberSoftKeyboardLayoutProvider.Default.CreateLayout(context);

        if (specialized is not null)
        {
            return specialized;
        }

        return context.Page switch
        {
            SoftKeyboardPages.Automatic or SoftKeyboardPages.Letters   => CreatePhysicalLayout(context),
            SoftKeyboardPages.Symbols or SoftKeyboardPages.MoreSymbols => Symbols.CreateLayout(context),
            _ => null,
        };
    }

    /// <summary>
    /// Builds six rows with all ten digits, 26 letters and twelve OEM character keys.
    /// OEM placement follows the captured layout's JIS signature instead of the language alone.
    /// </summary>
    private SoftKeyboardLayout CreatePhysicalLayout(SoftKeyboardLayoutContext context)
    {
        var jis = KeyboardLayoutLabels.GetLabel(Key.OemPlus, false, context.KeyboardLayoutHandle) == ";";

        Key[] numberRow = jis
            ? [.. Digits, Key.OemMinus, Key.Oem7, Key.Oem5]
            : [Key.Oem3, .. Digits, Key.OemMinus, Key.OemPlus];

        Key[] topRow = jis
            ? [.. TopLetters, Key.Oem3, Key.Oem4]
            : [.. TopLetters, Key.Oem4, Key.Oem6];

        Key[] homeRow = jis
            ? [.. HomeLetters, Key.OemPlus, Key.Oem1, Key.Oem6]
            : [.. HomeLetters, Key.Oem1, Key.Oem7, Key.Oem5];

        Key[] bottomRow = [.. BottomLetters, Key.OemComma, Key.OemPeriod, Key.Oem2, Key.Oem102];

        var builder = new SoftKeyboardLayoutBuilder(30);

        builder.AddRow(
            0,
            [
                Command(Key.Escape, "Esc", "取消组合 / Escape"),
                Command(Key.F6, "F6", "转换为平假名", "ひらがな"),
                Command(Key.F7, "F7", "转换为全角片假名", "カタカナ"),
                Command(Key.F8, "F8", "转换为半角片假名", "ｶﾀｶﾅ"),
                Command(Key.F9, "F9", "转换为全角字母数字", "ＡＢＣ"),
                Command(Key.F10, "F10", "转换为半角字母数字", "ABC"),
                new SoftKeyboardKey(SoftKeyboardKeyKind.InputMethods, InputMethodsIcon, "Input methods"),
            ],
            [4, 4, 4, 4, 4, 4, 6]
        );

        builder.AddRow(1, numberRow.Select(key => Character(context, key)), 2);
        builder.AddRow(1, [Command(Key.Back, BackspaceIcon, "Backspace", repeat: true)], 4, 26);
        builder.AddRow(2, [Command(Key.Tab, "Tab", "候选 / Tab")], 3);
        builder.AddRow(2, topRow.Select(key => Character(context, key)), 2, 3);
        builder.AddRow(2, [Command(Key.Delete, "Del", "Delete", repeat: true)], 3, 27);
        builder.AddRow(3, homeRow.Select(key => Character(context, key)), 2, 1);
        builder.AddRow(3, [Command(Key.Enter, EnterIcon, "确定 / Enter")], 5, 25);
        builder.AddRow(4, [new SoftKeyboardKey(SoftKeyboardKeyKind.Shift, GetShiftContent(context.ShiftState), "Shift")], 4);
        builder.AddRow(4, bottomRow.Select(key => Character(context, key)), 2, 4);
        builder.AddRow(4, [Command(Key.Up, "↑", "上一候选 / Up", repeat: true)], 4, 26);

        builder.AddRow(
            5,
            [
                new SoftKeyboardKey(SoftKeyboardKeyKind.ChangePage, "記号", "Literal symbols",
                    page: SoftKeyboardPages.Symbols),
                ..
                context.HasInputModeButton ? new[] { CreateInputModeKey(context) } : [],
                Command(Key.ImeNonConvert, "無変換", "无变换 / NonConvert"),
                Command(Key.Space, SpaceIcon, "空格 / 转换候选"),
                Command(Key.ImeConvert, "変換", "变换 / Convert"),
                Command(Key.Left, MoveLeftIcon, "Move cursor left", repeat: true),
                Command(Key.Down, "↓", "下一候选 / Down", repeat: true),
                Command(Key.Right, MoveRightIcon, "Move cursor right", repeat: true),
            ],
            context.HasInputModeButton ? [3, 3, 3, 9, 3, 3, 3, 3] : [3, 3, 12, 3, 3, 3, 3]
        );

        Debug.WriteLine(
            "[SoftKeyboard.Layout.Japanese] PHYSICAL layout created; "
            + $"jis={jis}, shift={context.ShiftState}, "
            + $"keyboardLayout=0x{context.KeyboardLayoutHandle.ToInt64():X}, rows=6."
        );

        return builder.Build();
    }

    /// <summary>
    /// Translates a physical key without changing native state. Kana caps use full-width Hiragana
    /// as reading aids; script and width of the actual composition remain owned by the IME.
    /// </summary>
    private static SoftKeyboardKey Character(SoftKeyboardLayoutContext context, Key key)
    {
        var physical = context.GetKeyLabel(key);
        var kana     = ToHiragana(context.GetKeyLabel(key, kana: true));
        var hint     = kana != physical ? kana : null;

        return new SoftKeyboardKey(
            SoftKeyboardKeyKind.VirtualKey, physical,
            hint is null ? physical : $"{physical} / {hint}", key, alternateHint: hint
        );
    }

    /// <summary>
    /// Normalizes the native half-width Kana table for readable hardware-style Hiragana caps.
    /// Unmapped keys retain the ordinary native translation or its explicit fallback name.
    /// </summary>
    private static string ToHiragana(string label)
    {
        return new string([
            .. label.Normalize(NormalizationForm.FormKC).Select(character => character switch
            {
                >= '\u30A1' and <= '\u30F6' => (char)(character - 0x60),
                '\u3099' => '\u309B',
                '\u309A' => '\u309C',
                _ => character,
            })
        ]);
    }

    /// <summary>
    /// Creates an IME-visible command using the normal focus-validated virtual-key route.
    /// Function captions describe the Microsoft IME defaults; user key templates remain effective.
    /// </summary>
    private static SoftKeyboardKey Command(
        Key key, object content, string name, string? hint = null, bool repeat = false)
    {
        return new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, content, name, key,
            isRepeatEnabled: repeat, alternateHint: hint);
    }
}