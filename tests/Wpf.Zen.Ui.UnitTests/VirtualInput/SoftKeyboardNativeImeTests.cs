// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Settings;
using TextBox = System.Windows.Controls.TextBox;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>Opt-in foreground tests against installed IMEs; never inject keys during the ordinary unit suite.</summary>
public class SoftKeyboardNativeImeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0x0411, JapaneseSoftKeyboardLayouts.Fifty, false)]
    [InlineData(0x0411, JapaneseSoftKeyboardLayouts.Twelve, false)]
    [InlineData(0x0404, SoftKeyboardSupplementalLayouts.Automatic, false)]
    [InlineData(0x0404, SoftKeyboardSupplementalLayouts.Automatic, true)]
    public void NativeSupplementalPanel_ProducesImeTextAndKeepsMenuFocus(
        int language, string supplemental, bool loadAfterEditor)
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("WPF_ZEN_NATIVE_IME_TESTS") == "1",
            "Set WPF_ZEN_NATIVE_IME_TESTS=1 to test installed Japanese/Zhuyin IMEs in a foreground window."
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
                AutoShowMode = SoftKeyboardAutoShowMode.AnyFocus,
                ExtensionProvider = DefaultSoftKeyboardExtensionProvider.Default
            };
            var root = new DockPanel();
            DockPanel.SetDock(editor, Dock.Top);
            root.Children.Add(editor);
            if (!loadAfterEditor)
            {
                root.Children.Add(keyboard);
            }

            var window = new Window
            {
                Title = "SoftKeyboard supplemental panel verification",
                Content = root,
                Width = 900,
                Height = 460
            };
            var backend = new TsfInputMethodService();
            SoftKeyboardInputMethod? original = null;
            var originalState = InputMethodState.DoNotCare;
            try
            {
                window.Show();
                ActivateTestWindow(window);
                editor.Focus();
                window.UpdateLayout();
                PumpFor(400);
                if (loadAfterEditor)
                {
                    // Existing text editors initialize their WPF TextStore before an on-demand
                    // keyboard is loaded. Verify this activation order as well as initial load.
                    root.Children.Add(keyboard);
                    window.UpdateLayout();
                    PumpFor(400);
                }

                original = keyboard.ActiveInputMethod;
                originalState = backend.GetImeState();
                var profile = keyboard.InputMethods.FirstOrDefault(p => p.LanguageId == language
                    && (language != 0x0404 || p.ProfileId == new Guid("B2F9C502-1742-11D4-9790-0080C882687E")));
                Assert.SkipUnless(profile is not null, "The requested input language is not installed.");
                Assert.True(backend.TryActivate(profile));
                PumpFor(500);
                Assert.True(backend.TrySetImeState(InputMethodState.On));
                keyboard.OnSessionInputLanguageChanged();
                PumpFor(200);
                output.WriteLine(
                    $"{keyboard.ActiveInputMethod?.DisplayName}, profile={keyboard.ActiveInputMethod?.ProfileId}, preference={keyboard.ActiveInputMethod?.TouchLayoutPreference}, IME={keyboard.ImeState}");
                if (language == 0x0404)
                {
                    TraceCandidateFunctions(profile.ClassId);
                }

                // Exercise the actual DropDownButton/ContextMenu focus boundary and radio action.
                var button =
                    (DropDownButton)keyboard.Template.FindName("PART_SettingsButton", keyboard);

                typeof(DropDownButton).GetMethod(
                    "OnClick", BindingFlags.Instance | BindingFlags.NonPublic
                )!.Invoke(button, null);

                PumpFor(100);
                Assert.True(button.IsDropDownOpen);
                Assert.True(keyboard.IsOpen);

                var layoutMenu = keyboard.SettingsMenu.Items.Cast<object>().OfType<MenuItem>()
                    .Single(item
                        => (item.DataContext as SoftKeyboardSetting)?.Id == "layout");

                var choice = layoutMenu.Items.Cast<RadioMenuItem>().Single(
                    item => (item.DataContext as SoftKeyboardSetting)?.Id == "layout." + supplemental
                );

                typeof(RadioMenuItem).GetMethod(
                    "OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(choice, null);

                PumpFor(100);

                output.WriteLine(
                    $"After choice: selected={keyboard.SelectedSupplementalLayout}, menu={keyboard.SettingsMenu.IsOpen}, button={button.IsDropDownOpen}");

                Assert.True(
                    PumpUntil(() => !button.IsDropDownOpen, 800), "DropDownButton did not observe menu dismissal.");

                Assert.Equal(supplemental, keyboard.SelectedSupplementalLayout);
                Assert.True(keyboard.IsOpen);

                var invoke =
                    typeof(SoftKeyboard).GetMethod("InvokeKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
                PumpFor(200);
                CaptureIfRequested(keyboard, supplemental);
                ActivateTestWindow(window);
                editor.Focus();
                Assert.True(
                    PumpUntil(() => editor.IsKeyboardFocused, 1000), "The menu must return input to the test editor.");

                PumpFor(300);
                string[] caps = language == 0x0411 ? ["か", "な"] : ["ㄋ", "ㄧ", "ˇ"];
                foreach (var cap in caps)
                {
                    var key = keyboard.CurrentLayout.Keys.Single(key => Equals(key.Content, cap));
                    invoke.Invoke(keyboard, [
                        key
                    ]);
                    PumpFor(300);
                    if (language == 0x0404)
                    {
                        output.WriteLine(
                            $"Zhuyin key {cap}: text={editor.Text}, count={keyboard.Candidates.Count}, state={keyboard.CandidatePresentationState}");
                        if (cap == "ㄋ")
                        {
                            // An unfinished reading is distinct from alternatives for an already
                            // converted syllable. Keep this reported scenario visible in test output.
                            var down = keyboard.CurrentLayout.Keys.Single(k => k.Key == Key.Down);
                            invoke.Invoke(keyboard, [
                                down
                            ]);
                            PumpFor(200);
                            output.WriteLine(
                                $"Zhuyin unfinished reading + Down: text={editor.Text}, count={keyboard.Candidates.Count}, state={keyboard.CandidatePresentationState}");
                            CaptureIfRequested(keyboard, "Zhuyin-Unfinished-" + loadAfterEditor);
                        }
                    }
                }

                if (language == 0x0411)
                {
                    Assert.NotNull(
                        typeof(SoftKeyboard).GetField(
                            "_activeTextComposition",
                            BindingFlags.Instance | BindingFlags.NonPublic
                        )!.GetValue(keyboard)
                    );
                }

                if (language == 0x0404)
                {
                    output.WriteLine(
                        $"Zhuyin before Down: text={editor.Text}, count={keyboard.Candidates.Count}, state={keyboard.CandidatePresentationState}");
                    var choose = keyboard.CurrentLayout.Keys.Single(key => key.Key == Key.Down);
                    invoke.Invoke(keyboard, [
                        choose
                    ]);
                    PumpFor(500);
                    output.WriteLine(
                        $"Zhuyin after Down: text={editor.Text}, state={keyboard.CandidatePresentationState}, candidates={string.Join(" / ", keyboard.Candidates.Select(c => c.Text))}");

                    Assert.True(
                        keyboard.HasCandidates,
                        $"Zhuyin after Down: text={editor.Text}, state={keyboard.CandidatePresentationState}"
                    );

                    CaptureIfRequested(keyboard, "Zhuyin-Candidates-" + loadAfterEditor);
                    var candidate = keyboard.Candidates.First(c => c.Text.TrimEnd('\0') == "你");
                    InvokeCandidate(keyboard, candidate);
                    PumpFor(200);
                }

                Assert.True(keyboard.SendActionKey(Key.Enter, ModifierKeys.None));
                var expected = language == 0x0411 ? "かな" : "你";
                Assert.True(
                    PumpUntil(() => editor.Text == expected, 2500), $"Expected {expected}, actual '{editor.Text}'");

                if (language == 0x0411)
                {
                    editor.SelectAll();
                    Assert.True(keyboard.SendActionKey(Key.Back, ModifierKeys.None));
                    PumpFor(100);
                    foreach (var alternate in new[]
                        {
                            "が", "ぱ", "っ", "ろ", "む", "を", "ー"
                        })
                    {
                        var key = keyboard.CurrentLayout.Keys.First(key
                            => Equals(key.Content, alternate) || key.Alternates.Contains(alternate));

                        typeof(SoftKeyboard).GetMethod(
                                "ExecuteCustomAction", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .Invoke(keyboard, [key.Action!, alternate]);

                        PumpFor(120);
                    }

                    Assert.NotNull(typeof(SoftKeyboard).GetField(
                        "_activeTextComposition", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(keyboard));
                    Assert.True(keyboard.SendActionKey(Key.Enter, ModifierKeys.None));
                    Assert.True(PumpUntil(() => editor.Text == "がぱっろむをー", 2000), "Alternate result: " + editor.Text);
                }

                Assert.True(backend.TrySetImeState(InputMethodState.Off));
                keyboard.OnSessionInputLanguageChanged();
                PumpFor(100);
                Assert.DoesNotContain(keyboard.CurrentLayout.Keys, key => Equals(key.Content, caps[0]));
                var other = keyboard.InputMethods.First(p => p.LanguageId != language);
                Assert.True(backend.TryActivate(other));
                PumpFor(350);
                keyboard.OnSessionInputLanguageChanged();
                PumpFor(100);
                Assert.Equal(SoftKeyboardSupplementalLayouts.Automatic, keyboard.SelectedSupplementalLayout);
                Assert.True(backend.TryActivate(profile));
                PumpFor(350);
                keyboard.OnSessionInputLanguageChanged();
                PumpFor(100);
                Assert.Equal(supplemental, keyboard.SelectedSupplementalLayout);
            }
            finally
            {
                keyboard.SettingsMenu.IsOpen = false;
                if (window.IsVisible)
                {
                    keyboard.SendActionKey(Key.Escape, ModifierKeys.None);
                    PumpFor(100);
                }

                if (original is not null)
                {
                    backend.TryActivate(original);
                    backend.TrySetImeState(originalState);
                }

                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(SoftKeyboardCandidateMode.Automatic)]
    [InlineData(SoftKeyboardCandidateMode.Embedded)]
    public void Weasel_PreservesNativePreviewOrCommitsExplicitlyEmbeddedCandidates(SoftKeyboardCandidateMode mode)
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
                CandidateMode = mode,
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
            var originalState = InputMethodState.DoNotCare;
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
                originalState = backend.GetImeState();
                output.WriteLine(string.Join(
                    Environment.NewLine,
                    keyboard.InputMethods.Select(p => $"{p.DisplayName}: {p.ClassId} / {p.LanguageId:X4}")));

                var weasel = keyboard.InputMethods.FirstOrDefault(p => p.DisplayName.Contains("小狼毫")
                    || p.DisplayName.Contains("Weasel", StringComparison.OrdinalIgnoreCase));
                Assert.SkipUnless(weasel is not null, "Weasel is not installed.");
                Assert.True(backend.TryActivate(weasel));
                PumpFor(500);
                Assert.True(backend.TrySetImeState(InputMethodState.On));
                keyboard.OnSessionInputLanguageChanged();
                PumpUntil(
                    () => keyboard.ActiveInputMethod?.ClassId == weasel.ClassId
                        && keyboard.ImeState == InputMethodState.On,
                    2000
                );
                foreach (var requested in new[]
                    {
                        2, 1, 0, 3, 4
                    })
                {
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

                    if (mode == SoftKeyboardCandidateMode.Automatic)
                    {
                        Assert.False(keyboard.HasCandidates);
                        Assert.Equal(
                            SoftKeyboardCandidatePresentationState.NativeFallback, keyboard.CandidatePresentationState);
                        Assert.True(IsNativeCandidateShown(), "Weasel must retain its own candidate/reading surface.");
                        Assert.True(keyboard.SendActionKey(Key.Space, ModifierKeys.None));
                        Assert.True(PumpUntil(() => !string.IsNullOrWhiteSpace(editor.Text), 1500));
                        break;
                    }

                    Assert.True(
                        PumpUntil(() => keyboard.Candidates.Count >= 3, 3000),
                        "Weasel did not expose at least three candidates.");

                    var candidate = keyboard.Candidates[Math.Min(requested, keyboard.Candidates.Count - 1)];
                    var expected = candidate.Text.TrimEnd('\0');
                    output.WriteLine($"Click {requested}: {expected}, selected={keyboard.SelectedCandidateIndex}");
                    var elapsed = Stopwatch.StartNew();
                    var selections = new List<uint?>();
                    var descriptor = DependencyPropertyDescriptor.FromProperty(
                        SoftKeyboard.SelectedCandidateIndexProperty, typeof(SoftKeyboard));
                    EventHandler selectionChanged = (_, _) => selections.Add(keyboard.SelectedCandidateIndex);
                    descriptor.AddValueChanged(keyboard, selectionChanged);
                    InvokeCandidate(keyboard, candidate);
                    InvokeCandidate(keyboard, candidate); // Duplicate clicks must not queue another number.
                    PumpFor(120);
                    descriptor.RemoveValueChanged(keyboard, selectionChanged);
                    output.WriteLine(
                        $"Selection updates after click: {string.Join(", ", selections)}; elapsed={elapsed.ElapsedMilliseconds}ms");
                    if (requested != 4)
                    {
                        Assert.DoesNotContain(
                            selections,
                            selection => selection.HasValue && selection.Value != candidate.Index);
                    }

                    if (requested == 4)
                    {
                        PumpFor(250);
                        output.WriteLine(
                            $"Partial result: text={editor.Text}; candidates={string.Join(" / ", keyboard.Candidates.Select(c => c.Text))}");
                        Assert.True(
                            keyboard.HasCandidates,
                            "The unconverted syllables should still expose candidates.");

                        InvokeCandidate(keyboard, keyboard.Candidates[0]);
                        Assert.True(PumpUntil(
                            () => !keyboard.HasCandidates && editor.Text.StartsWith(expected, StringComparison.Ordinal),
                            1500));
                        continue;
                    }

                    Assert.True(
                        PumpUntil(() => !keyboard.HasCandidates && editor.Text == expected, 1500),
                        $"Expected '{expected}', actual '{editor.Text}', selected={keyboard.SelectedCandidateIndex}, error={keyboard.CandidateError}");

                    Assert.True(
                        elapsed.ElapsedMilliseconds < 500,
                        $"Candidate commit took {elapsed.ElapsedMilliseconds} ms.");

                    PumpFor(120);
                    Assert.Equal(expected, editor.Text);
                }
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
                    backend.TrySetImeState(originalState);
                }

                window.Close();
                InputManager.Current.PreProcessInput -= trace;
            }
        });
    }

    private static void InvokeCandidate(
        SoftKeyboard keyboard, SoftKeyboardCandidate candidate)
    {
        keyboard.UpdateLayout();
        var item = FindCandidateItem(keyboard, candidate.Index);
        Assert.NotNull(item);
        item.Invoke();
    }

    // Query optional public conversion channels without calling them or altering IME state.
    // These HRESULTs distinguish a missing provider from an unsupported individual function.
    private void TraceCandidateFunctions(Guid classId)
    {
        if (WpfTextServicesAccessor.TryGetThreadManager() is not ITfThreadMgrEx manager)
        {
            return;
        }

        foreach (var providerId in new[]
            {
                classId, new Guid("9A698BB0-0F21-11D3-8DF1-00105A2799B5")
            })
        {
            object? providerObject = null;
            try
            {
                var result = manager.GetFunctionProvider(in providerId, out providerObject);
                output.WriteLine($"Candidate function provider {providerId}: HRESULT=0x{result:X8}");
                if (providerObject is not ITfFunctionProvider provider)
                {
                    continue;
                }

                foreach (var interfaceId in new[]
                    {
                        new Guid("87A2AD8F-F27B-4920-8501-67602280175D"),
                        new Guid("EA163CE2-7A65-4506-82A3-C528215DA64E")
                    })
                {
                    object? function = null;
                    try
                    {
                        var functionId = Guid.Empty;
                        result = provider.GetFunction(in functionId, in interfaceId, out function);
                        output.WriteLine(
                            $"Candidate function {interfaceId}: HRESULT=0x{result:X8}, available={function is not null}");
                    }
                    finally
                    {
                        ReleaseCom(function);
                    }
                }
            }
            finally
            {
                ReleaseCom(providerObject);
            }
        }
    }

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
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

    private static SoftKeyboardCandidateItem? FindCandidateItem(DependencyObject root, uint index)
    {
        if (root is SoftKeyboardCandidateItem item && item.Candidate?.Index == index)
        {
            return item;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var match = FindCandidateItem(VisualTreeHelper.GetChild(root, i), index);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
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

    private static void CaptureIfRequested(FrameworkElement element, string name)
    {
        var directory = Environment.GetEnvironmentVariable("WPF_ZEN_IME_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(element.ActualWidth),
            (int)Math.Ceiling(element.ActualHeight),
            96,
            96,
            PixelFormats.Pbgra32
        );

        var visual = new DrawingVisual();

        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(
                new VisualBrush(element),
                null,
                new Rect(0, 0, element.ActualWidth, element.ActualHeight)
            );
        }

        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, "SoftKeyboard-" + name + ".png"));
        encoder.Save(stream);
    }
}