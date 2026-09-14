// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Preserves the native VARIANT value used by a TSF compartment.
/// </summary>
[ComImport]
[Guid("BB08F7A9-607A-4384-8623-056892B64371")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfCompartment
{
    [PreserveSig]
    int SetValue(uint clientId, [In, MarshalAs(UnmanagedType.Struct)] ref object value);

    [PreserveSig]
    int GetValue([MarshalAs(UnmanagedType.Struct)] out object? value);
}
