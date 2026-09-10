// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Linq;
using System.Windows.Input;
using System.Windows.Markup;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.SoftKeyboardControl;

public class SoftKeyboardDefinitionTests
{
    [Fact]
    public void XamlDefinition_BuildsImmutablePositionedKeysAndVariants()
    {
        WpfTestThread.Run(() =>
        {
            var definition = (SoftKeyboardLayoutDefinition)XamlReader.Parse("""
                <ui:SoftKeyboardLayoutDefinition xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"
                    xmlns:sys="clr-namespace:System;assembly=System.Runtime" ColumnCount="10">
                    <ui:SoftKeyboardRowDefinition Offset="1">
                        <ui:SoftKeyboardKeyDefinition Text="e" Span="3">
                            <sys:String>é</sys:String>
                            <sys:String>ë</sys:String>
                        </ui:SoftKeyboardKeyDefinition>
                        <ui:SoftKeyboardKeyDefinition Kind="VirtualKey" Key="Back" Content="⌫" AutomationName="Backspace" Span="4" />
                    </ui:SoftKeyboardRowDefinition>
                </ui:SoftKeyboardLayoutDefinition>
                """);
            var layout = definition.CreateLayout(Context(SoftKeyboardDisplayMode.Letters));
            Assert.Equal(10, layout.ColumnCount);
            Assert.Equal((0, 1, 3), (layout.Keys[0].Row, layout.Keys[0].Column, layout.Keys[0].ColumnSpan));
            Assert.Equal(4, layout.Keys[1].Column);
            Assert.Equal(Key.Back, layout.Keys[1].Key);
            definition.Rows[0].Keys[0].Alternates.Clear();
            Assert.Equal(new[] { "é", "ë" }, layout.Keys[0].Alternates);
            definition.Rows[0].Keys[1].Span = 20;
            Assert.Throws<System.ArgumentException>(() => definition.CreateLayout(Context(SoftKeyboardDisplayMode.Letters)));
        });
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
