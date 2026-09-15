// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.VirtualInput.Candidates;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensibility;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Settings;
using MenuItem = System.Windows.Controls.MenuItem;
using Separator = System.Windows.Controls.Separator;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

public class SoftKeyboardSettingsTests
{
    [Fact]
    public void DefaultSettings_HaveIndependentRadioGroupsAndNoEmptyLayoutMenu()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            var root     = Assert.Single(keyboard.SettingsItems);
            Assert.Equal("keyboard", root.Id);
            Assert.Equal(5, root.Children.Count);
            var menu  = Assert.IsType<MenuItem>(Assert.Single(keyboard.SettingsMenu.Items.Cast<object>()));
            var open  = Assert.IsType<RadioMenuItem>(menu.Items[0]);
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
            var keyboard = new SoftKeyboard
            {
                SettingsProvider = new CustomSettingsProvider()
            };
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
    [InlineData("ja-JP", null, 4)]
    [InlineData("zh-TW", 0x0404, 3)]
    [InlineData("zh-TW", 0xF042, 2)]
    [InlineData("zh-TW", null, 2)]
    [InlineData("zh-CN", 0x0804, 2)]
    public void LayoutChoices_RequireAnImplementedRecognizedEnvironment(string culture, int? id, int choices)
    {
        var context = Context(culture, id);

        Assert.Equal(choices, LayoutOptions(context).Count);

        var settings = new SoftKeyboardSettingsContext(
            context,
            SoftKeyboardAutoShowMode.Disabled,
            SoftKeyboardAutoHideMode.Manual,
            LayoutOptions(context)
        );

        var nodes = DefaultSoftKeyboardSettingsProvider.Default.CreateSettings(settings).ToArray();

        Assert.Equal(choices > 2, nodes.Any(node => node.Id == "layout"));
        Assert.False(nodes.Last().IsSeparator);
        Assert.DoesNotContain(nodes, node => node.Id.StartsWith("candidates", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(JapaneseSoftKeyboardLayouts.Fifty, 46)]
    [InlineData(JapaneseSoftKeyboardLayouts.Twelve, 12)]
    public void JapanesePanels_CoverKanaAndRouteAlternativesThroughIme(string id, int mainKeys)
    {
        var context = Context("ja-JP", null, id);
        var layout = id == JapaneseSoftKeyboardLayouts.Fifty
            ? JapaneseFiftyLayoutProvider.Default.CreateLayout(context)!
            : JapaneseTwelveLayoutProvider.Default.CreateLayout(context)!;
        var keys = layout.Keys.Where(key => key.Action is JapaneseKanaAction).ToArray();
        Assert.Equal(mainKeys + (id == JapaneseSoftKeyboardLayouts.Fifty ? 1 : 0), keys.Length);
        var kana = keys.SelectMany(key => key.Alternates.Append(key.Content.ToString()!))
            .Where(value => value.Length == 1).ToHashSet();
        foreach (var value in "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんー")
        {
            Assert.Contains(value.ToString(), kana);
        }

        foreach (var key in keys)
        {
            Assert.IsAssignableFrom<ISoftKeyboardAlternateKeyAction>(key.Action);
            Assert.All(key.Alternates, alternate => Assert.InRange(Assert.Single(alternate), '\u3041', '\u30fc'));
        }

        Assert.DoesNotContain(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.Text);
    }

    [Theory]
    [InlineData(JapaneseSoftKeyboardLayouts.Fifty)]
    [InlineData(JapaneseSoftKeyboardLayouts.Twelve)]
    public void JapanesePanels_RepeatCursorAndEditingKeys(string id)
    {
        var context = Context("ja-JP", null, id);
        var layout = id == JapaneseSoftKeyboardLayouts.Fifty
            ? JapaneseFiftyLayoutProvider.Default.CreateLayout(context)!
            : JapaneseTwelveLayoutProvider.Default.CreateLayout(context)!;

        Assert.True(Assert.Single(layout.Keys, key => key.Key == Key.Left).IsRepeatEnabled);
        Assert.True(Assert.Single(layout.Keys, key => key.Key == Key.Back).IsRepeatEnabled);
    }

    [Fact]
    public void ZhuyinPreference_Chooses41PhoneticKeysOnlyInNativeLetterMode()
    {
        var layout = ZhuyinSoftKeyboardLayoutProvider.Default.CreateLayout(
            Context("zh-TW", 0x0404, ZhuyinSoftKeyboardLayouts.Standard)
        )!;

        var phonetic = layout.Keys.Where(key => key.Kind == SoftKeyboardKeyKind.Custom).ToArray();

        Assert.Equal(41, phonetic.Length);
        Assert.Equal(41, phonetic.Select(key => key.Content).Distinct().Count());
        Assert.Contains(phonetic, key => Equals(key.Content, "ㄅ"));
        Assert.Contains(phonetic, key => Equals(key.Content, "˙"));

        var choose = Assert.Single(layout.Keys, key => key.Key == Key.Down);

        Assert.Equal(Key.Down, choose.Key);
        Assert.Equal(SoftKeyboardKeyKind.VirtualKey, choose.Kind);
        Assert.True(Assert.Single(layout.Keys, key => key.Key == Key.Back).IsRepeatEnabled);

        foreach (bool? native in new bool?[] { false, null })
        {
            Assert.Null(
                ZhuyinSoftKeyboardLayoutProvider.Default.CreateLayout(
                    Context("zh-TW", 0x0404, ZhuyinSoftKeyboardLayouts.Standard, native: native)
                )
            );
        }

        Assert.Null(
            ZhuyinSoftKeyboardLayoutProvider.Default.CreateLayout(
                Context("zh-TW", 0x0404, SoftKeyboardSupplementalLayouts.Standard)
            )
        );

        Assert.Null(
            ZhuyinSoftKeyboardLayoutProvider.Default.CreateLayout(
                Context("zh-TW", null, ZhuyinSoftKeyboardLayouts.Standard)
            )
        );
    }

    [Fact]
    public void ModernMicrosoftBopomofo_UsesExactProfileFallbackWhenTouchQueryIsUnavailable()
    {
        var profile = new SoftKeyboardInputMethod(
            new Guid("B115690A-EA02-48D5-A231-E3578D2FDF80"),
            new Guid("B2F9C502-1742-11D4-9790-0080C882687E"),
            0x0404,
            "Name is not used for detection",
            true
        );

        var context = new SoftKeyboardLayoutContext(
            CultureInfo.GetCultureInfo("zh-TW"),
            InputScopeNameValue.Default,
            SoftKeyboardPages.Automatic,
            SoftKeyboardShiftState.Off,
            NativeMode(),
            profile
        );

        Assert.True(ZhuyinSoftKeyboardExtensionProvider.IsZhuyin(context));

        var extension = Assert.Single(
            ZhuyinSoftKeyboardExtensionProvider.Default.GetExtensions(
                new SoftKeyboardExtensionContext(context)
            )
        );

        var registration = Assert.Single(extension.SupplementalLayouts!.GetLayouts(context));

        Assert.Equal(ZhuyinSoftKeyboardLayouts.Standard, registration.Id);
        Assert.True(registration.IsAutomaticPreferred);
    }

    [Theory]
    [InlineData(InputScopeNameValue.Url)]
    [InlineData(InputScopeNameValue.EmailSmtpAddress)]
    [InlineData(InputScopeNameValue.Number)]
    public void SupplementalPreference_DoesNotOverrideSpecializedAutomaticScopes(InputScopeNameValue scope)
    {
        Assert.Null(
            JapaneseFiftyLayoutProvider.Default.CreateLayout(
                Context("ja-JP", null, JapaneseSoftKeyboardLayouts.Fifty, scope: scope)
            )
        );
    }

    private static IReadOnlyList<SoftKeyboardLayoutOption> LayoutOptions(SoftKeyboardLayoutContext context)
    {
        var extensions = DefaultSoftKeyboardExtensionProvider.Default.GetExtensions(
            new SoftKeyboardExtensionContext(context)
        );

        var layouts = extensions
            .Where(extension => extension.SupplementalLayouts is not null)
            .SelectMany(extension => extension.SupplementalLayouts!.GetLayouts(context));

        return
        [
            new SoftKeyboardLayoutOption(SoftKeyboardSupplementalLayouts.Automatic, "Automatic"),
            new SoftKeyboardLayoutOption(SoftKeyboardSupplementalLayouts.Standard, "Standard"),
            .. layouts.Select(layout => new SoftKeyboardLayoutOption(layout.Id, layout.DisplayName))
        ];
    }

    internal static SoftKeyboardLayoutContext Context(
        string culture,
        int? preference,
        string layout = SoftKeyboardSupplementalLayouts.Automatic,
        bool? native = true,
        InputScopeNameValue scope = InputScopeNameValue.Default)
    {
        var language = CultureInfo.GetCultureInfo(culture);

        var profile = new SoftKeyboardInputMethod(
            Guid.NewGuid(),
            Guid.NewGuid(),
            (ushort)language.LCID,
            culture,
            true,
            preference is { } id
                ? new SoftKeyboardTouchLayoutPreference(
                    SoftKeyboardTouchLayoutType.Optimized, (ushort)id
                )
                : null
        );

        return new SoftKeyboardLayoutContext(
            language,
            scope,
            SoftKeyboardPages.Automatic,
            SoftKeyboardShiftState.Off,
            native is null ? null : NativeMode(native.Value),
            profile,
            supplementalLayout: layout
        );
    }

    private static SoftKeyboardInputMode NativeMode(bool native = true)
    {
        return new SoftKeyboardInputMode(
            native ? "native" : "direct",
            native,
            true,
            new SoftKeyboardInputModeButton(
                native ? "native" : "direct",
                native ? "中" : "A",
                native ? "Native input" : "Direct input"
            )
        );
    }

    private sealed class CustomSettingsProvider : ISoftKeyboardSettingsProvider
    {
        public IEnumerable<SoftKeyboardSetting> CreateSettings(SoftKeyboardSettingsContext context) =>
            DefaultSoftKeyboardSettingsProvider.Default.CreateSettings(context)
                .Select(node => node with
                {
                    Header = "Options",
                    Icon = new FluentIconSource(SymbolRegular.Settings24)
                });
    }

    private sealed class ThrowingSettingsProvider : ISoftKeyboardSettingsProvider
    {
        public IEnumerable<SoftKeyboardSetting> CreateSettings(SoftKeyboardSettingsContext context)
            => throw new InvalidOperationException("Policy failure");
    }
}
