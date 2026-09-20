// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Carries the native TF_LANGUAGEPROFILE record returned by language-profile enumeration.
/// </summary>
/// <remarks>
/// Sequential field order and the integer BOOL preserve the COM enumeration buffer layout.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct TfLanguageProfile
{
    internal Guid   ClassId;
    internal ushort LanguageId;
    internal Guid   CategoryId;
    internal int    IsActive;
    internal Guid   ProfileId;
}