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
    /// <summary>
    /// Writes a native VARIANT under the supplied TSF client identity and preserves the HRESULT.
    /// </summary>
    [PreserveSig]
    int SetValue(uint clientId, [In, MarshalAs(UnmanagedType.Struct)] ref object value);

    /// <summary>
    /// Reads the native VARIANT; callers validate both HRESULT and value type before
    /// interpretation.
    /// </summary>
    [PreserveSig]
    int GetValue([MarshalAs(UnmanagedType.Struct)] out object? value);
}
