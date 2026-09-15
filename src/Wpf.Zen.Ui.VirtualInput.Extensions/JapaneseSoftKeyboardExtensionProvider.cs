// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using static Wpf.Zen.Ui.VirtualInput.Extensions.Internal.KeyboardLayoutParts;

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

#pragma warning disable SA1402 // The public layout providers share one tightly coupled repertoire helper.

/// <summary>
/// Contributes Japanese language routing, kana layouts, and kana-key input actions.
/// </summary>
public sealed class JapaneseSoftKeyboardExtensionProvider : ISoftKeyboardExtensionProvider
{
    private static readonly CompactSoftKeyboardLayoutProvider CompactProvider = new(
        new SoftKeyboardCompactRepertoire
        {
            Comma = "、",
            Period = "。",
            SymbolsTail = "？",
            MoreSymbolsTail = "〜",
            ExtraLetterKey = new SoftKeyboardKey(
                SoftKeyboardKeyKind.Text,
                "ー",
                "长音符和中点",
                text: "ー",
                alternates: ["・"],
                alternateHint: "・"
            ),
            SymbolsSecond = ["！", "？", "＠", "＃", "￥", "％", "＆", "＊", "ー", "＝"],
            SymbolsThird = ["「", "」", "（", "）", "・", "：", "；"],
            MoreSymbolsSecond = ["『", "』", "【", "】", "〈", "〉", "《", "》", "〔", "〕"],
            MoreSymbolsThird = ["［", "］", "｛", "｝", "…", "※", "〒"],
        }
    );

    private static readonly ISoftKeyboardLayoutProvider LanguageProvider = new JapaneseLanguageProvider();
    private static readonly ISoftKeyboardSupplementalLayoutProvider SupplementalProvider =
        new JapaneseSupplementalProvider();

    /// <summary>
    /// Gets the shared Japanese extension provider.
    /// </summary>
    public static JapaneseSoftKeyboardExtensionProvider Default { get; } = new();

    /// <inheritdoc />
    public IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return context.Layout.Culture.TwoLetterISOLanguageName == "ja"
            ?
            [
                new SoftKeyboardExtension(
                    layoutProvider: LanguageProvider,
                    supplementalLayouts: SupplementalProvider,
                    inputModeAdapter: JapaneseInputModeAdapter.Default
                )
            ]
            : [];
    }

    private sealed class JapaneseLanguageProvider : ISoftKeyboardLayoutProvider
    {
        public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
        {
            if (context.Culture.TwoLetterISOLanguageName != "ja")
            {
                return null;
            }

            return UrlSoftKeyboardLayoutProvider.Default.CreateLayout(context)
                ?? EmailSoftKeyboardLayoutProvider.Default.CreateLayout(context)
                ?? NumberSoftKeyboardLayoutProvider.Default.CreateLayout(context)
                ?? (context.InputMode?.IsNativeInput == true ? CompactProvider.CreateLayout(context) : null);
        }
    }

    private sealed class JapaneseSupplementalProvider : ISoftKeyboardSupplementalLayoutProvider
    {
        public IEnumerable<SoftKeyboardSupplementalLayout> GetLayouts(SoftKeyboardLayoutContext context)
        {
            if (context.Culture.TwoLetterISOLanguageName != "ja"
                || context.InputMethod is not { ClassId: var classId }
                || classId == Guid.Empty)
            {
                return [];
            }

            return
            [
                new SoftKeyboardSupplementalLayout(
                    JapaneseSoftKeyboardLayouts.Fifty,
                    "50音",
                    JapaneseFiftyLayoutProvider.Default,
                    isApplicable: IsApplicable
                ),
                new SoftKeyboardSupplementalLayout(
                    JapaneseSoftKeyboardLayouts.Twelve,
                    "12键",
                    JapaneseTwelveLayoutProvider.Default,
                    isApplicable: IsApplicable
                )
            ];
        }

        private static bool IsApplicable(SoftKeyboardLayoutContext context)
        {
            return context.Culture.TwoLetterISOLanguageName == "ja"
                && context.InputMethod is { ClassId: var classId }
                && classId != Guid.Empty;
        }
    }
}

/// <summary>
/// Builds the Japanese fifty-sound supplemental layout.
/// </summary>
public sealed class JapaneseFiftyLayoutProvider : ISoftKeyboardLayoutProvider
{
    /// <summary>
    /// Gets the shared fifty-sound provider.
    /// </summary>
    public static JapaneseFiftyLayoutProvider Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (!JapaneseLayoutHelpers.CanCreate(context, JapaneseSoftKeyboardLayouts.Fifty))
        {
            return null;
        }

        string[] rows =
        [
            "わらやまはなたさかあ",
            " り みひにちしきい",
            " るゆむふぬつすくう",
            " れ めへねてせけえ",
            "をろよもほのとそこお"
        ];

        var builder = new SoftKeyboardLayoutBuilder(10);
        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                if (rows[row][column] != ' ')
                {
                    builder.Add(JapaneseLayoutHelpers.Kana(rows[row][column].ToString(), row, column, 1));
                }
            }
        }

        builder.AddRow(
            5,
            JapaneseLayoutHelpers.Commands(context).Prepend(JapaneseLayoutHelpers.Kana("ー"))
                .Prepend(JapaneseLayoutHelpers.Kana("ん")),
            [1, 1, 1, 1, 1, 2, 1, 1, 1]
        );
        return builder.Build();
    }
}

