// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Appearance;

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
                    // 视是否开启Backdrop返回透明值，这样不会破坏任何原先设置的值以及继承机制
                    return Brushes.Transparent;
                }
            )
        );
    }

    // 必须将非客户区效果扩展到整个窗口，否则任何效果都出不来
    private static void ExtendFrameIntoClientArea(HWND hwnd, bool extend)
    {
        var margins = new MARGINS()
        {
            cxLeftWidth    = extend ? -1 : 0,
            cxRightWidth   = extend ? -1 : 0,
            cyBottomHeight = extend ? -1 : 0,
            cyTopHeight    = extend ? -1 : 0,
        };

        PInvoke.DwmExtendFrameIntoClientArea(hwnd, in margins);
    }

    private Color _originalBackgroundColor;

    private void InitializeInstance_Win11()
    {
    }

    private void OnSourceInitialized_Win11(HwndSource hwndSource)
    {
        // 1 设置 Backdrop 即可有大阴影
        // 2 不将非客户区扩展进客户区，则看不到背景效果

        var hwnd = (HWND)hwndSource.Handle;

        RemoveSystemMenu(hwnd);

        var ct = hwndSource.CompositionTarget;
        if (ct is not null)
        {
            _originalBackgroundColor = ct.BackgroundColor;
            ct.BackgroundColor       = Color.FromArgb(0, 0, 0, 0);
        }

        DwmAttributes.SetSystemBackdrop(hwnd, DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TABBEDWINDOW);
        DwmAttributes.SetUseImmersiveDarkMode(hwnd, false);

        ExtendFrameIntoClientArea(hwnd, true);
    }

    private void RemoveSystemMenu(HWND hwnd)
    {
        // 移除窗口菜单前先获取一次，以便系统生成菜单，
        // 否则移除 WS_SYSMENU 风格后无法再获得窗口菜单句柄、无法弹出窗口菜单
        PInvoke.GetSystemMenu(hwnd, false);

        // 移除 WS_SYSMENU 风格，否则启用 Backdrop 会出现原生系统按钮，即使顶部标题栏已被去除
        // 但带来的后果就是无法用 Alt + Space 弹出窗口菜单了
        var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(int)WINDOW_STYLE.WS_SYSMENU;
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);
    }

    private void UpdateBackdrop_Win11(ApplicationTheme? appTheme = null)
    {
        // 如果是高对比度，应该保留白色


        if (appTheme is not null)
        {
        }
    }

    private void OnWindowCornerPreferenceChanged(WindowCornerPreference corner)
    {
        var hwnd = (HWND)new WindowInteropHelper(this).Handle;

        if (hwnd != IntPtr.Zero)
        {
            var dwmcp = corner switch
            {
                WindowCornerPreference.Default    => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT,
                WindowCornerPreference.DoNotRound => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND,
                WindowCornerPreference.Round      => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND,
                WindowCornerPreference.RoundSmall => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUNDSMALL,
                _                                 => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT
            };
            DwmAttributes.SetWindowCornerPreference(hwnd, dwmcp);
        }
    }

    /* todo 把主题管理器改成单例模式
    监听方式：
    WeakEventManager<ApplicationThemeManager, EventArgs>
        .AddHandler(ApplicationThemeManager.Current,
                    nameof(ApplicationThemeManager.ThemeChanged),
    OnThemeChanged);
    移除：
    WeakEventManager<ApplicationThemeManager, EventArgs>
        .RemoveHandler(ApplicationThemeManager.Current,
                      nameof(ApplicationThemeManager.ThemeChanged),
                      OnThemeChanged);
    如此以来即可不用担心内存泄露问题
    */
}