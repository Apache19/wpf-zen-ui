// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Enumerates live TSF UI objects for discovery when their Begin notification was missed.
/// </summary>
/// <remarks>
/// Next projects one object: callers pass count = 1, inspect fetched and release each acquisition.
/// </remarks>
[ComImport]
[Guid("887AA91E-ACBA-4931-84DA-3C5208CF543F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumTfUIElements
{
    /// <summary>
    /// Acquires another enumerator with the same traversal position.
    /// </summary>
    [PreserveSig]
    int Clone(out IEnumTfUIElements enumerator);

    /// <summary>
    /// Fetches one object with count = 1; the caller balances each returned COM acquisition.
    /// </summary>
    [PreserveSig]
    int Next(uint count, [MarshalAs(UnmanagedType.IUnknown)] out object? element, out uint fetched);

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