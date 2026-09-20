// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

// Every member is declared in native vtable order even though only enumeration, description,
// activation, and language switching are used. Removing an unused slot would redirect later calls.
/// <summary>
/// Provides language-profile descriptions and legacy profile activation for the input catalog.
/// </summary>
/// <remarks>
/// Native slot order includes registration methods before the discovery operations used here.
/// </remarks>
[Guid("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComImport]
internal interface ITfInputProcessorProfiles
{
    [PreserveSig]
    int Register(in Guid classId);

    [PreserveSig]
    int Unregister(in Guid classId);

    [PreserveSig]
    int AddLanguageProfile(
        in Guid classId,
        ushort languageId,
        in Guid profileId,
        IntPtr description,
        uint descriptionLength,
        IntPtr iconFile,
        uint iconFileLength,
        uint iconIndex
    );

    [PreserveSig]
    int RemoveLanguageProfile(in Guid classId, ushort languageId, in Guid profileId);

    [PreserveSig]
    int EnumInputProcessorInfo(out IntPtr enumerator);

    [PreserveSig]
    int GetDefaultLanguageProfile(ushort languageId, in Guid categoryId, out Guid classId, out Guid profileId);

    [PreserveSig]
    int SetDefaultLanguageProfile(ushort languageId, in Guid classId, in Guid profileId);

    [PreserveSig]
    int ActivateLanguageProfile(in Guid classId, ushort languageId, in Guid profileId);

    [PreserveSig]
    int GetActiveLanguageProfile(in Guid classId, out ushort languageId, out Guid profileId);

    /// <summary>
    /// Reads the localized description used to label a language profile in the catalog.
    /// </summary>
    [PreserveSig]
    int GetLanguageProfileDescription(
        in Guid classId,
        ushort languageId,
        in Guid profileId,
        [MarshalAs(UnmanagedType.BStr)] out string? description
    );

    [PreserveSig]
    int GetCurrentLanguage(out ushort languageId);

    [PreserveSig]
    int ChangeCurrentLanguage(ushort languageId);

    [PreserveSig]
    int GetLanguageList(out IntPtr languageIds, out uint count);

    /// <summary>
    /// Acquires the language-specific enumerator used by the legacy catalog discovery path.
    /// </summary>
    [PreserveSig]
    int EnumLanguageProfiles(ushort languageId, out IEnumTfLanguageProfiles enumerator);

    [PreserveSig]
    int EnableLanguageProfile(in Guid classId, ushort languageId, in Guid profileId, int enable);

    [PreserveSig]
    int IsEnabledLanguageProfile(in Guid classId, ushort languageId, in Guid profileId, out int enabled);

    [PreserveSig]
    int EnableLanguageProfileByDefault(in Guid classId, ushort languageId, in Guid profileId, int enable);

    [PreserveSig]
    int SubstituteKeyboardLayout(in Guid classId, ushort languageId, in Guid profileId, IntPtr keyboardLayout);
}