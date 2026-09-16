// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Settings;
using TextBox = System.Windows.Controls.TextBox;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Runs opt-in foreground tests against installed IMEs, including real key injection and
/// composition.
/// </summary>
public class SoftKeyboardNativeImeTests(ITestOutputHelper output)
{
    private static readonly Guid IflytekServiceId =
        new("B722B5D7-0C4C-4933-A7B9-DF8C91F2C643");

    /// <summary>
    /// Exercises modern profile enumeration and exact keyboard-layout activation on Windows.
    /// </summary>
    [Fact]
    public void NativeProfiles_KeyboardLayoutRoundTrip()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IME_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IME_TESTS=1 to exercise installed Windows profiles.");
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new TextBox();
            var keyboard = new SoftKeyboard
            {
                Target = editor,
                IsOpen = true,
                AutoHideMode = SoftKeyboardAutoHideMode.Manual,
                ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default,
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, Width = 850, Height = 450 };
            var backend = new TsfInputMethodService();
            SoftKeyboardInputMethod? original = null;
            try
            {
                window.Show();
                ActivateTestWindow(window);
                editor.Focus();
                PumpFor(400);
                keyboard.RefreshInputMethods();
                original = keyboard.ActiveInputMethod;
                Assert.NotNull(original);
                foreach (var profile in keyboard.InputMethods)
                {
                    output.WriteLine($"{profile.DisplayName}: {profile.ClassId}, "
                        + $"HKL={profile.KeyboardLayout}, active={profile.IsActive}");
                }

                var layouts = keyboard.InputMethods.Where(p => p.KeyboardLayout != IntPtr.Zero).ToArray();
                Assert.SkipUnless(layouts.Length > 0, "No ordinary keyboard layouts are installed.");
                foreach (var layout in layouts)
                {
                    Assert.True(keyboard.TryActivateInputMethod(layout));
                    Assert.True(PumpUntil(() => keyboard.ActiveInputMethod?.KeyboardLayout
                        == layout.KeyboardLayout, 2000));
                    Assert.Equal(Guid.Empty, keyboard.ActiveInputMethod!.ClassId);
                }

                Assert.True(keyboard.TryActivateInputMethod(original));
                Assert.True(PumpUntil(() => keyboard.ActiveInputMethod is { } current
                    && current.ClassId == original.ClassId && current.ProfileId == original.ProfileId
                    && current.KeyboardLayout == original.KeyboardLayout, 2000));
            }
            finally
            {
                if (original is not null)
                {
                    backend.TryActivate(original);
                }

                window.Close();
            }
        });
    }

    /// <summary>
    /// Real Pinyin composition must end before literal text, then publish a fresh candidate
    /// session.
    /// </summary>
    [Fact]
    public void Pinyin_LiteralTextCancellationAndNextCandidateSession()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IME_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IME_TESTS=1 to exercise installed Microsoft Pinyin.");
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new TextBox();
            var keyboard = new SoftKeyboard
            {
                Target = editor,
                IsOpen = true,
                AutoHideMode = SoftKeyboardAutoHideMode.Manual,
                ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default,
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, Width = 850, Height = 450 };
            var backend = new TsfInputMethodService();
            SoftKeyboardInputMethod? original = null;
            SoftKeyboardInputMethod? pinyin = null;
            SoftKeyboardInputMode? pinyinMode = null;
            try
            {
                window.Show();
                ActivateTestWindow(window);
                editor.Focus();
                PumpFor(400);
                keyboard.RefreshInputMethods();
                original = keyboard.ActiveInputMethod;
                pinyin = keyboard.InputMethods.FirstOrDefault(p =>
                    p.ClassId == new Guid("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E"));
                Assert.SkipUnless(pinyin is not null, "Microsoft Pinyin is not installed.");
                Assert.True(keyboard.TryActivateInputMethod(pinyin));
                PumpFor(300);
                pinyinMode = backend.GetInputMode(pinyin);
                Assert.True(EnsureNativeInput(backend, pinyin));
                PumpFor(200);
                Assert.True(keyboard.SendActionKey(Key.N, ModifierKeys.None));
                Assert.True(keyboard.SendActionKey(Key.I, ModifierKeys.None));
                Assert.True(PumpUntil(() => keyboard.HasCandidates, 2000));

                var literal = keyboard.SendActionTextAsync("★");
                Assert.True(PumpUntil(() => literal.IsCompleted, 2000));
                Assert.True(literal.GetAwaiter().GetResult());
                Assert.True(PumpUntil(() => editor.Text == "★" && !keyboard.HasCandidates, 2000));

                Assert.True(keyboard.SendActionKey(Key.N, ModifierKeys.None));
                Assert.True(keyboard.SendActionKey(Key.I, ModifierKeys.None));
                Assert.True(PumpUntil(() => keyboard.HasCandidates, 2000));
                var candidate = keyboard.Candidates[0];
                var commit = keyboard.CommitCandidateAsync(candidate);
                Assert.True(PumpUntil(() => commit.IsCompleted, 2000));
                Assert.True(commit.GetAwaiter().GetResult());
                Assert.True(PumpUntil(() => editor.Text == "★" + candidate.Text
                    && !keyboard.HasCandidates, 2000));
            }
            finally
            {
                if (window.IsVisible)
                {
                    keyboard.SendActionKey(Key.Escape, ModifierKeys.None);
                    PumpFor(100);
                    if (pinyin is not null && pinyinMode is not null)
                    {
                        RestoreInputMode(backend, pinyin, pinyinMode);
                    }
                }

                if (original is not null)
                {
                    backend.TryActivate(original);
                }

                window.Close();
            }
        });
    }

    /// <summary>
    /// The Microsoft Pinyin mode key uses the standard language-bar command when available and
    /// otherwise toggles the native bit in the focused WPF editor's conversion compartment.
    /// </summary>
    [Fact]
    public void Pinyin_ModeKeyChangesNativeConversionState()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IME_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IME_TESTS=1 to exercise installed Microsoft Pinyin.");
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new TextBox();
            var keyboard = new SoftKeyboard
            {
                Target = editor,
                IsOpen = true,
                AutoHideMode = SoftKeyboardAutoHideMode.Manual,
                ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default,
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, Width = 850, Height = 450 };
            var backend = new TsfInputMethodService();
            SoftKeyboardInputMethod? original = null;
            SoftKeyboardInputMethod? pinyin = null;
            bool? originalNative = null;
            try
            {
                window.Show();
                ActivateTestWindow(window);
                editor.Focus();
                PumpFor(400);
                keyboard.RefreshInputMethods();
                original = keyboard.ActiveInputMethod;
                pinyin = keyboard.InputMethods.FirstOrDefault(p =>
                    p.ClassId == new Guid("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E"));
                Assert.SkipUnless(pinyin is not null, "Microsoft Pinyin is not installed.");
                Assert.True(keyboard.TryActivateInputMethod(pinyin));
                Assert.True(PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == pinyin.ClassId
                        && keyboard.InputModeButton is not null,
                    2000));

                var originalState = SoftKeyboardTsfInputMode.GetState(pinyin.ClassId);
                Assert.NotNull(originalState);
                Assert.True(originalState.OpenValue is 0 or 1);
                Assert.NotNull(originalState.ConversionValue);
                originalNative = (originalState.ConversionValue & 1) != 0;
                var originalButtonId = keyboard.InputModeButton!.Id;
                var modeKey = FindVisualDescendants<SoftKeyboardKeyControl>(keyboard).Single(
                    cap => cap.Key?.Kind == SoftKeyboardKeyKind.SwitchInputMode
                );

                modeKey.Invoke();

                Assert.True(PumpUntil(
                    () => SoftKeyboardTsfInputMode.GetState(pinyin.ClassId) is { } state
                        && ((state.ConversionValue & 1) != 0) != originalNative
                        && keyboard.InputModeButton is { } button
                        && button.Id != originalButtonId,
                    2000));

                var currentState = SoftKeyboardTsfInputMode.GetState(pinyin.ClassId)!;
                var currentNative = (currentState.ConversionValue & 1) != 0;
                Assert.Equal(currentNative ? "中" : "英", keyboard.InputModeButton!.Content);

                Assert.True(keyboard.SendActionKey(Key.N, ModifierKeys.None));
                Assert.True(keyboard.SendActionKey(Key.I, ModifierKeys.None));
                Assert.True(PumpUntil(
                    () => currentNative ? keyboard.HasCandidates : editor.Text == "ni",
                    2000));
            }
            finally
            {
                if (pinyin is not null
                    && originalNative is { } expected
                    && SoftKeyboardTsfInputMode.GetState(pinyin.ClassId) is { } state
                    && ((state.ConversionValue & 1) != 0) != expected)
                {
                    _ = SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(
                        pinyin,
                        state.ConversionValue!.Value ^ 1
                    );
                    PumpFor(200);
                }

                if (original is not null)
                {
                    backend.TryActivate(original);
                }

                window.Close();
            }
        });
    }

    /// <summary>
    /// Returning to Microsoft Pinyin in direct mode does not publish its transient native state.
    /// </summary>
    [Fact]
    public void Pinyin_ProfileActivationPublishesOnlySettledMode()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IME_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IME_TESTS=1 to exercise installed Microsoft Pinyin.");
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new TextBox();
            var keyboard = new SoftKeyboard
            {
                Target = editor,
                IsOpen = true,
                AutoHideMode = SoftKeyboardAutoHideMode.Manual,
                ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default,
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, Width = 850, Height = 450 };
            var backend = new TsfInputMethodService();
            SoftKeyboardInputMethod? original = null;
            SoftKeyboardInputMethod? pinyin = null;
            bool? originalNative = null;
            var publishedCaps = new List<object>();
            var descriptor = DependencyPropertyDescriptor.FromProperty(
                SoftKeyboard.InputModeButtonProperty,
                typeof(SoftKeyboard)
            );

            void RecordPinyinButton(object? sender, EventArgs args)
            {
                if (keyboard.ActiveInputMethod?.ClassId == pinyin?.ClassId
                    && keyboard.InputModeButton is { } button)
                {
                    publishedCaps.Add(button.Content);
                }
            }

            try
            {
                window.Show();
                ActivateTestWindow(window);
                editor.Focus();
                PumpFor(400);
                keyboard.RefreshInputMethods();
                original = keyboard.ActiveInputMethod;
                pinyin = keyboard.InputMethods.FirstOrDefault(profile =>
                    profile.ClassId == new Guid("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E"));
                Assert.SkipUnless(pinyin is not null, "Microsoft Pinyin is not installed.");
                var other = keyboard.InputMethods.FirstOrDefault(profile =>
                    profile.ClassId != pinyin.ClassId);
                Assert.SkipUnless(other is not null, "No second input profile is installed.");

                Assert.True(keyboard.TryActivateInputMethod(pinyin));
                Assert.True(PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == pinyin.ClassId
                        && keyboard.InputModeButton is not null,
                    2000));
                var pinyinState = SoftKeyboardTsfInputMode.GetState(pinyin.ClassId);
                Assert.NotNull(pinyinState?.ConversionValue);
                originalNative = (pinyinState.ConversionValue & 1) != 0;

                if (originalNative == true)
                {
                    Assert.True(SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(
                        pinyin,
                        pinyinState.ConversionValue!.Value & ~1
                    ));
                    Assert.True(PumpUntil(
                        () => Equals(keyboard.InputModeButton?.Content, "英"),
                        2000));
                }

                Assert.True(keyboard.TryActivateInputMethod(other));
                Assert.True(PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == other.ClassId,
                    2000));
                PumpFor(150);

                descriptor.AddValueChanged(keyboard, RecordPinyinButton);
                Assert.True(keyboard.TryActivateInputMethod(pinyin));
                Assert.True(PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == pinyin.ClassId
                        && Equals(keyboard.InputModeButton?.Content, "英"),
                    2000));

                Assert.DoesNotContain("中", publishedCaps);
            }
            finally
            {
                descriptor.RemoveValueChanged(keyboard, RecordPinyinButton);

                if (pinyin is not null
                    && originalNative == true
                    && keyboard.ActiveInputMethod?.ClassId == pinyin.ClassId
                    && SoftKeyboardTsfInputMode.GetState(pinyin.ClassId) is { } state
                    && state.ConversionValue is int conversion
                    && (conversion & 1) == 0)
                {
                    _ = SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(
                        pinyin,
                        conversion | 1
                    );
                    PumpFor(200);
                }

                if (original is not null)
                {
                    backend.TryActivate(original);
                }

                window.Close();
            }
        });
    }

    /// <summary>
    /// Microsoft Japanese IME without a standard language-bar command derives both its key cap
    /// and transition from the decoded conversion state rather than the generic open-state label.
    /// </summary>
    [Fact]
    public void Japanese_ModeKeyFollowsAndChangesConversionState()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IME_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IME_TESTS=1 to exercise installed Microsoft Japanese IME.");
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new TextBox();
            var keyboard = new SoftKeyboard
            {
                Target = editor,
                IsOpen = true,
                AutoHideMode = SoftKeyboardAutoHideMode.Manual,
                ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default,
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, Width = 850, Height = 450 };
            var backend = new TsfInputMethodService();
            SoftKeyboardInputMethod? original = null;
            SoftKeyboardInputMethod? japanese = null;
            int? originalOpen = null;
            int? originalConversion = null;

            try
            {
                window.Show();
                ActivateTestWindow(window);
                editor.Focus();
                PumpFor(400);
                keyboard.RefreshInputMethods();
                original = keyboard.ActiveInputMethod;
                japanese = keyboard.InputMethods.FirstOrDefault(profile =>
                    profile.ClassId != Guid.Empty && (profile.LanguageId & 0x03FF) == 0x11);
                Assert.SkipUnless(japanese is not null, "Microsoft Japanese IME is not installed.");
                Assert.True(keyboard.TryActivateInputMethod(japanese));
                Assert.True(PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == japanese.ClassId,
                    2000));

                var originalState = SoftKeyboardTsfInputMode.GetState(japanese.ClassId);
                Assert.NotNull(originalState?.ConversionValue);
                Assert.SkipUnless(originalState!.CanSwitch == false,
                    "The installed Japanese IME publishes a standard input-mode command.");
                originalOpen = originalState.OpenValue;
                originalConversion = originalState.ConversionValue;

                if (originalOpen == 0)
                {
                    Assert.True(SoftKeyboardTsfInputMode.TryToggleKeyboardOpen(japanese));
                    Assert.True(PumpUntil(
                        () => SoftKeyboardTsfInputMode.GetKeyboardOpenValue() == 1,
                        2000));
                }

                Assert.True(SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(japanese, 0));
                var observedDirectMode = PumpUntil(
                    () => keyboard.InputMode?.Id == "half-width-alphanumeric"
                        && Equals(keyboard.InputModeButton?.Content, "A"),
                    2000);
                var directState = SoftKeyboardTsfInputMode.GetState(japanese.ClassId);
                output.WriteLine(
                    $"Japanese direct: language={japanese.LanguageId:X4}, "
                    + $"culture={InputLanguageManager.Current.CurrentInputLanguage.Name}, "
                    + $"open={directState?.OpenValue}, conversion={directState?.ConversionValue}, "
                    + $"canSwitch={directState?.CanSwitch}, mode={keyboard.InputMode?.Id}, "
                    + $"button={keyboard.InputModeButton?.Content}, "
                    + $"error={keyboard.ExtensionError}"
                );
                Assert.True(observedDirectMode);

                var modeKey = FindVisualDescendants<SoftKeyboardKeyControl>(keyboard).Single(
                    cap => cap.Key?.Kind == SoftKeyboardKeyKind.SwitchInputMode
                );
                modeKey.Invoke();

                Assert.True(PumpUntil(
                    () => SoftKeyboardTsfInputMode.GetState(japanese.ClassId) is { } state
                        && state.ConversionValue is int conversion
                        && (conversion & 1) != 0
                        && keyboard.InputMode?.Id == "hiragana"
                        && Equals(keyboard.InputModeButton?.Content, "あ"),
                    2000));
            }
            finally
            {
                if (japanese is not null && originalConversion is int conversion)
                {
                    _ = SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(
                        japanese,
                        conversion
                    );

                    var currentOpen = SoftKeyboardTsfInputMode.GetKeyboardOpenValue();
                    if (originalOpen is int expectedOpen
                        && currentOpen is int actualOpen
                        && (actualOpen != 0) != (expectedOpen != 0))
                    {
                        _ = SoftKeyboardTsfInputMode.TryToggleKeyboardOpen(japanese);
                    }

                    PumpFor(200);
                }

                if (original is not null)
                {
                    backend.TryActivate(original);
                }

                window.Close();
            }
        });
    }

    /// <summary>
    /// Checks iFlytek profile selection and repeated composition through the core platform button.
    /// </summary>
    [Fact]
    public void Iflytek_CoreButtonSupportsProfileMenuSelectionAndChineseInput()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IFLYTEK_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IFLYTEK_TESTS=1 to exercise installed iFlytek in a foreground window.");
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new TextBox { Height = 70, TextWrapping = TextWrapping.Wrap };
            var keyboard = new SoftKeyboard
            {
                IsOpen = true, Target = editor,
                AutoHideMode = SoftKeyboardAutoHideMode.Manual,
                ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default
            };
            var root = new DockPanel();
            DockPanel.SetDock(editor, Dock.Top);
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, Width = 850, Height = 450 };
            var backend = new TsfInputMethodService();
            SoftKeyboardInputMethod? original = null;
            try
            {
                window.Show();
                ActivateTestWindow(window);
                editor.Focus();
                PumpFor(400);
                keyboard.RefreshInputMethods();
                original = keyboard.ActiveInputMethod;
                var iflytek = keyboard.InputMethods.FirstOrDefault(p =>
                    p.ClassId == IflytekServiceId);
                Assert.SkipUnless(iflytek is not null, "iFlytek is not installed.");
                var other = keyboard.InputMethods.First(p => p.ClassId != iflytek.ClassId);
                Assert.True(keyboard.TryActivateInputMethod(other));
                PumpFor(400);
                SoftKeyboardInputMethod? otherActive = keyboard.ActiveInputMethod;
                if (otherActive is not null)
                {
                    _ = EnsureNativeInput(backend, otherActive);
                }

                keyboard.ShowInputMethodMenu();
                PumpFor(200);
                var menu = (ContextMenu)typeof(SoftKeyboard).GetField(
                    "_inputMethodMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(keyboard)!;
                var item = menu.Items.Cast<MenuItem>().Single(i =>
                    ((SoftKeyboardInputMethod)i.CommandParameter).ClassId == iflytek.ClassId);
                item.Focus();
                PumpFor(100);
                SoftKeyboardCommands.ActivateInputMethod.Execute(item.CommandParameter, keyboard);
                menu.IsOpen = false;
                PumpFor(600);
                output.WriteLine(
                    $"Menu activation: {keyboard.InputMode}, raw={SoftKeyboardTsfInputMode.GetKeyboardOpenValue()}"
                );
                Assert.Equal(iflytek.ClassId, keyboard.ActiveInputMethod?.ClassId);
                // The TIP can remember English mode across activations. Verify the reported
                // mode against actual input before preparing Chinese for the sustained run.
                Assert.NotNull(keyboard.InputModeButton);
                foreach (var key in new[] { Key.N, Key.I, Key.H, Key.A, Key.O, Key.Space })
                {
                    Assert.True(keyboard.SendActionKey(key, ModifierKeys.None));
                    PumpFor(80);
                }

                Assert.True(PumpUntil(() => editor.Text.Trim() is "你好" or "nihao", 2000),
                    $"Unexpected iFlytek input '{editor.Text}'.");
                var initiallyNative = editor.Text.Trim() == "你好";
                editor.Clear();
                if (!initiallyNative)
                {
                    Assert.True(SoftKeyboardTsfInputMode.TrySwitchInputMode(iflytek.ClassId));
                    PumpFor(500);
                }

                var expected = string.Empty;
                for (var cycle = 0; cycle < 60; cycle++)
                {
                    foreach (var key in new[] { Key.N, Key.I, Key.H, Key.A, Key.O })
                    {
                        Assert.True(keyboard.SendActionKey(key, ModifierKeys.None));
                        PumpFor(60);
                    }

                    GC.Collect();
                    PumpFor(150);
                    Assert.Equal(SoftKeyboardCandidatePresentationState.NativeFallback,
                        keyboard.CandidatePresentationState);
                    Assert.True(keyboard.SendActionKey(Key.Space, ModifierKeys.None));
                    expected += "你好";
                    Assert.True(PumpUntil(() => editor.Text == expected, 2000),
                        $"Cycle {cycle}: expected '{expected}', actual '{editor.Text}'.");
                    GC.Collect();
                    PumpFor(300);
                    Assert.NotNull(keyboard.InputModeButton);
                }
            }
            finally
            {
                if (window.IsVisible)
                {
                    keyboard.SendActionKey(Key.Escape, ModifierKeys.None);
                    PumpFor(100);
                }

                if (original is not null)
                {
                    backend.TryActivate(original);
                }

                window.Close();
            }
        });
    }

    /// <summary>
    /// Checks iFlytek platform-button changes and COM cleanup without a vendor state adapter.
    /// </summary>
    [Fact]
    public void Iflytek_CoreButtonSurvivesGarbageCollection()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IFLYTEK_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IFLYTEK_TESTS=1 to exercise installed iFlytek in a foreground window."
        );
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new TextBox { Height = 40 };
            var keyboard = new SoftKeyboard
            {
                IsOpen = true,
                Target = editor,
                AutoHideMode = SoftKeyboardAutoHideMode.Manual,
                ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default
            };
            var root = new DockPanel();
            DockPanel.SetDock(editor, Dock.Top);
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, Width = 850, Height = 420 };
            var backend = new TsfInputMethodService();
            SoftKeyboardInputMethod? original = null;
            try
            {
                window.Show();
                ActivateTestWindow(window);
                editor.Focus();
                PumpFor(400);
                keyboard.RefreshInputMethods();
                original = keyboard.ActiveInputMethod;
                var iflytek = keyboard.InputMethods.FirstOrDefault(p =>
                    p.ClassId == IflytekServiceId);
                output.WriteLine(string.Join("; ", keyboard.InputMethods.Select(p => $"{p.DisplayName}: {p.ClassId}")));
                output.WriteLine("Observation error: " + keyboard.InputMethodError);
                Assert.SkipUnless(iflytek is not null, "iFlytek is not installed.");
                Assert.True(backend.TryActivate(iflytek));
                PumpFor(400);
                Assert.True(PumpUntil(() => keyboard.InputModeButton is not null, 2000));

                for (var cycle = 0; cycle < 3; cycle++)
                {
                    var previousButton = keyboard.InputModeButton!.Id;
                    typeof(SoftKeyboard).GetMethod(
                        "SwitchInputModeCore",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )!
                        .Invoke(keyboard, null);
                    Assert.True(PumpUntil(
                        () => keyboard.InputModeButton is { } button
                            && button.Id != previousButton,
                        2000));
                    GC.Collect();
                    PumpFor(250);
                }
            }
            finally
            {
                if (window.IsVisible)
                {
                    keyboard.SendActionKey(Key.Escape, ModifierKeys.None);
                    PumpFor(100);
                }

                if (original is not null)
                {
                    backend.TryActivate(original);
                }

                window.Close();
            }
        });
    }

    [Fact]
    public void Weasel_CoreNegotiationPreservesNativePreview()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IME_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IME_TESTS=1 to run foreground tests with an installed Weasel IME."
        );
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new TextBox
            {
                Height = 40,
                FontSize = 24
            };
            var keyboard = new SoftKeyboard
            {
                IsOpen = true,
                Target = editor,
                AutoHideMode = SoftKeyboardAutoHideMode.Manual,
                ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default
            };
            var root = new DockPanel();
            DockPanel.SetDock(editor, Dock.Top);
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window
            {
                Title = "SoftKeyboard native IME verification",
                Content = root,
                Width = 850,
                Height = 420
            };
            var backend = new TsfInputMethodService();
            SoftKeyboardInputMethod? original = null;
            SoftKeyboardInputMode? originalMode = null;
            PreProcessInputEventHandler trace = (_, args) =>
            {
                if (args.StagingItem.Input is KeyboardFocusChangedEventArgs focus &&
                    focus.RoutedEvent == Keyboard.GotKeyboardFocusEvent)
                {
                    output.WriteLine($"Focus: {focus.OldFocus?.GetType().Name} -> {focus.NewFocus?.GetType().Name}");
                }

                if (args.StagingItem.Input is TextCompositionEventArgs composition)
                {
                    output.WriteLine(
                        $"Text: {composition.RoutedEvent.Name}: '{composition.Text}', composing='{composition.TextComposition.CompositionText}'");
                }
            };
            InputManager.Current.PreProcessInput += trace;
            try
            {
                window.Show();
                ActivateTestWindow(window);
                editor.Focus();
                window.UpdateLayout();
                PumpFor(400);
                PumpUntil(() => keyboard.ActiveInputMethod is not null, 1500);
                original = keyboard.ActiveInputMethod;
                originalMode = original is null ? null : backend.GetInputMode(original);
                output.WriteLine(string.Join(
                    Environment.NewLine,
                    keyboard.InputMethods.Select(p => $"{p.DisplayName}: {p.ClassId} / {p.LanguageId:X4}")));

                var weasel = keyboard.InputMethods.FirstOrDefault(p => p.DisplayName.Contains("小狼毫")
                    || p.DisplayName.Contains("Weasel", StringComparison.OrdinalIgnoreCase));
                Assert.SkipUnless(weasel is not null, "Weasel is not installed.");
                Assert.True(backend.TryActivate(weasel));
                PumpFor(500);
                Assert.True(EnsureNativeInput(backend, weasel));
                keyboard.OnSessionInputLanguageChanged();
                PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == weasel.ClassId
                        && keyboard.InputMode?.IsNativeInput == true,
                    2000
                );
                editor.SelectAll();
                Assert.True(keyboard.SendActionKey(Key.Back, ModifierKeys.None));
                PumpFor(80);
                foreach (var key in new[]
                    {
                        Key.N, Key.I, Key.H, Key.A, Key.O
                    })
                {
                    Assert.True(keyboard.SendActionKey(key, ModifierKeys.None));
                    PumpFor(100);
                }

                Assert.False(keyboard.HasCandidates);
                Assert.Equal(
                    SoftKeyboardCandidatePresentationState.NativeFallback, keyboard.CandidatePresentationState);
                Assert.True(IsNativeCandidateShown(), "Weasel must retain its own candidate/reading surface.");
                Assert.True(keyboard.SendActionKey(Key.Space, ModifierKeys.None));
                Assert.True(PumpUntil(() => !string.IsNullOrWhiteSpace(editor.Text), 1500));
            }
            finally
            {
                if (window.IsVisible)
                {
                    keyboard.SendActionKey(Key.Escape, ModifierKeys.None);
                    PumpUntil(() => !keyboard.HasCandidates, 500);
                }

                if (original is not null)
                {
                    backend.TryActivate(original);
                    if (originalMode is not null)
                    {
                        RestoreInputMode(backend, original, originalMode);
                    }
                }

                window.Close();
                InputManager.Current.PreProcessInput -= trace;
            }
        });
    }

    private static bool EnsureNativeInput(
        TsfInputMethodService backend,
        SoftKeyboardInputMethod inputMethod)
    {
        var mode = backend.GetInputMode(inputMethod);
        return mode is not null
            && (mode.IsNativeInput || backend.TrySwitchInputMode(inputMethod, mode));
    }

    private static bool EnsureDirectInput(
        TsfInputMethodService backend,
        SoftKeyboardInputMethod inputMethod)
    {
        var mode = backend.GetInputMode(inputMethod);
        return mode is not null
            && (!mode.IsNativeInput || backend.TrySwitchInputMode(inputMethod, mode));
    }

    private static void RestoreInputMode(
        TsfInputMethodService backend,
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardInputMode expected)
    {
        var current = backend.GetInputMode(inputMethod);
        if (current is not null && current.IsNativeInput != expected.IsNativeInput)
        {
            _ = backend.TrySwitchInputMode(inputMethod, current);
        }
    }

    private static bool IsNativeCandidateShown()
    {
        var manager = WpfTextServicesAccessor.TryGetThreadManager() as ITfUIElementMgr;
        if (manager is null || manager.EnumUIElements(out var value) < 0)
        {
            return false;
        }

        try
        {
            var enumerator = (IEnumTfUIElements)value!;
            while (enumerator.Next(1, out var element, out var count) >= 0 && count == 1)
            {
                try
                {
                    if (element is ITfCandidateListUIElementBehavior candidate
                        && candidate.GetGuid(out var guid) >= 0 &&
                        guid == new Guid("36C3C795-7159-45AA-AB12-30229A51DBD3")
                        && candidate.IsShown(out var shown) >= 0 && shown != 0)
                    {
                        return true;
                    }
                }
                finally
                {
                    if (element is not null)
                    {
                        Marshal.ReleaseComObject(element);
                    }
                }
            }

            return false;
        }
        finally
        {
            if (value is not null)
            {
                Marshal.ReleaseComObject(value);
            }
        }
    }

    private static void ActivateTestWindow(Window window)
    {
        // Opt-in desktop tests need a real foreground host before exercising SendInput guards.
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), IntPtr.Zero);
        var currentThread = NativeMethods.GetCurrentThreadId();
        var attached = foregroundThread != currentThread &&
            NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            Assert.True(window.Activate(), "The native IME test window could not become foreground.");
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr process);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AttachThreadInput(
            uint from, uint to, [MarshalAs(UnmanagedType.Bool)] bool attach);
    }

    private static bool PumpUntil(Func<bool> predicate, int timeout)
    {
        var elapsed = Stopwatch.StartNew();
        while (!predicate() && elapsed.ElapsedMilliseconds < timeout)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(20)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                frame.Continue = false;
            };

            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        return predicate();
    }

    private static void PumpFor(int milliseconds)
    {
        var elapsed = Stopwatch.StartNew();
        PumpUntil(() => elapsed.ElapsedMilliseconds >= milliseconds, milliseconds + 100);
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
