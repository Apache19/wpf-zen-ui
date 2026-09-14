// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Projects the compartment lookup slot from the SDK's ITfCompartmentMgr contract.
/// </summary>
[ComImport]
[Guid("7DCF57AC-18AD-438B-824D-979BFFB74B7C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfCompartmentMgr
{
    [PreserveSig]
    int GetCompartment(in Guid id, out ITfCompartment compartment);
}
