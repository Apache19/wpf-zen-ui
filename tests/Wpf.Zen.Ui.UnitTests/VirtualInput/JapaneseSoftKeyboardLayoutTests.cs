// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensibility;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Extensions.Layouts;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;
using Wpf.Zen.Ui.VirtualInput.Layouts;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Verifies complete physical input coverage and native Kana labels independently of composition.
/// </summary>
public class JapaneseSoftKeyboardLayoutTests
{
    /// <summary>
    /// Checks the six-row surface with the real key template at a desktop touch-keyboard size.
    /// An optional directory captures the rendered result for visual review.
    /// </summary>
    /// <param name="shift">The character layer to render.</param>
    [Theory]
    [InlineData(SoftKeyboardShiftState.Off)]
    [InlineData(SoftKeyboardShiftState.Once)]
    public void DefaultTemplate_KeepsCharacterCapsInsideTheirSlots(SoftKeyboardShiftState shift)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var handle = WindowsKeyboardLayouts.GetLoaded()
                .FirstOrDefault(value => (value.ToInt64() & 0xFFFF) == 0x0411);
            var layout = JapaneseSoftKeyboardLayoutProvider.Default.CreateLayout(
                Context(shift: shift, handle: handle))!;
            var panel = new SoftKeyboardPanel { ColumnCount = layout.ColumnCount };
            foreach (var key in layout.Keys)
            {
                var control = new SoftKeyboardKeyControl
                {
                    Key = key,
                    Background = Brushes.WhiteSmoke,
                    Foreground = Brushes.Black,
                    BorderBrush = Brushes.LightGray,
                };
                SoftKeyboardPanel.SetRow(control, key.Row);
                SoftKeyboardPanel.SetColumn(control, key.Column);
                SoftKeyboardPanel.SetColumnSpan(control, key.ColumnSpan);
                panel.Children.Add(control);
            }

