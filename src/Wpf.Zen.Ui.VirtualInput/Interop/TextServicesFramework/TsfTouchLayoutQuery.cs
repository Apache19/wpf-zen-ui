// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Queries the active TIP on WPF's dispatcher without creating or activating another TSF manager.
/// </summary>
internal static class TsfTouchLayoutQuery
{
    internal static SoftKeyboardTouchLayoutPreference? Query(Guid classId)
    {
        object? providerObject = null;
        object? functionObject = null;

        try
        {
            // WPF owns this borrowed manager. Reuse the cached accessor: WPF has no public API for
            // TIP functions. A missing host/interface leaves the preference unknown, never guessed.
            if (classId == Guid.Empty
                || WpfTextServicesAccessor.TryGetThreadManager() is not ITfThreadMgrEx manager
                || manager.GetFunctionProvider(in classId, out providerObject) != 0
                || providerObject is not ITfFunctionProvider provider)
            {
                return null;
            }

            var functionId = Guid.Empty;
            var interfaceId = typeof(ITfFnGetPreferredTouchKeyboardLayout).GUID;

            if (provider.GetFunction(in functionId, in interfaceId, out functionObject) != 0
                || functionObject is not ITfFnGetPreferredTouchKeyboardLayout function
                || function.GetLayout(out var type, out var id) != 0)
            {
                return null;
            }

            return new SoftKeyboardTouchLayoutPreference(type, id);
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            // Only release references returned by GetFunctionProvider/GetFunction. The manager is WPF-owned.
            if (functionObject is not null && Marshal.IsComObject(functionObject))
            {
                Marshal.ReleaseComObject(functionObject);
            }

            if (providerObject is not null && Marshal.IsComObject(providerObject))
            {
                Marshal.ReleaseComObject(providerObject);
            }
        }
    }
}
