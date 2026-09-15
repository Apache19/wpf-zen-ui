// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Wpf.Zen.Ui.VirtualInput.Extensibility;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

public class SoftKeyboardRoutingTests
{
    [Theory]
    [InlineData(InputScopeNameValue.Url)]
    [InlineData(InputScopeNameValue.EmailSmtpAddress)]
    public void EntireAddressScenario_CanReplaceEveryPageAcrossLanguages(InputScopeNameValue scope)
    {
        var replacement = Layout("whole-address", 7, 2);
        var provider = new CompositeSoftKeyboardLayoutProvider(
            new SoftKeyboardLayoutRule(c => c.InputScope == scope, _ => replacement),
            DefaultSoftKeyboardLayoutProvider.Default);

        foreach (var culture in new[]
            {
                "en-US", "ja-JP", "zh-CN"
            })
        {
            foreach (var page in SoftKeyboardPages.BuiltInPages.Concat(["app:address-tools"]))
            {
                // Exact snapshot identity also proves that no default letter rows or command bars
                // were silently added to the consumer's complete geometry.
                Assert.Same(replacement, provider.CreateLayout(Context(culture, scope, page)));
            }
        }

        Assert.NotSame(replacement, provider.CreateLayout(Context("ja-JP", InputScopeNameValue.Default)));
    }

    [Fact]
    public void LanguageSubtree_OwnsAddressesSymbolsNumbersAndCustomPages()
    {
        var urlMain    = Layout("URL", 3, 2);
        var urlSymbols = Layout("URL symbols", 5, 3);
        var email      = Layout("Email", 9, 1);
        var symbols    = Layout("Japanese symbols", 8, 2);
        var numbers    = Layout("Japanese numbers", 6, 3);
        var tools      = Layout("Japanese tools", 11, 1);

        var url = new CompositeSoftKeyboardLayoutProvider(
            new SoftKeyboardLayoutRule(c => c.Page == SoftKeyboardPages.Symbols, _ => urlSymbols),
            new SoftKeyboardLayoutRule(_ => true, _ => urlMain));

        var japanese = new CompositeSoftKeyboardLayoutProvider(
            new SoftKeyboardLayoutRule(c => c.InputScope == InputScopeNameValue.Url, url.CreateLayout),
            new SoftKeyboardLayoutRule(c => c.InputScope == InputScopeNameValue.EmailSmtpAddress, _ => email),
            new SoftKeyboardLayoutRule(c => c.Page == SoftKeyboardPages.Symbols, _ => symbols),
            new SoftKeyboardLayoutRule(c => c.InputScope == InputScopeNameValue.Number, _ => numbers),
            new SoftKeyboardLayoutRule(c => c.Page == "app:tools", _ => tools),
            JapaneseExtensionLayoutProvider.Default
        );

        var provider = new CompositeSoftKeyboardLayoutProvider(
            new SoftKeyboardLayoutRule(c => c.Culture.TwoLetterISOLanguageName == "ja", japanese.CreateLayout),
            DefaultSoftKeyboardLayoutProvider.Default
        );

        Assert.Same(
            urlMain, provider.CreateLayout(Context("ja-JP", InputScopeNameValue.Url))
        );

        Assert.Same(
            urlSymbols,
            provider.CreateLayout(
                Context("ja-JP", InputScopeNameValue.Url, SoftKeyboardPages.Symbols)
            )
        );

        Assert.Same(
            email,
            provider.CreateLayout(
                Context("ja-JP", InputScopeNameValue.EmailSmtpAddress, SoftKeyboardPages.Symbols)
            )
        );

        Assert.Same(
            symbols,
            provider.CreateLayout(
                Context("ja-JP", InputScopeNameValue.Default, SoftKeyboardPages.Symbols)
            )
        );

        Assert.Same(
            numbers,
            provider.CreateLayout(
                Context("ja-JP", InputScopeNameValue.Number)
            )
        );

        Assert.Same(
            tools,
            provider.CreateLayout(
                Context("ja-JP", InputScopeNameValue.Default, "app:tools")
            )
        );

        Assert.NotSame(
            urlMain,
            provider.CreateLayout(Context("zh-CN", InputScopeNameValue.Url)
            )
        );

        Assert.Null(
            provider.CreateLayout(
                Context("en-US", InputScopeNameValue.Default, "app:tools")
            )
        );

        var nativeText = Assert.IsType<SoftKeyboardLayout>(
            provider.CreateLayout(
                Context("ja-JP", InputScopeNameValue.Default)
            )
        );

        Assert.Contains(nativeText.Keys, key => key.Text == "ー");
    }

