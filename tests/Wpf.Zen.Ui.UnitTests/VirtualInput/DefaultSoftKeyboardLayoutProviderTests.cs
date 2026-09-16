// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensibility;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

public class DefaultSoftKeyboardLayoutProviderTests
{
    /// <summary>
    /// Every built-in surface removes unavailable mode switches and fills the released grid space.
    /// Capability, selection, and observed state are tested independently of input language.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void ModeSwitch_RequiresActiveProfileAndPublishedCommand(
        bool selected,
        bool hasMode,
        bool hasButton,
        bool visible)
    {
        var method = selected
            ? new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0409, "Test", true)
            : null;
        var inputMode = hasMode
            ? new SoftKeyboardInputMode("test", false)
            : null;
        var inputModeButton = hasButton
            ? new SoftKeyboardInputModeButton("test", "A", "Test mode")
            : null;

        foreach (var scope in new[]
            { InputScopeNameValue.Default, InputScopeNameValue.Url, InputScopeNameValue.EmailSmtpAddress })
        {
            foreach (var page in SoftKeyboardPages.BuiltInPages)
            {
                var context = new SoftKeyboardLayoutContext(CultureInfo.GetCultureInfo("en-US"),
                    scope, page, SoftKeyboardShiftState.Off, inputMode, method,
                    inputModeButton: inputModeButton);
                var layout = Assert.IsType<SoftKeyboardLayout>(
                    DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(context));

                Assert.Equal(visible, context.CanSwitchInputMode);
                Assert.Equal(visible ? 1 : 0,
                    layout.Keys.Count(key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode));
                Assert.Single(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.InputMethods);
                foreach (var row in layout.Keys.Where(key => key.Row == 3
                    || page == SoftKeyboardPages.Numbers).GroupBy(key => key.Row))
                {
                    var end = 0;
                    foreach (var key in row.OrderBy(key => key.Column))
                    {
                        Assert.Equal(end, key.Column);
                        end += key.ColumnSpan;
                    }

                    Assert.Equal(layout.ColumnCount, end);
                }

                if (visible)
                {
                    Assert.Equal(
                        "A",
                        layout.Keys.Single(key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode).Content
                    );
                }
            }
        }
    }

    [Theory]
    [InlineData(InputScopeNameValue.Url, "http", "/")]
    [InlineData(InputScopeNameValue.EmailSmtpAddress, "@", "-")]
    public void AddressLayouts_PreserveLiteralShortcutsAndPlacementAcrossInputStates(
        InputScopeNameValue scope, string shortcut, string punctuation)
    {
        foreach (var mode in new[]
            {
                SoftKeyboardPages.Automatic, SoftKeyboardPages.Letters
            })
        {
            foreach (var shift in Enum.GetValues<SoftKeyboardShiftState>())
            {
                foreach (var culture in new[]
                    {
                        "en-US", "zh-CN", "ja-JP"
                    })
                {
                    foreach (var native in new[]
                        {
                            true, false
                        })
                    {
                        var layout = Assert.IsType<SoftKeyboardLayout>(
                            DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(
                                new SoftKeyboardLayoutContext(
                                    CultureInfo.GetCultureInfo(culture), scope, mode, shift,
                                    Mode(native),
                                    new SoftKeyboardInputMethod(
                                        Guid.NewGuid(),
                                        Guid.NewGuid(),
                                        0x0409,
                                        "Test IME",
                                        true
                                    ),
                                    inputModeButton: Button(native)
                                )
                            )
                        );

                        Assert.Equal(20, layout.ColumnCount);

                        bool url = scope == InputScopeNameValue.Url;

                        Assert.Equal(
                            [10, 9, 9, url ? 10 : 9],
                            layout.Keys.GroupBy(key => key.Row).Select(row => row.Count())
                        );

                        var bottom = layout.Keys.Where(key => key.Row == 3).ToArray();

                        Assert.Equal(
                            url ? [0, 2, 4, 6, 8, 10, 12, 14, 16, 18] : [0, 2, 4, 6, 10, 12, 14, 16, 18],
                            bottom.Select(key => key.Column)
                        );

                        Assert.Equal(
                            url ? [2, 2, 2, 2, 2, 2, 2, 2, 2, 2] : [2, 2, 2, 4, 2, 2, 2, 2, 2],
                            bottom.Select(key => key.ColumnSpan)
                        );

                        Assert.Equal("123", bottom[0].Content);
                        Assert.Equal(SoftKeyboardPages.Symbols, bottom[0].Page);
                        Assert.Equal(SoftKeyboardKeyKind.SwitchInputMode, bottom[1].Kind);
                        Assert.Equal(shortcut, bottom[2].Text);

                        if (url)
                        {
                            Assert.Equal(SoftKeyboardKeyKind.Text, bottom[3].Kind);
                            Assert.Equal(":", bottom[3].Content);
                            Assert.Equal(":", bottom[3].Text);
                            // Compare the shared remaining actions after verifying the URL-only colon placement.
                            bottom = bottom.Where((_, index) => index != 3).ToArray();
                        }

                        Assert.Equal(Key.Space, bottom[3].Key);
                        Assert.Equal(".com", bottom[4].Text);

                        Assert.Contains(
                            culture switch { "zh-CN" => ".cn", "ja-JP" => ".jp", _ => ".us" },
                            bottom[4].Alternates
                        );

                        Assert.Contains(bottom[4].AlternateHint!, bottom[4].Alternates);
                        Assert.True(bottom[4].Alternates.Count > 1);
                        Assert.Equal(".", bottom[5].Content);
                        Assert.Equal(".", bottom[5].Text);
                        Assert.Equal("?", bottom[5].AlternateHint);
                        Assert.Equal("?", Assert.Single(bottom[5].Alternates));
                        Assert.Equal(punctuation, bottom[6].Text);
                        Assert.Equal(SoftKeyboardKeyKind.InputMethods, bottom[7].Kind);
                        Assert.Equal(Key.Enter, bottom[8].Key);

                        if (scope == InputScopeNameValue.Url)
                        {
                            Assert.Equal("https", Assert.Single(bottom[2].Alternates));
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(InputScopeNameValue.Url)]
    [InlineData(InputScopeNameValue.EmailSmtpAddress)]
    public void AddressScopes_AllowExplicitNumericAndSymbolPagesAndReturnToAddressLetters(InputScopeNameValue scope)
    {
        foreach (var mode in new[]
            {
                SoftKeyboardPages.Numbers, SoftKeyboardPages.Symbols, SoftKeyboardPages.MoreSymbols
            })
        {
            var layout = Assert.IsType<SoftKeyboardLayout>(DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(new(
                CultureInfo.InvariantCulture, scope, mode, SoftKeyboardShiftState.Off)));

            Assert.DoesNotContain(layout.Keys, key => key.Text == ".com");

            var back = Assert.Single(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.ChangePage
                && key.Page == SoftKeyboardPages.Letters);

            var letters = Assert.IsType<SoftKeyboardLayout>(DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(new(
                CultureInfo.InvariantCulture, scope, back.Page, SoftKeyboardShiftState.Off)));

            Assert.Contains(letters.Keys, key => key.Text == ".com");
        }
    }

    [Theory]
    [InlineData(false, false, "! @ # $ % & _ - = +", "; : ( ) / ' \"")]
    [InlineData(false, true, "% [ ] { } < > € £ ¥", "* ` ° × ÷ ~ |")]
    [InlineData(true, false, "! @ # $ % & _ - = +", "; : ( ) / ' \"")]
    [InlineData(true, true, "% [ ] { } < > € £ ¥", "* ` ° × ÷ ~ |")]
    public void SymbolPages_DeclareExpectedPunctuation(bool native, bool more, string second, string third)
    {
        var layout = Assert.IsType<SoftKeyboardLayout>(
            DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(
                Context(more ? SoftKeyboardPages.MoreSymbols : SoftKeyboardPages.Symbols, native)
            )
        );

        Assert.Equal(
            second, string.Join(" ", layout.Keys.Where(key => key.Row == 1).Select(key => key.Text))
        );

        Assert.Equal(
            third,
            string.Join(
                " ",
                layout.Keys.Where(key => key is { Row: 2, Kind: SoftKeyboardKeyKind.Text }).Select(key => key.Text)
            )
        );

        var pageKey = Assert.Single(
            layout.Keys,
            key => key.Kind == SoftKeyboardKeyKind.ChangePage
                && key.Page == (more ? SoftKeyboardPages.Symbols : SoftKeyboardPages.MoreSymbols)
        );

        var icon = Assert.IsType<FluentIconSource>(pageKey.Content);

        Assert.Equal(more ? SymbolRegular.CaretLeft24 : SymbolRegular.CaretRight24, icon.Symbol);
        Assert.True(icon.Filled);
    }

    [Fact]
    public void EveryDefaultSurface_UsesFourRowsAndContainsBothInputMethodActions()
    {
        foreach (string mode in SoftKeyboardPages.BuiltInPages)
        {
            foreach (bool native in new[]
                {
                    false, true
                })
            {
                var layout = Assert.IsType<SoftKeyboardLayout>(
                    DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(
                        Context(mode, native)
                    )
                );

                Assert.Equal([0, 1, 2, 3], layout.Keys.Select(key => key.Row).Distinct());
                Assert.Single(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode);
                Assert.Single(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.InputMethods);
                Assert.DoesNotContain(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.Close);

                foreach (var row in layout.Keys.GroupBy(key => key.Row))
                {
                    int end = 0;
                    foreach (var key in row.OrderBy(key => key.Column))
                    {
                        Assert.True(key.Column >= end);
                        end = key.Column + key.ColumnSpan;
                        Assert.True(end <= layout.ColumnCount);
                    }
                }
            }
        }
    }

    [Fact]
    public void JapaneseNativeLayout_ExposesLongVowelVariantWithoutAddingRows()
    {
        var context = new SoftKeyboardLayoutContext(
            CultureInfo.GetCultureInfo("ja-JP"),
            InputScopeNameValue.Default,
            SoftKeyboardPages.Letters,
            SoftKeyboardShiftState.Off,
            Mode(true, "あ"),
            new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0411, "Japanese", true),
            inputModeButton: Button(true, "あ")
        );

        var provider = JapaneseSoftKeyboardExtensionProvider.Default
            .GetExtensions(new SoftKeyboardExtensionContext(context))
            .Select(extension => extension.LayoutProvider)
            .First(layoutProvider => layoutProvider is not null)!;

        var layout = Assert.IsType<SoftKeyboardLayout>(provider.CreateLayout(context));
        var key    = Assert.Single(layout.Keys, key => key.Text == "ー");

        Assert.Equal(1, key.Row);
        Assert.Contains("・", key.Alternates);
        Assert.Equal(10, layout.Keys.Count(k => k.Row == 1));
    }

    private static SoftKeyboardLayoutContext Context(string mode, bool native = false)
    {
        return new SoftKeyboardLayoutContext(
            CultureInfo.GetCultureInfo("zh-CN"),
            InputScopeNameValue.Default,
            mode,
            SoftKeyboardShiftState.Off,
            Mode(native),
            new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0804, "Test IME", true),
            inputModeButton: Button(native)
        );
    }

    private static SoftKeyboardInputMode Mode(bool native, string? label = null)
    {
        return new SoftKeyboardInputMode(
            native ? "native" : "direct",
            native
        );
    }

    private static SoftKeyboardInputModeButton Button(bool native, string? label = null)
    {
        return new SoftKeyboardInputModeButton(
            native ? "native" : "direct",
            label ?? (native ? "中" : "A"),
            native ? "Native input" : "Direct input"
        );
    }
}
