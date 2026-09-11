// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

// ITfThreadMgrEx inherits the full ITfThreadMgr vtable. Unused slots are intentionally retained
// in SDK order so ActivateEx and GetActiveFlags cannot be dispatched to the wrong native method.
[ComImport]
[Guid("3E90ADE3-7594-4CB0-BB58-69628F5F458C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfThreadMgrEx
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

    [PreserveSig]
    int SetFocus([MarshalAs(UnmanagedType.IUnknown)] object documentManager);

    [PreserveSig]
    int AssociateFocus(
        IntPtr window,
        [MarshalAs(UnmanagedType.IUnknown)] object? newDocumentManager,
        [MarshalAs(UnmanagedType.IUnknown)] out object? previousDocumentManager
    );

    [PreserveSig]
    int IsThreadFocus(out int isFocused);

    [PreserveSig]
    int GetFunctionProvider(in Guid classId, [MarshalAs(UnmanagedType.IUnknown)] out object? provider);

    [PreserveSig]
    int EnumFunctionProviders([MarshalAs(UnmanagedType.IUnknown)] out object? enumerator);

    [PreserveSig]
    int GetGlobalCompartment([MarshalAs(UnmanagedType.IUnknown)] out object? compartmentManager);

    [PreserveSig]
    int ActivateEx(out uint clientId, uint flags);

    [PreserveSig]
    int GetActiveFlags(out uint flags);
}