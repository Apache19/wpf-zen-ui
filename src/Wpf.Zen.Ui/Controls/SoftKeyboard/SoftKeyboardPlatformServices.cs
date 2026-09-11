// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Collects native boundaries so keyboard behavior can be tested without sending process-global input.
/// </summary>
/// <remarks>
/// The type is internal because replacing platform services is a test seam rather than an application
/// extension point. Public customization remains focused on layouts and target adapters.
/// </remarks>
internal sealed class SoftKeyboardPlatformServices
{
    internal static SoftKeyboardPlatformServices Default { get; } = new(
        new WindowsInputBackend(),
        new WindowsInputPaneBackend(),
        new TsfInputMethodService(),
        new TsfCandidateBackend()
    );

    internal SoftKeyboardPlatformServices(
        ISoftKeyboardInputBackend input,
        ISoftKeyboardInputPaneBackend inputPane,
        ISoftKeyboardInputMethodBackend inputMethods,
        ISoftKeyboardCandidateBackend candidates)
    {
        Input = input ?? throw new ArgumentNullException(nameof(input));
        InputPane = inputPane ?? throw new ArgumentNullException(nameof(inputPane));
        InputMethods = inputMethods ?? throw new ArgumentNullException(nameof(inputMethods));
        Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
    }

    internal ISoftKeyboardInputBackend Input { get; }

    internal ISoftKeyboardInputPaneBackend InputPane { get; }

    internal ISoftKeyboardInputMethodBackend InputMethods { get; }

    internal ISoftKeyboardCandidateBackend Candidates { get; }

    private sealed class WindowsInputBackend : ISoftKeyboardInputBackend
    {
        public bool SendVirtualKey(Key key, bool shift)
        {
            return SoftKeyboardInputInjector.SendVirtualKey(key, shift);
        }

        public bool SendUnicode(string text)
        {
            return SoftKeyboardInputInjector.SendUnicode(text);
        }

        public bool IsHostForeground(IntPtr host)
        {
            HWND foreground = PInvoke.GetForegroundWindow();
            return host != IntPtr.Zero
                && !foreground.IsNull
                && PInvoke.GetAncestor((HWND)host, GET_ANCESTOR_FLAGS.GA_ROOT)
                    == PInvoke.GetAncestor(foreground, GET_ANCESTOR_FLAGS.GA_ROOT);
        }
    }

    private sealed class WindowsInputPaneBackend : ISoftKeyboardInputPaneBackend
    {
        public SoftKeyboardSystemKeyboardSuppressionState TryHide(IntPtr host)
        {
            return SystemInputPaneController.TryHide(host);
        }

        public bool TryShow(IntPtr host)
        {
            return SystemInputPaneController.TryShow(host);
        }
    }

    private sealed class TsfCandidateBackend : ISoftKeyboardCandidateBackend
    {
        public void Register(SoftKeyboard keyboard)
        {
            SoftKeyboardTsfCandidateBridge.Register(keyboard);
        }

        public void Unregister(SoftKeyboard keyboard)
        {
            SoftKeyboardTsfCandidateBridge.Unregister(keyboard);
        }

        public void Refresh(SoftKeyboard keyboard)
        {
            SoftKeyboardTsfCandidateBridge.Refresh(keyboard);
        }

        public void ReleasePresentation(SoftKeyboard keyboard)
        {
            SoftKeyboardTsfCandidateBridge.ReleasePresentation(keyboard);
        }

        public bool TryMovePage(SoftKeyboard keyboard, int delta)
        {
            return SoftKeyboardTsfCandidateBridge.TryMovePage(keyboard, delta);
        }

        public bool TryCommit(SoftKeyboard keyboard, uint index)
        {
            return SoftKeyboardTsfCandidateBridge.TryCommit(keyboard, index);
        }
    }
}

#pragma warning disable SA1402 // The internal test seam is kept together with its four narrow contracts.
internal interface ISoftKeyboardInputBackend
{
    bool SendVirtualKey(Key key, bool shift);

    bool SendUnicode(string text);

    bool IsHostForeground(IntPtr host);
}

internal interface ISoftKeyboardInputPaneBackend
{
    SoftKeyboardSystemKeyboardSuppressionState TryHide(IntPtr host);

    bool TryShow(IntPtr host);
}

internal interface ISoftKeyboardInputMethodBackend
{
    void Refresh(ObservableCollection<SoftKeyboardInputMethod> destination, out SoftKeyboardInputMethod? active);

    bool TryActivate(SoftKeyboardInputMethod profile);

    InputMethodState GetImeState();

    bool TrySetImeState(InputMethodState state);
}

internal interface ISoftKeyboardCandidateBackend
{
    void Register(SoftKeyboard keyboard);

    void Unregister(SoftKeyboard keyboard);

    void Refresh(SoftKeyboard keyboard);

    void ReleasePresentation(SoftKeyboard keyboard);

    bool TryMovePage(SoftKeyboard keyboard, int delta);

    bool TryCommit(SoftKeyboard keyboard, uint index);
}
#pragma warning restore SA1402
