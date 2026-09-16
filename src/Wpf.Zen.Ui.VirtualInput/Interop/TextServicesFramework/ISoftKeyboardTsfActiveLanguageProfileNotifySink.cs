// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Defines the legacy TSF callback for TIP activation within the active language.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[ComVisible(true)]
[Guid("B246CB75-A93E-4652-BF8C-B3FE0CFD7E57")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISoftKeyboardTsfActiveLanguageProfileNotifySink
{
    /// <summary>
    /// Receives notification that a TSF language profile changed activation state.
    /// </summary>
    /// <returns>An HRESULT value.</returns>
    [PreserveSig]
    int OnActivated(ref Guid classId, ref Guid profileId, int activated);
}
