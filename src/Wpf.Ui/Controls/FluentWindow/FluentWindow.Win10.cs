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
            cxLeftWidth    = -1,
            cxRightWidth   = -1,
            cyBottomHeight = -1,
            cyTopHeight    = -1,
        };

        // PInvoke.DwmExtendFrameIntoClientArea((HWND)hwndSource.Handle, in margins);

        // 移除 WS_SYSMENU 和 WS_CAPTION 样式
        var hwnd  = (HWND)hwndSource.Handle;
        var style = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(WINDOW_STYLE.WS_CAPTION);
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)style);
    }

    private IntPtr NcCalcSizeHandler_Win10(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var hwnd = (HWND)hwndIntPtr;

        var rcClientArea = Marshal.PtrToStructure<RECT>(lParam);

        EnsureWindowDpi(hwnd);

        var dpi       = _cachedDpi;
        var winBorder = GetLogicalWindowBorderThickness(hwnd, WindowBorderKind.Layout);
        var thickness = DpiHelper.LogicalThicknessToDevice(winBorder, dpi.DpiScaleX, dpi.DpiScaleY);

        rcClientArea.left  += (int)Math.Ceiling(thickness.Left) - 1;
        rcClientArea.right -= (int)Math.Ceiling(thickness.Right) - 1;

        // if (Utilities.GetHwndState(hwnd) == WindowState.Maximized)
        {
            rcClientArea.top += (int)Math.Ceiling(thickness.Top);
        }

        rcClientArea.bottom -= (int)Math.Ceiling(thickness.Bottom);

        Marshal.StructureToPtr(rcClientArea, lParam, false);

        handled = true;

        return wParam != IntPtr.Zero ? new IntPtr(PInvoke.WVR_REDRAW) : IntPtr.Zero;
    }

    private IntPtr SysCommandHandler_Win10(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        // 更新窗口大小
        const SET_WINDOW_POS_FLAGS swpFlag
            = SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
              SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE;

        var hwnd = (HWND)hwndIntPtr;

        // 处理系统命令，临时恢复窗口标题栏以支持标准窗口操作的动画
        var cmd = (uint)wParam & 0xFFF0;
        if (cmd is PInvoke.SC_CLOSE or PInvoke.SC_MINIMIZE or PInvoke.SC_MAXIMIZE or PInvoke.SC_RESTORE)
        {
            // 临时恢复 WS_CAPTION 样式
            var style = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)(style | WINDOW_STYLE.WS_CAPTION));

            // PInvoke.SetWindowPos(hwnd, HWND.Null, 0, 0, 0, 0, swpFlag);

            // 延迟移除 WS_CAPTION 样式
            Task.Delay(1).ContinueWith(
                _ =>
                {
                    // 检查窗口句柄是否仍然有效
                    if (PInvoke.IsWindow(hwnd))
                    {
                        var currentStyle = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
                        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)(currentStyle & ~WINDOW_STYLE.WS_CAPTION));

                        // PInvoke.SetWindowPos(hwnd, HWND.Null, 0, 0, 0, 0, swpFlag);
                    }
                },
                TaskScheduler.FromCurrentSynchronizationContext()
            );
        }

        handled = false;
        return IntPtr.Zero;
    }

    /*
     https://learn.microsoft.com/zh-cn/windows/win32/dwm/customframe#drawing-in-the-extended-frame-window
    在需要调整客户端区域大小之前，处理 WM_NCCALCSIZE 消息的结果将不可见。
    在此之前，窗口的初始视图会显示标准框架和扩展边框。
    若要解决此问题，必须调整窗口大小或执行在创建窗口时启动 WM_NCCALCSIZE 消息的操作。
    这可以通过使用 SetWindowPos 函数移动窗口并调整其大小来实现。
    以下代码演示了对 SetWindowPos 的调用，该调用强制使用当前窗口矩形属性和 SWP_FRAMECHANGED 标志发送WM_NCCALCSIZE消息。
       // Handle window creation.
       if (message == WM_CREATE)
       {
           RECT rcClient;
           GetWindowRect(hWnd, &rcClient);

           // Inform the application of the frame change.
           SetWindowPos(hWnd,
                        NULL,
                        rcClient.left, rcClient.top,
                        RECTWIDTH(rcClient), RECTHEIGHT(rcClient),
                        SWP_FRAMECHANGED);

           fCallDWP = true;
           lRet = 0;
       }
     */
}