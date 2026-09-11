// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

[ComImport]
[Guid("EA1EA135-19DF-11D7-A6D2-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfUIElementMgr
{
    [PreserveSig]
    int BeginUIElement([MarshalAs(UnmanagedType.IUnknown)] object element, ref int show, out uint elementId);

    [PreserveSig]
    int UpdateUIElement(uint elementId);

    [PreserveSig]
    int EndUIElement(uint elementId);

    [PreserveSig]
    int GetUIElement(uint elementId, [MarshalAs(UnmanagedType.IUnknown)] out object? element);

    [PreserveSig]
    int EnumUIElements([MarshalAs(UnmanagedType.IUnknown)] out object? enumerator);
}