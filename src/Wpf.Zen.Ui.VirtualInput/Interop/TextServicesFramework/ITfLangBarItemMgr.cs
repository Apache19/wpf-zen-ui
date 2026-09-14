// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Projects language-bar lookup slots in the order defined by ctfutb.h.
/// </summary>
[ComImport]
[Guid("BA468C55-9956-4FB1-A59D-52A7DD7CC6AA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfLangBarItemMgr
{
    [PreserveSig]
    int EnumItems(out IntPtr enumerator);

    [PreserveSig]
    int GetItem(in Guid id, [MarshalAs(UnmanagedType.IUnknown)] out object? item);
}
