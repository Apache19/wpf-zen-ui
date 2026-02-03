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
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Hardware;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow Win10 partial
/// </summary>
public partial class FluentWindow
{
    private void OnSourceInitialized_Win10(HwndSource hwndSource)
    {
        var margins = new MARGINS()
        {
            cxLeftWidth    = 0,
            cxRightWidth   = 0,
            cyBottomHeight = 0,
            cyTopHeight    = 0,
        };

        PInvoke.DwmExtendFrameIntoClientArea((HWND)hwndSource.Handle, in margins);
    }

    private IntPtr NcCalcSizeHandler_Win10(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var hwnd         = (HWND)hwndIntPtr;
        var rcClientArea = Marshal.PtrToStructure<RECT>(lParam);

        EnsureWindowDpi(hwnd);

        var dpi          = _cachedDpi;
        var windowBorder = GetLogicalWindowBorderThickness(hwnd, WindowBorderKind.Layout);
        var thickness    = DpiHelper.LogicalThicknessToDevice(windowBorder, dpi.DpiScaleX, dpi.DpiScaleY);

        rcClientArea.left  += (int)thickness.Left;
        rcClientArea.right -= (int)thickness.Right;

        if (Utilities.GetHwndState(hwnd) == WindowState.Maximized)
        {
            rcClientArea.top += (int)thickness.Top;
        }

        rcClientArea.bottom -= (int)thickness.Bottom;

        Marshal.StructureToPtr(rcClientArea, lParam, false);

        handled = true;

        IntPtr retVal = IntPtr.Zero;
        if (wParam.ToInt32() != 0) // wParam == TRUE
        {
            retVal = new IntPtr(PInvoke.WVR_REDRAW);
        }

        return retVal;
    }
}