    [Fact]
    public void ProfileOverride_IsMoreSpecificThanItsLanguageFallback()
    {
        var classId     = Guid.NewGuid();
        var profileId   = Guid.NewGuid();
        var replacement = Layout("Vendor URL", 4, 2);

        var provider = new CompositeSoftKeyboardLayoutProvider(
            new SoftKeyboardLayoutRule(
                c => c.InputMethod?.ClassId == classId && c.InputMethod.ProfileId == profileId
                    && c.InputScope == InputScopeNameValue.Url,
                _ => replacement
            ),
            JapaneseExtensionLayoutProvider.Default
        );

        Assert.Same(replacement, provider.CreateLayout(WithProfile(profileId)));
        var fallback = Assert.IsType<SoftKeyboardLayout>(provider.CreateLayout(WithProfile(Guid.NewGuid())));
        Assert.NotSame(replacement, fallback);
        Assert.Contains(fallback.Keys, key => key.Text == "「");
        return;

        SoftKeyboardLayoutContext WithProfile(Guid id)
        {
            return new SoftKeyboardLayoutContext(
                CultureInfo.GetCultureInfo("ja-JP"),
                InputScopeNameValue.Url,
                SoftKeyboardPages.Symbols,
                SoftKeyboardShiftState.Off,
                new SoftKeyboardInputMode("native", true, true),
                new SoftKeyboardInputMethod(classId, id, 0x0411, "Test profile", true)
            );
        }
    }

    [Theory]
    [InlineData(InputScopeNameValue.Url)]
    [InlineData(InputScopeNameValue.EmailSmtpAddress)]
    public void BuiltInAddressProviders_OwnCompleteMainPagesAndDeclineOtherContexts(InputScopeNameValue scope)
    {
        ISoftKeyboardLayoutProvider provider = scope == InputScopeNameValue.Url
            ? UrlSoftKeyboardLayoutProvider.Default
            : EmailSoftKeyboardLayoutProvider.Default;
        foreach (var culture in new[]
            {
                "en-US", "ja-JP", "zh-CN"
            })
        {
            foreach (var page in new[]
                {
                    SoftKeyboardPages.Automatic, SoftKeyboardPages.Letters
                })
            {
                var layout = Assert.IsType<SoftKeyboardLayout>(provider.CreateLayout(Context(culture, scope, page)));

                Assert.Equal(20, layout.ColumnCount);
                Assert.Equal([0, 1, 2, 3], layout.Keys.Select(key => key.Row).Distinct());
                Assert.Equal(scope == InputScopeNameValue.Url ? 37 : 36, layout.Keys.Count);
                Assert.Contains(layout.Keys, key => key.Text == (scope == InputScopeNameValue.Url ? "http" : "@"));
                Assert.DoesNotContain(layout.Keys, key => key.Text == "ー");
            }
        }

        Assert.Null(provider.CreateLayout(Context("ja-JP", scope, SoftKeyboardPages.Symbols)));
        Assert.Null(provider.CreateLayout(Context("ja-JP", InputScopeNameValue.Default)));
    }

    [Fact]
    public void BuiltInLanguageProviders_MatchEnvironmentBeforeScopeAndPreserveContext()
    {
        Assert.Null(JapaneseExtensionLayoutProvider.Default.CreateLayout(Context("zh-CN", InputScopeNameValue.Url)));
        Assert.Null(ChineseExtensionLayoutProvider.Default.CreateLayout(Context("ja-JP", InputScopeNameValue.Number)));
        var context = Context("ja-JP", InputScopeNameValue.Url, "app:page");
        var seen    = false;
        var nested = new SoftKeyboardLayoutRule(_ => true, c =>
        {
            seen = true;
            Assert.Same(context, c);
            return null;
        });
        new SoftKeyboardLayoutRule(_ => true, nested.CreateLayout).CreateLayout(context);
        Assert.True(seen);
    }

    private static SoftKeyboardLayoutContext Context(
        string culture, InputScopeNameValue scope, string page = SoftKeyboardPages.Automatic) =>
        new(
            CultureInfo.GetCultureInfo(culture),
            scope,
            page,
            SoftKeyboardShiftState.Off,
            new SoftKeyboardInputMode("native", true, true)
        );

    private static SoftKeyboardLayout Layout(string marker, int columns, int rows)
    {
        var builder = new SoftKeyboardLayoutBuilder(columns);
        for (var row = 0; row < rows; row++)
            builder.AddRow(row, [SoftKeyboardKey.FromText(marker)], columns);
        return builder.Build();
    }

    private sealed class JapaneseExtensionLayoutProvider : ISoftKeyboardLayoutProvider
    {
        internal static JapaneseExtensionLayoutProvider Default { get; } = new();

        public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
        {
            return JapaneseSoftKeyboardExtensionProvider.Default
                .GetExtensions(new SoftKeyboardExtensionContext(context))
                .Select(extension => extension.LayoutProvider)
                .FirstOrDefault(provider => provider is not null)
                ?.CreateLayout(context);
        }
    }

    private sealed class ChineseExtensionLayoutProvider : ISoftKeyboardLayoutProvider
    {
        internal static ChineseExtensionLayoutProvider Default { get; } = new();

        public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
        {
            return ChineseSoftKeyboardExtensionProvider.Default
                .GetExtensions(new SoftKeyboardExtensionContext(context))
                .Select(extension => extension.LayoutProvider)
                .FirstOrDefault(provider => provider is not null)
                ?.CreateLayout(context);
        }
    }
}
