// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Internal;

/// <summary>
/// Resolves labels without changing the process input language or retaining dead-key state.
/// </summary>
internal static class KeyboardLayoutLabels
{
    internal static IntPtr GetCurrentLayout()
    {
        return PInvoke.GetKeyboardLayout(0);
    }

    internal static unsafe string GetLabel(Key key, bool shifted, IntPtr keyboardLayoutHandle, bool kana = false)
    {
        var   virtualKey = KeyInterop.VirtualKeyFromKey(key);
        HKL   layout     = new(keyboardLayoutHandle);
        byte* state      = stackalloc byte[256];

        new Span<byte>(state, 256).Clear();

        if (kana)
        {
            state[0x15] = 1;
        }

        if (shifted)
        {
            state[0x10] = 0x80;
        }

        const int bufferLength = 8;
        char*     buffer       = stackalloc char[bufferLength];

        var scanCode = PInvoke.MapVirtualKeyEx(
            (uint)virtualKey,
            MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC,
            layout
        );

        // Flag 4 (Windows 10 1607+) preserves the keyboard buffer: painting a cap must
        // never consume a hardware dead key or manufacture a composition transition.
        // GetKeyboardLayout returns a borrowed HKL. The pointer overload accepts it directly
        // and keeps both buffers scoped to this call without wrapping the layout in an owning handle.
        var length = PInvoke.ToUnicodeEx(
            (uint)virtualKey,
            scanCode,
            state,
            buffer,
            bufferLength,
            4,
            layout
        );

        if (length != 1 || char.IsControl(buffer[0]))
        {
            var fallback = key.ToString();
            return shifted ? fallback.ToUpperInvariant() : fallback.ToLowerInvariant();
        }

        return buffer[0].ToString();
    }
}
