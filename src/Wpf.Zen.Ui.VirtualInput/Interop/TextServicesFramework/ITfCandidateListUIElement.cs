// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Reads candidate text, selection and page metadata from a TSF UI element.
/// </summary>
/// <remarks>
/// Inherited UI-element slots precede candidate slots; indices refer to the complete list.
/// </remarks>
[ComImport]
[Guid("EA1EA138-19DF-11D7-A6D2-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfCandidateListUIElement
{
    /// <summary>
    /// Reads the native object description as a COM BSTR.
    /// </summary>
    [PreserveSig]
    int GetDescription([MarshalAs(UnmanagedType.BStr)] out string? description);

    /// <summary>
    /// Reads the stable identifier supplied by this UI element.
    /// </summary>
    [PreserveSig]
    int GetGuid(out Guid guid);

    /// <summary>
    /// Requests native surface visibility using a Win32 BOOL.
    /// </summary>
    [PreserveSig]
    int Show(int show);

    /// <summary>
    /// Reads the native surface visibility as a Win32 BOOL.
    /// </summary>
    [PreserveSig]
    int IsShown(out int show);

    /// <summary>
    /// Reports which candidate-list facets changed since the previous update.
    /// </summary>
    [PreserveSig]
    int GetUpdatedFlags(out uint flags);

    /// <summary>
    /// Acquires the document that owns this candidate list for focused-document validation.
    /// </summary>
    [PreserveSig]
    int GetDocumentMgr([MarshalAs(UnmanagedType.IUnknown)] out object? documentManager);

    /// <summary>
    /// Reads the size of the complete candidate list before validating an absolute index.
    /// </summary>
    [PreserveSig]
    int GetCount(out uint count);

    /// <summary>
    /// Reads the selected absolute index in the complete candidate list.
    /// </summary>
    [PreserveSig]
    int GetSelection(out uint index);

    /// <summary>
    /// Reads candidate text at an absolute list index for the managed snapshot.
    /// </summary>
    [PreserveSig]
    int GetString(uint index, [MarshalAs(UnmanagedType.BStr)] out string? text);

    /// <summary>
    /// Copies absolute page starts into caller-owned storage and reports the page count.
    /// </summary>
    [PreserveSig]
    int GetPageIndex(IntPtr indices, uint size, out uint pageCount);

    /// <summary>
    /// Supplies absolute page starts from caller-owned native storage.
    /// </summary>
    [PreserveSig]
    int SetPageIndex(IntPtr indices, uint pageCount);

    /// <summary>
    /// Reads the zero-based page number used to select the visible candidate range.
    /// </summary>
    [PreserveSig]
    int GetCurrentPage(out uint page);
}