// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
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
        // 由于通过 WM_NCCALCSIZE 删除了标准帧，DWM不会绘制窗口阴影。
        // 使用 DwmExtendFrameIntoClientArea 并传入 -1 边距，把整个窗口扩展成非客户区即可解决。
        var margins = new MARGINS()
        {
            cxLeftWidth    = -1,
            cxRightWidth   = -1,
            cyBottomHeight = -1,
            cyTopHeight    = -1,
        };

        PInvoke.DwmExtendFrameIntoClientArea((HWND)hwndSource.Handle, in margins);
    }

    // 旧系统的 DWM 存在一些问题，如果调整全部四个边距会出现假标题栏
    // 但只调整三个边框，DWM 却不会绘制另一个边框
    // 所以只能采取折中方案，移除全部边框，并用Border冒充窗口边框
    private IntPtr OnWmNcCalcSize_Win10(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        if (wParam.ToInt32() != 0)
        {
            var hwnd        = (HWND)hwndIntPtr;
            var isMaximized = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

            var p = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);

            var l = Math.Abs(p.rgrc._1.left - p.rgrc._2.left);
            var b = Math.Abs(p.rgrc._1.bottom - p.rgrc._2.bottom);

            var cx = _cxBorder = l > 0 ? l : _cxBorder;
            var cy = _cyBorder = b > 0 ? b : _cyBorder;

            if (isMaximized)
            {
                p.rgrc._0.left   += cx;
                p.rgrc._0.right  -= cx;
                p.rgrc._0.top    += cy;
                p.rgrc._0.bottom -= cy;

                Marshal.StructureToPtr(p, lParam, false);
            }

            handled = true;
            return new IntPtr(PInvoke.WVR_REDRAW);
        }

        handled = false;
        return IntPtr.Zero;
    }
}