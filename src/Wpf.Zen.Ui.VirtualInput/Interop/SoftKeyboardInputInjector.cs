// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Wpf.Zen.Ui.VirtualInput.Interop;

/// <summary>
/// Delivers input through the same Win32 keyboard stream used by hardware keyboards.
/// </summary>
/// <remarks>
/// Directly assigning TextBox.Text would bypass WPF editing commands, selection, undo grouping,
/// dead keys, and TSF. SendInput is process-global, so every call is preceded by a foreground-window
/// and focus validation in <see cref="SoftKeyboard"/>; this type never attempts to activate a window.
/// </remarks>
internal static class SoftKeyboardInputInjector
{
    internal static bool SendVirtualKey(Key key, bool shift)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey is <= 0 or > ushort.MaxValue)
        {
            return false;
        }

        var inputs = new List<INPUT>(shift ? 4 : 2);
        if (shift)
        {
            inputs.Add(
                CreateVirtualKeyInput(
                    KeyInterop.VirtualKeyFromKey(Key.LeftShift),
                    false,
                    false
                )
            );
        }

        var extended = IsExtendedKey(key);

        inputs.Add(CreateVirtualKeyInput(virtualKey, false, extended));
        inputs.Add(CreateVirtualKeyInput(virtualKey, true, extended));

        if (shift)
        {
            inputs.Add(
                CreateVirtualKeyInput(
                    KeyInterop.VirtualKeyFromKey(Key.LeftShift),
                    true,
                    false
                )
            );
        }

        return Send(inputs);
    }

    internal static bool SendUnicode(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var inputs = new List<INPUT>(text.Length * 2);
        foreach (var character in text)
        {
            inputs.Add(CreateUnicodeInput(character, false));
            inputs.Add(CreateUnicodeInput(character, true));
        }

        return Send(inputs);
    }

    private static bool Send(IReadOnlyCollection<INPUT> inputCollection)
    {
        var inputs = inputCollection.ToArray();

        // INPUT includes the complete native union, whose size differs between x86 and x64.
        var sent = PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>());

        return sent == inputs.Length;
    }

    private static INPUT CreateVirtualKeyInput(int virtualKey, bool isKeyUp, bool isExtended)
    {
        return new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new KEYBDINPUT
                {
                    wVk = (VIRTUAL_KEY)virtualKey,
                    dwFlags = (isKeyUp ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0)
                              | (isExtended ? KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY : 0),
                },
            },
        };
    }

    private static INPUT CreateUnicodeInput(char character, bool isKeyUp)
    {
        return new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new KEYBDINPUT
                {
                    wScan = character,
                    dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE
                              | (isKeyUp ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0),
                },
            },
        };
    }

    private static bool IsExtendedKey(Key key)
    {
        return key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Delete or Key.Insert
            or Key.Home or Key.End or Key.PageUp or Key.PageDown;
    }
}
