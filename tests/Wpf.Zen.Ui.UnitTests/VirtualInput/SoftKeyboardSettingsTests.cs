// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Layouts.Internal;
using Wpf.Zen.Ui.VirtualInput.Layouts.Providers;
using Wpf.Zen.Ui.VirtualInput.Settings;
using MenuItem = System.Windows.Controls.MenuItem;
using Separator = System.Windows.Controls.Separator;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

public class SoftKeyboardSettingsTests
{
    [Theory]
    [InlineData(SoftKeyboardCandidateMode.Automatic, false, true)]
    [InlineData(SoftKeyboardCandidateMode.Native, false, false)]
    [InlineData(SoftKeyboardCandidateMode.Embedded, true, true)]
    public void CandidatePolicy_PreservesKnownIncompleteUiUnlessExplicitlyRequested(SoftKeyboardCandidateMode mode, bool weasel, bool other)
    {
        Assert.Equal(weasel, Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework.TsfCandidateSession.ShouldEmbedCandidate(
            mode, new Guid("36C3C795-7159-45AA-AB12-30229A51DBD3")));
        Assert.Equal(other, Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework.TsfCandidateSession.ShouldEmbedCandidate(mode, Guid.Empty));
        WpfTestThread.Run(() =>
        {
            var context = Context("zh-CN", null);
            var settings = new SoftKeyboardSettingsContext(context, SoftKeyboardAutoShowMode.Disabled,
                SoftKeyboardAutoHideMode.Manual, SupplementalLayoutCatalog.GetOptions(context), mode);
            var node = DefaultSoftKeyboardSettingsProvider.Default.CreateSettings(settings).Single(n => n.Id == "candidates");
            var selected = Assert.Single(node.Children.Where(n => n.IsChecked));
            var keyboard = new SoftKeyboard();
            selected.Apply!(keyboard);
            Assert.Equal(mode, keyboard.CandidateMode);
        });
    }

