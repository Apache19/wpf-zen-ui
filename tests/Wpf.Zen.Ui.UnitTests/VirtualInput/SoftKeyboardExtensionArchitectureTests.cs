// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensibility;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Layouts.Providers;
using Wpf.Zen.Ui.VirtualInput.Settings;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Verifies the core/extension boundary and independent selection of language and vendor
/// capabilities.
/// </summary>
public class SoftKeyboardExtensionArchitectureTests
{
    /// <summary>
    /// Keeps language and vendor policy in the opt-in extension assembly.
    /// </summary>
    [Fact]
    public void CoreAssembly_ContainsNoLanguageOrVendorAdapters()
    {
        var assembly = typeof(SoftKeyboard).Assembly;
        var names    = assembly.GetTypes().Select(type => type.FullName ?? string.Empty).ToArray();
        var image    = Encoding.UTF8.GetString(File.ReadAllBytes(assembly.Location));

        Assert.DoesNotContain(names, name => name.Contains("Chinese", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("Japanese", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("Zhuyin", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("Weasel", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("Iflytek", StringComparison.Ordinal));
        Assert.DoesNotContain(
            "36C3C795-7159-45AA-AB12-30229A51DBD3",
            image,
            StringComparison.OrdinalIgnoreCase
        );
    }

    /// <summary>
    /// Japanese open and conversion compartments remain distinct instead of becoming one Boolean.
    /// </summary>
    [Theory]
    [InlineData(0, 0x00, "direct", false)]
    [InlineData(1, 0x00, "half-width-alphanumeric", false)]
    [InlineData(1, 0x08, "full-width-alphanumeric", false)]
    [InlineData(1, 0x09, "hiragana", true)]
    [InlineData(1, 0x0B, "full-width-katakana", true)]
    [InlineData(1, 0x03, "half-width-katakana", true)]
    public void JapaneseMode_PreservesEveryStandardState(
        int open,
        int conversion,
        string id,
        bool native)
    {
        var mode = Assert.IsType<SoftKeyboardInputMode>(
            JapaneseInputModeAdapter.Decode(open, conversion, canSwitch: true)
        );

        Assert.Equal(id, mode.Id);
        Assert.Equal(native, mode.IsNativeInput);
        Assert.True(mode.CanSwitch);
        Assert.Null(mode.Button);
    }

    /// <summary>
    /// Chinese native-layout state requires both open and Native conversion values.
    /// </summary>
    [Theory]
    [InlineData(0, 0x01, false)]
    [InlineData(1, 0x00, false)]
    [InlineData(1, 0x01, true)]
    public void ChineseMode_UsesBothStandardCompartments(
        int open,
        int conversion,
        bool native)
    {
        var mode = Assert.IsType<SoftKeyboardInputMode>(
            ChineseInputModeAdapter.Decode(open, conversion, canSwitch: true)
        );

        Assert.Equal(native, mode.IsNativeInput);
        Assert.Null(mode.Button);
    }

    /// <summary>
    /// Unexpected open values are left to vendor adapters instead of being treated as true.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(2)]
    public void StandardModeAdapters_RejectVendorOpenValues(int? open)
    {
        Assert.Null(ChineseInputModeAdapter.Decode(open, 1, canSwitch: true));
        Assert.Null(JapaneseInputModeAdapter.Decode(open, 1, canSwitch: true));
    }

    [Fact]
    public void DefaultCoreConfiguration_HasOnlyAutomaticAndStandardLayouts()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard();

            Assert.Same(EmptySoftKeyboardExtensionProvider.Default, keyboard.ExtensionProvider);
            Assert.DoesNotContain(keyboard.SettingsItems, setting => setting.Id == "layout");
            Assert.Null(keyboard.ExtensionError);
            Assert.NotEmpty(keyboard.CurrentLayout.Keys);
        });
    }

    [Fact]
    public void IndependentLanguageCapabilities_ComposeInDeclarationOrder()
    {
        var inputMethod = new SoftKeyboardInputMethod(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0x0804,
            "Chinese test profile",
            true
        );
        var context = new SoftKeyboardExtensionContext(
            new SoftKeyboardLayoutContext(
                CultureInfo.GetCultureInfo("zh-CN"),
                InputScopeNameValue.Default,
                SoftKeyboardPages.Symbols,
                SoftKeyboardShiftState.Off,
                new SoftKeyboardInputMode("chinese", true, true),
                inputMethod
            )
        );
        var provider = new CompositeSoftKeyboardExtensionProvider(
            ChineseSoftKeyboardExtensionProvider.Default,
            JapaneseSoftKeyboardExtensionProvider.Default
        );

        SoftKeyboardExtension[] extensions = provider.GetExtensions(context).ToArray();

        Assert.NotNull(extensions[0].LayoutProvider);
        Assert.Single(extensions);
    }

    [Fact]
    public void DuplicateSupplementalLayout_UsesFirstRegistrationAndReportsExtensionError()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var first  = new MarkerLayoutProvider("first");
            var second = new MarkerLayoutProvider("second");
            var keyboard = new SoftKeyboard
            {
                ExtensionProvider = new FixedExtensionProvider(
                    new SoftKeyboardExtension(
                        supplementalLayouts: new FixedSupplementalProvider(
                            new SoftKeyboardSupplementalLayout(
                                "sample:shared",
                                "First",
                                first,
                                isAutomaticPreferred: true
                            )
                        )
                    ),
                    new SoftKeyboardExtension(
                        supplementalLayouts: new FixedSupplementalProvider(
                            new SoftKeyboardSupplementalLayout(
                                "sample:shared",
                                "Second",
                                second,
                                isAutomaticPreferred: true
                            )
                        )
                    )
                ),
            };

            Assert.NotNull(keyboard.ExtensionError);
            Assert.Contains(keyboard.CurrentLayout.Keys, key => key.Text == "first");
            SoftKeyboardSetting layout = Assert.Single(
                keyboard.SettingsItems,
                setting => setting.Id == "layout"
            );
            Assert.Single(layout.Children, setting => setting.Id == "layout.sample:shared");

            keyboard.SelectSupplementalLayout("sample:shared");

            Assert.Contains(keyboard.CurrentLayout.Keys, key => key.Text == "first");
            Assert.DoesNotContain(keyboard.CurrentLayout.Keys, key => key.Text == "second");
        });
    }

    [Fact]
    public void SupplementalApplicability_DrivesCatalogValidationAndAutomaticSelection()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard
            {
                ExtensionProvider = new FixedExtensionProvider(
                    new SoftKeyboardExtension(
                        supplementalLayouts: new FixedSupplementalProvider(
                            new SoftKeyboardSupplementalLayout(
                                "sample:hidden",
                                "Hidden",
                                new MarkerLayoutProvider("hidden"),
                                isAutomaticPreferred: true,
                                isApplicable: _ => false
                            )
                        )
                    )
                ),
            };

            Assert.DoesNotContain(keyboard.SettingsItems, setting => setting.Id == "layout");
            Assert.Throws<ArgumentException>(() =>
                keyboard.SelectSupplementalLayout("sample:hidden"));
            Assert.DoesNotContain(keyboard.CurrentLayout.Keys, key => key.Text == "hidden");
        });
    }

    private sealed class FixedExtensionProvider(params SoftKeyboardExtension[] extensions)
        : ISoftKeyboardExtensionProvider
    {
        public IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context)
        {
            return extensions;
        }
    }

    private sealed class FixedSupplementalProvider(params SoftKeyboardSupplementalLayout[] layouts)
        : ISoftKeyboardSupplementalLayoutProvider
    {
        public IEnumerable<SoftKeyboardSupplementalLayout> GetLayouts(
            SoftKeyboardLayoutContext context)
        {
            return layouts;
        }
    }

    private sealed class MarkerLayoutProvider(string text) : ISoftKeyboardLayoutProvider
    {
        public SoftKeyboardLayout CreateLayout(SoftKeyboardLayoutContext context)
        {
            return new SoftKeyboardLayoutBuilder(1)
                .AddRow(0, [SoftKeyboardKey.FromText(text)])
                .Build();
        }
    }
}