/// <summary>
/// Builds the Japanese twelve-key supplemental layout with long-press kana groups.
/// </summary>
public sealed class JapaneseTwelveLayoutProvider : ISoftKeyboardLayoutProvider
{
    /// <summary>
    /// Gets the shared twelve-key provider.
    /// </summary>
    public static JapaneseTwelveLayoutProvider Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (!JapaneseLayoutHelpers.CanCreate(context, JapaneseSoftKeyboardLayouts.Twelve))
        {
            return null;
        }

        string[] groups =
        [
            "あいうえお", "かきくけこ", "さしすせそ",
            "たちつてと", "なにぬねの", "はひふへほ",
            "まみむめも", "やゆよ", "らりるれろ",
            "ぁぃぅぇぉゃゅょっ", "わをんー",
            "がぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽゔ"
        ];

        var builder = new SoftKeyboardLayoutBuilder(12);
        for (var index = 0; index < groups.Length; index++)
        {
            var values = groups[index].Select(character => character.ToString()).ToArray();
            var alternatives = values.Skip(1)
                .Concat(values.SelectMany(JapaneseLayoutHelpers.Variants)).Distinct().ToArray();
            var label = index == 9 ? "小字" : index == 11 ? "濁音・半濁音" : values[0];

            builder.Add(
                new SoftKeyboardKey(
                    SoftKeyboardKeyKind.Custom,
                    label,
                    string.Join("、", values),
                    row: index / 3,
                    column: (index % 3) * 4,
                    columnSpan: 4,
                    action: new JapaneseKanaAction(values[0]),
                    alternates: alternatives,
                    alternateHint: index == 9 ? "ぁゃっ" : string.Concat(values.Skip(1))
                )
            );
        }

        builder.AddRow(4, JapaneseLayoutHelpers.Commands(context), [1, 1, 2, 4, 1, 1, 2]);
        return builder.Build();
    }
}

internal static class JapaneseLayoutHelpers
{
    internal static bool CanCreate(SoftKeyboardLayoutContext context, string identifier)
    {
        return context is { CanSwitchInputMode: true, InputMode.IsNativeInput: true }
            && context.SupplementalLayout == identifier
            && context.Page is SoftKeyboardPages.Automatic or SoftKeyboardPages.Letters
            && UrlSoftKeyboardLayoutProvider.Default.CreateLayout(context) is null
            && EmailSoftKeyboardLayoutProvider.Default.CreateLayout(context) is null
            && NumberSoftKeyboardLayoutProvider.Default.CreateLayout(context) is null;
    }

    internal static SoftKeyboardKey Kana(string kana, int row = 0, int column = 0, int span = 1)
    {
        var variants = Variants(kana);
        return new SoftKeyboardKey(
            SoftKeyboardKeyKind.Custom,
            kana,
            kana,
            row: row,
            column: column,
            columnSpan: span,
            action: new JapaneseKanaAction(kana),
            alternates: variants,
            alternateHint: string.Concat(variants)
        );
    }

    internal static string[] Variants(string kana)
    {
        const string plain = "かきくけこさしすせそたちつてとはひふへほ";
        const string voiced = "がぎぐげござじずぜぞだぢづでどばびぶべぼ";
        var result = new List<string>();
        var index = plain.IndexOf(kana, StringComparison.Ordinal);
        if (index >= 0)
        {
            result.Add(voiced[index].ToString());
        }

        index = "はひふへほ".IndexOf(kana, StringComparison.Ordinal);
        if (index >= 0)
        {
            result.Add("ぱぴぷぺぽ"[index].ToString());
        }

        index = "あいうえおやゆよつ".IndexOf(kana, StringComparison.Ordinal);
        if (index >= 0)
        {
            result.Add("ぁぃぅぇぉゃゅょっ"[index].ToString());
        }

        if (kana == "う")
        {
            result.Add("ゔ");
        }

        return [.. result];
    }

    internal static IEnumerable<SoftKeyboardKey> Commands(SoftKeyboardLayoutContext context)
    {
        return
        [
            SoftKeyboardKey.Navigate(SoftKeyboardPages.Symbols, "123", "数字和符号"),
            CreateInputModeKey(context),
            new SoftKeyboardKey(SoftKeyboardKeyKind.InputMethods, InputMethodsIcon, "输入法"),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, SpaceIcon, "转换或空格", key: Key.Space),
            new SoftKeyboardKey(
                SoftKeyboardKeyKind.VirtualKey,
                MoveLeftIcon,
                "左移",
                key: Key.Left,
                isRepeatEnabled: true
            ),
            new SoftKeyboardKey(
                SoftKeyboardKeyKind.VirtualKey,
                BackspaceIcon,
                "退格",
                key: Key.Back,
                isRepeatEnabled: true
            ),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, EnterIcon, "确认", key: Key.Enter)
        ];
    }
}
#pragma warning restore SA1402
