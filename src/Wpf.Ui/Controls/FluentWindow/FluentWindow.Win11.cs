// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Hardware;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow Win11
/// </summary>
public partial class FluentWindow
{
    private void OnSourceInitialized_Win11(HwndSource hwndSource)
    {
    }

    private IntPtr NcCalcSizeHandler_Win11(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        /*var hwnd         = (HWND)hwndIntPtr;
        var rcClientArea = Marshal.PtrToStructure<RECT>(lParam);

        EnsureWindowDpi(hwnd);

        var dpi       = _cachedDpi;
        var winBorder = GetLogicalWindowBorderThickness(hwnd, WindowBorderKind.Layout);
        var thickness = DpiHelper.LogicalThicknessToDevice(winBorder, dpi.DpiScaleX, dpi.DpiScaleY);

        rcClientArea.left   += (int)thickness.Left;
        rcClientArea.right  -= (int)thickness.Right;
        rcClientArea.top    += (int)thickness.Top;
        rcClientArea.bottom -= (int)thickness.Bottom;

        Marshal.StructureToPtr(rcClientArea, lParam, false);

        handled = true;

        return wParam.ToInt32() != 0 ? new IntPtr(PInvoke.WVR_REDRAW) : IntPtr.Zero;*/

        if (wParam.ToInt32() != 0)
        {
            var hwnd        = (HWND)hwndIntPtr;
            var isMaximized = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

            var p = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);

            var l = Math.Abs(p.rgrc._1.left - p.rgrc._2.left);
            var r = Math.Abs(p.rgrc._1.right - p.rgrc._2.right);
            var y = Math.Abs(p.rgrc._1.bottom - p.rgrc._2.bottom);

            _cxBorder = l;
            _cyBorder = y;

            p.rgrc._0.left   += l;
            p.rgrc._0.right  -= r;
            p.rgrc._0.top    += isMaximized ? y : 1;
            p.rgrc._0.bottom -= y;

            Marshal.StructureToPtr(p, lParam, false);

            handled = true;
            return new IntPtr(PInvoke.WVR_REDRAW);
        }

        handled = false;
        return IntPtr.Zero;
    }
}