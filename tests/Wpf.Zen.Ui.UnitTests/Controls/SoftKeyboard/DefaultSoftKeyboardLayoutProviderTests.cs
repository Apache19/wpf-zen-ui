// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.SoftKeyboardControl;

public class DefaultSoftKeyboardLayoutProviderTests
{
    [Theory]
    [InlineData(InputScopeNameValue.Url, "http", "/")]
    [InlineData(InputScopeNameValue.EmailSmtpAddress, "@", "-")]
    public void AddressLayouts_PreserveLiteralShortcutsAndPlacementAcrossInputStates(
        InputScopeNameValue scope, string shortcut, string punctuation)
    {
        foreach (var mode in new[] { SoftKeyboardDisplayMode.Automatic, SoftKeyboardDisplayMode.Letters })
        foreach (var shift in System.Enum.GetValues<SoftKeyboardShiftState>())
        foreach (var culture in new[] { "en-US", "zh-CN", "ja-JP" })
        foreach (var ime in new[] { InputMethodState.On, InputMethodState.Off })
        {
            var layout = DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(new(
                CultureInfo.GetCultureInfo(culture), scope, mode, shift, ime));
            Assert.Equal(20, layout.ColumnCount);
            bool url = scope == InputScopeNameValue.Url;
            Assert.Equal(new[] { 10, 9, 9, url ? 10 : 9 }, layout.Keys.GroupBy(key => key.Row).Select(row => row.Count()));
            var bottom = layout.Keys.Where(key => key.Row == 3).ToArray();
            Assert.Equal(url ? new[] { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18 } : new[] { 0, 2, 4, 6, 10, 12, 14, 16, 18 }, bottom.Select(key => key.Column));
            Assert.Equal(url ? new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 } : new[] { 2, 2, 2, 4, 2, 2, 2, 2, 2 }, bottom.Select(key => key.ColumnSpan));
            Assert.Equal("&", bottom[0].Content);
            Assert.Equal(SoftKeyboardDisplayMode.Symbols, bottom[0].DisplayMode);
            Assert.Equal(SoftKeyboardKeyKind.ToggleIme, bottom[1].Kind);
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
            Assert.Contains(".cn", bottom[4].Alternates);
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

    [Theory]
    [InlineData(InputScopeNameValue.Url)]
    [InlineData(InputScopeNameValue.EmailSmtpAddress)]
    public void AddressScopes_AllowExplicitNumericAndSymbolPagesAndReturnToAddressLetters(InputScopeNameValue scope)
    {
        foreach (var mode in new[] { SoftKeyboardDisplayMode.Numbers, SoftKeyboardDisplayMode.Symbols, SoftKeyboardDisplayMode.MoreSymbols })
        {
            var layout = DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(new(
                CultureInfo.InvariantCulture, scope, mode, SoftKeyboardShiftState.Off));
            Assert.DoesNotContain(layout.Keys, key => key.Text == ".com");
            var back = Assert.Single(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.ChangeDisplayMode
                && key.DisplayMode == SoftKeyboardDisplayMode.Letters);
            var letters = DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(new(
                CultureInfo.InvariantCulture, scope, back.DisplayMode, SoftKeyboardShiftState.Off));
            Assert.Contains(letters.Keys, key => key.Text == ".com");
        }
    }

    [Theory]
    [InlineData(false, false, "! @ # $ % & _ - = +", "; : ( ) / ' \"")]
    [InlineData(false, true, "% [ ] { } < > € £ ¥", "* ` ° × ÷ ~ |")]
    [InlineData(true, false, "！ @ # ￥ % & _ - = +", "/ ； ： “ ” （ ）")]
    [InlineData(true, true, "、 【 】 ｛ ｝ 〈 〉 * … •", "µ ½ ｜ © ¶ ° ~")]
    public void SymbolPages_DeclareExpectedPunctuation(bool native, bool more, string second, string third)
    {
        var layout = DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(Context(
            more ? SoftKeyboardDisplayMode.MoreSymbols : SoftKeyboardDisplayMode.Symbols, native));
        Assert.Equal(second, string.Join(" ", layout.Keys.Where(key => key.Row == 1).Select(key => key.Text)));
        Assert.Equal(third, string.Join(" ", layout.Keys.Where(key => key.Row == 2 && key.Kind == SoftKeyboardKeyKind.Text).Select(key => key.Text)));
        var pageKey = Assert.Single(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.ChangeDisplayMode
            && key.DisplayMode == (more ? SoftKeyboardDisplayMode.Symbols : SoftKeyboardDisplayMode.MoreSymbols));
        var icon = Assert.IsType<FluentIconSource>(pageKey.Content);
        Assert.Equal(more ? SymbolRegular.CaretLeft24 : SymbolRegular.CaretRight24, icon.Symbol);
        Assert.True(icon.Filled);
    }

    [Fact]
    public void EveryDefaultSurface_UsesFourRowsAndContainsBothInputMethodActions()
    {
        foreach (SoftKeyboardDisplayMode mode in System.Enum.GetValues<SoftKeyboardDisplayMode>())
        {
            foreach (bool native in new[] { false, true })
            {
                var layout = DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(Context(mode, native));
                Assert.Equal(new[] { 0, 1, 2, 3 }, layout.Keys.Select(key => key.Row).Distinct());
                Assert.Single(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.ToggleIme);
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
        var layout = DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(new SoftKeyboardLayoutContext(
            CultureInfo.GetCultureInfo("ja-JP"), InputScopeNameValue.Default,
            SoftKeyboardDisplayMode.Letters, SoftKeyboardShiftState.Off, InputMethodState.On));
        var key = Assert.Single(layout.Keys, key => key.Text == "ー");
        Assert.Equal(1, key.Row);
        Assert.Contains("・", key.Alternates);
        Assert.Equal(10, layout.Keys.Count(key => key.Row == 1));
    }

    private static SoftKeyboardLayoutContext Context(SoftKeyboardDisplayMode mode, bool native = false) => new(
        CultureInfo.GetCultureInfo("en-US"), InputScopeNameValue.Default, mode,
        SoftKeyboardShiftState.Off, native ? InputMethodState.On : InputMethodState.Off);
}
