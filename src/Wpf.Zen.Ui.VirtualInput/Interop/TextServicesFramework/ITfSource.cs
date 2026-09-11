// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

[ComImport]
[Guid("4EA48A35-60AE-446F-8FD6-E6A8D82459F7")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfSource
{
    [PreserveSig]
    int AdviseSink(ref Guid interfaceId, IntPtr sink, out uint cookie);

    [PreserveSig]
    int UnadviseSink(uint cookie);
}