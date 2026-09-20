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
using Wpf.Zen.Ui.VirtualInput.Extensions.Candidates;
using Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;
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
    /// The default extension embeds candidates for explicitly supported input methods.
    /// </summary>
    /// <param name="classId">The TSF text service class identifier.</param>
    /// <param name="expected">Whether the service uses embedded candidate presentation.</param>
    [Theory]
    [InlineData("03B5835F-F03C-411B-9CE2-AA23E1171E36", true)]
    [InlineData("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E", true)]
    [InlineData("86598FB9-66A2-463E-B9C2-AEB906D477AD", false)]
    [InlineData("AE51F1C0-807F-4A64-AC55-F2ADF92E2603", false)]
    [InlineData("A3F4CDED-B1E9-41EE-9CA6-7B4D0DE6CB0A", false)]
    public void WindowsCandidatePolicy_EmbedsSupportedInputMethods(
        string classId,
        bool expected)
    {
        Assert.Equal(
            expected,
            WindowsCandidatePresentationBehavior.IsEmbeddedInputMethod(Guid.Parse(classId))
        );
    }

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
    /// A verified active profile selects its language extension before a lagging ambient culture.
    /// </summary>
    [Fact]
    public void LanguageExtensionRouting_PrefersActiveProfileLanguage()
    {
        var inputMethod = new SoftKeyboardInputMethod(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0x0411,
            "Japanese TIP",
            true
        );
        var layout = new SoftKeyboardLayoutContext(
            CultureInfo.GetCultureInfo("zh-CN"),
            InputScopeNameValue.Default,
            SoftKeyboardPages.Letters,
            SoftKeyboardShiftState.Off,
            inputMethod: inputMethod
        );
        var extensions = DefaultSoftKeyboardExtensionProvider.Default
            .GetExtensions(new SoftKeyboardExtensionContext(layout))
            .ToArray();

        var extension = extensions.Single(item => item.InputModeAdapter is not null);
        Assert.Same(MicrosoftJapaneseInputModeAdapter.Default, extension.InputModeAdapter);
        Assert.Contains(extensions, item => item.CandidateBehavior is not null);
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
                inputMethod
            )
        );
        var provider = new CompositeSoftKeyboardExtensionProvider(
            ChineseSoftKeyboardExtensionProvider.Default,
            JapaneseSoftKeyboardExtensionProvider.Default
        );

        SoftKeyboardExtension[] extensions = provider.GetExtensions(context).ToArray();

        Assert.NotNull(extensions[0].InputModeAdapter);
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