            var border = new Border { Child = panel, Background = Brushes.White, Padding = new Thickness(16) };
            var window = new Window { Content = border, Width = 900, Height = 420, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                foreach (SoftKeyboardKeyControl key in panel.Children)
                {
                    Assert.NotNull(key.Template);
                    Assert.True(key.ActualWidth > 40);
                    Assert.True(key.ActualHeight >= 52);
                    var bounds = key.TransformToAncestor(panel)
                        .TransformBounds(new Rect(key.RenderSize));
                    Assert.True(bounds.Left >= -0.5 && bounds.Right <= panel.ActualWidth + 0.5);
                    Assert.True(bounds.Top >= -0.5 && bounds.Bottom <= panel.ActualHeight + 0.5);
                }

                if (Environment.GetEnvironmentVariable("WPF_ZEN_LAYOUT_PREVIEW_DIRECTORY") is { } directory)
                {
                    Directory.CreateDirectory(directory);
                    var bitmap = new RenderTargetBitmap((int)border.ActualWidth, (int)border.ActualHeight,
                        96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(border);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(directory, $"japanese-{shift}.png"));
                    encoder.Save(file);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Every mode preserves all character keys and conversion commands on one nonoverlapping page.
    /// </summary>
    [Fact]
    public void MainPage_CoversPhysicalInputInEveryShiftAndButtonState()
    {
        Key[] punctuation =
        [
            Key.Oem1, Key.OemPlus, Key.OemComma, Key.OemMinus, Key.OemPeriod, Key.Oem2,
            Key.Oem3, Key.Oem4, Key.Oem5, Key.Oem6, Key.Oem7, Key.Oem102,
        ];
        Key[] commands =
        [
            Key.Back, Key.Delete, Key.Tab, Key.Enter, Key.Space, Key.Escape,
            Key.ImeConvert, Key.ImeNonConvert, Key.Left, Key.Right, Key.Up, Key.Down,
            Key.F6, Key.F7, Key.F8, Key.F9, Key.F10,
        ];
        var characters = Enumerable.Range((int)Key.A, 26).Select(value => (Key)value)
            .Concat(Enumerable.Range((int)Key.D0, 10).Select(value => (Key)value))
            .Concat(punctuation).ToArray();

        var provider = JapaneseSoftKeyboardLayoutProvider.Default;
        foreach (var shift in Enum.GetValues<SoftKeyboardShiftState>())
        {
            foreach (var button in new[] { false, true })
            {
                var layout = provider.CreateLayout(Context(shift: shift, button: button))!;
                Assert.Equal(30, layout.ColumnCount);
                Assert.Equal(6, layout.Keys.Select(key => key.Row).Distinct().Count());
                Assert.DoesNotContain(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.Text);
                Assert.All(layout.Keys, key => Assert.Empty(key.Alternates));
                foreach (var key in characters.Concat(commands))
                {
                    Assert.Single(layout.Keys, cap => cap.Kind == SoftKeyboardKeyKind.VirtualKey
                        && cap.Key == key);
                }

                Assert.Equal(button ? 1 : 0,
                    layout.Keys.Count(key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode));
                Assert.Single(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.InputMethods);
                foreach (var row in layout.Keys.GroupBy(key => key.Row))
                {
                    var end = 0;
                    foreach (var key in row.OrderBy(key => key.Column))
                    {
                        Assert.True(key.Column >= end);
                        end = key.Column + key.ColumnSpan;
                        Assert.True(end <= layout.ColumnCount);
                    }

                    Assert.Equal(layout.ColumnCount, end);
                }
            }
        }
    }

    /// <summary>
    /// Language registration supplies a complete keyboard even without a recognized vendor mode.
    /// Explicit pages and editor-specific scopes still delegate to their existing layouts.
    /// </summary>
    [Fact]
    public void RegistrationAndSpecializedPages_PreserveRouting()
    {
        var context = Context();
        var extension = Assert.Single(JapaneseSoftKeyboardExtensionProvider.Default
            .GetExtensions(new SoftKeyboardExtensionContext(context)));
        var provider = Assert.IsType<JapaneseSoftKeyboardLayoutProvider>(extension.LayoutProvider);
        Assert.Contains(provider.CreateLayout(context)!.Keys, key => key.Key == Key.D1);
        Assert.Contains(provider.CreateLayout(Context(scope: InputScopeNameValue.Url))!.Keys,
            key => key.Text == "http");
        Assert.Contains(provider.CreateLayout(Context(scope: InputScopeNameValue.EmailSmtpAddress))!.Keys,
            key => key.Text == ".com");
        Assert.Contains(provider.CreateLayout(Context(scope: InputScopeNameValue.Number))!.Keys,
            key => key.Text == "1");
        Assert.Contains(provider.CreateLayout(Context(page: SoftKeyboardPages.Letters,
            scope: InputScopeNameValue.Number))!.Keys, key => key.Key == Key.D1);
        Assert.Contains(provider.CreateLayout(Context(page: SoftKeyboardPages.Numbers))!.Keys,
            key => key.Text == "1");
        Assert.Contains(provider.CreateLayout(Context(page: SoftKeyboardPages.Symbols))!.Keys,
            key => key.Text == "「");
        Assert.Null(provider.CreateLayout(Context(page: "app:unknown")));
        Assert.Null(provider.CreateLayout(new SoftKeyboardLayoutContext(
            CultureInfo.GetCultureInfo("en-US"), InputScopeNameValue.Default,
            SoftKeyboardPages.Letters, SoftKeyboardShiftState.Off)));
    }

    /// <summary>
    /// Uses an already installed Japanese keyboard without activating it or changing IME settings.
    /// Digit, small-Kana and voiced-mark labels must come from the native translation table.
    /// </summary>
    [Fact]
    public void InstalledJapaneseLayout_TranslatesKanaAndSmallKanaWithoutChangingPhysicalLabels()
    {
        var handle = WindowsKeyboardLayouts.GetLoaded()
            .FirstOrDefault(value => (value.ToInt64() & 0xFFFF) == 0x0411);
        Assert.SkipWhen(handle == IntPtr.Zero, "A loaded Japanese keyboard layout is required.");
        var normal = Context(handle: handle);
        var shifted = Context(shift: SoftKeyboardShiftState.Once, handle: handle);
        var layout = JapaneseSoftKeyboardLayoutProvider.Default.CreateLayout(normal)!;
        var small = JapaneseSoftKeyboardLayoutProvider.Default.CreateLayout(shifted)!;

        Assert.Equal("ぬ", layout.Keys.Single(key => key.Key == Key.D1).AlternateHint);
        Assert.Equal("あ", layout.Keys.Single(key => key.Key == Key.D3).AlternateHint);
        Assert.Equal("ぁ", small.Keys.Single(key => key.Key == Key.D3).AlternateHint);
        Assert.Equal("つ", layout.Keys.Single(key => key.Key == Key.Z).AlternateHint);
        Assert.Equal("っ", small.Keys.Single(key => key.Key == Key.Z).AlternateHint);
        Assert.Equal("1", normal.GetKeyLabel(Key.D1));
        Assert.Equal("1", layout.Keys.Single(key => key.Key == Key.D1).Content);
        Assert.Contains(layout.Keys, key => key.AlternateHint is "゙" or "゛");
        Assert.Contains(layout.Keys, key => key.AlternateHint is "゚" or "゜");
        Assert.True(layout.Keys.Concat(small.Keys).Any(key => key.AlternateHint is "ー"),
            string.Join(", ", layout.Keys.Concat(small.Keys)
                .Where(key => key.Kind == SoftKeyboardKeyKind.VirtualKey)
                .Select(key => $"{key.Key}={key.Content}")));
        Assert.Contains(layout.Keys.Concat(small.Keys), key => key.AlternateHint is "ろ");
    }

    /// <summary>
    /// Captures one language/profile pair while allowing tests to pin the native layout identity.
    /// </summary>
    private static SoftKeyboardLayoutContext Context(
        string page = SoftKeyboardPages.Automatic,
        InputScopeNameValue scope = InputScopeNameValue.Default,
        SoftKeyboardShiftState shift = SoftKeyboardShiftState.Off,
        bool button = true,
        IntPtr handle = default)
    {
        return new SoftKeyboardLayoutContext(CultureInfo.GetCultureInfo("ja-JP"), scope, page, shift,
            new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0411, "Japanese", true),
            handle, inputModeButton: button ? new SoftKeyboardInputModeButton("ja", "あ", "Japanese") : null);
    }
}
