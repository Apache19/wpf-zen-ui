// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Defines the COM callback for a TSF compartment value change.
/// </summary>
/// <remarks>
/// The interface is public only because the CLR must expose it to COM. Applications should
/// observe the properties on <see cref="SoftKeyboard"/> instead.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[ComVisible(true)]
[Guid("743ABD5F-F26D-48DF-8CC5-238492419B64")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISoftKeyboardTsfCompartmentEventSink
{
    /// <summary>
    /// Receives the identifier of the compartment whose value changed.
    /// </summary>
    /// <param name="compartmentId">The changed TSF compartment identifier.</param>
    /// <returns>An HRESULT value.</returns>
    [PreserveSig]
    int OnChange(ref Guid compartmentId);
}
