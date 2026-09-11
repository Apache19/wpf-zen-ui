// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.SoftKeyboardControl;

public class SoftKeyboardKeyControlTests
{
    [Fact]
    public void AlternateHint_IsPresentedSeparatelyAndDoesNotChangeSubmittedText()
    {
        WithCap((cap, requests, _) =>
        {
            cap.Key = new SoftKeyboardKey(SoftKeyboardKeyKind.Text, ".", "Period", text: ".",
                alternates: ["?", "!"], alternateHint: "?");
            cap.UpdateLayout();
            var hint = Assert.IsType<System.Windows.Controls.TextBlock>(cap.Template.FindName("AlternateHint", cap));
            Assert.Equal("?", hint.Text);
            Assert.Equal(Visibility.Visible, hint.Visibility);
            Assert.Equal(".", cap.Content);
            cap.Invoke();
            Assert.Equal(".", Assert.Single(requests).Key.Text);
            Assert.Null(requests[0].AlternateText);
            requests.Clear();
            cap.BeginPress();
            cap.AdvanceHold();
            cap.SelectAlternative("!");
            Assert.Equal("!", Assert.Single(requests).AlternateText);

            cap.Key = new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "x", "x", text: "x");
            cap.UpdateLayout();
            Assert.Equal(Visibility.Collapsed, hint.Visibility);
            Assert.Equal("x", cap.Content);
        });
    }

    [Fact]
    public void ShortPress_SubmitsOneRequestWithoutAKeyboardOrFocusChange()
    {
        WithCap((cap, requests, editor) =>
        {
            cap.BeginPress();
            Assert.True(cap.IsPressed);
            cap.CompletePress(true);
            cap.CompletePress(true);
            Assert.Same(cap.Key, Assert.Single(requests).Key);
            Assert.Null(requests[0].AlternateText);
            Assert.Same(editor, Keyboard.FocusedElement);
            Assert.False(cap.IsPressed);
        });
    }

    [Fact]
    public void Repeat_SubmitsPerTickAndSuppressesRelease()
    {
        WithCap((cap, requests, _) =>
        {
            cap.Key = new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "Back", "Backspace", Key.Back, isRepeatEnabled: true);
            cap.BeginPress();
            cap.AdvanceHold();
            cap.AdvanceHold();
            cap.CompletePress(true);
            cap.AdvanceHold();
            Assert.Equal(2, requests.Count);
        });
    }

    [Fact]
    public void RepeatRequest_CanSynchronouslyCancelWithoutRestarting()
    {
        WithCap((cap, requests, _) =>
        {
            cap.Key = new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "x", "x", text: "x", isRepeatEnabled: true);
            cap.InputRequested += (_, _) => cap.CancelInteraction();
            cap.BeginPress();
            cap.AdvanceHold();
            cap.AdvanceHold();
            cap.CompletePress(true);
            Assert.Single(requests);
            Assert.False(cap.IsPressed);
        });
    }

    [Fact]
    public void Alternatives_TakePrecedenceAndCommitOnlyTheSelectedValue()
    {
        WithCap((cap, requests, _) =>
        {
            cap.Key = new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "e", "e", text: "e", alternates: ["é", "è"], isRepeatEnabled: true);
            cap.BeginPress();
            cap.AdvanceHold();
            Assert.True(cap.IsAlternatePopupOpen);
            Assert.Empty(requests);
            cap.CompletePress(true);
            cap.SelectAlternative("unlisted");
            Assert.Empty(requests);
            cap.SelectAlternative("é");
            cap.SelectAlternative("é");
            cap.CompletePress(true);
            Assert.Equal("é", Assert.Single(requests).AlternateText);
            Assert.False(cap.IsAlternatePopupOpen);
        });
    }

    [Fact]
    public void ReleaseOverAlternative_SubmitsWithoutPrimaryAction()
    {
        WithCap((cap, requests, _) =>
        {
            cap.BeginPress();
            cap.AdvanceHold();
            cap.CompletePress(false, "é");
            Assert.Equal("é", Assert.Single(requests).AlternateText);
        });
    }

    [Fact]
    public void MissingPopup_FallsBackToPrimaryRelease()
    {
        WithCap((cap, requests, _) =>
        {
            cap.Template = new ControlTemplate(typeof(SoftKeyboardKeyControl));
            cap.ApplyTemplate();
            cap.BeginPress();
            cap.AdvanceHold();
            Assert.False(cap.IsAlternatePopupOpen);
            cap.CompletePress(true);
            Assert.Null(Assert.Single(requests).AlternateText);
        });
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("disable")]
    [InlineData("hide")]
    [InlineData("model")]
    [InlineData("template")]
    [InlineData("unload")]
    [InlineData("capture")]
    public void Cancellation_RejectsLateSelectionAndRelease(string reason)
    {
        WithCap((cap, requests, editor) =>
        {
            cap.BeginPress();
            cap.AdvanceHold();
            Assert.True(cap.IsAlternatePopupOpen);
            switch (reason)
            {
                case "cancel": cap.CancelInteraction(); break;
                case "disable": cap.IsEnabled = false; break;
                case "hide": cap.Visibility = Visibility.Hidden; break;
                case "model": cap.Key = new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "a", "a", text: "a"); break;
                case "template": cap.Template = new ControlTemplate(typeof(SoftKeyboardKeyControl)); break;
                case "unload": ((Panel)cap.Parent).Children.Remove(cap); break;
                case "capture":
                    Assert.True(cap.CaptureMouse());
                    Assert.True(editor.CaptureMouse());
                    editor.ReleaseMouseCapture();
                    break;
            }

            DrainDispatcher();
            cap.AdvanceHold();
            cap.SelectAlternative("é");
            cap.CompletePress(true);
            Assert.Empty(requests);
            Assert.False(cap.IsAlternatePopupOpen);
            Assert.False(cap.IsPressed);
        });
    }

    [Fact]
    public void PopupDismissal_CancelsWithoutInput()
    {
        WithCap((cap, requests, _) =>
        {
            cap.BeginPress();
            cap.AdvanceHold();
            var popup = (Popup)cap.Template.FindName("PART_AlternatePopup", cap);
            popup.IsOpen = false;
            cap.CompletePress(true);
            Assert.Empty(requests);
            Assert.False(cap.IsAlternatePopupOpen);
        });
    }

    [Fact]
    public void HostCanRejectInteractionBeforeAnyRequest()
    {
        WithCap((cap, requests, _) =>
        {
            cap.InteractionStarted += (_, _) => cap.CancelInteraction();
            cap.BeginPress();
            cap.AdvanceHold();
            cap.CompletePress(true);
            cap.Invoke();
            Assert.Empty(requests);
        });
    }

    [Fact]
    public void AutomationInvoke_UsesTheSameRequestAndKeepsEditorFocus()
    {
        WithCap((cap, requests, editor) =>
        {
            var peer = UIElementAutomationPeer.CreatePeerForElement(cap)!;
            Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
            Assert.Equal("e", peer.GetName());
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            DrainDispatcher();
            Assert.Single(requests);
            Assert.Same(editor, Keyboard.FocusedElement);
        });
    }

    [Fact]
    public void Reload_ReconnectsTheExistingPopupTemplate()
    {
        WithCap((cap, requests, _) =>
        {
            var parent = (Panel)cap.Parent;
            parent.Children.Remove(cap);
            DrainDispatcher();
            parent.Children.Add(cap);
            DrainDispatcher();
            cap.BeginPress();
            cap.AdvanceHold();
            cap.SelectAlternative("è");
            Assert.Equal("è", Assert.Single(requests).AlternateText);
        });
    }

    [Fact]
    public void ReleaseOutside_CancelsPrimaryAction()
    {
        WithCap((cap, requests, _) =>
        {
            cap.BeginPress();
            cap.CompletePress(false);
            Assert.Empty(requests);
        });
    }

    private static void WithCap(Action<SoftKeyboardKeyControl, List<SoftKeyboardInputRequestedEventArgs>, System.Windows.Controls.TextBox> test)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new System.Windows.Controls.TextBox();
            var cap = new SoftKeyboardKeyControl
            {
                Key = new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "e", "e", text: "e", alternates: ["é", "è"]),
                Width = 80,
                Height = 48,
            };
            var requests = new List<SoftKeyboardInputRequestedEventArgs>();
            // Listen on an ancestor, verifying the typed routed event contract as well as the cap.
            var root = new StackPanel();
            root.AddHandler(
                SoftKeyboardKeyControl.InputRequestedEvent,
                new EventHandler<SoftKeyboardInputRequestedEventArgs>((_, e) => requests.Add(e)));
            root.Children.Add(editor);
            root.Children.Add(cap);
            var window = new Window { Content = root, Width = 400, Height = 250, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.True(editor.Focus());
                test(cap, requests, editor);
            }
            finally
            {
                cap.CancelInteraction();
                window.Close();
            }
        });
    }

    private static void DrainDispatcher() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
