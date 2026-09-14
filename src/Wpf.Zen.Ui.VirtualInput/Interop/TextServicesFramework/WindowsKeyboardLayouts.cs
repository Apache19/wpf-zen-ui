// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Reads the session's available keyboard handles without loading or activating registered layouts.
/// </summary>
internal static class WindowsKeyboardLayouts
{
    /// <summary>
    /// Captures the currently loaded input locales. A registered TSF keyboard profile alone does
    /// not establish availability; language settings may describe a pending installation.
    /// </summary>
    internal static HashSet<IntPtr> GetLoaded()
    {
        var result = new HashSet<IntPtr>();
        var count = PInvoke.GetKeyboardLayoutList(default(Span<HKL>));
        if (count <= 0)
        {
            return result;
        }

        var layouts = new HKL[count];
        var copied = PInvoke.GetKeyboardLayoutList(layouts.AsSpan());
        for (var index = 0; index < copied; index++)
        {
            result.Add((IntPtr)layouts[index]);
        }

        return result;
    }
}
