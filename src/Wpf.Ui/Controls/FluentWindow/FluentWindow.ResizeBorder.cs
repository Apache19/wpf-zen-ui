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

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow ResizeBorder
/// </summary>
public partial class FluentWindow
{
    #if NET9_0_OR_GREATER
    private static readonly Lock _systemMetricCacheLock = new();
    #else
    private static readonly object _systemMetricCacheLock = new();
    #endif

    private static int  _cachedSmCxPaddedBorder;
    private static int  _cachedSmCxSizeFrame;
    private static int  _cachedSmCySizeFrame;
    private static bool _systemMetricCacheInvalidated;

    // 调查一下哪些消息和参数是触发相关值变化的
    private static void InvalidateSystemMetricCache() => _systemMetricCacheInvalidated = true;

    private static void EnsureSystemMetricCache()
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

    // todo 缓存dpi
    private static Thickness GetWindowBorderThickness(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return default;
        }

        // dpi转换逻辑需要修正：从GetSystemMetrics读取出来的参数是设备相关像素（系统或监视器），而应用在窗口上时，需要转换成窗口本身的dpi。

        var dpi = DpiHelper.GetSystemDpi();

        double scaleX = dpi.DpiX / (double)DpiHelper.DefaultDpi;
        double scaleY = dpi.DpiY / (double)DpiHelper.DefaultDpi;

        var paddingBorder = 0;

        WINDOW_STYLE style = (WINDOW_STYLE)PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE);

        EnsureSystemMetricCache();

        if ((style & WINDOW_STYLE.WS_CAPTION) == WINDOW_STYLE.WS_CAPTION)
        {
            paddingBorder = _cachedSmCxPaddedBorder;
        }

        var maximize = (style & WINDOW_STYLE.WS_MAXIMIZE) == WINDOW_STYLE.WS_MAXIMIZE;

        var frameX = _cachedSmCxSizeFrame;
        var frameY = _cachedSmCySizeFrame;

        var xw = (paddingBorder + frameX) / scaleX;
        var yh = (paddingBorder + frameY) / scaleY;

        // 给顶部留1像素边框,最大化时则按全边框
        return new Thickness(xw, maximize ? yh : 1, xw, yh);
    }

    private static readonly IntPtr[,] _HitTestBorders = new[,]
    {
        // @formatter:off
        { (IntPtr)PInvoke.HTTOPLEFT,    (IntPtr)PInvoke.HTTOP,     (IntPtr)PInvoke.HTTOPRIGHT    },
        { (IntPtr)PInvoke.HTLEFT,       IntPtr.Zero,               (IntPtr)PInvoke.HTRIGHT       },
        { (IntPtr)PInvoke.HTBOTTOMLEFT, (IntPtr)PInvoke.HTBOTTOM,  (IntPtr)PInvoke.HTBOTTOMRIGHT },
        // @formatter:on
    };

    private IntPtr HitTestResizeBorder(Rect wRect, Point mPoint)
    {
        var row = 1;
        var col = 1;

        if (mPoint.X >= wRect.Left && mPoint.X < wRect.Left + (int)_chrome.ResizeBorderThickness.Left)
        {
            col = 0; // left
        }
        else if (mPoint.X < wRect.Right && mPoint.X >= wRect.Right - _chrome.ResizeBorderThickness.Right)
        {
            col = 2; // right
        }

        if (mPoint.Y >= wRect.Top && mPoint.Y < wRect.Top + _chrome.ResizeBorderThickness.Top)
        {
            row = 0; // top
        }
        else if (mPoint.Y < wRect.Bottom && mPoint.Y >= wRect.Bottom - (int)_chrome.ResizeBorderThickness.Bottom)
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