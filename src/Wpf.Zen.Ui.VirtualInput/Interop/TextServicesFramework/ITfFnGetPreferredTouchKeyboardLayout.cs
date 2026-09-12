// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

[ComImport]
[Guid("5F309A41-590A-4ACC-A97F-D8EFFF13FDFC")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfFnGetPreferredTouchKeyboardLayout
{
    // Inherited ITfFunction slot must precede GetLayout in the COM vtable.
    [PreserveSig]
    int GetDisplayName([MarshalAs(UnmanagedType.BStr)] out string name);

    [PreserveSig]
    int GetLayout(out SoftKeyboardTouchLayoutType type, out ushort id);
}
