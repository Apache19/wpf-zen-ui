// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

#pragma warning disable CA1060 // The control-prefixed name prevents collision with the library's shared NativeMethods type.

/// <summary>
/// Contains the narrow Win32 ABI used by the soft-keyboard input boundary.
/// </summary>
/// <remarks>
/// These calls are kept independent from the control and layout code so a failure cannot leave the
/// visual state half-updated. All APIs in this type have existed since before the oldest OS supported
/// by the assembly; Windows Runtime input-pane calls use a separate feature-detected boundary.
/// </remarks>
internal static class SoftKeyboardNativeMethods
{
    internal const uint InputKeyboard = 1;
    internal const uint KeyEventExtendedKey = 0x0001;
    internal const uint KeyEventKeyUp = 0x0002;
    internal const uint KeyEventUnicode = 0x0004;
    internal const uint MapVkToVsc = 0;
    internal const uint GaRoot = 2;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(
        uint inputCount,
        [MarshalAs(UnmanagedType.LPArray), In] Input[] inputs,
        int inputSize
    );

    [DllImport("user32.dll")]
    internal static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    internal static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr keyboardLayout);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int ToUnicodeEx(
        uint virtualKey,
        uint scanCode,
        byte[] keyboardState,
        [Out] StringBuilder buffer,
        int bufferLength,
        uint flags,
        IntPtr keyboardLayout
    );

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        internal uint Type;
        internal InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        // INPUT's anonymous union is sized by its largest member. KEYBDINPUT alone is only
        // 24 bytes on x64, while MOUSEINPUT is 32 bytes; omitting the latter makes the containing
        // INPUT 32 bytes instead of the ABI-required 40 and causes SendInput to reject every call.
        // Keeping the unused member here is therefore part of the native layout contract.
        [FieldOffset(0)]
        internal MouseInput Mouse;

        [FieldOffset(0)]
        internal KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        internal int X;
        internal int Y;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        internal ushort VirtualKey;
        internal ushort ScanCode;
        internal uint Flags;
        internal uint Time;
        internal UIntPtr ExtraInfo;
    }
}

#pragma warning restore CA1060
