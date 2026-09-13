// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

public class SoftKeyboardCompositionTests
{
    [Fact]
    public void Composition_UsesFirstAcceptedSnapshotAndCopiesRegistrations()
    {
        var empty  = new SoftKeyboardLayout(Array.Empty<SoftKeyboardKey>());
        var called = 0;
        ISoftKeyboardLayoutProvider[] providers =
        [
            new SoftKeyboardLayoutRule(_ => false, _ => throw new Exception("Must not execute")),
            new SoftKeyboardLayoutRule(_ => true, _ => null),
            new SoftKeyboardLayoutRule(_ => true, _ => empty),
            new SoftKeyboardLayoutRule(_ => true, _ =>
                {
                    called++;
                    return null;
                }
            ),
        ];
        var composite = new CompositeSoftKeyboardLayoutProvider(providers);
        providers[2] = providers[3];
        Assert.Same(empty, composite.CreateLayout(Context()));
        Assert.Equal(0, called);
    }

    [Fact]
    public void ProfileOverride_OnlyReplacesItsOwnLanguageAndPage()
    {
        var custom = new SoftKeyboardLayoutBuilder(1).AddRow(0, [SoftKeyboardKey.FromText("独")]).Build();
        var provider = new CompositeSoftKeyboardLayoutProvider(
            new SoftKeyboardLayoutRule(
                c => c.Culture.Name == "ja-JP" && c.Page == SoftKeyboardPages.Symbols,
                _ => custom
            ),
            DefaultSoftKeyboardLayoutProvider.Default
        );

        Assert.Same(custom, provider.CreateLayout(Context("ja-JP", SoftKeyboardPages.Symbols)));
        Assert.NotSame(custom, provider.CreateLayout(Context("ja-JP", SoftKeyboardPages.Letters)));
        Assert.NotSame(custom, provider.CreateLayout(Context("zh-CN", SoftKeyboardPages.Symbols)));
        Assert.Null(provider.CreateLayout(Context(page: "app:unhandled")));
    }

    [Fact]
    public void ProviderExceptions_AreNotTreatedAsDeclines()
    {
        var provider = new CompositeSoftKeyboardLayoutProvider(
            new SoftKeyboardLayoutRule(_ => true, _ => throw new InvalidOperationException("defect")),
            DefaultSoftKeyboardLayoutProvider.Default);
        Assert.Throws<InvalidOperationException>(() => provider.CreateLayout(Context()));
    }

    [Fact]
    public void Builder_FailedRowsAreAtomicAndBuiltLayoutsRemainIndependent()
    {
        var builder = new SoftKeyboardLayoutBuilder(4).AddRow(0, [SoftKeyboardKey.FromText("A")], 2);
        var first   = builder.Build();
        Assert.Throws<ArgumentException>(() => builder.AddRow(
            1, [SoftKeyboardKey.FromText("B"), SoftKeyboardKey.FromText("C")], [2]));
        Assert.Throws<ArgumentException>(() => builder.AddRow(1, [SoftKeyboardKey.FromText("B")], 5));
        Assert.Single(builder.Build().Keys);
        builder.AddRow(1, [SoftKeyboardKey.Navigate("app:page3", "3", "Third page")], 2, 1);
        Assert.Single(first.Keys);
        var key = builder.Build().Keys[1];
        Assert.Equal((1, 1, 2, "app:page3"), (key.Row, key.Column, key.ColumnSpan, key.Page));
    }

