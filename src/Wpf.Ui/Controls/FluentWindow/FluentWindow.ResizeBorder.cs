// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32;
using Windows.Win32.Foundation;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow ResizeBorder
/// </summary>
public partial class FluentWindow
{
    private static readonly IntPtr[,] _HitTestBorders = new[,]
    {
        // @formatter:off
        { (IntPtr)PInvoke.HTTOPLEFT,    (IntPtr)PInvoke.HTTOP,     (IntPtr)PInvoke.HTTOPRIGHT    },
        { (IntPtr)PInvoke.HTLEFT,       IntPtr.Zero,               (IntPtr)PInvoke.HTRIGHT       },
        { (IntPtr)PInvoke.HTBOTTOMLEFT, (IntPtr)PInvoke.HTBOTTOM,  (IntPtr)PInvoke.HTBOTTOMRIGHT },
        // @formatter:on
    };

    private int _cxBorder;
    private int _cyBorder;

    // 修改：传入原始物理坐标
    private IntPtr HitTestResizeBorder(in RECT wRect, in System.Drawing.Point mPoint)
    {
        var row = 1;
        var col = 1;

        var cx = _cxBorder;
        var cy = _cyBorder;

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