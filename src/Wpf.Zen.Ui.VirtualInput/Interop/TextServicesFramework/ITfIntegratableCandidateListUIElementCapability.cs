// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Identifies candidate elements that support the TSF integrated-candidate contract.
/// </summary>
/// <remarks>
/// Only interface identity is required during presentation negotiation. Candidate operations
/// continue through <see cref="ITfCandidateListUIElementBehavior"/> after this capability has
/// been established.
/// </remarks>
[ComImport]
[Guid("C7A6F54F-B180-416F-B2BF-7BF2E4683D7B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfIntegratableCandidateListUIElementCapability
{
}
