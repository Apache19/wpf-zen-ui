// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

/// <summary>
/// Provides Japanese kana and standard Taiwan phonetic surfaces for recognized input profiles.
/// </summary>
/// <remarks>
/// Only native letter pages are handled. URL/email/number scopes retain their specialized providers.
/// Japanese twelve-key groups expose remaining kana, voiced variants and small kana by long press;
/// each selection enters IME pre-edit. This provider does not emulate Windows' proprietary flick UI.
/// Japanese key actions select full-width hiragana/kana-key mode; Roman-key input is not required.
/// Zhuyin uses the standard phonetic mapping indicated by the TIP, not a mapping guessed from zh-TW.
/// </remarks>
public sealed class SupplementalSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    /// <summary>
    /// Gets the shared stateless supplemental provider.
    /// </summary>
    public static SupplementalSoftKeyboardLayoutProvider Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.ImeState != InputMethodState.On
            || context.Page is not (SoftKeyboardPages.Automatic or SoftKeyboardPages.Letters)
            || UrlSoftKeyboardLayoutProvider.Default.CreateLayout(context) is not null
            || EmailSoftKeyboardLayoutProvider.Default.CreateLayout(context) is not null
            || NumberSoftKeyboardLayoutProvider.Default.CreateLayout(context) is not null)
        {
            return null;
        }

        return SupplementalLayoutCatalog.Resolve(context) switch
        {
            SoftKeyboardSupplementalLayouts.JapaneseFifty  => CreateFifty(context),
            SoftKeyboardSupplementalLayouts.JapaneseTwelve => CreateTwelve(context),
            SoftKeyboardSupplementalLayouts.Zhuyin         => CreateZhuyin(context),
            _                                              => null,
        };
    }

    private static SoftKeyboardLayout CreateFifty(SoftKeyboardLayoutContext context)
    {
        // Gojuon columns run from the a-column on the right to wa on the left.
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
                    builder.Add(Kana(rows[row][column].ToString()).WithPlacement(row, column, 1));
                }
            }
        }

        builder.AddRow(
            5,
            Commands(context).Prepend(Kana("ー")).Prepend(Kana("ん")),
            [1, 1, 1, 1, 1, 2, 1, 1, 1]
        );

        return builder.Build();
    }

    private static SoftKeyboardLayout CreateTwelve(SoftKeyboardLayoutContext context)
    {
        string[] groups =
        [
            "あいうえお", "かきくけこ", "さしすせそ",
            "たちつてと", "なにぬねの", "はひふへほ",
            "まみむめも", "やゆよ", "らりるれろ",
            "ぁぃぅぇぉゃゅょっ", "わをんー",
            "がぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽゔ"
        ];

        var builder = new SoftKeyboardLayoutBuilder(12);

        for (var i = 0; i < groups.Length; i++)
        {
            var values       = groups[i].Select(c => c.ToString()).ToArray();
            var alternatives = values.Skip(1).Concat(values.SelectMany(Variants)).Distinct().ToArray();
            var label        = i == 9 ? "小字" : i == 11 ? "濁音・半濁音" : values[0];

            builder.Add(
                new SoftKeyboardKey(
                    SoftKeyboardKeyKind.Custom,
                    label,
                    string.Join("、", values),
                    action: new JapaneseKanaAction(values[0]),
                    alternates: alternatives,
                    alternateHint: i == 9 ? "ぁゃっ" : i == 11 ? "゛ ゜" : string.Concat(values.Skip(1)),
                    row: i / 3,
                    column: (i % 3) * 4,
                    columnSpan: 4
                )
            );
        }

        builder.AddRow(4, Commands(context), [1, 1, 2, 4, 1, 1, 2]);
        return builder.Build();
    }

    private static SoftKeyboardKey Kana(string kana)
    {
        return new SoftKeyboardKey(
            SoftKeyboardKeyKind.Custom,
            kana,
            kana,
            action: new JapaneseKanaAction(kana),
            alternates: Variants(kana),
            alternateHint: string.Concat(Variants(kana))
        );
    }

    private static string[] Variants(string kana)
    {
        const string plain  = "かきくけこさしすせそたちつてとはひふへほ";
        const string voiced = "がぎぐげござじずぜぞだぢづでどばびぶべぼ";

        var result = new List<string>();
        var index  = plain.IndexOf(kana, StringComparison.Ordinal);

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

    private static SoftKeyboardLayout CreateZhuyin(SoftKeyboardLayoutContext context)
    {
        string[] labels =
        [
            "ㄅㄉˇˋㄓˊ˙ㄚㄞㄢㄦ",
            "ㄆㄊㄍㄐㄔㄗㄧㄛㄟㄣ",
            "ㄇㄋㄎㄑㄕㄘㄨㄜㄠㄤ",
            "ㄈㄌㄏㄒㄖㄙㄩㄝㄡㄥ"
        ];

        Key[][] keys =
        [
            [Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9, Key.D0, Key.OemMinus],
            [Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P],
            [Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L, Key.OemSemicolon],
            [Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M, Key.OemComma, Key.OemPeriod, Key.OemQuestion]
        ];

        var builder = new SoftKeyboardLayoutBuilder(22);

        for (var row = 0; row < labels.Length; row++)
        {
            for (var column = 0; column < labels[row].Length; column++)
            {
                builder.Add(
                    SoftKeyboardKey.FromAction(
                        new PhoneticKeyAction(keys[row][column]),
                        labels[row][column].ToString(),
                        labels[row][column].ToString()
                    ).WithPlacement(row, column * 2, 2)
                );
            }
        }

        builder.AddRow(4, Commands(context, zhuyin: true), [2, 2, 3, 8, 2, 2, 3]);
        return builder.Build();
    }

    private static IEnumerable<SoftKeyboardKey> Commands(SoftKeyboardLayoutContext context, bool zhuyin = false)
    {
        return
        [
            SoftKeyboardKey.Navigate(SoftKeyboardPages.Symbols, "123", "数字和符号"),

            KeyboardLayoutParts.CreateImeKey(context),

            new SoftKeyboardKey(
                SoftKeyboardKeyKind.InputMethods,
                KeyboardLayoutParts.InputMethodsIcon,
                "输入法"
            ),

            new SoftKeyboardKey(
                SoftKeyboardKeyKind.VirtualKey,
                KeyboardLayoutParts.SpaceIcon,
                zhuyin ? "空格 / 候选翻页" : "转换 / 空格",
                key: Key.Space
            ),

            // This is an ordinary IME navigation key. It may request alternatives for a completed
            // syllable, but cannot force a TIP to publish a candidate UI element to our TSF sink.
            zhuyin
                ? new SoftKeyboardKey(
                    SoftKeyboardKeyKind.VirtualKey,
                    "↓",
                    "下移 / 输入法选字",
                    key: Key.Down
                )
                : new SoftKeyboardKey(
                    SoftKeyboardKeyKind.VirtualKey,
                    KeyboardLayoutParts.MoveLeftIcon,
                    "左移",
                    key: Key.Left,
                    isRepeatEnabled: true
                ),

            new SoftKeyboardKey(
                SoftKeyboardKeyKind.VirtualKey,
                KeyboardLayoutParts.BackspaceIcon,
                "退格",
                key: Key.Back,
                isRepeatEnabled: true
            ),

            new SoftKeyboardKey(
                SoftKeyboardKeyKind.VirtualKey,
                KeyboardLayoutParts.EnterIcon,
                "确认",
                key: Key.Enter
            )
        ];
    }

    private sealed class PhoneticKeyAction(Key key) : ISoftKeyboardKeyAction
    {
        // A latched alphabetic Shift must not turn a phonetic key into punctuation or an IME command.
        public void Execute(SoftKeyboardActionContext context)
        {
            context.TrySendKey(key);
        }
    }
}