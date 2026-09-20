// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Resolves optional functions exposed by a TSF text service, including touch-layout queries.
/// </summary>
/// <remarks>
/// Function and interface identifiers select a capability; the result must support that contract.
/// </remarks>
[ComImport]
[Guid("101D6610-0990-11D3-8DF0-00105A2799B5")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfFunctionProvider
{
    [PreserveSig]
    int GetType(out Guid type);

    /// <summary>
    /// Reads the native object description as a COM BSTR.
    /// </summary>
    [PreserveSig]
    int GetDescription([MarshalAs(UnmanagedType.BStr)] out string description);

    /// <summary>
    /// Acquires the requested function interface; failure leaves that optional capability
    /// unavailable.
    /// </summary>
    [PreserveSig]
    int GetFunction(in Guid functionId, in Guid interfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object? function);
}
