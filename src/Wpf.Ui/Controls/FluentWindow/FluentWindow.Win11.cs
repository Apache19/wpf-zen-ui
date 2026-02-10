// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Hardware;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow Win11
/// </summary>
public partial class FluentWindow
{
    private static void InitializeStatic_Win11()
    {
        BackgroundProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(
                BackgroundProperty.DefaultMetadata.DefaultValue,
                null,
                static (d, baseValue) =>
                {
                    // 视是否开启情况返回透明值，这样不会破坏任何原先设置的值以及继承机制
                    return Brushes.Transparent;
                }
            )
        );
    }

    private void InitializeInstance_Win11()
    {
    }

    private void OnSourceInitialized_Win11(HwndSource hwndSource)
    {
        // 1 设置 Backdrop 即可有大阴影
        // 2 不将非客户区扩展进客户区，则看不到背景效果

        var hwnd = (HWND)hwndSource.Handle;

        RemoveSystemMenu(hwnd);

        hwndSource.CompositionTarget?.BackgroundColor = Color.FromArgb(0, 0x00, 0x00, 0x00);

        new DwmWindowAttributes()
           .SystemBackdrop(DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW)
           .UseImmersiveDarkMode(true)
           .SetToWindow(this);

        var margins = new MARGINS()
        {
            cxLeftWidth    = -1,
            cxRightWidth   = -1,
            cyBottomHeight = -1,
            cyTopHeight    = -1,
        };

        // 必须将非客户区效果扩展到整个窗口，否则任何效果都出不来
        PInvoke.DwmExtendFrameIntoClientArea((HWND)hwndSource.Handle, in margins);
    }

    private void RemoveSystemMenu(HWND hwnd)
    {
        // 移除窗口菜单前先获取一次，以便系统生成菜单，否则移除 WS_SYSMENU 风格后无法弹出窗口菜单
        PInvoke.GetSystemMenu(hwnd, false);

        // 移除 WS_SYSMENU 风格，否则会出现原生系统按钮，即使顶部标题栏已被去除
        // 但带来的后果就是无法用 Alt + Space 弹出窗口菜单了
        var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(int)WINDOW_STYLE.WS_SYSMENU;
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);
    }

    private void UpdateBackdrop_Win11(ApplicationTheme? appTheme = null)
    {

    }
}