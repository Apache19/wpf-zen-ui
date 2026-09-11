// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

[ComImport]
[Guid("887AA91E-ACBA-4931-84DA-3C5208CF543F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumTfUIElements
{
    [PreserveSig]
    int Clone(out IEnumTfUIElements enumerator);

    [PreserveSig]
    int Next(uint count, [MarshalAs(UnmanagedType.IUnknown)] out object? element, out uint fetched);

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Skip(uint count);
}