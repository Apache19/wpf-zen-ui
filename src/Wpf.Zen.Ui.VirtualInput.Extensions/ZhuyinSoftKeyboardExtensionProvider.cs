// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using static Wpf.Zen.Ui.VirtualInput.Extensions.Internal.KeyboardLayoutParts;

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

#pragma warning disable SA1402 // The provider and its only public layout implementation are cohesive.

/// <summary>
/// Recognizes Microsoft Bopomofo and compatible touch-layout declarations and contributes Zhuyin.
/// </summary>
public sealed class ZhuyinSoftKeyboardExtensionProvider : ISoftKeyboardExtensionProvider
{
    private static readonly Guid MicrosoftBopomofoClassId = new("B115690A-EA02-48D5-A231-E3578D2FDF80");
    private static readonly Guid MicrosoftBopomofoProfileId = new("B2F9C502-1742-11D4-9790-0080C882687E");

    /// <summary>
    /// Gets the shared Zhuyin extension provider.
    /// </summary>
    public static ZhuyinSoftKeyboardExtensionProvider Default { get; } = new();

    /// <inheritdoc />
    public IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return IsZhuyin(context.Layout)
            ? [new SoftKeyboardExtension(supplementalLayouts: ZhuyinCatalog.Default)]
            : [];
    }

    /// <summary>
    /// Determines whether a layout context declares the standard Taiwan phonetic key arrangement.
    /// </summary>
    /// <param name="context">The profile and touch-layout context to inspect.</param>
    /// <returns>
    /// True only for the documented touch layout or exact Microsoft Bopomofo profile.
    /// </returns>
    public static bool IsZhuyin(SoftKeyboardLayoutContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        bool hasTouchLayout = context.InputMethod is
            {
                LanguageId: 0x0404,
                TouchLayoutPreference:
                {
                    Id: 0x0404,
                    Type: SoftKeyboardTouchLayoutType.Classic or SoftKeyboardTouchLayoutType.Optimized
                }
            };
        bool isMicrosoftBopomofo =
            context.InputMethod is { LanguageId: 0x0404, TouchLayoutPreference: null } profile
                && profile.ClassId == MicrosoftBopomofoClassId
                && profile.ProfileId == MicrosoftBopomofoProfileId;

        return hasTouchLayout || isMicrosoftBopomofo;
    }

    private sealed class ZhuyinCatalog : ISoftKeyboardSupplementalLayoutProvider
    {
        internal static ZhuyinCatalog Default { get; } = new();

        public IEnumerable<SoftKeyboardSupplementalLayout> GetLayouts(SoftKeyboardLayoutContext context)
        {
            return IsZhuyin(context)
                ?
                [
                    new SoftKeyboardSupplementalLayout(
                        ZhuyinSoftKeyboardLayouts.Standard,
                        "注音",
                        ZhuyinSoftKeyboardLayoutProvider.Default,
                        isAutomaticPreferred: true,
                        isApplicable: IsZhuyin
                    )
                ]
                : [];
        }
    }
}

/// <summary>
/// Builds the standard Taiwan phonetic supplemental layout.
/// </summary>
public sealed class ZhuyinSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    /// <summary>
    /// Gets the shared Zhuyin layout provider.
    /// </summary>
    public static ZhuyinSoftKeyboardLayoutProvider Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (!ZhuyinSoftKeyboardExtensionProvider.IsZhuyin(context)
            || !context.CanSwitchInputMode || context.InputMode?.IsNativeInput != true
            || context.SupplementalLayout != ZhuyinSoftKeyboardLayouts.Standard
            || context.Page is not (SoftKeyboardPages.Automatic or SoftKeyboardPages.Letters)
            || UrlSoftKeyboardLayoutProvider.Default.CreateLayout(context) is not null
            || EmailSoftKeyboardLayoutProvider.Default.CreateLayout(context) is not null
            || NumberSoftKeyboardLayoutProvider.Default.CreateLayout(context) is not null)
        {
            return null;
        }

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
                    new SoftKeyboardKey(
                        SoftKeyboardKeyKind.Custom,
                        labels[row][column].ToString(),
                        labels[row][column].ToString(),
                        row: row,
                        column: column * 2,
                        columnSpan: 2,
                        action: new PhoneticKeyAction(keys[row][column])
                    )
                );
            }
        }

        builder.AddRow(4, Commands(context), [2, 2, 3, 8, 2, 2, 3]);
        return builder.Build();
    }

    private static IEnumerable<SoftKeyboardKey> Commands(SoftKeyboardLayoutContext context)
    {
        return
        [
            SoftKeyboardKey.Navigate(SoftKeyboardPages.Symbols, "123", "数字和符号"),
            CreateInputModeKey(context),
            new SoftKeyboardKey(SoftKeyboardKeyKind.InputMethods, InputMethodsIcon, "输入法"),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, SpaceIcon, "空格或候选翻页", key: Key.Space),
            new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, MoveDownIcon, "下移或输入法选字", key: Key.Down),
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

    private sealed class PhoneticKeyAction(Key key) : ISoftKeyboardKeyAction
    {
        public void Execute(SoftKeyboardActionContext context)
        {
            context.TrySendKey(key);
        }
    }
}
#pragma warning restore SA1402
