// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Implements guarded input injection against the foreground root window.
/// </summary>
internal sealed class WindowsInputBackend : ISoftKeyboardInputBackend
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

        return host != IntPtr.Zero && !foreground.IsNull
            && PInvoke.GetAncestor((HWND)host, GET_ANCESTOR_FLAGS.GA_ROOT)
                == PInvoke.GetAncestor(foreground, GET_ANCESTOR_FLAGS.GA_ROOT);
    }
}
