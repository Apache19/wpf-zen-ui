// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Advertises the optional TSF integration interface to verify that interface availability alone
/// never permits embedded candidate presentation.
/// </summary>
[ComImport]
[Guid("C7A6F54F-B180-416F-B2BF-7BF2E4683D7B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfIntegratableCandidateListUIElementCapability
{
}
