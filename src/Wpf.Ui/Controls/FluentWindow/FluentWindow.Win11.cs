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
using Wpf.Ui.Interop;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

// todo 亚克力界面时，检查是否开启透明，未开启的话，黑暗模式把背景色调暗一点，现在看起来还不够暗

/// <summary>
/// FluentWindow Win11
/// </summary>
public partial class FluentWindow
{
    private static readonly Color BlackTransparentColor = Color.FromArgb(0, 0, 0, 0);
    private static readonly Brush BlackTransparentBrush = new SolidColorBrush(BlackTransparentColor);

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
                    var fw = (FluentWindow)d;
                    return fw._isBackdropApplied ? BlackTransparentBrush : baseValue;
                }
            )
        );
    }

    // 必须将非客户区效果扩展到整个窗口，否则任何效果都出不来
    private static void ExtendFrameIntoClientArea(HWND hwnd, bool extend)
    {
        var margins = default(MARGINS);
        if (extend)
        {
            margins.cxLeftWidth    = -1;
            margins.cxRightWidth   = -1;
            margins.cyBottomHeight = -1;
            margins.cyTopHeight    = -1;
        }

        PInvoke.DwmExtendFrameIntoClientArea(hwnd, in margins);
    }

    private Color _originalBackgroundColor = Color.FromRgb(0, 0, 0);
    private bool  _isBackdropApplied;

    private void InitializeInstance_Win11()
    {
    }

    private void OnSourceInitialized_Win11(HwndSource hwndSource)
    {
        UnsafeNativeMethods.RemoveWindowCaption(hwndSource.Handle);
    }

    // todo 移除系统菜单以后，应让窗口响应 Alt+Space 弹出系统菜单
    private void RemoveSystemMenu(HWND hwnd)
    {
        // 移除窗口菜单前先获取一次，以便系统生成菜单，
        // 否则移除 WS_SYSMENU 风格后无法再获得窗口菜单句柄、无法弹出窗口菜单
        PInvoke.GetSystemMenu(hwnd, false);

        var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(int)WINDOW_STYLE.WS_SYSMENU;
        _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);
    }

    // 窗口创建Hwnd时、属性变更时、主题变更时，需要调用此方法
    private void UpdateBackdrop_Win11()
    {
        if (PresentationSource.FromVisual(this) is not HwndSource hwndSource ||
            hwndSource.CompositionTarget == null)
        {
            return;
        }

        var hwnd = (HWND)hwndSource.Handle;

        // 如果是高对比度，则移除Backdrop、退出暗黑模式
        if (SystemParameters.HighContrast)
        {
            if (hwnd != IntPtr.Zero && PInvoke.IsWindow(hwnd))
            {
                DwmAttributes.SetUseImmersiveDarkMode(hwnd, false);

                // 移除Backdrop
                DwmAttributes.SetSystemBackdrop(hwnd, DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE);

                // 将窗口背景色还原
                hwndSource.CompositionTarget.BackgroundColor = _originalBackgroundColor;

                // 还原背景画刷
                _isBackdropApplied = false;
                CoerceValue(BackgroundProperty);
            }
        }
        else
        {
            if (hwnd != IntPtr.Zero && PInvoke.IsWindow(hwnd))
            {
                // 根据 CurrentTheme 设置暗黑/明亮模式
                var theme = ThemeManager.CurrentTheme;

                DwmAttributes.SetUseImmersiveDarkMode(
                    hwnd,
                    theme == ApplicationTheme.Dark
                );

                // 系统开启了将主题色应用到窗口边框或标题栏且激活窗口时DWM会在窗口顶部用强调色绘制标题栏背景，
                // 为了解决此问题，可以将标题栏颜色设置为DWMWA_COLOR_NONE。
                DwmAttributes.SetCaptionColor(hwnd, PInvoke.DWMWA_COLOR_NONE);

                // 移除 WS_SYSMENU 风格，否则启用 Backdrop 会出现原生系统按钮，即使顶部标题栏已被去除。
                // 带来的后果就是无法用 Alt + Space 弹出窗口菜单了
                // todo 确认：已经使用主题Api去掉了系统菜单，看看不调用这个会如何
                RemoveSystemMenu(hwnd);

                var dwmResult = false;
                var wbt       = WindowBackdropType;

                if (IsSupportedBackdrop(wbt))
                {
                    var backdrop = wbt switch
                    {
                        WindowBackdropType.None     => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE,
                        WindowBackdropType.Auto     => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_AUTO,
                        WindowBackdropType.Acrylic  => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW,
                        WindowBackdropType.Mica     => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW,
                        WindowBackdropType.Mica_Alt => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TABBEDWINDOW,
                        _                           => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE
                    };

                    dwmResult = DwmAttributes.SetSystemBackdrop(hwnd, backdrop);
                }

                var applied = wbt != WindowBackdropType.None && dwmResult;

                // 如果窗口没有 Owner、BackdropType 为 Auto 时，系统不会为其应用 Backdrop，
                // 此时不要移除窗口背景色，否则看起来会很怪异。
                if (applied && wbt == WindowBackdropType.Auto && Owner == null)
                {
                    applied = false;
                }

                if (applied)
                {
                    // 备份窗口背景色
                    var bkColor = hwndSource.CompositionTarget.BackgroundColor;
                    if (bkColor != BlackTransparentColor)
                    {
                        _originalBackgroundColor = bkColor;
                    }

                    // 将窗口背景色设置为BlackTransparentBrush
                    hwndSource.CompositionTarget.BackgroundColor = BlackTransparentColor;
                }
                else
                {
                    hwndSource.CompositionTarget.BackgroundColor = _originalBackgroundColor;
                }

                ExtendFrameIntoClientArea(hwnd, applied);

                _isBackdropApplied = applied;
                CoerceValue(BackgroundProperty);
            }
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
}