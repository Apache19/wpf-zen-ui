// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Defines the COM-callable TSF UI-element callback implemented by the soft-keyboard session.
/// </summary>
/// <remarks>
/// This interop contract is public only because the CLR requires a COM-callable interface type to
/// be externally visible. It is hidden from IntelliSense and is not intended for application code.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[ComVisible(true)]
[Guid("EA1EA136-19DF-11D7-A6D2-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISoftKeyboardTsfUIElementSink
{
    /// <summary>
    /// Receives notification that a TSF UI element is beginning.
    /// </summary>
    /// <param name="elementId">
    /// The TSF UI-element identifier.
    /// </param>
    /// <param name="show">
    /// Receives whether the input method may show its native surface.
    /// </param>
    /// <returns>
    /// An HRESULT value.
    /// </returns>
    [PreserveSig]
    int BeginUIElement(uint elementId, ref int show);

    /// <summary>
    /// Receives notification that a TSF UI element changed.
    /// </summary>
    /// <param name="elementId">
    /// The TSF UI-element identifier.
    /// </param>
    /// <returns>
    /// An HRESULT value.
    /// </returns>
    [PreserveSig]
    int UpdateUIElement(uint elementId);

    /// <summary>
    /// Receives notification that a TSF UI element ended.
    /// </summary>
    /// <param name="elementId">
    /// The TSF UI-element identifier.
    /// </param>
    /// <returns>
    /// An HRESULT value.
    /// </returns>
    [PreserveSig]
    int EndUIElement(uint elementId);
}