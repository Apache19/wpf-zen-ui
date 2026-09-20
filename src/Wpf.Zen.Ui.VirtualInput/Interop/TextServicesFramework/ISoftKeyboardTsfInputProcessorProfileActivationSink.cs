// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Defines the COM callback for exact TIP and keyboard-layout profile activation.
/// </summary>
/// <remarks>
/// The interface is public only because the CLR must expose it to COM. Applications should
/// observe the properties on <see cref="SoftKeyboard"/> instead.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[ComVisible(true)]
[Guid("71C6E74E-0F28-11D8-A82A-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISoftKeyboardTsfInputProcessorProfileActivationSink
{
    /// <summary>
    /// Receives one exact input-processor profile activation or deactivation.
    /// </summary>
    /// <param name="profileType">
    /// Distinguishes a text-service profile from a keyboard layout.
    /// </param>
    /// <param name="languageId">The native language identifier associated with the profile.</param>
    /// <param name="classId">The text-service class identifier for a TSF profile.</param>
    /// <param name="categoryId">The category whose active profile is changing.</param>
    /// <param name="profileId">The profile identifier within the text service.</param>
    /// <param name="keyboardLayout">The native keyboard-layout handle carried by TSF.</param>
    /// <param name="flags">
    /// Activation flags, including whether this profile is becoming active.
    /// </param>
    /// <returns>An HRESULT value. TSF ignores this return value.</returns>
    [PreserveSig]
    int OnActivated(
        uint profileType,
        ushort languageId,
        ref Guid classId,
        ref Guid categoryId,
        ref Guid profileId,
        IntPtr keyboardLayout,
        uint flags
    );
}
