// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
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
        // 1 设置 Backdrop 即可有大阴影
        // 2 不将非客户区扩展进客户区，则看不到背景效果

        Background = Brushes.Transparent;

        hwndSource.CompositionTarget?.BackgroundColor = Color.FromArgb(128, 255, 255, 255);

        new DwmWindowAttributes()
           .SystemBackdrop(DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW)
           //.UseImmersiveDarkMode(true)
           .SetToWindow(this);

        var margins = new MARGINS()
        {
            cxLeftWidth    = -1,
            cxRightWidth   = -1,
            cyBottomHeight = -1,
            cyTopHeight    = -1,
        };

        // 移除 WS_SYSMENU 风格，否则会出现原生系统按钮，即使顶部标题栏已被去除
        // 但带来的后果就是无法用 Alt + Space 弹出窗口菜单了
        var style = PInvoke.GetWindowLong((HWND)hwndSource.Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(int)WINDOW_STYLE.WS_SYSMENU;
        PInvoke.SetWindowLong((HWND)hwndSource.Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);

        PInvoke.DwmExtendFrameIntoClientArea((HWND)hwndSource.Handle, in margins);
    }

    // Win11 以上系统处理 WM_NCCALCSIZE 消息
    // 即使移除了标题栏和顶部边框，DWM 仍会完整绘制出应有的边框
    private IntPtr OnWmNcCalcSize_Win11(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        Debug.WriteLine($"OnWmNcCalcSize_Win11: wParam={wParam}");
        if (wParam.ToInt32() != 0)
        {
            var hwnd        = (HWND)hwndIntPtr;
            var isMaximized = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

            var p = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);

            // 第2个矩形为原客户区矩形，第3个矩形为原窗口矩形
            // 通过计算即可得出正确的窗口边框粗细
            var l = Math.Abs(p.rgrc._1.left - p.rgrc._2.left);
            var b = Math.Abs(p.rgrc._1.bottom - p.rgrc._2.bottom);

            var cx = _cxBorder = l > 0 ? l : _cxBorder;
            var cy = _cyBorder = b > 0 ? b : _cyBorder;

            // 将计算得出的边框粗细用于调整新客户区范围
            // 这样做可免去通过获取系统指标并经DPI缩放计算边框粗细的复杂过程
            p.rgrc._0.left   += cx;
            p.rgrc._0.right  -= cx;
            p.rgrc._0.top    += isMaximized ? cy : 0; // 非最大化时移除标题栏和顶部顶部边框
            p.rgrc._0.bottom -= cy;

            Marshal.StructureToPtr(p, lParam, false);

            handled = true;
            return IntPtr.Zero; // 返回非0值会使Win32移动客户区
        }

        // 不处理 wParam 为 FALSE 的情况
        handled = false;
        return IntPtr.Zero;
    }
}