// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Wpf.Ui.Appearance;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

// todo 亚克力界面时，检查是否开启透明，未开启的话，黑暗模式把背景色调暗一点，现在看起来还不够暗

/// <summary>
/// FluentWindow Win11
/// </summary>
public partial class FluentWindow
{
    private static readonly Color BlackTransparentColor = Color.FromArgb(0, 0, 0, 0);
    private static readonly Brush BlackTransparentBrush = CreateBlackTransparentBrush();

    private static Brush CreateBlackTransparentBrush()
    {
        var brush = new SolidColorBrush(BlackTransparentColor);

        // FluentWindow metadata is process-wide and can be initialized by any UI thread. An unfrozen Freezable
        // remains bound to that first Dispatcher and fails when backdrop coercion returns it to a window on another
        // STA. The brush is immutable by design, so freezing both documents that contract and makes it safe to share.
        brush.Freeze();

        return brush;
    }

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

    /// <summary>
    /// Synchronizes the DWM frame surface with the selected backdrop and non-client strategies.
    /// </summary>
    /// <remarks>
    /// Backdrop materials require the conventional <c>-1</c> sheet-of-glass extension. A window using
    /// <see cref="FluentWindowFrameStrategy.Windows11Client"/> must instead keep a one-physical-pixel extension
    /// when no backdrop is active; otherwise its client rectangle covers every native frame pixel and DWM drops
    /// both the visible outline and rounded-corner treatment. The one-pixel extension does not require a transparent
    /// WPF composition target and therefore does not alter the window's normal background.
    /// </remarks>
    private void UpdateDwmFrameExtension_Win11(HWND hwnd, bool isBackdropApplied)
    {
        var margins = FluentWindowNonClientArea.CalculateDwmFrameMargins(
            GetFrameStrategy(),
            isBackdropApplied
        );
        _ = PInvoke.DwmExtendFrameIntoClientArea(hwnd, in margins);
    }

    /// <summary>
    /// Applies the active accent color to a fully client-rendered Win11 DWM frame.
    /// </summary>
    /// <remarks>
    /// Extending one DWM frame pixel preserves the native outline and rounded corners, but that synthetic frame does
    /// not automatically participate in the user's "show accent color on title bars and window borders" setting.
    /// Native-frame mode must remain on <c>DWMWA_COLOR_DEFAULT</c> because Windows already implements the correct
    /// activation policy there. Restoring the default in every non-accent state also prevents a forced color from
    /// surviving activation loss or a runtime switch back to <see cref="UseNativeWindowBorder"/>.
    /// </remarks>
    private void UpdateDwmBorderColor_Win11()
    {
        if (!Utilities.IsWin_11_Or_Greater || _hwnd == HWND.Null || !PInvoke.IsWindow(_hwnd))
        {
            return;
        }

        var borderColor = PInvoke.DWMWA_COLOR_DEFAULT;
        if (
            FluentWindowNonClientArea.ShouldApplyDwmAccentBorder(
                UseNativeWindowBorder,
                IsNcActive,
                IsAccentOnTitleBarAndBorder,
                SystemParameters.HighContrast
            )
            && PInvoke.DwmGetColorizationColor(out var colorizationColor, out _) == HRESULT.S_OK
        )
        {
            // DwmGetColorizationColor and DWMWA_BORDER_COLOR intentionally use opposite byte orders.
            borderColor = FluentWindowNonClientArea.ConvertDwmColorizationColorToColorRef(colorizationColor);
        }

        _ = DwmAttributes.SetBorderColor(_hwnd, borderColor);
    }

    private Color _originalBackgroundColor = Color.FromRgb(0, 0, 0);
    private bool _isBackdropApplied;

    // 窗口创建Hwnd时、属性变更时、主题变更时，需要调用此方法
    private void UpdateBackdrop_Win11()
    {
        if (
            PresentationSource.FromVisual(this) is not HwndSource hwndSource
            || hwndSource.CompositionTarget == null
        )
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

                // High contrast removes the full-window backdrop sheet. Windows11Client still retains its minimal
                // frame bridge so DWM can render the system outline instead of relying on a low-contrast WPF color.
                UpdateDwmFrameExtension_Win11(hwnd, false);

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

                DwmAttributes.SetUseImmersiveDarkMode(hwnd, theme == ApplicationTheme.Dark);

                // 系统开启了将主题色应用到窗口边框或标题栏且激活窗口时DWM会在窗口顶部用强调色绘制标题栏背景，
                // 为了解决此问题，可以将标题栏颜色设置为DWMWA_COLOR_NONE。
                DwmAttributes.SetCaptionColor(hwnd, PInvoke.DWMWA_COLOR_NONE);

                var dwmResult = false;
                var wbt = WindowBackdropType;

                if (IsSupportedBackdrop(wbt))
                {
                    var backdrop = wbt switch
                    {
                        WindowBackdropType.None => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE,
                        WindowBackdropType.Auto => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_AUTO,
                        WindowBackdropType.Acrylic => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW,
                        WindowBackdropType.Mica => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW,
                        WindowBackdropType.Mica_Alt => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TABBEDWINDOW,
                        _ => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE,
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

                UpdateDwmFrameExtension_Win11(hwnd, applied);

                _isBackdropApplied = applied;
                CoerceValue(BackgroundProperty);
            }
        }

        // Backdrop and frame changes can cause DWM to reevaluate non-client attributes. Apply the border policy last
        // so a runtime backdrop or native-border transition cannot silently discard the explicit active color.
        UpdateDwmBorderColor_Win11();
    }

    private void OnWindowCornerPreferenceChanged(WindowCornerPreference corner)
    {
        var hwnd = (HWND)new WindowInteropHelper(this).Handle;

        if (hwnd != IntPtr.Zero)
        {
            var dwmcp = corner switch
            {
                WindowCornerPreference.Default => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT,
                WindowCornerPreference.DoNotRound => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND,
                WindowCornerPreference.Round => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND,
                WindowCornerPreference.RoundSmall => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUNDSMALL,
                _ => DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT,
            };
            DwmAttributes.SetWindowCornerPreference(hwnd, dwmcp);
        }
    }
}