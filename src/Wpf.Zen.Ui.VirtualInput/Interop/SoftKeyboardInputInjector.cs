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
    internal static bool SendVirtualKey(Key key, ModifierKeys modifiers)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey is <= 0 or > ushort.MaxValue)
        {
            return false;
        }

        const ModifierKeys SupportedModifiers = ModifierKeys.Shift
            | ModifierKeys.Control
            | ModifierKeys.Alt
            | ModifierKeys.Windows;

        if ((modifiers & ~SupportedModifiers) != ModifierKeys.None)
        {
            throw new ArgumentOutOfRangeException(nameof(modifiers));
        }

        var modifierKeys = GetModifierKeys(modifiers);
        var inputs = new List<INPUT>((modifierKeys.Length * 2) + 2);
        foreach (var modifierKey in modifierKeys)
        {
            inputs.Add(CreateVirtualKeyInput(KeyInterop.VirtualKeyFromKey(modifierKey), false, false));
        }

        var extended = IsExtendedKey(key);

        inputs.Add(CreateVirtualKeyInput(virtualKey, false, extended));
        inputs.Add(CreateVirtualKeyInput(virtualKey, true, extended));

        for (var index = modifierKeys.Length - 1; index >= 0; index--)
        {
            inputs.Add(CreateVirtualKeyInput(KeyInterop.VirtualKeyFromKey(modifierKeys[index]), true, false));
        }

        var sent = Send(inputs);
        if (!sent)
        {
            // SendInput can report a partial insertion. A partial chord may have delivered one or
            // more key-down records without their matching key-up records, so always issue an
            // idempotent release sequence before returning failure.
            var releases = new List<INPUT>(modifierKeys.Length + 1)
            {
                CreateVirtualKeyInput(virtualKey, true, extended),
            };

            for (var index = modifierKeys.Length - 1; index >= 0; index--)
            {
                releases.Add(
                    CreateVirtualKeyInput(KeyInterop.VirtualKeyFromKey(modifierKeys[index]), true, false)
                );
            }

            Send(releases);
        }

        return sent;
    }

    private static Key[] GetModifierKeys(ModifierKeys modifiers)
    {
        var keys = new List<Key>(4);
        if ((modifiers & ModifierKeys.Control) != 0)
        {
            keys.Add(Key.LeftCtrl);
        }

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            keys.Add(Key.LeftShift);
        }

        if ((modifiers & ModifierKeys.Alt) != 0)
        {
            keys.Add(Key.LeftAlt);
        }

        if ((modifiers & ModifierKeys.Windows) != 0)
        {
            keys.Add(Key.LWin);
        }

        return [.. keys];
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
        var scanCode = PInvoke.MapVirtualKeyEx(
            (uint)virtualKey,
            MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC,
            PInvoke.GetKeyboardLayout(0)
        );

        return new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new KEYBDINPUT
                {
                    wVk = (VIRTUAL_KEY)virtualKey,
                    wScan = unchecked((ushort)scanCode),
                    // A mapped scan code makes ordinary keys follow the same path as physical
                    // keyboard input. Retain the virtual-key fallback for keys that have no
                    // representation in the active keyboard layout.
                    dwFlags = (scanCode != 0 ? KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE : 0)
                              | (isKeyUp ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0)
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
        return key is Key.RightAlt or Key.RightCtrl or Key.NumLock or Key.Insert or Key.Delete
            or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Up or Key.Down
            or Key.Left or Key.Right or Key.Apps or Key.LWin or Key.RWin;
    }
}
