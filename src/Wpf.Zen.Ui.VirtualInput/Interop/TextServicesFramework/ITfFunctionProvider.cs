// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

[ComImport]
[Guid("101D6610-0990-11D3-8DF0-00105A2799B5")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfFunctionProvider
{
    [PreserveSig]
    int GetType(out Guid type);

    [PreserveSig]
    int GetDescription([MarshalAs(UnmanagedType.BStr)] out string description);

    [PreserveSig]
    int GetFunction(in Guid functionId, in Guid interfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object? function);
}
