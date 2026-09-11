// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

[ComImport]
[Guid("85FAD185-58CE-497A-9460-355366B64B9A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfCandidateListUIElementBehavior
{
    [PreserveSig]
    int GetDescription([MarshalAs(UnmanagedType.BStr)] out string? description);

    [PreserveSig]
    int GetGuid(out Guid guid);

    [PreserveSig]
    int Show(int show);

    [PreserveSig]
    int IsShown(out int show);

    [PreserveSig]
    int GetUpdatedFlags(out uint flags);

    [PreserveSig]
    int GetDocumentMgr([MarshalAs(UnmanagedType.IUnknown)] out object? documentManager);

    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int GetSelection(out uint index);

    [PreserveSig]
    int GetString(uint index, [MarshalAs(UnmanagedType.BStr)] out string? text);

    [PreserveSig]
    int GetPageIndex(IntPtr indices, uint size, out uint pageCount);

    [PreserveSig]
    int SetPageIndex(IntPtr indices, uint pageCount);

    [PreserveSig]
    int GetCurrentPage(out uint page);

    [PreserveSig]
    int SetSelection(uint index);

    [PreserveSig]
    int FinalizeCandidate();

    [PreserveSig]
    int Abort();
}