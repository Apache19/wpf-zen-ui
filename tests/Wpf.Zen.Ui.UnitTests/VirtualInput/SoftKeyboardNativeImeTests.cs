// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Interop;
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
    /// <summary>
    /// Verifies that installed WeChat Input keeps its native candidate window for consecutive
    /// compositions and commits through its own selection handling.
    /// </summary>
    [Fact]
    public void WeChat_NativeCandidatesOwnSelectionAndReset()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_WECHAT_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_WECHAT_TESTS=1 to exercise installed WeChat Input in the foreground.");
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
            SoftKeyboardInputMethod? wechat = null;
            SoftKeyboardInputObservation? originalMode = null;
            var preedit = string.Empty;
            TextCompositionManager.AddPreviewTextInputUpdateHandler(editor, (_, args) =>
            {
                preedit = args.TextComposition.CompositionText;
            });
            try
            {
                window.Show();
                ActivateTestWindow(window);
                Assert.True(editor.Focus());
                PumpFor(400);
                keyboard.RefreshInputMethods();
                original = keyboard.ActiveInputMethod;
                wechat = keyboard.InputMethods.FirstOrDefault(p => p.ClassId == KnownTextServices.WeChat);
                Assert.SkipUnless(wechat is not null, "WeChat Input is not installed.");
                Assert.True(keyboard.TryActivateInputMethod(wechat));
                Assert.True(PumpUntil(() => keyboard.ActiveInputMethod?.ClassId == wechat.ClassId, 2000));
                originalMode = ReadNativeState(wechat.ClassId);
                Assert.True(EnsureNativeInput(backend, wechat));
                Assert.True(PumpUntil(
                    () => IsKnownNative(keyboard.ActiveInputMethod, ReadNativeState(wechat.ClassId)) == true,
                    2000));

                Assert.True(keyboard.SendActionKey(Key.H, ModifierKeys.None));
                Assert.True(keyboard.SendActionKey(Key.F, ModifierKeys.None));
                Assert.True(PumpUntil(() => IsNativeCandidateShown(null), 3000),
                    "WeChat must keep its native candidate window visible.");
                Assert.Equal(SoftKeyboardCandidatePresentationState.NativeFallback,
                    keyboard.CandidatePresentationState);
                Assert.Empty(keyboard.Candidates);
                Assert.True(keyboard.SendActionKey(Key.Space, ModifierKeys.None));
                Assert.True(PumpUntil(() => !IsNativeCandidateShown(null), 3000));
                var first = editor.Text;
                Assert.NotEmpty(first);
                Assert.DoesNotContain("hf", first, StringComparison.OrdinalIgnoreCase);

                preedit = string.Empty;
                Assert.True(keyboard.SendActionKey(Key.C, ModifierKeys.None));
                Assert.True(keyboard.SendActionKey(Key.S, ModifierKeys.None));
                Assert.True(PumpUntil(() => IsNativeCandidateShown(null), 3000));
                Assert.Equal(SoftKeyboardCandidatePresentationState.NativeFallback,
                    keyboard.CandidatePresentationState);
                Assert.Empty(keyboard.Candidates);
                Assert.NotEmpty(preedit);
                Assert.DoesNotContain("hf", preedit.Replace("'", string.Empty), StringComparison.OrdinalIgnoreCase);
                Assert.True(keyboard.SendActionKey(Key.Space, ModifierKeys.None));
                Assert.True(PumpUntil(() => !IsNativeCandidateShown(null)
                    && editor.Text.Length > first.Length, 3000));
                Assert.StartsWith(first, editor.Text);
                Assert.DoesNotContain("cs", editor.Text, StringComparison.OrdinalIgnoreCase);

            }
            finally
            {
                if (window.IsVisible)
                {
                    keyboard.SendActionKey(Key.Escape, ModifierKeys.None);
                    PumpFor(100);
                    if (wechat is not null && originalMode is not null)
                    {
                        RestoreInputMode(backend, wechat, originalMode);
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
    /// Reads the initial Microsoft Pinyin presentation without focusing any scoped editor or
    /// issuing a profile or mode command. Run alone to exercise a fresh WPF text-services host.
    /// </summary>
    [Fact]
    public void Pinyin_InitialPresentationWithoutScopedEditorFocus()
    {
        VerifyInitialPinyinPresentation(focusOutsideEditor: false);
    }

    /// <summary>
    /// Displays Pinyin state while only an editor outside the keyboard's target scope has focus.
    /// </summary>
    [Fact]
    public void Pinyin_InitialPresentationWithOutsideEditorFocus()
    {
        VerifyInitialPinyinPresentation(focusOutsideEditor: true);
    }

    /// <summary>
    /// Hosts the cold-start scenario with either a plain button or an out-of-scope editor focused.
    /// </summary>
    private void VerifyInitialPinyinPresentation(bool focusOutsideEditor)
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IME_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IME_TESTS=1 to exercise the default Microsoft Pinyin profile.");
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new TextBox { Height = 40 };
            Control outside = focusOutsideEditor
                ? new TextBox { Text = "Outside input scope" }
                : new System.Windows.Controls.Button { Content = "Outside input scope" };
            var keyboard = new SoftKeyboard
            {
                IsOpen = true,
                TargetScope = editor,
                AutoHideMode = SoftKeyboardAutoHideMode.Manual,
                ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default,
            };
            var root = new StackPanel();
            root.Children.Add(outside);
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, Width = 850, Height = 420 };
            var editorFocused = false;
            editor.GotKeyboardFocus += (_, _) => editorFocused = true;
            try
            {
                window.Show();
                ActivateTestWindow(window);
                Assert.True(outside.Focus());
                Assert.True(PumpUntil(() => keyboard.ActiveInputMethod is not null, 2000));
                Assert.SkipUnless(keyboard.ActiveInputMethod?.ClassId == KnownTextServices.MicrosoftPinyin,
                    "Microsoft Pinyin must be the initial active profile for this read-only test.");
                Assert.True(PumpUntil(() => keyboard.InputModeButton is not null, 2000),
                    DescribeLanguageBar(keyboard)
                    + $" open={SoftKeyboardTsfInputMode.GetKeyboardOpenValue()}, "
                    + $"conversion={SoftKeyboardTsfInputMode.GetKeyboardConversionValue()}.");
                if (focusOutsideEditor)
                {
                    Assert.True(PumpUntil(() => keyboard.InputModeButton?.Id
                        is not (null or "windows:input-mode:unknown"), 2000));
                }

                Assert.Contains(keyboard.CurrentLayout.Keys,
                    key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode);
                Assert.False(editorFocused);
                Assert.Null(keyboard.ActiveTarget);
                Assert.Same(outside, Keyboard.FocusedElement);
                output.WriteLine($"Initial presentation: {keyboard.InputModeButton!.Id}");
            }
            finally
            {
                window.Close();
            }
        });
    }

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
            SoftKeyboardInputObservation? pinyinMode = null;
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
                    () => keyboard.ActiveInputMethod?.ClassId == pinyin.ClassId,
                    2000));
                pinyinMode = ReadNativeState(pinyin.ClassId);
                var state = ReadNativeState(pinyin.ClassId);
                Assert.NotNull(state?.ConversionValue);
                if ((state!.ConversionValue!.Value & 1) == 0)
                {
                    Assert.True(PumpUntil(
                        () => FindVisualDescendants<SoftKeyboardKeyControl>(keyboard).Any(
                            cap => cap.Key?.Kind == SoftKeyboardKeyKind.SwitchInputMode
                        ),
                        2000));
                    FindVisualDescendants<SoftKeyboardKeyControl>(keyboard).Single(
                        cap => cap.Key?.Kind == SoftKeyboardKeyKind.SwitchInputMode
                    ).Invoke();
                    Assert.True(PumpUntil(
                        () => (ReadNativeState(pinyin.ClassId)?.ConversionValue & 1)
                            != 0,
                        2000));
                }

                PumpFor(200);
                Assert.True(keyboard.SendActionKey(Key.N, ModifierKeys.None));
                Assert.True(keyboard.SendActionKey(Key.I, ModifierKeys.None));
                Assert.True(
                    PumpUntil(() => keyboard.HasCandidates, 2000),
                    $"presentation={keyboard.CandidatePresentationState}, "
                    + $"active={keyboard.ActiveInputMethod?.ClassId}, "
                    + $"behaviors={keyboard.GetTsfCandidateBehaviors().Count}, "
                    + $"extensionError={keyboard.ExtensionError}"
                );

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
                    && !keyboard.HasCandidates, 2000),
                    $"Expected=★{candidate.Text}, actual={editor.Text}, candidates={keyboard.HasCandidates}, "
                    + $"error={keyboard.CandidateError}");
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
    /// Exercises Pinyin's verified native/direct icon transition when this build exposes a button.
    /// The Dispatcher wait allows notification delivery and the single 30ms icon recheck.
    /// </summary>
    [Fact]
    public void Pinyin_LanguageBarModeKeyRefreshesIconAfterClick()
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
                    () => keyboard.ActiveInputMethod?.ClassId == pinyin.ClassId,
                    2000), DescribeLanguageBar(keyboard));
                var nativeButton = SoftKeyboardTsfInputMode.GetInputModeButton(pinyin.ClassId);
                Assert.SkipUnless(nativeButton?.Content is System.Windows.Media.Imaging.BitmapSource,
                    "This Pinyin build does not expose a language-bar icon. " + DescribeLanguageBar(keyboard));
                Assert.True(PumpUntil(
                    () => keyboard.InputModeButton is not null
                        && FindVisualDescendants<SoftKeyboardKeyControl>(keyboard).Any(
                            cap => cap.Key?.Kind == SoftKeyboardKeyKind.SwitchInputMode),
                    2000), DescribeLanguageBar(keyboard));

                var originalState = ReadNativeState(pinyin.ClassId);
                Assert.NotNull(originalState);
                Assert.True(originalState.OpenValue is 0 or 1);
                Assert.NotNull(originalState.ConversionValue);
                originalNative = (originalState.ConversionValue & 1) != 0;
                output.WriteLine($"Windows={Environment.OSVersion}; profile={pinyin.DisplayName}");
                var imeFile = System.IO.Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.Windows), "System32", "InputMethod", "CHS", "ChsIME.exe");
                if (System.IO.File.Exists(imeFile))
                {
                    output.WriteLine($"IME version={FileVersionInfo.GetVersionInfo(imeFile).FileVersion}");
                }
                var originalButtonId = keyboard.InputModeButton!.Id;
                var modeKey = FindVisualDescendants<SoftKeyboardKeyControl>(keyboard).Single(
                    cap => cap.Key?.Kind == SoftKeyboardKeyKind.SwitchInputMode
                );

                modeKey.Invoke();

                Assert.True(PumpUntil(
                    () => ReadNativeState(pinyin.ClassId) is { } state
                        && ((state.ConversionValue & 1) != 0) != originalNative
                        && keyboard.InputModeButton is { } button
                        && button.Id != originalButtonId,
                    2000));

                var currentState = ReadNativeState(pinyin.ClassId)!;
                var currentNative = (currentState.ConversionValue & 1) != 0;
                Assert.IsAssignableFrom<System.Windows.Media.Imaging.BitmapSource>(
                    keyboard.InputModeButton!.Content);
                Assert.Same(keyboard.InputModeButton.Content,
                    FindVisualDescendants<SoftKeyboardKeyControl>(keyboard).Single(
                        cap => cap.Key?.Kind == SoftKeyboardKeyKind.SwitchInputMode).Key!.Content);

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
                    && ReadNativeState(pinyin.ClassId) is { } state
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
    /// Returning to Microsoft Pinyin publishes its available language-bar presentation.
    /// </summary>
    [Fact]
    public void Pinyin_ProfileActivationPublishesPlatformPresentation()
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
                pinyin = keyboard.InputMethods.FirstOrDefault(profile =>
                    profile.ClassId == new Guid("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E"));
                Assert.SkipUnless(pinyin is not null, "Microsoft Pinyin is not installed.");
                var other = keyboard.InputMethods.FirstOrDefault(profile =>
                    profile.ClassId != pinyin.ClassId);
                Assert.SkipUnless(other is not null, "No second input profile is installed.");

                Assert.True(keyboard.TryActivateInputMethod(pinyin));
                Assert.True(PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == pinyin.ClassId,
                    2000));
                Assert.SkipUnless(SoftKeyboardTsfInputMode.GetInputModeButton(pinyin.ClassId) is not null,
                    "This Pinyin build does not expose a usable language-bar button.");
                var pinyinState = ReadNativeState(pinyin.ClassId);
                Assert.NotNull(pinyinState?.ConversionValue);
                originalNative = (pinyinState.ConversionValue & 1) != 0;

                if (originalNative == true)
                {
                    Assert.True(SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(
                        pinyin,
                        pinyinState.ConversionValue!.Value & ~1
                    ));
                    Assert.True(PumpUntil(
                        () => ReadNativeState(pinyin.ClassId).ConversionValue == 0,
                        2000));
                }

                Assert.True(keyboard.TryActivateInputMethod(other));
                Assert.True(PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == other.ClassId,
                    2000));
                PumpFor(150);

                Assert.True(keyboard.TryActivateInputMethod(pinyin));
                Assert.True(PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == pinyin.ClassId
                        && keyboard.InputModeButton is { } button
                        && button.Equals(SoftKeyboardTsfInputMode.GetInputModeButton(pinyin.ClassId)),
                    2000));
            }
            finally
            {
                if (pinyin is not null
                    && originalNative == true
                    && keyboard.ActiveInputMethod?.ClassId == pinyin.ClassId
                    && ReadNativeState(pinyin.ClassId) is { } state
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
                    $"Menu activation: {keyboard.InputModeButton}, raw={SoftKeyboardTsfInputMode.GetKeyboardOpenValue()}"
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
                    Assert.True(SoftKeyboardTsfInputMode.TryInvokeInputModeButton(iflytek, new Rect()) == SoftKeyboardInputModeCommandResult.Accepted);
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
                        "SwitchInputMode",
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
            SoftKeyboardInputObservation? originalMode = null;
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
                originalMode = original is null ? null : ReadNativeState(original.ClassId);
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
                        && IsKnownNative(keyboard.ActiveInputMethod, ReadNativeState(Guid.Empty)) == true,
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
                Assert.True(IsNativeCandidateShown(new Guid("36C3C795-7159-45AA-AB12-30229A51DBD3")), "Weasel must retain its own candidate/reading surface.");
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

    // Explicit diagnostic reads are used only to prepare/restore known native test scenarios.
    /// <summary>
    /// Captures provider identity and button flags when a real-IME assertion times out.
    /// </summary>
    private static string DescribeLanguageBar(SoftKeyboard keyboard)
    {
        var description = $"profile={keyboard.ActiveInputMethod?.ClassId}, "
            + $"button={keyboard.InputModeButton?.Id}, error={keyboard.InputMethodError}, "
            + $"focused={keyboard.ActiveTarget == Keyboard.FocusedElement}.";
        if (WpfTextServicesAccessor.TryGetThreadManager() is not ITfLangBarItemMgr manager)
        {
            return description + " No language-bar manager.";
        }

        var itemId = new Guid("2C77A81E-41CC-4178-A3A7-5F8A987568E6");
        var result = manager.GetItem(in itemId, out var item);
        try
        {
            if (item is ITfLangBarItemButton button)
            {
                var infoResult = button.GetInfo(out var info);
                var statusResult = button.GetStatus(out var status);
                return description + $" itemResult=0x{result:X8}, infoResult=0x{infoResult:X8}, "
                    + $"service={info.ServiceId}, item={info.ItemId}, style=0x{info.Style:X8}, "
                    + $"statusResult=0x{statusResult:X8}, status=0x{status:X8}.";
            }

            return description + $" itemResult=0x{result:X8}, hasItem={item is not null}.";
        }
        finally
        {
            if (item is not null && Marshal.IsComObject(item))
            {
                Marshal.ReleaseComObject(item);
            }
        }
    }

    private static SoftKeyboardInputObservation ReadNativeState(Guid serviceId)
    {
        return new SoftKeyboardInputObservation(true, SoftKeyboardTsfInputMode.GetKeyboardOpenValue(),
            true, SoftKeyboardTsfInputMode.GetKeyboardConversionValue());
    }

    private static bool? IsKnownNative(SoftKeyboardInputMethod? profile, SoftKeyboardInputObservation state)
    {
        if (profile?.ClassId == KnownTextServices.WeChat && state.ConversionValue == 0)
        {
            return state.OpenValue is 0 or 1 ? state.OpenValue == 1 : null;
        }

        if (profile?.ClassId == new Guid("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E"))
        {
            return state.OpenValue == 1 && state.ConversionValue is { } conversion
                ? conversion == 1025 : null;
        }
        if (profile?.ClassId == new Guid("03B5835F-F03C-411B-9CE2-AA23E1171E36"))
        {
            return state.OpenValue is { } open ? open == 1 : null;
        }
        return null;
    }

    private static bool EnsureNativeInput(
        TsfInputMethodService backend,
        SoftKeyboardInputMethod inputMethod)
    {
        var mode = ReadNativeState(inputMethod.ClassId);
        return mode is not null
            && (IsKnownNative(inputMethod, mode) == true || SoftKeyboardTsfInputMode.TryInvokeInputModeButton(inputMethod, new Rect()) == SoftKeyboardInputModeCommandResult.Accepted);
    }

    /// <summary>
    /// Switching from a Chinese TIP publishes the state read from the newly active Japanese
    /// profile. Subsequent mode clicks select Hiragana from retained modes and preserve
    /// conversion on close, including Roman-key input.
    /// </summary>
    [Fact]
    public void Japanese_ProfileActivationPublishesObservedGenerationState()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IME_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IME_TESTS=1 to exercise installed Chinese and Japanese IMEs.");
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
            var stopwatch = Stopwatch.StartNew();
            var descriptors = new[]
            {
                DependencyPropertyDescriptor.FromProperty(
                    SoftKeyboard.ActiveInputMethodProperty,
                    typeof(SoftKeyboard)
                ),
                DependencyPropertyDescriptor.FromProperty(
                    SoftKeyboard.InputModeButtonProperty,
                    typeof(SoftKeyboard)
                ),
            };

            void TraceState(object? sender, EventArgs args)
            {
                var raw = ReadNativeState(
                    keyboard.ActiveInputMethod?.ClassId ?? Guid.Empty
                );
                output.WriteLine(
                    $"{stopwatch.ElapsedMilliseconds,4} ms: "
                    + $"profile={keyboard.ActiveInputMethod?.DisplayName}, "
                    + $"buttonId={keyboard.InputModeButton?.Id}, content={keyboard.InputModeButton?.Content}, "
                    + $"open={raw?.OpenValue}, conversion={raw?.ConversionValue}, "
                    + $"wpf={InputMethod.Current.ImeConversionMode}"
                );
            }

            try
            {
                window.Show();
                ActivateTestWindow(window);
                editor.Focus();
                PumpFor(400);
                keyboard.RefreshInputMethods();
                original = keyboard.ActiveInputMethod;

                var japanese = keyboard.InputMethods.FirstOrDefault(profile =>
                    profile.ClassId != Guid.Empty && (profile.LanguageId & 0x03FF) == 0x11);
                var chinese = keyboard.ActiveInputMethod is { } active
                    && active.ClassId != Guid.Empty
                    && (active.LanguageId & 0x03FF) == 0x04
                        ? active
                        : keyboard.InputMethods.FirstOrDefault(profile =>
                            profile.ClassId != Guid.Empty
                            && (profile.LanguageId & 0x03FF) == 0x04);
                Assert.SkipUnless(chinese is not null && japanese is not null,
                    "A Chinese TIP or Microsoft Japanese IME is not installed.");

                foreach (var descriptor in descriptors)
                {
                    descriptor.AddValueChanged(keyboard, TraceState);
                }

                if (keyboard.ActiveInputMethod?.ProfileId != chinese.ProfileId)
                {
                    Assert.True(keyboard.TryActivateInputMethod(chinese));
                    Assert.True(PumpUntil(
                        () => keyboard.ActiveInputMethod?.ProfileId == chinese.ProfileId,
                        2000));
                }

                var chineseState = ReadNativeState(chinese.ClassId);
                var chineseConversion = chineseState?.ConversionValue;
                Assert.SkipUnless(chineseConversion is not null,
                    "The active Chinese TIP does not expose conversion state.");
                if ((chineseConversion.GetValueOrDefault() & 1) == 0)
                {
                    Assert.True(SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(
                        chinese,
                        chineseConversion.GetValueOrDefault() | 1
                    ));
                    Assert.True(PumpUntil(
                        () => SoftKeyboardTsfInputMode.GetKeyboardConversionValue()
                            is int currentConversion
                            && (currentConversion & 1) != 0,
                        2000));
                }

                japanese = keyboard.InputMethods.First(profile =>
                    profile.ClassId == japanese.ClassId && profile.ProfileId == japanese.ProfileId);
                stopwatch.Restart();
                Assert.True(keyboard.TryActivateInputMethod(japanese));
                Assert.True(PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == japanese.ClassId,
                    2000));
                PumpFor(1500);
                TraceState(null, EventArgs.Empty);

                var japaneseState = Assert.IsType<SoftKeyboardInputObservation>(
                    ReadNativeState(japanese.ClassId)
                );
                Assert.NotNull(japaneseState.ConversionValue);
                Assert.NotNull(keyboard.InputModeButton);

                try
                {
                    foreach (var retainedConversion in new[] { 0x19, 0x0B, 0x08, 0x03 })
                    {
                        Assert.True(SoftKeyboardTsfInputMode.TrySetKeyboardOpenValue(japanese, 1));
                        Assert.True(SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(
                            japanese, retainedConversion));
                        PumpFor(150);
                        Assert.True(SoftKeyboardTsfInputMode.TrySetKeyboardOpenValue(japanese, 0));
                        Assert.True(PumpUntil(
                            () => keyboard.InputModeButton?.Id.EndsWith(":direct") == true, 2000));

                        typeof(SoftKeyboard).GetMethod("SwitchInputMode",
                            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(keyboard, null);
                        Assert.True(PumpUntil(
                            () => Equals(keyboard.InputModeButton?.Content, "あ"), 2000));
                        PumpFor(150);
                        Assert.Equal("あ", keyboard.InputModeButton?.Content);
                        var enabledConversion = SoftKeyboardTsfInputMode.GetKeyboardConversionValue();
                        Assert.NotNull(enabledConversion);
                        Assert.Equal(0x09, enabledConversion.Value & ~0x10);
                        output.WriteLine($"Retained conversion={retainedConversion:X2}; "
                            + $"enabled conversion={enabledConversion:X2}, button=あ.");

                        typeof(SoftKeyboard).GetMethod("SwitchInputMode",
                            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(keyboard, null);
                        Assert.True(PumpUntil(
                            () => keyboard.InputModeButton?.Id.EndsWith(":direct") == true, 2000));
                        Assert.Equal(enabledConversion,
                            SoftKeyboardTsfInputMode.GetKeyboardConversionValue());
                    }
                }
                finally
                {
                    RestoreInputMode(backend, japanese, japaneseState);
                    PumpFor(150);
                }

                editor.Clear();
                Assert.True(keyboard.SendActionKey(Key.A, ModifierKeys.None));
                if (IsKnownNative(keyboard.ActiveInputMethod, ReadNativeState(Guid.Empty)) == true)
                {
                    Assert.True(PumpUntil(
                        () => editor.Text.Any(IsJapaneseCharacter),
                        2000
                    ));
                }
                else
                {
                    Assert.True(PumpUntil(
                        () => editor.Text.Any(character => character is 'a' or 'A'),
                        2000
                    ));
                    Assert.DoesNotContain(editor.Text, IsJapaneseCharacter);
                }
            }
            finally
            {
                foreach (var descriptor in descriptors)
                {
                    descriptor.RemoveValueChanged(keyboard, TraceState);
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
    /// Exercises the actual Japanese surface through SendInput while an IME owns composition.
    /// Uses the installed input style, checking digits, OEM marks and function conversions.
    /// </summary>
    [Fact]
    public void Japanese_PhysicalSurfacePreservesRomajiAndKanaComposition()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IME_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IME_TESTS=1 to exercise the installed Microsoft Japanese IME.");
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
            var window = new Window { Content = root, Width = 1000, Height = 550 };
            var backend = new TsfInputMethodService();
            SoftKeyboardInputMethod? original = null;
            SoftKeyboardInputMethod? japanese = null;
            SoftKeyboardInputObservation? originalMode = null;
            var invoke = typeof(SoftKeyboard).GetMethod("InvokeKey",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            // Invoke the published caps rather than bypassing layout selection or Shift handling.
            void Press(Key key)
            {
                var cap = Assert.Single(keyboard.CurrentLayout.Keys,
                    cap => cap.Kind == SoftKeyboardKeyKind.VirtualKey && cap.Key == key);
                invoke.Invoke(keyboard, [cap]);
                PumpFor(100);
            }

            void Shift()
            {
                invoke.Invoke(keyboard, [Assert.Single(keyboard.CurrentLayout.Keys,
                    cap => cap.Kind == SoftKeyboardKeyKind.Shift)]);
                PumpFor(100);
            }

            void Kana(string label)
            {
                var cap = keyboard.CurrentLayout.Keys.FirstOrDefault(cap => cap.AlternateHint == label);
                if (cap is null)
                {
                    Shift();
                    cap = keyboard.CurrentLayout.Keys.First(cap => cap.AlternateHint == label);
                }

                Press(cap.Key);
            }

            try
            {
                window.Show();
                ActivateTestWindow(window);
                Assert.True(editor.Focus());
                PumpFor(400);
                keyboard.RefreshInputMethods();
                original = keyboard.ActiveInputMethod;
                japanese = keyboard.InputMethods.FirstOrDefault(
                    profile => profile.ClassId == KnownTextServices.MicrosoftJapanese);
                Assert.SkipUnless(japanese is not null, "Microsoft Japanese IME is not installed.");
                Assert.True(keyboard.TryActivateInputMethod(japanese));
                Assert.True(PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == japanese.ClassId, 2000));
                PumpFor(500);
                originalMode = ReadNativeState(japanese.ClassId);
                Assert.True(SoftKeyboardTsfInputMode.TrySetKeyboardOpenValue(japanese, 1));
                Assert.True(SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(japanese, 0x19));
                Assert.True(PumpUntil(() => Equals(keyboard.InputModeButton?.Content, "あ")
                    && keyboard.CurrentLayout.Keys.Any(cap => cap.Key == Key.D3 && Equals(cap.Content, "3")),
                    2000));

                // Probe the installed preference: accepted ROMAN compartment writes do not
                // necessarily change the modern IME's configured Romaji/Kana input style.
                Press(Key.A);
                Press(Key.F6);
                Press(Key.Enter);
                Assert.True(PumpUntil(() => editor.Text is "あ" or "ち", 2000), editor.Text);
                var roman = editor.Text == "あ";
                editor.Clear();

                if (roman)
                {
                    foreach (var key in new[] { Key.S, Key.U, Key.P, Key.A, Key.OemMinus })
                    {
                        Press(key);
                    }

                    Press(Key.F7);
                    Press(Key.Enter);
                    Assert.True(PumpUntil(() => editor.Text == "スーパー", 2000), editor.Text);
                    output.WriteLine($"Romaji composition committed: {editor.Text}");
                }
                else
                {
                    Press(Key.D1);
                    Kana("ろ");
                    Kana("か");
                    var voiced = keyboard.CurrentLayout.Keys.First(cap => cap.AlternateHint is "゙" or "゛");
                    Press(voiced.Key);
                    Kana("ぁ");
                    Assert.Equal(SoftKeyboardShiftState.Off, keyboard.ShiftState);
                    Kana("ー");
                    Press(Key.F7);
                    Press(Key.Enter);
                    Assert.True(PumpUntil(() => editor.Text == "ヌロガァー", 2000), editor.Text);
                    output.WriteLine($"Kana composition committed: {editor.Text}");
                }
            }
            finally
            {
                if (window.IsVisible)
                {
                    keyboard.SendActionKey(Key.Escape, ModifierKeys.None);
                    PumpFor(100);
                    if (japanese is not null && originalMode is not null)
                    {
                        RestoreInputMode(backend, japanese, originalMode);
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

    private static bool IsJapaneseCharacter(char character)
    {
        return character is >= '\u3040' and <= '\u30FF';
    }

    private static bool EnsureDirectInput(
        TsfInputMethodService backend,
        SoftKeyboardInputMethod inputMethod)
    {
        var mode = ReadNativeState(inputMethod.ClassId);
        return mode is not null
            && (IsKnownNative(inputMethod, mode) == false || SoftKeyboardTsfInputMode.TryInvokeInputModeButton(inputMethod, new Rect()) == SoftKeyboardInputModeCommandResult.Accepted);
    }

    private static void RestoreInputMode(
        TsfInputMethodService backend,
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardInputObservation expected)
    {
        if (expected.OpenValue is { } open)
        {
            _ = SoftKeyboardTsfInputMode.TrySetKeyboardOpenValue(inputMethod, open);
        }
        if (expected.ConversionValue is { } conversion)
        {
            _ = SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(inputMethod, conversion);
        }

    }

    /// <summary>
    /// Observes the native visibility reported by active TSF candidate elements.
    /// </summary>
    /// <param name="elementType">An optional element type filter for a specific provider.</param>
    private static bool IsNativeCandidateShown(Guid? elementType)
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
                        && candidate.GetGuid(out var guid) >= 0
                        && (!elementType.HasValue || guid == elementType.Value)
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
