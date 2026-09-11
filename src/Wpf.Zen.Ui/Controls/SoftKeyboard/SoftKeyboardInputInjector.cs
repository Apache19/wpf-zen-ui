// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

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
        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0 || virtualKey > ushort.MaxValue)
        {
            return false;
        }

        var inputs = new List<SoftKeyboardNativeMethods.Input>(shift ? 4 : 2);
        if (shift)
        {
            inputs.Add(CreateVirtualKeyInput(KeyInterop.VirtualKeyFromKey(Key.LeftShift), false, false));
        }

        bool extended = IsExtendedKey(key);
        inputs.Add(CreateVirtualKeyInput(virtualKey, false, extended));
        inputs.Add(CreateVirtualKeyInput(virtualKey, true, extended));

        if (shift)
        {
            inputs.Add(CreateVirtualKeyInput(KeyInterop.VirtualKeyFromKey(Key.LeftShift), true, false));
        }

        return Send(inputs);
    }

    internal static bool SendUnicode(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var inputs = new List<SoftKeyboardNativeMethods.Input>(text.Length * 2);
        foreach (char character in text)
        {
            inputs.Add(CreateUnicodeInput(character, false));
            inputs.Add(CreateUnicodeInput(character, true));
        }

        return Send(inputs);
    }

    private static bool Send(IReadOnlyCollection<SoftKeyboardNativeMethods.Input> inputCollection)
    {
        var inputs = inputCollection.ToArray();
        uint sent = SoftKeyboardNativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf(typeof(SoftKeyboardNativeMethods.Input))
        );

        return sent == inputs.Length;
    }

    private static SoftKeyboardNativeMethods.Input CreateVirtualKeyInput(int virtualKey, bool isKeyUp, bool isExtended)
    {
        return new SoftKeyboardNativeMethods.Input
        {
            Type = SoftKeyboardNativeMethods.InputKeyboard,
            Data = new SoftKeyboardNativeMethods.InputUnion
            {
                Keyboard = new SoftKeyboardNativeMethods.KeyboardInput
                {
                    VirtualKey = (ushort)virtualKey,
                    Flags = (isKeyUp ? SoftKeyboardNativeMethods.KeyEventKeyUp : 0)
                        | (isExtended ? SoftKeyboardNativeMethods.KeyEventExtendedKey : 0),
                },
            },
        };
    }

    private static SoftKeyboardNativeMethods.Input CreateUnicodeInput(char character, bool isKeyUp)
    {
        return new SoftKeyboardNativeMethods.Input
        {
            Type = SoftKeyboardNativeMethods.InputKeyboard,
            Data = new SoftKeyboardNativeMethods.InputUnion
            {
                Keyboard = new SoftKeyboardNativeMethods.KeyboardInput
                {
                    ScanCode = character,
                    Flags = SoftKeyboardNativeMethods.KeyEventUnicode
                        | (isKeyUp ? SoftKeyboardNativeMethods.KeyEventKeyUp : 0),
                },
            },
        };
    }

    private static bool IsExtendedKey(Key key) =>
        key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Delete or Key.Insert
            or Key.Home or Key.End or Key.PageUp or Key.PageDown;
}
