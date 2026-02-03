// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Wpf.Ui.Hardware;
using Wpf.Ui.Win32;

namespace Wpf.Ui.Controls;

public partial class FluentWindow
{
    private void OnSourceInitialized_Win11(HwndSource hwndSource)
    {

    }

    private IntPtr NcCalcSizeHandler_Win11(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = true;

        var rcClientArea = Marshal.PtrToStructure<RECT>(lParam);

        EnsureWindowDpi(hwnd);

        var dpi          = _cachedDpi;
        var windowBorder = GetLogicalWindowBorderThickness(hwnd, WindowBorderKind.Layout);
        var thickness    = DpiHelper.LogicalThicknessToDevice(windowBorder, dpi.DpiScaleX, dpi.DpiScaleY);

        rcClientArea.left   += (int)Math.Ceiling(thickness.Left);
        rcClientArea.right  -= (int)Math.Ceiling(thickness.Right);
        rcClientArea.top    += (int)Math.Ceiling(thickness.Top);
        rcClientArea.bottom -= (int)Math.Ceiling(thickness.Bottom);

        Marshal.StructureToPtr(rcClientArea, lParam, false);

        IntPtr retVal = IntPtr.Zero;
        if (wParam.ToInt32() != 0) // wParam == TRUE
        {
            retVal = new IntPtr(PInvoke.WVR_REDRAW);
        }

        return retVal;
    }
}
