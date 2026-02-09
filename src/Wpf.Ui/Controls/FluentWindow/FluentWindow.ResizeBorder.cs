// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Hardware;
using Wpf.Ui.Win32;
using Point = System.Drawing.Point;

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

    private void InvalidateBorderMetricsCache()
    {
        _systemMetricCacheInvalidated = true;
    }

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

    private (int CX, int CY) GetWindowBorderThickness(HWND hwnd)
    {
        EnsureSystemMetricCache();

        int cxBorder = _cachedSmCxSizeFrame + _cachedSmCxPaddedBorder;
        int cyBorder = _cachedSmCySizeFrame + _cachedSmCxPaddedBorder;

        // gpt Debug 输出 _cachedSmCxSizeFrame _cachedSmCySizeFrame _cachedSmCxPaddedBorder
        Debug.WriteLine($"_cachedSmCxSizeFrame: {_cachedSmCxSizeFrame}, _cachedSmCySizeFrame: {_cachedSmCySizeFrame}, _cachedSmCxPaddedBorder: {_cachedSmCxPaddedBorder}");

        return (cxBorder, cyBorder);
    }

    private Thickness GetLogicalWindowBorderThickness(HWND hwnd, WindowBorderKind kind)
    {
        if (hwnd == IntPtr.Zero)
        {
            return default;
        }

        EnsureSystemMetricCache();

        var paddingBorder = _cachedSmCxPaddedBorder;
        var isMaximized   = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

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
    private static IntPtr HitTestResizeBorder(in RECT wRect, in Point mPoint, (int CX, int CY) border)
    {
        var row = 1;
        var col = 1;

        var cx = border.CX;
        var cy = border.CY;

        if (mPoint.X >= wRect.left && mPoint.X < wRect.left + cx)
        {
            col = 0; // left
        }
        else if (mPoint.X < wRect.right && mPoint.X >= wRect.right - cx)
        {
            col = 2; // right
        }

        if (mPoint.Y >= wRect.top && mPoint.Y < wRect.top + cy)
        {
            row = 0; // top
        }
        else if (mPoint.Y < wRect.bottom && mPoint.Y >= wRect.bottom - cy)
        {
            row = 2; // bottom
        }

        return _HitTestBorders[row, col];
    }

    /// <summary>
    /// Get the bounding rectangle for the window in physical coordinates.
    /// </summary>
    /// <returns>The bounding rectangle for the window.</returns>
    private static RECT GetWindowRect(IntPtr hwnd)
    {
        if (PInvoke.GetWindowRect((HWND)hwnd, out RECT rect))
        {
            return rect;
        }

        throw new Win32Exception();
    }
}