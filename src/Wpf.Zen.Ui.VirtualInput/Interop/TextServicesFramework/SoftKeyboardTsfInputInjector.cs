// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Delivers an unmodified composition key through WPF's active TSF keystroke manager.
/// </summary>
internal static class SoftKeyboardTsfInputInjector
{
    private const int KeyDownData = 1;
    private const int KeyUpData = unchecked((int)0xC0000001);

    /// <summary>
    /// Sends one key only when the active text service claims its key-down event.
    /// </summary>
    /// <remarks>
    /// WPF passes a repeat count of one and no scan code for ordinary keys. Matching that contract
    /// is intentional: the text service consumes semantic virtual keys, while SendInput remains the
    /// fallback for keys that belong to WPF editing commands or the application.
    /// </remarks>
    internal static bool TrySendVirtualKey(Key key)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0
            || WpfTextServicesAccessor.TryGetThreadManager() is not ITfKeystrokeMgr manager)
        {
            return false;
        }

        try
        {
            if (manager.TestKeyDown(virtualKey, KeyDownData, out var testEaten) < 0
                || !testEaten)
            {
                return false;
            }

            if (manager.KeyDown(virtualKey, KeyDownData, out var downEaten) < 0
                || !downEaten)
            {
                return false;
            }

            if (manager.TestKeyUp(virtualKey, KeyUpData, out var testUpEaten) >= 0
                && testUpEaten)
            {
                _ = manager.KeyUp(virtualKey, KeyUpData, out _);
            }

            return true;
        }
        catch (Exception exception) when (
            exception is COMException
                or InvalidCastException
                or ObjectDisposedException)
        {
            return false;
        }
    }
}
