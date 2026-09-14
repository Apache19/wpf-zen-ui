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
/// Verifies the core/extension boundary and independent selection of language and vendor capabilities.
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
            WeaselCandidateBehavior.CandidateElementId.ToString("D"),
            image,
            StringComparison.OrdinalIgnoreCase
        );
    }

    /// <summary>
    /// iFlytek's two nonzero open values must remain distinguishable for the A/中 key.
    /// </summary>
    [Theory]
    [InlineData(null, InputMethodState.DoNotCare)]
    [InlineData(0, InputMethodState.Off)]
    [InlineData(1, InputMethodState.Off)]
    [InlineData(2, InputMethodState.On)]
    [InlineData(3, InputMethodState.DoNotCare)]
    public void IflytekOpenValue_PreservesNativeAndDirectModes(int? value, InputMethodState expected)
    {
        Assert.Equal(expected, IflytekSoftKeyboardExtensionProvider.DecodeOpenValue(value));
    }

    /// <summary>
    /// Vendor selection depends on the TSF service identity, not a localized display name.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IflytekExtension_OnlyAppliesToItsService(bool matchesService)
    {
        var profile = new SoftKeyboardInputMethod(
            matchesService ? IflytekSoftKeyboardExtensionProvider.ServiceId : Guid.NewGuid(),
            Guid.NewGuid(), 0x0804, "讯飞输入法", true
        );
        var context = new SoftKeyboardExtensionContext(new SoftKeyboardLayoutContext(
            CultureInfo.GetCultureInfo("zh-CN"), InputScopeNameValue.Default,
            SoftKeyboardPages.Letters, SoftKeyboardShiftState.Off, inputMethod: profile
        ));
        var adapters = DefaultSoftKeyboardExtensionProvider.Default.GetExtensions(context)
            .Where(extension => extension.InputStateAdapter is not null).ToArray();
        Assert.Equal(matchesService ? 1 : 0, adapters.Length);
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
    public void IndependentLanguageAndVendorCapabilities_ComposeInDeclarationOrder()
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
                InputMethodState.On,
                inputMethod
            )
        );
        var provider = new CompositeSoftKeyboardExtensionProvider(
            ChineseSoftKeyboardExtensionProvider.Default,
            WeaselSoftKeyboardExtensionProvider.Default
        );

        SoftKeyboardExtension[] extensions = provider.GetExtensions(context).ToArray();

        Assert.NotNull(extensions[0].LayoutProvider);
        Assert.NotNull(extensions[1].CandidateBehavior);
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
