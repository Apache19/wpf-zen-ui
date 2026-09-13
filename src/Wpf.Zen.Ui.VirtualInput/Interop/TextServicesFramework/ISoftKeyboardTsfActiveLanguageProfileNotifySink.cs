// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Defines the COM-callable active-language-profile callback implemented by the input monitor.
/// </summary>
/// <remarks>
/// This interop contract is public only to satisfy CLR COM visibility requirements and is hidden
/// from IntelliSense. Applications should observe <see cref="SoftKeyboard.ActiveInputMethod"/>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[ComVisible(true)]
[Guid("B246CB75-A93E-4652-BF8C-B3FE0CFD7E57")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISoftKeyboardTsfActiveLanguageProfileNotifySink
{
    /// <summary>
    /// Receives notification that a TSF language profile changed activation state.
    /// </summary>
    /// <param name="classId">
    /// The text service class identifier.
    /// </param>
    /// <param name="profileId">
    /// The language profile identifier.
    /// </param>
    /// <param name="activated">
    /// A nonzero value when the profile became active.
    /// </param>
    /// <returns>
    /// An HRESULT value.
    /// </returns>
    [PreserveSig]
    int OnActivated(ref Guid classId, ref Guid profileId, int activated);
}