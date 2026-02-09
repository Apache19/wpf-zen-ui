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

    private IntPtr OnWmNcCalcSize_Win11(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var hwnd           = (HWND)hwndIntPtr;
        var rcClientArea   = Marshal.PtrToStructure<RECT>(lParam);
        var isMisMaximized = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

        var (cx, cy) = GetWindowBorderThickness(hwnd);

        rcClientArea.left   += cx;
        rcClientArea.right  -= cx;
        rcClientArea.top    += isMisMaximized ? cy : 0;
        rcClientArea.bottom -= cy;

        Marshal.StructureToPtr(rcClientArea, lParam, false);

        handled = true;
        return wParam.ToInt32() != 0 ? new IntPtr(PInvoke.WVR_REDRAW) : IntPtr.Zero;
    }
}