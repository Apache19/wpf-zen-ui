// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Pairs TSF sink registration cookies with their originating COM event source.
/// </summary>
/// <remarks>
/// The subscriber retains its callback pointer until it has attempted to unadvise the cookie.
/// </remarks>
[ComImport]
[Guid("4EA48A35-60AE-446F-8FD6-E6A8D82459F7")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfSource
{
    /// <summary>
    /// Registers the callback interface and returns the cookie required by this source to unadvise.
    /// </summary>
    [PreserveSig]
    int AdviseSink(ref Guid interfaceId, IntPtr sink, out uint cookie);

    /// <summary>
    /// Ends the registration identified by a cookie previously returned by this source.
    /// </summary>
    [PreserveSig]
    int UnadviseSink(uint cookie);
}