// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Win32;

#pragma warning disable SA1027 // Use tabs correctly
#pragma warning disable SA1028
// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow Win10 partial
/// </summary>
public partial class FluentWindow
{
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttrib Attribute;
        public IntPtr                  Data;
        public int                     SizeOfData;
    }

    private enum AccentState
    {
        ACCENT_DISABLED                   = 0,
        ACCENT_ENABLE_GRADIENT            = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND          = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND   = 4,
        ACCENT_INVALID_STATE              = 5,
    }

    [Flags]
    public enum AccentFlags : uint
    {
        None = 0x0, // 无特殊标志

        // EnableGradient    = 0x1, // 启用渐变 已弃用
        EnableAcrylicBlur = 0x2,   // 自定义名称
        DrawLeftBorder    = 0x20,  // 绘制左侧边框
        DrawTopBorder     = 0x40,  // 绘制顶部边框
        DrawRightBorder   = 0x80,  // 绘制右侧边框
        DrawBottomBorder  = 0x100, // 绘制底部边框
        DrawAllBorders    = DrawLeftBorder | DrawTopBorder | DrawRightBorder | DrawBottomBorder
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public AccentFlags AccentFlags;
        public uint        GradientColor;
        public int         AnimationId;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    private void OnSourceInitialized_Win10(HwndSource hwndSource)
    {
        // 移除 WS_SYSMENU 风格，否则会出现原生系统按钮，即使顶部标题栏已被去除
        // 但带来的后果就是无法用 Alt + Space 弹出窗口菜单了
        var style = PInvoke.GetWindowLong((HWND)hwndSource.Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(int)WINDOW_STYLE.WS_SYSMENU;
        PInvoke.SetWindowLong((HWND)hwndSource.Handle, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);

        // ACCENT_ENABLE_ACRYLICBLURBEHIND 会是蓝色的

        // 创建 AccentPolicy 结构体
        var accentPolicy = new AccentPolicy
        {
            AccentState   = AccentState.ACCENT_ENABLE_GRADIENT,
            GradientColor = 0xFF707070, // 设置颜色（ARGB格式）
            AnimationId   = 0,
            AccentFlags   = AccentFlags.EnableAcrylicBlur,
        };

        // 分配内存并复制结构体数据
        int    accentPolicySize = Marshal.SizeOf(accentPolicy);
        IntPtr accentPolicyPtr  = Marshal.AllocHGlobal(accentPolicySize);
        Marshal.StructureToPtr(accentPolicy, accentPolicyPtr, false);

        try
        {
            // 创建 WindowCompositionAttributeData 结构体
            var wca = new WindowCompositionAttributeData
            {
                Attribute  = WindowCompositionAttrib.WCA_ACCENT_POLICY,
                Data       = accentPolicyPtr,
                SizeOfData = accentPolicySize
            };

            _ = SetWindowCompositionAttribute(hwndSource.Handle, ref wca);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPolicyPtr);
        }

        // 设置窗口背景为透明
        hwndSource.CompositionTarget?.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
    }

    // https://github.com/MicrosoftDocs/win32/blob/docs/desktop-src/dwm/customframe.md

    private IntPtr OnWmActive_Win10(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var margins = new MARGINS()
        {
            cxLeftWidth    = 2,
            cxRightWidth   = 2,
            cyBottomHeight = 2,
            cyTopHeight    = 2,
        };

        var hwnd    = (HWND)hwndIntPtr;
        // _ = PInvoke.DwmExtendFrameIntoClientArea(hwnd, in margins);

        handled = false;
        return IntPtr.Zero;
    }

    private IntPtr OnWmNcPaint_Win10(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var hwnd       = (HWND)hwndIntPtr;
        var isNcActive = IsNcActive;
        var isMaximize = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

        // gpt 请帮我处理 WM_NCPAINT 消息，在isNcActive == true、isMaximize == false时给窗口画一个黑色边框

        // 以下是处理 `WM_NCPAINT` 消息的代码，在 `isNcActive == true` 且 `isMaximize == false` 时给窗口画一个黑色边框：
        // 当窗口非最大化且非客户区处于活动状态时，绘制黑色边框
        if (isNcActive && !isMaximize)
        {
            var ret = PInvoke.DefWindowProc(hwnd, msg, (uint)wParam, lParam);
            // 获取窗口非客户区的设备上下文
            var hdc = PInvoke.GetWindowDC(hwnd);

            if (hdc != IntPtr.Zero)
            {
                try
                {
                    // 获取窗口矩形
                    RECT windowRect;
                    PInvoke.GetWindowRect(hwnd, out windowRect);

                    // 计算边框矩形（整个窗口外框）
                    RECT borderRect = new RECT
                    {
                        left   = 13,
                        top    = 0,
                        right  = windowRect.right - windowRect.left,
                        bottom = windowRect.bottom - windowRect.top
                    };

                    // 创建黑色画笔
                    var hPen   = PInvoke.CreatePen(PEN_STYLE.PS_SOLID, 2, (COLORREF)0x00FFFF); // 黑色
                    var oldPen = PInvoke.SelectObject(hdc, hPen);

                    // 创建空画刷（透明填充）
                    var hBrush   = PInvoke.GetStockObject(GET_STOCK_OBJECT_FLAGS.NULL_BRUSH);
                    var oldBrush = PInvoke.SelectObject(hdc, hBrush);

                    // 绘制矩形边框
                    PInvoke.Rectangle(hdc, borderRect.left, borderRect.top, borderRect.right, borderRect.bottom);

                    // 恢复原来的对象
                    PInvoke.SelectObject(hdc, oldPen);
                    PInvoke.SelectObject(hdc, oldBrush);

                    // 删除创建的画笔
                    PInvoke.DeleteObject(hPen);
                }
                finally
                {
                    // 释放设备上下文
                    PInvoke.ReleaseDC(hwnd, hdc);
                }

                handled = true;
                return ret;
            }
        }

        handled = false;
        return IntPtr.Zero;
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

            // if (isMaximized)
            {
                p.rgrc._0.left   += 2;
                p.rgrc._0.right  -= 2;
                p.rgrc._0.top    += isMaximized ? cy : 0;
                p.rgrc._0.bottom -= 2;

                Marshal.StructureToPtr(p, lParam, false);
            }

            handled = true;
            return IntPtr.Zero;
        }

        handled = false;
        return IntPtr.Zero;
    }
}