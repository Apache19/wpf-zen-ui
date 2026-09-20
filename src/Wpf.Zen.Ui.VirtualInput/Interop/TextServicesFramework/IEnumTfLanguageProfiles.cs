// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Enumerates native language-profile records for one language in the input-method catalog.
/// </summary>
/// <remarks>
/// Next projects storage for one record: callers pass count = 1 and inspect fetched.
/// </remarks>
[Guid("3D61BF11-AC5F-42C8-A4CB-931BCC28C744")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComImport]
internal interface IEnumTfLanguageProfiles
{
    /// <summary>
    /// Acquires another enumerator with the same traversal position.
    /// </summary>
    [PreserveSig]
    int Clone(out IEnumTfLanguageProfiles enumerator);

    /// <summary>
    /// Fetches one profile with count = 1; fetched determines whether a record was returned.
    /// </summary>
    [PreserveSig]
    int Next(uint count, out TfLanguageProfile profile, out uint fetched);

    /// <summary>
    /// Resets enumeration to its initial position.
    /// </summary>
    [PreserveSig]
    int Reset();

    /// <summary>
    /// Advances enumeration by the requested number of entries.
    /// </summary>
    [PreserveSig]
    int Skip(uint count);
}