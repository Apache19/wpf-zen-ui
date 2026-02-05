// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Hardware;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow ResizeBorder
/// </summary>
public partial class FluentWindow
{
    private enum WindowBorderKind
    {
        Layout, // 布局 / 视觉边框（WindowBorder）
        Resize  // 命中测试 / Resize 边框
    }

#if NET9_0_OR_GREATER
    private readonly Lock _systemMetricCacheLock = new();
#else
    private readonly object _systemMetricCacheLock = new();
#endif

    private DisplayDpi _cachedDpi;
    private bool       _dpiCacheInvalidated = true;

    private int  _cachedSmCxPaddedBorder;
    private int  _cachedSmCxSizeFrame;
    private int  _cachedSmCySizeFrame;
    private bool _systemMetricCacheInvalidated = true;

    // todo 调查一下哪些消息和参数是触发相关值变化的，考虑改名为只失效Border相关
    private void InvalidateSystemMetricCache()
    {
        _systemMetricCacheInvalidated = true;
    }

    // todo 同样，要调查哪些消息需要监听
    private void InvalidateDpi()
    {
        _dpiCacheInvalidated = true;
    }

    private void EnsureSystemMetricCache()
    {
        if (!_systemMetricCacheInvalidated)
        {
            return;
        }

        lock (_systemMetricCacheLock)
        {
            if (!_systemMetricCacheInvalidated)
            {
                return;
            }

            _cachedSmCxPaddedBorder = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXPADDEDBORDER);
            _cachedSmCxSizeFrame    = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSIZEFRAME);
            _cachedSmCySizeFrame    = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSIZEFRAME);

            _systemMetricCacheInvalidated = false;
        }
    }

    private void EnsureWindowDpi(HWND hwnd)
    {
        if (!_dpiCacheInvalidated)
        {
            return;
        }

        _cachedDpi           = DpiHelper.GetWindowDpi(hwnd);
        _dpiCacheInvalidated = false;
    }

    private Thickness GetLogicalWindowBorderThickness(HWND hwnd, WindowBorderKind kind)
    {
        if (hwnd == IntPtr.Zero)
        {
            return default;
        }

        WINDOW_STYLE style = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);

        EnsureSystemMetricCache();

        var paddingBorder = 0;
        //if ((style & WINDOW_STYLE.WS_CAPTION) != 0)
        {
            paddingBorder = _cachedSmCxPaddedBorder;
        }

        var isMaximized = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

        var frameX = _cachedSmCxSizeFrame;
        var frameY = _cachedSmCySizeFrame;

        EnsureWindowDpi(hwnd);
        var dpi = _cachedDpi;

        var xw = (paddingBorder + frameX) / dpi.DpiScaleX;
        var yh = (paddingBorder + frameY) / dpi.DpiScaleY;

        switch (kind)
        {
            case WindowBorderKind.Layout:
            {
                // 顶部：Win11 保留 1px 用于显示原生边框，最大化则吃满
                var top = isMaximized ? yh : 1;
                return new Thickness(xw, top, xw, yh);
            }

            case WindowBorderKind.Resize:
            {
                // 最大化不可调整尺寸，返回 0 厚度
                if (isMaximized)
                {
                    return new Thickness(0);
                }

                // 顶部：排除 PaddingBorder，只包含 SmCySizeFrame
                // var top = frameX / dpi.DpiScaleY;
                return new Thickness(xw, yh, xw, yh);
            }

            default:
                return default;
        }
    }

    private static readonly IntPtr[,] _HitTestBorders = new[,]
    {
        // @formatter:off
        { (IntPtr)PInvoke.HTTOPLEFT,    (IntPtr)PInvoke.HTTOP,     (IntPtr)PInvoke.HTTOPRIGHT    },
        { (IntPtr)PInvoke.HTLEFT,       IntPtr.Zero,               (IntPtr)PInvoke.HTRIGHT       },
        { (IntPtr)PInvoke.HTBOTTOMLEFT, (IntPtr)PInvoke.HTBOTTOM,  (IntPtr)PInvoke.HTBOTTOMRIGHT },
        // @formatter:on
    };

    // 传进参数全部是逻辑坐标
    private static IntPtr HitTestResizeBorder(in Rect wRect, in Point mPoint, in Thickness borderThickness)
    {
        var row = 1;
        var col = 1;

        if (mPoint.X >= wRect.Left && mPoint.X < wRect.Left + (int)borderThickness.Left)
        {
            col = 0; // left
        }
        else if (mPoint.X < wRect.Right && mPoint.X >= wRect.Right - borderThickness.Right)
        {
            col = 2; // right
        }

        if (mPoint.Y >= wRect.Top && mPoint.Y < wRect.Top + borderThickness.Top)
        {
            row = 0; // top
        }
        else if (mPoint.Y < wRect.Bottom && mPoint.Y >= wRect.Bottom - (int)borderThickness.Bottom)
        {
            row = 2; // bottom
        }

        return _HitTestBorders[row, col];
    }

    /// <summary>
    /// Get the bounding rectangle for the window in physical coordinates.
    /// </summary>
    /// <returns>The bounding rectangle for the window.</returns>
    private static Rect GetWindowRect(IntPtr hwnd)
    {
        if (PInvoke.GetWindowRect((HWND)hwnd, out RECT rect))
        {
            return new Rect(rect.left, rect.top, rect.Width, rect.Height);
        }

        throw new Win32Exception();
    }
}