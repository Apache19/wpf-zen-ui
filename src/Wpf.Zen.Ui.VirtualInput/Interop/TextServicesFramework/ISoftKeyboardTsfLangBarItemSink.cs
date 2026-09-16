// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Defines the COM callback for language-bar item presentation changes.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[ComVisible(true)]
[Guid("57DBE1A0-DE25-11D2-AFDD-00105A2799B5")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISoftKeyboardTsfLangBarItemSink
{
    /// <summary>
    /// Receives flags identifying changed icon, text, tooltip, bitmap, or status facets.
    /// </summary>
    /// <returns>An HRESULT value.</returns>
    [PreserveSig]
    int OnUpdate(uint flags);
}
