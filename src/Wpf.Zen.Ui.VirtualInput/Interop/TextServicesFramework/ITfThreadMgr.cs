// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

[ComImport]
[Guid("AA80E801-2021-11D2-93E0-0060B067B86E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfThreadMgr
{
    [PreserveSig]
    int Activate(out uint clientId);

    [PreserveSig]
    int Deactivate();

    [PreserveSig]
    int CreateDocumentMgr([MarshalAs(UnmanagedType.IUnknown)] out object? documentManager);

    [PreserveSig]
    int EnumDocumentMgrs([MarshalAs(UnmanagedType.IUnknown)] out object? enumerator);

    [PreserveSig]
    int GetFocus([MarshalAs(UnmanagedType.IUnknown)] out object? documentManager);
}