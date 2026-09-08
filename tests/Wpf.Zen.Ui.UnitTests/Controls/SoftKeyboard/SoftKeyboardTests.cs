// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Zen.Ui.AutomationPeers;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.SoftKeyboardControl;

public class SoftKeyboardTests
{
    [Fact]
    public void Defaults_AreClosedManualAndNonFocusable()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            var metadata = Assert.IsType<FrameworkPropertyMetadata>(
                SoftKeyboard.IsOpenProperty.GetMetadata(typeof(SoftKeyboard))
            );

            Assert.False(keyboard.IsOpen);
            Assert.False(keyboard.Focusable);
            Assert.False(keyboard.IsTabStop);
            Assert.Equal(SoftKeyboardAutoShowMode.Disabled, keyboard.AutoShowMode);
            Assert.Equal(SoftKeyboardAutoHideMode.FocusLeavesScope, keyboard.AutoHideMode);
            Assert.True(metadata.BindsTwoWayByDefault);
            Assert.NotEmpty(keyboard.CurrentLayout.Rows);
        });
    }

    [Fact]
    public void LayoutProvider_UsesInputScopeAndDisplayMode()
    {
        WpfTestThread.Run(() =>
        {
            ISoftKeyboardLayoutProvider provider = DefaultSoftKeyboardLayoutProvider.Default;
            var phone = provider.CreateLayout(
                new SoftKeyboardLayoutContext(
                    CultureInfo.GetCultureInfo("en-US"),
                    InputScopeNameValue.TelephoneNumber,
                    SoftKeyboardDisplayMode.Automatic,
                    SoftKeyboardShiftState.Off
                )
            );
            var symbols = provider.CreateLayout(
                new SoftKeyboardLayoutContext(
                    CultureInfo.GetCultureInfo("en-US"),
                    InputScopeNameValue.Default,
                    SoftKeyboardDisplayMode.Symbols,
                    SoftKeyboardShiftState.Off
                )
            );

            Assert.Contains(phone.Rows.SelectMany(row => row.Keys), key => key.Text == "+");
            Assert.Contains(phone.Rows.SelectMany(row => row.Keys), key => key.Text == "0");
            Assert.Contains(phone.Rows.SelectMany(row => row.Keys), key => key.Key == Key.Left);
            Assert.Contains(phone.Rows.SelectMany(row => row.Keys), key => key.Key == Key.Right);
            Assert.Contains(symbols.Rows.SelectMany(row => row.Keys), key => key.Text == "$");
        });
    }

    [Fact]
    public void ShiftOnce_IsConsumedByOneCharacterAndCapsLockPersists()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out RecordingInputBackend input, out _, out _);
            var keyboard = new SoftKeyboard(services) { IsOpen = true };
            var editor = new System.Windows.Controls.TextBox();
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;

            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            window.Show();
            Assert.True(editor.Focus());

            InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.Shift));
            Assert.Equal(SoftKeyboardShiftState.Once, keyboard.ShiftState);
            InvokeKey(keyboard, FindKey(keyboard, Key.A));

            Assert.Equal((Key.A, true), Assert.Single(input.VirtualKeys));
            Assert.Equal(SoftKeyboardShiftState.Off, keyboard.ShiftState);

            InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.Shift));
            InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.Shift));
            Assert.Equal(SoftKeyboardShiftState.Locked, keyboard.ShiftState);
            InvokeKey(keyboard, FindKey(keyboard, Key.B));
            Assert.Equal(SoftKeyboardShiftState.Locked, keyboard.ShiftState);

            window.Close();
        });
    }

    [Fact]
    public void HeldNavigationKey_RepeatsWithoutMovingFocusToKeyboard()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out RecordingInputBackend input, out _, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services) { IsOpen = true };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            window.Show();
            Assert.True(editor.Focus());

            SoftKeyboardKey left = FindKey(keyboard, Key.Left);
            typeof(SoftKeyboard)
                .GetField("_heldKey", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(keyboard, left);
            typeof(SoftKeyboard)
                .GetMethod("OnHoldTimerTick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyboard, [null, EventArgs.Empty]);
            typeof(SoftKeyboard)
                .GetMethod("OnRepeatTimerTick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyboard, [null, EventArgs.Empty]);

            Assert.Equal([(Key.Left, false), (Key.Left, false)], input.VirtualKeys);
            Assert.Same(editor, Keyboard.FocusedElement);
            keyboard.IsOpen = false;
            window.Close();
        });
    }

    [Fact]
    public void GeneratedLetterButton_ClickRoutesInputWithoutTakingEditorFocus()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out RecordingInputBackend input, out _, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services) { IsOpen = true };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            window.Show();
            window.UpdateLayout();
            Assert.True(editor.Focus());

            SoftKeyboardKey letter = FindKey(keyboard, Key.A);
            Wpf.Zen.Ui.Controls.Button button = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                item => ReferenceEquals(item.DataContext, letter)
            );
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));

            Assert.Equal((Key.A, false), Assert.Single(input.VirtualKeys));
            Assert.Same(editor, Keyboard.FocusedElement);
            window.Close();
        });
    }

    [Fact]
    public void GeneratedLetterButton_ClickInsertsThroughWindowsInputStream()
    {
        WpfApplicationTestHost.Run(() =>
        {
            Key receivedKey = Key.None;
            var editor = new System.Windows.Controls.TextBox();
            editor.PreviewKeyDown += (_, eventArgs) => receivedKey = eventArgs.Key;
            var keyboard = new SoftKeyboard { IsOpen = true };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            window.Show();
            Assert.True(window.Activate());
            window.UpdateLayout();
            Assert.True(editor.Focus());
            Assert.True(
                (bool)typeof(SoftKeyboard)
                    .GetMethod("IsWindowForeground", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, null)!
            );

            SoftKeyboardKey letter = FindKey(keyboard, Key.A);
            Wpf.Zen.Ui.Controls.Button button = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                item => ReferenceEquals(item.DataContext, letter)
            );
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
            PumpDispatcher();

            Assert.Equal(Key.A, receivedKey);
            Assert.Same(editor, Keyboard.FocusedElement);
            window.Close();
        });
    }

    [Fact(Explicit = true)]
    public void InstalledMicrosoftPinyin_PublishesCandidatesToEmbeddedSurfaceWhenAvailable()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard { IsOpen = true };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            window.Show();
            Assert.True(window.Activate());
            window.UpdateLayout();
            PumpDispatcher();

            SoftKeyboardInputMethod? pinyin = keyboard.InputMethods.FirstOrDefault(inputMethod =>
                inputMethod.ClassId == new Guid("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E")
            );
            if (pinyin is null)
            {
                window.Close();
                return;
            }

            Assert.True(editor.Focus());
            typeof(SoftKeyboard)
                .GetMethod("ActivateInputMethod", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyboard, [pinyin]);
            PumpDispatcher();
            InvokeKey(keyboard, FindKey(keyboard, Key.N));
            InvokeKey(keyboard, FindKey(keyboard, Key.I));
            PumpDispatcher();
            PumpDispatcher();

            object? textEditor = typeof(System.Windows.Controls.Primitives.TextBoxBase)
                .GetField("_textEditor", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(editor);
            object? textStore = textEditor?.GetType()
                .GetField("_textstore", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(textEditor);

            Assert.True(
                keyboard.CandidatePresentationState == SoftKeyboardCandidatePresentationState.Embedded,
                $"Candidate state was {keyboard.CandidatePresentationState}; text was '{editor.Text}'; "
                    + $"IME state was {keyboard.ImeState}; conversion was {InputMethod.Current.ImeConversionMode}; "
                    + $"language was {InputLanguageManager.Current.CurrentInputLanguage.Name}; "
                    + $"active profile was {keyboard.ActiveInputMethod?.DisplayName ?? "<none>"}; "
                    + $"WPF TextStore was {(textStore is null ? "missing" : "attached")}."
            );
            Assert.NotEmpty(keyboard.Candidates);

            keyboard.ShowDisplayMode(SoftKeyboardDisplayMode.Emoji);
            PumpDispatcher();
            PumpDispatcher();
            Assert.Equal(string.Empty, editor.Text);
            Assert.False(keyboard.HasCandidates);

            SoftKeyboardEmoji emoji = keyboard.Emojis.First();
            typeof(SoftKeyboard)
                .GetMethod("InsertEmoji", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyboard, [emoji]);
            PumpDispatcher();
            Assert.Equal(emoji.Text, editor.Text);

            keyboard.ShowDisplayMode(SoftKeyboardDisplayMode.Letters);
            InvokeKey(keyboard, FindKey(keyboard, Key.N));
            InvokeKey(keyboard, FindKey(keyboard, Key.I));
            PumpDispatcher();
            PumpDispatcher();

            Assert.Equal(SoftKeyboardCandidatePresentationState.Embedded, keyboard.CandidatePresentationState);
            Assert.NotEmpty(keyboard.Candidates);
            Assert.True(
                keyboard.CanGoToNextCandidatePage,
                $"Microsoft Pinyin exposed only {keyboard.CandidatePageCount} candidate page(s) for 'ni'."
            );
            uint pageBeforeNavigation = keyboard.CandidatePageNumber;
            Wpf.Zen.Ui.Controls.Button nextPage = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                button => button.Name == "PART_NextCandidatePageButton"
            );
            nextPage.RaiseEvent(
                new RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                    nextPage
                )
            );
            PumpDispatcher();

            Assert.Equal(pageBeforeNavigation + 1U, keyboard.CandidatePageNumber);
            Assert.NotEmpty(keyboard.Candidates);

            SoftKeyboardCandidate candidate = keyboard.Candidates[0];
            typeof(SoftKeyboard)
                .GetMethod("CommitCandidate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyboard, [candidate]);
            PumpDispatcher();

            Assert.Equal(emoji.Text + candidate.Text, editor.Text);
            Assert.False(keyboard.HasCandidates);
            window.Close();
        });
    }

    [Fact]
    public void NativeInputStructure_MatchesWindowsAbiOnCurrentArchitecture()
    {
        int expectedSize = IntPtr.Size == 8 ? 40 : 28;

        Assert.Equal(
            expectedSize,
            System.Runtime.InteropServices.Marshal.SizeOf<SoftKeyboardNativeMethods.Input>()
        );
    }

    [Fact]
    public void AbcButton_TogglesImeStateWithoutTakingEditorFocus()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services) { IsOpen = true };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            window.Show();
            window.UpdateLayout();
            Assert.True(editor.Focus());
            Assert.Equal(InputMethodState.On, keyboard.ImeState);

            Wpf.Zen.Ui.Controls.Button button = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                item => Equals(item.Tag, SoftKeyboardKeyKind.ToggleIme)
            );
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));

            Assert.Equal(InputMethodState.Off, keyboard.ImeState);
            Assert.Equal(SoftKeyboardDisplayMode.Letters, keyboard.DisplayMode);
            Assert.Same(editor, Keyboard.FocusedElement);

            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
            Assert.Equal(InputMethodState.On, keyboard.ImeState);
            window.Close();
        });
    }

    [Fact]
    public void ImeConversionTransition_PreservesShapePreferenceWithoutForcingFullShape()
    {
        ImeConversionModeValues halfShapeNative = TsfInputMethodService.GetConversionModeForImeState(
            ImeConversionModeValues.Alphanumeric | ImeConversionModeValues.Roman,
            InputMethodState.On
        );
        ImeConversionModeValues fullShapeNative = TsfInputMethodService.GetConversionModeForImeState(
            ImeConversionModeValues.Alphanumeric
                | ImeConversionModeValues.FullShape
                | ImeConversionModeValues.Symbol,
            InputMethodState.On
        );
        ImeConversionModeValues fullShapeLetters = TsfInputMethodService.GetConversionModeForImeState(
            ImeConversionModeValues.Native
                | ImeConversionModeValues.FullShape
                | ImeConversionModeValues.Roman,
            InputMethodState.Off
        );

        Assert.Equal(
            ImeConversionModeValues.Native | ImeConversionModeValues.Roman,
            halfShapeNative
        );
        Assert.False((halfShapeNative & ImeConversionModeValues.FullShape) != 0);
        Assert.Equal(
            ImeConversionModeValues.Native
                | ImeConversionModeValues.FullShape
                | ImeConversionModeValues.Symbol,
            fullShapeNative
        );
        Assert.Equal(
            ImeConversionModeValues.Alphanumeric
                | ImeConversionModeValues.FullShape
                | ImeConversionModeValues.Roman,
            fullShapeLetters
        );
    }

    [Fact]
    public void AbcButton_ActivatesEnabledTipBeforeOpeningImeFromDirectKeyboardLayout()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out _, out FakeInputMethodBackend inputMethods);
            inputMethods.UseDirectKeyboardLayout();
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services) { IsOpen = true };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            window.Show();
            window.UpdateLayout();
            Assert.True(editor.Focus());

            Wpf.Zen.Ui.Controls.Button button = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                item => Equals(item.Tag, SoftKeyboardKeyKind.ToggleIme)
            );
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));

            Assert.Equal(1, inputMethods.ActivationCount);
            Assert.Equal(InputMethodState.On, keyboard.ImeState);
            Assert.NotNull(keyboard.ActiveInputMethod);
            Assert.Same(editor, Keyboard.FocusedElement);
            window.Close();
        });
    }

    [Fact]
    public void HeldCharacter_OpensImmutableAccentAlternatives()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _)) { IsOpen = true };
            var window = new Window { Content = keyboard, Width = 700, Height = 500, ShowInTaskbar = false };
            try
            {
                window.Show();
                SoftKeyboardKey key = FindKey(keyboard, Key.A);
                typeof(SoftKeyboard)
                    .GetField("_heldKey", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(keyboard, key);
                typeof(SoftKeyboard)
                    .GetMethod("OnHoldTimerTick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, [null, EventArgs.Empty]);

                Assert.True(keyboard.IsAlternatePopupOpen);
                Assert.Contains("á", keyboard.AlternateValues);
                Assert.Contains("ä", keyboard.AlternateValues);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ExplicitTarget_OutsideScopeIsRejected()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            var scope = new Grid();
            var external = new System.Windows.Controls.TextBox();
            var window = new Window { Content = scope, ShowInTaskbar = false };
            window.Show();
            keyboard.TargetScope = scope;
            keyboard.Target = external;

            Assert.Null(keyboard.ActiveTarget);

            scope.Children.Add(external);
            keyboard.Target = null;
            keyboard.Target = external;
            Assert.Same(external, keyboard.ActiveTarget);
            window.Close();
        });
    }

    [Fact]
    public void AutoShow_TracksSupportedTargetsAndAutoHideLeavesScope()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var scope = new Grid();
            var editor = new System.Windows.Controls.TextBox();
            scope.Children.Add(editor);
            var keyboard = new SoftKeyboard
            {
                TargetScope = scope,
                AutoShowMode = SoftKeyboardAutoShowMode.AnyFocus,
            };
            scope.Children.Add(keyboard);
            var window = new Window { Content = scope, ShowInTaskbar = false };
            window.Show();

            keyboard.OnSessionFocusChanged(editor, touchOrPenInitiated: false);
            Assert.True(keyboard.IsOpen);
            Assert.Same(editor, keyboard.ActiveTarget);

            keyboard.OnSessionFocusChanged(new System.Windows.Controls.Button(), touchOrPenInitiated: false);
            Assert.False(keyboard.IsOpen);
            window.Close();
        });
    }

    [Fact]
    public void CandidateBackend_ControlsStateAndCommitWithoutNativeTsf()
    {
        WpfTestThread.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var keyboard = new SoftKeyboard(services);
            candidates.Register(keyboard);
            candidates.Publish(keyboard, 2, 3, "first", "second");

            Assert.Equal(SoftKeyboardCandidatePresentationState.Embedded, keyboard.CandidatePresentationState);
            Assert.Equal(2, keyboard.Candidates.Count);
            Assert.True(keyboard.HasCandidates);
            Assert.Equal(2U, keyboard.CandidatePageNumber);
            Assert.Equal(3U, keyboard.CandidatePageCount);
            Assert.True(keyboard.CanGoToPreviousCandidatePage);
            Assert.True(keyboard.CanGoToNextCandidatePage);

            Assert.True(candidates.TryCommit(keyboard, keyboard.Candidates[1].Index));
            Assert.Equal(1U, candidates.LastCommittedIndex);
        });
    }

    [Fact]
    public void CandidatePagingButtons_AreTouchInvokableAndKeepEditorFocus()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services) { IsOpen = true, Target = editor };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, Width = 640, ShowInTaskbar = false };
            window.Show();
            window.UpdateLayout();
            Assert.True(editor.Focus());
            candidates.Publish(keyboard, 2, 3, "first", "second");
            window.UpdateLayout();

            Wpf.Zen.Ui.Controls.Button previous = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                button => button.Name == "PART_PreviousCandidatePageButton"
            );
            Wpf.Zen.Ui.Controls.Button next = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                button => button.Name == "PART_NextCandidatePageButton"
            );

            Assert.True(previous.IsEnabled);
            Assert.True(next.IsEnabled);
            next.RaiseEvent(
                new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, next)
            );
            Assert.Equal(1, candidates.LastPageDelta);
            Assert.Same(editor, Keyboard.FocusedElement);

            previous.RaiseEvent(
                new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, previous)
            );
            Assert.Equal(-1, candidates.LastPageDelta);
            Assert.Same(editor, Keyboard.FocusedElement);
            window.Close();
        });
    }

    [Fact]
    public void CandidateBar_ReservesHeightWhileActiveImeHasNoCandidates()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services) { IsOpen = true, Target = editor };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, Width = 640, ShowInTaskbar = false };
            window.Show();
            window.UpdateLayout();
            Assert.True(editor.Focus());

            var candidateBar = Assert.IsType<Grid>(keyboard.Template.FindName("CandidateBar", keyboard));
            var previous = Assert.IsType<Wpf.Zen.Ui.Controls.Button>(
                keyboard.Template.FindName("PART_PreviousCandidatePageButton", keyboard)
            );
            Assert.Equal(Visibility.Visible, candidateBar.Visibility);
            Assert.True(candidateBar.ActualHeight >= 44d);
            Assert.Equal(Visibility.Collapsed, previous.Visibility);
            double emptyHeight = keyboard.ActualHeight;

            candidates.Publish(keyboard, 1, 2, "candidate");
            window.UpdateLayout();
            Assert.Equal(Visibility.Visible, candidateBar.Visibility);
            Assert.Equal(Visibility.Visible, previous.Visibility);

            keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.None);
            window.UpdateLayout();
            Assert.Equal(Visibility.Visible, candidateBar.Visibility);
            Assert.Equal(emptyHeight, keyboard.ActualHeight);

            Wpf.Zen.Ui.Controls.Button imeToggle = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                button => Equals(button.Tag, SoftKeyboardKeyKind.ToggleIme)
            );
            imeToggle.RaiseEvent(
                new RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
                    imeToggle
                )
            );
            Assert.Equal(Visibility.Collapsed, candidateBar.Visibility);
            window.Close();
        });
    }

    [Fact]
    public void EqualCandidateSnapshots_PreserveItemsForPointerClickCompletion()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services) { IsOpen = true, Target = editor };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, Width = 640, ShowInTaskbar = false };
            window.Show();
            Assert.True(editor.Focus());
            candidates.Publish(keyboard, 1, 1, "first", "second");
            window.UpdateLayout();
            SoftKeyboardCandidate firstItem = keyboard.Candidates[0];
            Wpf.Zen.Ui.Controls.Button firstButton = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                button => ReferenceEquals(button.DataContext, firstItem)
            );

            Assert.True(editor.Focus());
            candidates.Publish(keyboard, 1, 1, "first", "second");
            window.UpdateLayout();

            Assert.Same(firstItem, keyboard.Candidates[0]);
            Assert.Same(
                firstButton,
                Assert.Single(
                    Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                    button => ReferenceEquals(button.DataContext, firstItem)
                )
            );

            firstButton.RaiseEvent(
                new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, firstButton)
            );
            Assert.Equal(0U, candidates.LastCommittedIndex);
            window.Close();
        });
    }

    [Fact]
    public void ClosingKeyboard_ReleasesEmbeddedCandidatePresentation()
    {
        WpfTestThread.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var keyboard = new SoftKeyboard(services) { IsOpen = true };
            candidates.Publish(keyboard, 1, 1, "candidate");

            keyboard.IsOpen = false;

            Assert.Equal(1, candidates.ReleasePresentationCount);
            Assert.Empty(keyboard.Candidates);
            Assert.Equal(SoftKeyboardCandidatePresentationState.None, keyboard.CandidatePresentationState);
        });
    }

    [Fact]
    public void HidingKeyboard_ReleasesCandidatesAndShowingItRefreshesTheBridge()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services) { IsOpen = true, Target = editor };
            var keyboardHost = new Grid();
            keyboardHost.Children.Add(keyboard);
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboardHost);
            var window = new Window { Content = root, Width = 640, ShowInTaskbar = false };
            window.Show();
            window.UpdateLayout();
            candidates.Publish(keyboard, 1, 1, "candidate");
            int releasesBeforeHide = candidates.ReleasePresentationCount;
            int refreshesBeforeHide = candidates.RefreshCount;

            keyboardHost.Visibility = Visibility.Collapsed;

            Assert.True(candidates.ReleasePresentationCount > releasesBeforeHide);
            Assert.Empty(keyboard.Candidates);

            keyboardHost.Visibility = Visibility.Visible;
            window.UpdateLayout();

            Assert.True(candidates.RefreshCount > refreshesBeforeHide);
            window.Close();
        });
    }

    [Fact]
    public void FocusedEditor_RetriesCandidateBridgeAfterWpfFocusProcessing()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services) { IsOpen = true };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            window.Show();
            window.UpdateLayout();
            int refreshCountBeforeFocus = candidates.RefreshCount;

            Assert.True(editor.Focus());
            PumpDispatcher();

            Assert.Same(editor, keyboard.ActiveTarget);
            Assert.True(candidates.RefreshCount > refreshCountBeforeFocus);
            window.Close();
        });
    }

    [Fact]
    public void EmojiFilter_IsCategoryAndSearchAwareWithoutPersistence()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard
            {
                EmojiCategory = SoftKeyboardEmojiCategory.Food,
                EmojiSearchText = "pizza",
            };

            SoftKeyboardEmoji emoji = Assert.Single(keyboard.Emojis);
            Assert.Equal("🍕", emoji.Text);
            Assert.Equal(SoftKeyboardEmojiCategory.Food, emoji.Category);
        });
    }

    [Fact]
    public void AutomationPeer_ExpandsAndCollapsesWithoutMakingControlFocusable()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            var peer = new SoftKeyboardAutomationPeer(keyboard);
            var provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
                peer.GetPattern(PatternInterface.ExpandCollapse)
            );

            provider.Expand();
            Assert.True(keyboard.IsOpen);
            Assert.False(keyboard.Focusable);
            Assert.Equal(ExpandCollapseState.Expanded, provider.ExpandCollapseState);

            provider.Collapse();
            Assert.False(keyboard.IsOpen);
            Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);
        });
    }

    [Fact]
    public void PlatformFailureStates_DoNotDisableLayoutOrModeSwitching()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out FakeInputPaneBackend pane);
            pane.Result = SoftKeyboardSystemKeyboardSuppressionState.Unavailable;
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services) { IsOpen = true, Target = editor };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window { Content = root, ShowInTaskbar = false };
            window.Show();
            window.UpdateLayout();
            Assert.True(editor.Focus());

            Assert.Equal(SoftKeyboardSystemKeyboardSuppressionState.Unavailable, keyboard.SystemKeyboardSuppressionState);
            keyboard.ShowDisplayMode(SoftKeyboardDisplayMode.Symbols);
            Assert.Equal(SoftKeyboardDisplayMode.Symbols, keyboard.DisplayMode);
            Assert.NotEmpty(keyboard.CurrentLayout.Rows);
            window.Close();
        });
    }

    [Fact]
    public void InputPaneSuppression_RequiresOpenVisibleLoadedFocusedTargetAndRestoresConservatively()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out FakeInputPaneBackend pane);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services) { IsOpen = true, Target = editor };

            // IsOpen can be data-bound before the control enters a visual tree. With no HWND and no
            // focused target there must be no native InputPane call.
            Assert.Equal(0, pane.HideCount);
            Assert.Equal(SoftKeyboardSystemKeyboardSuppressionState.NotRequested, keyboard.SystemKeyboardSuppressionState);

            var keyboardHost = new Grid();
            keyboardHost.Children.Add(keyboard);
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboardHost);
            var window = new Window { Content = root, ShowInTaskbar = false };
            window.Show();
            window.UpdateLayout();
            Assert.True(editor.Focus());

            Assert.True(pane.HideCount > 0);
            Assert.Equal(SoftKeyboardSystemKeyboardSuppressionState.Hidden, keyboard.SystemKeyboardSuppressionState);
            int hideCountBeforeCollapse = pane.HideCount;

            keyboardHost.Visibility = Visibility.Collapsed;

            Assert.Equal(hideCountBeforeCollapse, pane.HideCount);
            Assert.Equal(1, pane.ShowCount);
            Assert.Equal(SoftKeyboardSystemKeyboardSuppressionState.NotRequested, keyboard.SystemKeyboardSuppressionState);

            typeof(SoftKeyboard)
                .GetMethod("RequestSystemKeyboardHide", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyboard, null);
            Assert.Equal(hideCountBeforeCollapse, pane.HideCount);

            keyboardHost.Visibility = Visibility.Visible;
            window.UpdateLayout();
            Assert.True(pane.HideCount > hideCountBeforeCollapse);
            Assert.Equal(SoftKeyboardSystemKeyboardSuppressionState.Hidden, keyboard.SystemKeyboardSuppressionState);

            keyboard.IsOpen = false;

            Assert.Equal(2, pane.ShowCount);
            Assert.Equal(SoftKeyboardSystemKeyboardSuppressionState.NotRequested, keyboard.SystemKeyboardSuppressionState);
            int hideCountAfterClose = pane.HideCount;
            typeof(SoftKeyboard)
                .GetMethod("RequestSystemKeyboardHide", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyboard, null);
            Assert.Equal(hideCountAfterClose, pane.HideCount);
            window.Close();
        });
    }

    [Fact]
    public void InputPaneSuppression_DoesNotShowAfterRejectedHideOrWhileAnotherKeyboardNeedsIt()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out FakeInputPaneBackend pane);
            var editor = new System.Windows.Controls.TextBox();
            var firstKeyboard = new SoftKeyboard(services) { IsOpen = true, Target = editor };
            var secondKeyboard = new SoftKeyboard(services) { IsOpen = true, Target = editor };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(firstKeyboard);
            root.Children.Add(secondKeyboard);
            var window = new Window { Content = root, ShowInTaskbar = false };
            window.Show();
            window.UpdateLayout();
            Assert.True(editor.Focus());
            Assert.True(pane.HideCount >= 2);

            firstKeyboard.IsOpen = false;

            Assert.Equal(0, pane.ShowCount);
            secondKeyboard.IsOpen = false;
            Assert.Equal(1, pane.ShowCount);

            pane.Result = SoftKeyboardSystemKeyboardSuppressionState.Rejected;
            firstKeyboard.IsOpen = true;
            Assert.Equal(SoftKeyboardSystemKeyboardSuppressionState.Rejected, firstKeyboard.SystemKeyboardSuppressionState);
            firstKeyboard.IsOpen = false;
            Assert.Equal(1, pane.ShowCount);
            window.Close();
        });
    }

    [Fact]
    public void DefaultTemplate_ClosedMeasuresZeroAndGeneratedKeysRemainNonFocusable()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out _);
            var keyboard = new SoftKeyboard(services);
            var window = new Window
            {
                Content = keyboard,
                Width = 340,
                SizeToContent = SizeToContent.Height,
                ShowInTaskbar = false,
            };
            window.Show();
            window.UpdateLayout();

            Assert.Equal(0d, keyboard.ActualHeight);

            keyboard.IsOpen = true;
            window.UpdateLayout();
            Assert.True(keyboard.ActualHeight >= 44d);
            Assert.All(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                button =>
                {
                    Assert.False(button.Focusable);
                    Assert.False(button.IsTabStop);
                }
            );
            window.Close();
        });
    }

    [Fact]
    public void InputPaneSuppression_DoesNotDetachWpfSharedWinRtObjects()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var firstEditor = new System.Windows.Controls.TextBox();
            var secondEditor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard();
            var scope = new StackPanel();
            scope.Children.Add(firstEditor);
            scope.Children.Add(secondEditor);
            scope.Children.Add(keyboard);
            var window = new Window { Content = scope, ShowInTaskbar = false };
            window.Show();

            // Focusing first lets WPF acquire its own cached InputPane activation factory. The
            // keyboard must not manually release that shared COM identity when requesting TryHide.
            Assert.True(firstEditor.Focus());
            keyboard.Target = firstEditor;
            keyboard.IsOpen = true;

            Assert.True(secondEditor.Focus());
            Assert.Same(secondEditor, Keyboard.FocusedElement);
            window.Close();
        });
    }

    private static SoftKeyboardPlatformServices CreateServices(
        out RecordingInputBackend input,
        out FakeCandidateBackend candidates,
        out FakeInputPaneBackend inputPane
    ) => CreateServices(out input, out candidates, out inputPane, out _);

    private static SoftKeyboardPlatformServices CreateServices(
        out RecordingInputBackend input,
        out FakeCandidateBackend candidates,
        out FakeInputPaneBackend inputPane,
        out FakeInputMethodBackend inputMethods
    )
    {
        input = new RecordingInputBackend();
        candidates = new FakeCandidateBackend();
        inputPane = new FakeInputPaneBackend();
        inputMethods = new FakeInputMethodBackend();
        return new SoftKeyboardPlatformServices(
            input,
            inputPane,
            inputMethods,
            candidates
        );
    }

    private static SoftKeyboardKey FindKey(SoftKeyboard keyboard, Key key) =>
        keyboard.CurrentLayout.Rows.SelectMany(row => row.Keys).First(item => item.Key == key);

    private static SoftKeyboardKey FindKey(SoftKeyboard keyboard, SoftKeyboardKeyKind kind) =>
        keyboard.CurrentLayout.Rows.SelectMany(row => row.Keys).First(item => item.Kind == kind);

    [Fact]
    public void NullEmojiSearchBinding_IsCoercedToEmpty()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _));
            System.Windows.Data.BindingOperations.SetBinding(
                keyboard,
                SoftKeyboard.EmojiSearchTextProperty,
                new System.Windows.Data.Binding("Search") { Source = new { Search = (string?)null } }
            );
            Assert.Equal(string.Empty, keyboard.EmojiSearchText);
            Assert.NotEmpty(keyboard.Emojis);
        });
    }

    [Fact]
    public void NumericScope_ExplicitLettersAndSymbolsOverrideAutomaticLayout()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _)) { IsOpen = true };
            var editor = new System.Windows.Controls.TextBox();
            InputMethod.SetInputScope(editor, new InputScope
            {
                Names = { new InputScopeName(InputScopeNameValue.Number) },
            });
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            try
            {
                window.Show();
                Assert.True(editor.Focus());
                Assert.Equal(SoftKeyboardDisplayMode.Automatic, keyboard.DisplayMode);
                Assert.DoesNotContain(keyboard.CurrentLayout.Rows.SelectMany(row => row.Keys), key => key.Key == Key.A);
                InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.ChangeDisplayMode));
                Assert.NotNull(FindKey(keyboard, Key.A));
                keyboard.ShowDisplayMode(SoftKeyboardDisplayMode.Symbols);
                Assert.Contains(keyboard.CurrentLayout.Rows.SelectMany(row => row.Keys), key => key.Text == "$");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(SoftKeyboardDisplayMode.Emoji)]
    [InlineData(SoftKeyboardDisplayMode.InputMethods)]
    public void AuxiliarySurface_ReleasesCandidatesAndProvidesPureNavigation(SoftKeyboardDisplayMode mode)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out var candidates, out _, out var inputMethods))
            {
                IsOpen = true,
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            try
            {
                window.Show();
                Assert.True(editor.Focus());
                candidates.Publish(keyboard, 1, 1, "candidate");
                int released = candidates.ReleasePresentationCount;
                keyboard.ShowDisplayMode(mode);
                window.UpdateLayout();
                Assert.False(keyboard.CanPresentCandidates);
                Assert.False(keyboard.HasCandidates);
                Assert.True(candidates.ReleasePresentationCount > released);
                InputMethodState imeState = keyboard.ImeState;
                int activations = inputMethods.ActivationCount;
                var back = Assert.Single(
                    Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                    button => button.IsVisible && button.Tag is SoftKeyboardDisplayMode.Letters);
                back.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, back));
                Assert.Equal(SoftKeyboardDisplayMode.Letters, keyboard.DisplayMode);
                Assert.Equal(imeState, keyboard.ImeState);
                Assert.Equal(activations, inputMethods.ActivationCount);
                Assert.Same(editor, Keyboard.FocusedElement);

                keyboard.ShowDisplayMode(mode);
                window.UpdateLayout();
                var close = Assert.Single(
                    Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                    button => button.IsVisible && button.Tag is SoftKeyboardKeyKind.Close);
                close.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, close));
                Assert.False(keyboard.IsOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InterruptedHold_DoesNotSendFurtherInput(bool disable)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _)) { IsOpen = true };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            try
            {
                window.Show();
                Assert.True(editor.Focus());
                typeof(SoftKeyboard).GetField("_heldKey", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(keyboard, FindKey(keyboard, Key.Back));
                typeof(SoftKeyboard).GetMethod("OnHoldTimerTick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, [null, EventArgs.Empty]);
                int count = input.VirtualKeys.Count;
                if (disable)
                {
                    keyboard.IsEnabled = false;
                    InvokeKey(keyboard, FindKey(keyboard, Key.Back));
                }
                else
                {
                    keyboard.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                    {
                        RoutedEvent = Mouse.LostMouseCaptureEvent,
                    });
                }

                typeof(SoftKeyboard).GetMethod("OnRepeatTimerTick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, [null, EventArgs.Empty]);
                Assert.Equal(count, input.VirtualKeys.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FocusOutsideScope_ReleasesCandidatesAndRejectsStaleCommit()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out var candidates, out _)) { IsOpen = true };
            var editor = new System.Windows.Controls.TextBox();
            var other = new System.Windows.Controls.TextBox();
            var scope = new StackPanel();
            scope.Children.Add(editor);
            scope.Children.Add(keyboard);
            keyboard.TargetScope = scope;
            keyboard.Target = editor;
            var root = new StackPanel();
            root.Children.Add(scope);
            root.Children.Add(other);
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            try
            {
                window.Show();
                Assert.True(editor.Focus());
                Assert.True(keyboard.CanPresentCandidates);
                candidates.Publish(keyboard, 1, 1, "candidate");
                SoftKeyboardCandidate stale = keyboard.Candidates[0];
                Assert.True(other.Focus());
                Assert.False(keyboard.CanPresentCandidates);
                Assert.False(keyboard.HasCandidates);
                typeof(SoftKeyboard).GetMethod("CommitCandidate", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, [stale]);
                Assert.Null(candidates.LastCommittedIndex);
                Assert.Same(other, Keyboard.FocusedElement);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SurfaceTransition_CancelsActiveCompositionWithEscape()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _)) { IsOpen = true };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            try
            {
                window.Show();
                Assert.True(editor.Focus());
                var composition = new TextComposition(
                    InputManager.Current,
                    editor,
                    string.Empty,
                    TextCompositionAutoComplete.Off
                );
                _ = TextCompositionManager.StartComposition(composition);

                keyboard.ShowDisplayMode(SoftKeyboardDisplayMode.Emoji);

                Assert.Equal(SoftKeyboardDisplayMode.Emoji, keyboard.DisplayMode);
                Assert.Contains((Key.Escape, false), input.VirtualKeys);
                composition.Complete();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DirectUnicodeInput_QueuesCompositionCancellationBeforeText()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _)) { IsOpen = true };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            try
            {
                window.Show();
                Assert.True(editor.Focus());
                keyboard.ShowDisplayMode(SoftKeyboardDisplayMode.Symbols);
                input.Operations.Clear();
                var composition = new TextComposition(
                    InputManager.Current,
                    editor,
                    string.Empty,
                    TextCompositionAutoComplete.Off
                );
                _ = TextCompositionManager.StartComposition(composition);

                InvokeKey(
                    keyboard,
                    new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "★", "Star", text: "★")
                );

                Assert.Equal(["Key:Escape", "Text:★"], input.Operations);
                composition.Complete();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CloseKey_RequestsCompositionCancellationOnlyOnce()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _)) { IsOpen = true };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            try
            {
                window.Show();
                Assert.True(editor.Focus());
                var composition = new TextComposition(
                    InputManager.Current,
                    editor,
                    string.Empty,
                    TextCompositionAutoComplete.Off
                );
                _ = TextCompositionManager.StartComposition(composition);

                InvokeKey(keyboard, new SoftKeyboardKey(SoftKeyboardKeyKind.Close, "Close", "Close"));

                Assert.False(keyboard.IsOpen);
                Assert.Equal(1, input.VirtualKeys.Count(item => item == (Key.Escape, false)));
                composition.Complete();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ImeToggle_WaitsForCompositionCancellationToComplete()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _)) { IsOpen = true };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            try
            {
                window.Show();
                Assert.True(editor.Focus());
                Assert.Equal(InputMethodState.On, keyboard.ImeState);
                var composition = new TextComposition(
                    InputManager.Current,
                    editor,
                    string.Empty,
                    TextCompositionAutoComplete.Off
                );
                _ = TextCompositionManager.StartComposition(composition);

                InvokeKey(keyboard, new SoftKeyboardKey(SoftKeyboardKeyKind.ToggleIme, "ABC", "Toggle IME"));

                Assert.Contains((Key.Escape, false), input.VirtualKeys);
                Assert.Equal(InputMethodState.On, keyboard.ImeState);
                composition.Complete();
                PumpDispatcher();
                Assert.Equal(InputMethodState.Off, keyboard.ImeState);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void WindowReactivation_QueuesCandidateBridgeRefresh()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out var candidates, out _)) { IsOpen = true };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window { Content = root, Width = 700, Height = 500, ShowInTaskbar = false };
            try
            {
                window.Show();
                Assert.True(editor.Focus());
                typeof(SoftKeyboard).GetMethod("OnWindowDeactivated", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, [window, EventArgs.Empty]);
                int refreshesBeforeActivation = candidates.RefreshCount;

                typeof(SoftKeyboard).GetMethod("OnWindowActivated", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, [window, EventArgs.Empty]);
                PumpDispatcher();

                Assert.True(candidates.RefreshCount > refreshesBeforeActivation);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void InvokeKey(SoftKeyboard keyboard, SoftKeyboardKey key)
    {
        typeof(SoftKeyboard)
            .GetMethod("InvokeKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(keyboard, [key]);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T result)
            {
                yield return result;
            }

            foreach (T descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void PumpDispatcher()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(100),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            (_, _) => frame.Continue = false,
            System.Windows.Threading.Dispatcher.CurrentDispatcher
        );
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    private sealed class RecordingInputBackend : ISoftKeyboardInputBackend
    {
        internal List<(Key Key, bool Shift)> VirtualKeys { get; } = [];

        internal List<string> Operations { get; } = [];

        public bool SendVirtualKey(Key key, bool shift)
        {
            VirtualKeys.Add((key, shift));
            Operations.Add($"Key:{key}");
            return true;
        }

        public bool SendUnicode(string text)
        {
            Operations.Add($"Text:{text}");
            return true;
        }

        public bool IsHostForeground(IntPtr host) => true;
    }

    private sealed class FakeInputPaneBackend : ISoftKeyboardInputPaneBackend
    {
        internal SoftKeyboardSystemKeyboardSuppressionState Result { get; set; } =
            SoftKeyboardSystemKeyboardSuppressionState.Hidden;

        internal int HideCount { get; private set; }

        internal int ShowCount { get; private set; }

        public SoftKeyboardSystemKeyboardSuppressionState TryHide(IntPtr host)
        {
            HideCount++;
            return Result;
        }

        public bool TryShow(IntPtr host)
        {
            ShowCount++;
            return true;
        }
    }

    private sealed class FakeInputMethodBackend : ISoftKeyboardInputMethodBackend
    {
        private static readonly Guid FakeClassId = new("D9400B6B-9E48-4D79-90A6-62A3C085B3C2");
        private static readonly Guid FakeProfileId = new("451C2C15-CE41-49D1-A05E-D6474366F501");
        private InputMethodState _imeState = InputMethodState.On;
        private bool _profileActive = true;

        internal int ActivationCount { get; private set; }

        public void Refresh(
            ObservableCollection<SoftKeyboardInputMethod> destination,
            out SoftKeyboardInputMethod? active
        )
        {
            destination.Clear();
            var profile = new SoftKeyboardInputMethod(FakeClassId, FakeProfileId, 0x0804, "Test IME", _profileActive);
            destination.Add(profile);
            active = _profileActive ? profile : null;
        }

        public bool TryActivate(SoftKeyboardInputMethod profile)
        {
            ActivationCount++;
            _profileActive = true;
            return true;
        }

        public InputMethodState GetImeState() => _imeState;

        public bool TrySetImeState(InputMethodState state)
        {
            _imeState = state;
            return true;
        }

        internal void UseDirectKeyboardLayout()
        {
            _imeState = InputMethodState.Off;
            _profileActive = false;
        }
    }

    private sealed class FakeCandidateBackend : ISoftKeyboardCandidateBackend
    {
        internal uint? LastCommittedIndex { get; private set; }

        internal int? LastPageDelta { get; private set; }

        internal int RefreshCount { get; private set; }

        internal int ReleasePresentationCount { get; private set; }

        public void Register(SoftKeyboard keyboard)
        {
        }

        public void Unregister(SoftKeyboard keyboard)
        {
        }

        public void Refresh(SoftKeyboard keyboard)
        {
            RefreshCount++;
        }

        public void ReleasePresentation(SoftKeyboard keyboard)
        {
            ReleasePresentationCount++;
            keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.None);
        }

        public bool TryMovePage(SoftKeyboard keyboard, int delta)
        {
            LastPageDelta = delta;
            return true;
        }

        public bool TryCommit(SoftKeyboard keyboard, uint index)
        {
            LastCommittedIndex = index;
            return true;
        }

        internal void Publish(SoftKeyboard keyboard, uint pageNumber, uint pageCount, params string[] values)
        {
            keyboard.ReplaceCandidates(
                values.Select((value, index) => new SoftKeyboardCandidate((uint)index, value, index == 0)),
                SoftKeyboardCandidatePresentationState.Embedded,
                pageNumber,
                pageCount
            );
        }
    }
}
