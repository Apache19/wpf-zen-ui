// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

[Guid("3D61BF11-AC5F-42C8-A4CB-931BCC28C744")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComImport]
internal interface IEnumTfLanguageProfiles
{
    [PreserveSig]
    int Clone(out IEnumTfLanguageProfiles enumerator);

    [PreserveSig]
    int Next(uint count, out TfLanguageProfile profile, out uint fetched);

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Skip(uint count);
}