    [Fact]
    public void Builder_PreservesCustomActionWhenPlacingKeys()
    {
        var action = new NoOpAction();
        var layout = new SoftKeyboardLayoutBuilder(2).AddRow(
            0, [SoftKeyboardKey.FromAction(action, "Fn", "Custom function")], 2).Build();
        Assert.Same(action, Assert.Single(layout.Keys).Action);
        Assert.Throws<ArgumentException>(() => new SoftKeyboardKey(SoftKeyboardKeyKind.Custom, "Fn", "Function"));
        Assert.Throws<ArgumentException>(() => new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "x", "x", action: action));
    }

    [Theory]
    [InlineData("ja-JP", "、", "「", "〒")]
    [InlineData("zh-CN", "，", "“", "µ")]
    [InlineData("ko-KR", ",", "'", "°")]
    [InlineData("en-US", ",", "'", "°")]
    public void NativeLanguages_HaveIndependentPunctuation(
        string culture, string comma, string firstSymbol, string secondSymbol)
    {
        var firstContext  = Context(culture, SoftKeyboardPages.Symbols);
        var secondContext = Context(culture, SoftKeyboardPages.MoreSymbols);
        var first         = CreateExtendedLayout(firstContext);
        var second        = CreateExtendedLayout(secondContext);

        Assert.Contains(first.Keys, key => key.Text == comma);
        Assert.Contains(first.Keys, key => key.Text == firstSymbol);
        Assert.Contains(second.Keys, key => key.Text == secondSymbol);

        var direct = DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(
            new SoftKeyboardLayoutContext(
                CultureInfo.GetCultureInfo(culture),
                InputScopeNameValue.Default,
                SoftKeyboardPages.Symbols,
                SoftKeyboardShiftState.Off,
                InputMethodState.Off
            )
        )!;

        Assert.Contains(direct.Keys, key => key.Text == ",");
        Assert.DoesNotContain(direct.Keys, key => key.Text == "「" || key.Text == "“");
    }

    private static SoftKeyboardLayout CreateExtendedLayout(SoftKeyboardLayoutContext context)
    {
        var extensionLayout = DefaultSoftKeyboardExtensionProvider.Default
            .GetExtensions(new SoftKeyboardExtensionContext(context))
            .Select(extension => extension.LayoutProvider)
            .FirstOrDefault(provider => provider?.CreateLayout(context) is not null)
            ?.CreateLayout(context);

        return extensionLayout ?? DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(context)!;
    }

    [Fact]
    public void Context_FreezesCultureAndRejectsInvalidPages()
    {
        var culture = new CultureInfo("en-US");
        var context = new SoftKeyboardLayoutContext(
            culture, InputScopeNameValue.Number, SoftKeyboardPages.Numbers, SoftKeyboardShiftState.Off);
        culture.NumberFormat.NumberDecimalSeparator = "!";
        Assert.Equal(".", context.Culture.NumberFormat.NumberDecimalSeparator);
        Assert.True(context.Culture.IsReadOnly);
        Assert.Throws<ArgumentException>(() => Context(page: " "));
        Assert.Throws<ArgumentException>(() => SoftKeyboardKey.Navigate(string.Empty, "x", "x"));
    }

    [Fact]
    public void Control_ResolvesCustomPagesAndReportsFallbackThenRecovers()
    {
        WpfApplicationTestHost.Run(
            () =>
            {
                var custom = new SoftKeyboardLayoutBuilder(1).AddRow(0, [SoftKeyboardKey.FromText("独")]).Build();
                var keyboard = new SoftKeyboard
                {
                    LayoutProvider = new SoftKeyboardLayoutRule(c => c.Page == "app:third", _ => custom),
                };

                keyboard.ShowPage("app:third");

                Assert.Same(custom, keyboard.CurrentLayout);
                Assert.Null(keyboard.LayoutError);

                keyboard.ShowPage("app:missing");

                Assert.Equal(SoftKeyboardPages.Automatic, keyboard.Page);
                Assert.IsType<InvalidOperationException>(keyboard.LayoutError);
                Assert.NotEmpty(keyboard.CurrentLayout.Keys);

                keyboard.ShowPage("app:third");

                Assert.Same(custom, keyboard.CurrentLayout);
                Assert.Null(keyboard.LayoutError);

                keyboard.LayoutProvider =
                    new SoftKeyboardLayoutRule(_ => true, _ => throw new InvalidOperationException("defect")
                    );

                Assert.Equal("defect", keyboard.LayoutError!.Message);
                Assert.NotEmpty(keyboard.CurrentLayout.Keys);
            }
        );
    }

    private static SoftKeyboardLayoutContext Context(string culture = "en-US", string page = SoftKeyboardPages.Letters)
    {
        return new SoftKeyboardLayoutContext(
            CultureInfo.GetCultureInfo(culture),
            InputScopeNameValue.Default,
            page,
            SoftKeyboardShiftState.Off,
            InputMethodState.On
        );
    }

    private sealed class NoOpAction : ISoftKeyboardKeyAction
    {
        public void Execute(SoftKeyboardActionContext context) { }
    }
}