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
using Windows.Win32.Graphics.Dwm;
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
    // 虽然能通过各种方式实现

    private void OnSourceInitialized_Win10(HwndSource hwndSource)
    {
        // 如果全部设置为 -1 则会使DWM把非客户区框架扩展到整个窗口。
        // 对于Win7，则是“毛玻璃效果”，Win10则视背景是否开启。
        // 由于把整个窗口作为客户区，DWM不会绘制窗口阴影。这样设置可使 DWM 阴影显示出来。
        // 但这会带来性能开销。
        var margins = new MARGINS()
        {
            cxLeftWidth    = -1,
            cxRightWidth   = -1,
            cyBottomHeight = -1,
            cyTopHeight    = -1,
        };

        // PInvoke.DwmExtendFrameIntoClientArea((HWND)hwndSource.Handle, in margins);

        new DwmWindowAttributes()
            .AllowNcPaint(false)
            .NcRenderingPolicy(DWMNCRENDERINGPOLICY.DWMNCRP_USEWINDOWSTYLE)
            .CaptionColor(0)
            .ForceIconicRepresentation(true)
            .SystemBackdrop(DWM_SYSTEMBACKDROP_TYPE.DWMSBT_AUTO)
            .SetToWindow(this);
    }

    private IntPtr NcCalcSizeHandler_Win10(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var hwnd = (HWND)hwndIntPtr;

        if (Utilities.GetHwndState(hwnd) == WindowState.Maximized)
        {
            var rect = Marshal.PtrToStructure<RECT>(lParam);

            EnsureWindowDpi(hwnd);

            var dpi       = _cachedDpi;
            var winBorder = GetLogicalWindowBorderThickness(hwnd, WindowBorderKind.Layout);
            var thickness = DpiHelper.LogicalThicknessToDevice(winBorder, dpi.DpiScaleX, dpi.DpiScaleY);

            rect.left   += (int)thickness.Left;
            rect.right  -= (int)thickness.Right;
            rect.top    += (int)thickness.Top;
            rect.bottom -= (int)thickness.Bottom;

            Marshal.StructureToPtr(rect, lParam, false);
        }

        handled = true;
        return wParam != IntPtr.Zero ? new IntPtr(PInvoke.WVR_REDRAW) : IntPtr.Zero;
    }
}