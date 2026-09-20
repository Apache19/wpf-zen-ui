// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Exposes the TSF keystroke manager implemented by WPF's dispatcher thread manager.
/// </summary>
[ComImport]
[Guid("AA80E7F0-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfKeystrokeMgr
{
    [PreserveSig]
    int AdviseKeyEventSink(
        uint clientId,
        [MarshalAs(UnmanagedType.IUnknown)] object sink,
        [MarshalAs(UnmanagedType.Bool)] bool foreground);

    [PreserveSig]
    int UnadviseKeyEventSink(uint clientId);

    [PreserveSig]
    int GetForeground(out Guid classId);

    /// <summary>
    /// Asks whether the active text service would consume the key-down event.
    /// </summary>
    [PreserveSig]
    int TestKeyDown(
        int virtualKey,
        int keyData,
        [MarshalAs(UnmanagedType.Bool)] out bool eaten);

    /// <summary>
    /// Asks whether the active text service would consume the key-up event.
    /// </summary>
    [PreserveSig]
    int TestKeyUp(
        int virtualKey,
        int keyData,
        [MarshalAs(UnmanagedType.Bool)] out bool eaten);

    /// <summary>
    /// Delivers the key-down event and reports whether the text service consumed it.
    /// </summary>
    [PreserveSig]
    int KeyDown(
        int virtualKey,
        int keyData,
        [MarshalAs(UnmanagedType.Bool)] out bool eaten);

    /// <summary>
    /// Delivers the key-up event and reports whether the text service consumed it.
    /// </summary>
    [PreserveSig]
    int KeyUp(
        int virtualKey,
        int keyData,
        [MarshalAs(UnmanagedType.Bool)] out bool eaten);
}