    [Fact]
    public void DefaultSettings_HaveIndependentRadioGroupsAndNoEmptyLayoutMenu()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            var root = Assert.Single(keyboard.SettingsItems);
            Assert.Equal("keyboard", root.Id);
            Assert.Equal(5, root.Children.Count);
            var menu = Assert.IsType<MenuItem>(Assert.Single(keyboard.SettingsMenu.Items.Cast<object>()));
            var open = Assert.IsType<RadioMenuItem>(menu.Items[0]);
            var close = Assert.IsType<RadioMenuItem>(menu.Items[3]);
            Assert.NotEqual(open.GroupName, close.GroupName);
            Assert.IsType<Separator>(menu.Items[2]);
            root.Children[0].Apply!(keyboard);
            Assert.Equal(SoftKeyboardAutoShowMode.AnyFocus, keyboard.AutoShowMode);
            Assert.True(keyboard.SettingsItems[0].Children[0].IsChecked);
            root.Children[4].Apply!(keyboard);
            Assert.Equal(SoftKeyboardAutoHideMode.Manual, keyboard.AutoHideMode);
            Assert.Equal(SoftKeyboardAutoShowMode.AnyFocus, keyboard.AutoShowMode);
        });
    }

    [Fact]
    public void Providers_CanCustomizeDataAndFailuresRestoreDefaultSettings()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard { SettingsProvider = new CustomSettingsProvider() };
            Assert.Equal("Options", keyboard.SettingsItems[0].Header);
            Assert.NotNull(keyboard.SettingsItems[0].Icon);
            Assert.Null(keyboard.SettingsError);
            keyboard.SettingsProvider = new ThrowingSettingsProvider();
            Assert.IsType<InvalidOperationException>(keyboard.SettingsError);
            Assert.Equal("键盘开关", keyboard.SettingsItems[0].Header);
            keyboard.SettingsProvider = DefaultSoftKeyboardSettingsProvider.Default;
            Assert.Null(keyboard.SettingsError);
        });
    }

    [Theory]
    [InlineData("ja-JP", null, 3)]
    [InlineData("zh-TW", 0x0404, 3)]
    [InlineData("zh-TW", 0xF042, 1)]
    [InlineData("zh-TW", null, 1)]
    [InlineData("zh-CN", 0x0804, 1)]
    public void LayoutChoices_RequireAnImplementedRecognizedEnvironment(string culture, int? id, int choices)
    {
        var context = Context(culture, id);
        Assert.Equal(choices, SupplementalLayoutCatalog.GetOptions(context).Count);
        var settings = new SoftKeyboardSettingsContext(context, SoftKeyboardAutoShowMode.Disabled,
            SoftKeyboardAutoHideMode.Manual, SupplementalLayoutCatalog.GetOptions(context));
        var nodes = DefaultSoftKeyboardSettingsProvider.Default.CreateSettings(settings).ToArray();
        Assert.Equal(choices > 1, nodes.Any(node => node.Id == "layout"));
        Assert.False(nodes.Last().IsSeparator);
    }

    [Theory]
    [InlineData(SoftKeyboardSupplementalLayouts.JapaneseFifty, 46)]
    [InlineData(SoftKeyboardSupplementalLayouts.JapaneseTwelve, 12)]
    public void JapanesePanels_CoverKanaAndRouteAlternativesThroughIme(string id, int mainKeys)
    {
        var context = Context("ja-JP", null, id);
        var layout = DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(context)!;
        var keys = layout.Keys.Where(key => key.Action is JapaneseKanaAction).ToArray();
        Assert.Equal(mainKeys + (id == SoftKeyboardSupplementalLayouts.JapaneseFifty ? 1 : 0), keys.Length);
        var kana = keys.SelectMany(key => key.Alternates.Append(key.Content.ToString()!))
            .Where(value => value.Length == 1).ToHashSet();
        foreach (var value in "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんー")
            Assert.Contains(value.ToString(), kana);
        foreach (var key in keys)
        {
            Assert.IsAssignableFrom<ISoftKeyboardAlternateKeyAction>(key.Action);
            Assert.All(key.Alternates, alternate => Assert.InRange(Assert.Single(alternate), '\u3041', '\u30fc'));
        }

        Assert.DoesNotContain(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.Text);
    }

    [Fact]
    public void ZhuyinPreference_Chooses41PhoneticKeysOnlyInNativeLetterMode()
    {
        var context = Context("zh-TW", 0x0404);
        var layout = DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(context)!;
        var phonetic = layout.Keys.Where(key => key.Kind == SoftKeyboardKeyKind.Custom).ToArray();
        Assert.Equal(41, phonetic.Length);
        Assert.Equal(41, phonetic.Select(key => key.Content).Distinct().Count());
        Assert.Contains(phonetic, key => Equals(key.Content, "ㄅ"));
        Assert.Contains(phonetic, key => Equals(key.Content, "˙"));
        var choose = Assert.Single(layout.Keys.Where(key => key.Key == Key.Down));
        Assert.Equal(Key.Down, choose.Key);
        Assert.Equal(SoftKeyboardKeyKind.VirtualKey, choose.Kind);
        foreach (var mode in new[] { InputMethodState.Off, InputMethodState.DoNotCare })
            Assert.Null(SupplementalSoftKeyboardLayoutProvider.Default.CreateLayout(Context("zh-TW", 0x0404, ime: mode)));
        Assert.Null(SupplementalSoftKeyboardLayoutProvider.Default.CreateLayout(Context("zh-TW", 0x0404,
            SoftKeyboardSupplementalLayouts.Standard)));
        Assert.Null(SupplementalSoftKeyboardLayoutProvider.Default.CreateLayout(Context("zh-TW", null)));
    }

    [Fact]
    public void ModernMicrosoftBopomofo_UsesExactProfileFallbackWhenTouchQueryIsUnavailable()
    {
        var profile = new SoftKeyboardInputMethod(new Guid("B115690A-EA02-48D5-A231-E3578D2FDF80"),
            new Guid("B2F9C502-1742-11D4-9790-0080C882687E"), 0x0404, "Name is not used for detection", true);
        var context = new SoftKeyboardLayoutContext(CultureInfo.GetCultureInfo("zh-TW"), InputScopeNameValue.Default,
            SoftKeyboardPages.Automatic, SoftKeyboardShiftState.Off, InputMethodState.On, profile);
        Assert.True(SupplementalLayoutCatalog.IsZhuyin(context));
        Assert.Equal(SoftKeyboardSupplementalLayouts.Zhuyin, SupplementalLayoutCatalog.Resolve(context));
    }

    [Theory]
    [InlineData(InputScopeNameValue.Url)]
    [InlineData(InputScopeNameValue.EmailSmtpAddress)]
    [InlineData(InputScopeNameValue.Number)]
    public void SupplementalPreference_DoesNotOverrideSpecializedAutomaticScopes(InputScopeNameValue scope)
    {
        Assert.Null(SupplementalSoftKeyboardLayoutProvider.Default.CreateLayout(Context("ja-JP", null,
            SoftKeyboardSupplementalLayouts.JapaneseFifty, scope: scope)));
    }

    internal static SoftKeyboardLayoutContext Context(string culture, int? preference,
        string layout = SoftKeyboardSupplementalLayouts.Automatic, InputMethodState ime = InputMethodState.On,
        InputScopeNameValue scope = InputScopeNameValue.Default)
    {
        var language = CultureInfo.GetCultureInfo(culture);
        var profile = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), (ushort)language.LCID, culture, true,
            preference is { } id ? new SoftKeyboardTouchLayoutPreference(SoftKeyboardTouchLayoutType.Optimized, (ushort)id) : null);
        return new SoftKeyboardLayoutContext(language, scope, SoftKeyboardPages.Automatic,
            SoftKeyboardShiftState.Off, ime, profile, supplementalLayout: layout);
    }

    private sealed class CustomSettingsProvider : ISoftKeyboardSettingsProvider
    {
        public IEnumerable<SoftKeyboardSetting> CreateSettings(SoftKeyboardSettingsContext context) =>
            DefaultSoftKeyboardSettingsProvider.Default.CreateSettings(context)
                .Select(node => node with { Header = "Options", Icon = new FluentIconSource(SymbolRegular.Settings24) });
    }

    private sealed class ThrowingSettingsProvider : ISoftKeyboardSettingsProvider
    {
        public IEnumerable<SoftKeyboardSetting> CreateSettings(SoftKeyboardSettingsContext context) => throw new InvalidOperationException("Policy failure");
    }
}
