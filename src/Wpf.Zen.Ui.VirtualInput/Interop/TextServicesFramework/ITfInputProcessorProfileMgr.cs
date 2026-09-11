// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

// Every member is declared in SDK vtable order. Only ActivateProfile is used, but omitting later
// declarations would make a future extension deceptively unsafe and harder to compare with msctf.idl.
[Guid("71C6E74C-0F28-11D8-A82A-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComImport]
internal interface ITfInputProcessorProfileMgr
{
    [PreserveSig]
    int ActivateProfile(
        uint profileType,
        ushort languageId,
        in Guid classId,
        in Guid profileId,
        IntPtr keyboardLayout,
        uint flags
    );

    [PreserveSig]
    int DeactivateProfile(
        uint profileType,
        ushort languageId,
        in Guid classId,
        in Guid profileId,
        IntPtr keyboardLayout,
        uint flags
    );

    [PreserveSig]
    int GetProfile(
        uint profileType,
        ushort languageId,
        in Guid classId,
        in Guid profileId,
        IntPtr keyboardLayout,
        IntPtr profile
    );

    [PreserveSig]
    int EnumProfiles(ushort languageId, out IntPtr enumerator);

    [PreserveSig]
    int ReleaseInputProcessor(in Guid classId, uint flags);

    [PreserveSig]
    int RegisterProfile(
        in Guid classId,
        ushort languageId,
        in Guid profileId,
        IntPtr description,
        uint descriptionLength,
        IntPtr iconFile,
        uint iconFileLength,
        uint iconIndex,
        IntPtr substituteKeyboardLayout,
        uint preferredLayout,
        int enabledByDefault,
        uint flags
    );

    [PreserveSig]
    int UnregisterProfile(in Guid classId, ushort languageId, in Guid profileId, uint flags);

    [PreserveSig]
    int GetActiveProfile(in Guid categoryId, IntPtr profile);
}