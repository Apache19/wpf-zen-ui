// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Infrastructure;
using Wpf.Zen.Ui.VirtualInput.Input;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Hosts actual WPF controls while replacing native input, profiles, pane requests, and candidate registration.
/// </summary>
internal sealed class SoftKeyboardRegressionScope : IDisposable
{
    internal SoftKeyboardRegressionScope()
    {
        Method = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0409, "Test IME", true);
        Input = new RecordingInput();
        Keyboard = new SoftKeyboard(new SoftKeyboardPlatformServices(
            Input, new InputPane(), new InputMethods(Method), new BackendSoftKeyboardCandidateSource(new Candidates())
        ));
        Panel = new StackPanel();
        Editor = new TextBox();
        OtherEditor = new TextBox();
        Panel.Children.Add(Editor);
        Panel.Children.Add(OtherEditor);
        Panel.Children.Add(Keyboard);
        Keyboard.Target = Editor;
        Keyboard.IsOpen = true;
        Window = new Window { Content = Panel, ShowInTaskbar = false, Width = 700, Height = 400 };
        Window.Show();
        Assert.True(Editor.Focus());
        Window.UpdateLayout();
    }

    internal SoftKeyboardInputMethod Method { get; }
    internal RecordingInput Input { get; }
    internal SoftKeyboard Keyboard { get; }
    internal StackPanel Panel { get; }
    internal TextBox Editor { get; }
    internal TextBox OtherEditor { get; }
    internal Window Window { get; }

    /// <summary>
    /// Processes pending input/layout callbacks before a test starts or observes an asynchronous transition.
    /// </summary>
    internal async Task DrainAsync()
    {
        await Dispatcher.CurrentDispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Window.Close();
    }

    internal sealed class RecordingInput : ISoftKeyboardInputBackend
    {
        internal List<string> Text { get; } = [];

        /// <inheritdoc />
        public bool SendVirtualKey(Key key, ModifierKeys modifiers)
        {
            return true;
        }

        /// <inheritdoc />
        public bool SendUnicode(string text)
        {
            Text.Add(text);
            return true;
        }

        /// <inheritdoc />
        public bool IsHostForeground(IntPtr host)
        {
            return true;
        }
    }

    private sealed class InputPane : ISoftKeyboardInputPaneBackend
    {
        /// <inheritdoc />
        public SoftKeyboardSystemKeyboardSuppressionState TryHide(IntPtr host)
        {
            return SoftKeyboardSystemKeyboardSuppressionState.Unavailable;
        }

        /// <inheritdoc />
        public bool TryShow(IntPtr host)
        {
            return false;
        }
    }

    private sealed class InputMethods(SoftKeyboardInputMethod method) : ISoftKeyboardInputMethodBackend
    {
        /// <inheritdoc />
        public void Refresh(ObservableCollection<SoftKeyboardInputMethod> destination, out SoftKeyboardInputMethod? active)
        {
            destination.Clear();
            destination.Add(method);
            active = method;
        }

        /// <inheritdoc />
        public bool TryActivate(SoftKeyboardInputMethod profile)
        {
            return true;
        }

        /// <inheritdoc />
        public SoftKeyboardInputMode GetInputMode(SoftKeyboardInputMethod inputMethod)
        {
            return new SoftKeyboardInputMode("native", "中", "Native input", true, true);
        }

        /// <inheritdoc />
        public bool TrySwitchInputMode(
            SoftKeyboardInputMethod inputMethod,
            SoftKeyboardInputMode currentMode)
        {
            return true;
        }
    }

    private sealed class Candidates : ISoftKeyboardCandidateBackend
    {
        /// <inheritdoc />
        public void Register(SoftKeyboard keyboard)
        {
        }

        /// <inheritdoc />
        public void Unregister(SoftKeyboard keyboard)
        {
        }

        /// <inheritdoc />
        public void Refresh(SoftKeyboard keyboard)
        {
        }

        /// <inheritdoc />
        public void ReleasePresentation(SoftKeyboard keyboard)
        {
        }

        /// <inheritdoc />
        public bool TryMovePage(SoftKeyboard keyboard, int delta)
        {
            return false;
        }

        /// <inheritdoc />
        public bool TryCommit(SoftKeyboard keyboard, uint index)
        {
            return false;
        }
    }
}
