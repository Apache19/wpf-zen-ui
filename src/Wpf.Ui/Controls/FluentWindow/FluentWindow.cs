// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

// todo WM_NCMOUSELEAVE 如果要使用Dwm绘制系统按钮，则需要调用DwmDefWindowProc，否则按钮高亮不会去除

/// <summary>
/// FluentWindow
/// </summary>
public partial class FluentWindow : Window
{
    static FluentWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(typeof(FluentWindow))
        );
    }

    #region DependencyProperties

    #region ActualWindowState

    /// <summary>
    /// Identifies the <see cref="ActualWindowState"/> dependency property.
    /// </summary>
    private static readonly DependencyPropertyKey ActualWindowStatePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActualWindowState),
            typeof(WindowState),
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(WindowState.Normal)
        );

    /// <summary>
    /// Identifies the <see cref="ActualWindowState"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActualWindowStateProperty =
        ActualWindowStatePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the real state of the window.
    /// This property reflects the actual state of the native Windows window, updated through window message handling.
    /// </summary>
    public WindowState ActualWindowState
    {
        get => (WindowState)GetValue(ActualWindowStateProperty);
        private set => SetValue(ActualWindowStatePropertyKey, value);
    }

    #endregion

    #region IsNcActive

    private static readonly DependencyPropertyKey IsNcActivePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsNcActive),
            typeof(bool),
            typeof(FluentWindow),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsNcActive"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsNcActiveProperty =
        IsNcActivePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the window's non-client area
    /// (title bar, borders, etc.) is active.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="System.Windows.Window.IsActive"/>, which represents
    /// the logical activation state of the window, <see cref="IsNcActive"/>
    /// reflects the visual activation state of the non-client area as reported
    /// by the operating system.
    ///
    /// <para>
    /// When a modal window is shown, clicking the parent window behind it may
    /// cause the system to momentarily toggle the non-client activation state
    /// (often accompanied by a flash), which is reflected by changes in
    /// <see cref="IsNcActive"/> even though <see cref="System.Windows.Window.IsActive"/>
    /// remains <c>true</c> or <c>false</c>.
    /// </para>
    /// </remarks>
    public bool IsNcActive
    {
        get => (bool)GetValue(IsNcActiveProperty);
        private set => SetValue(IsNcActivePropertyKey, value);
    }

    #endregion

    #region OsVersion

    // todo 将来移植到静态资源类里

    private static readonly DependencyPropertyKey OsVersionPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(OsVersion),
            typeof(OsVersion),
            typeof(FluentWindow),
            new PropertyMetadata(OsVersion.Unknown)
        );

    /// <summary>
    /// Identifies the <see cref="OsVersion"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty OsVersionProperty =
        OsVersionPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the version of the current operating system.
    /// </summary>
    public OsVersion OsVersion
    {
        get { return (OsVersion)GetValue(OsVersionProperty); }
        private set { SetValue(OsVersionPropertyKey, value); }
    }

    #endregion

    #region IsAccentOnTitleBarAndBorder

    private static readonly DependencyPropertyKey IsAccentOnTitleBarAndBorderPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsAccentOnTitleBarAndBorder),
            typeof(bool),
            typeof(FluentWindow),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsAccentOnTitleBarAndBorder"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsAccentOnTitleBarAndBorderProperty =
        IsAccentOnTitleBarAndBorderPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the accent color is applied to the title bar and window border.
    /// </summary>
    public bool IsAccentOnTitleBarAndBorder
    {
        get { return (bool)GetValue(IsAccentOnTitleBarAndBorderProperty); }
        private set { SetValue(IsAccentOnTitleBarAndBorderPropertyKey, value); }
    }

    #endregion

    #endregion

    internal static readonly uint WM_NCHITTEST_RESOLVED = PInvoke.RegisterWindowMessage("WM_NCHITTEST_RESOLVED");
    internal static readonly uint WM_NCHITTEST_REQUEST  = PInvoke.RegisterWindowMessage("WM_NCHITTEST_REQUEST");

    private readonly MessageHandler     _msgNcCalcSize;
    private readonly MessageHandler?    _msgActive;
    private readonly Action<HwndSource> _onSourceInitialized;

    public FluentWindow()
    {
        OsVersion = Utilities.OsVersion;

        switch (Utilities.OsVersion)
        {
            case OsVersion.Unknown:
            case OsVersion.Vista:
            case OsVersion.Win7:
            case OsVersion.Win8:
            case OsVersion.Win10:
                _msgNcCalcSize       = OnWmNcCalcSize_Win10;
                _msgActive           = OnWmActive_Win10;
                _onSourceInitialized = OnSourceInitialized_Win10;
                break;

            case OsVersion.Win11:
            default:
                _msgNcCalcSize       = OnWmNcCalcSize_Win11;
                _onSourceInitialized = OnSourceInitialized_Win11;
                break;
        }

        UpdateSystemAccentColor();
        IsAccentOnTitleBarAndBorder = ApplicationAccentColorManager.IsAccentColorOnTitleBarsEnabled;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            hwndSource.AddHook(FluentWindowSourceHook);
            _onSourceInitialized(hwndSource);
        }
    }

    private IntPtr FluentWindowSourceHook(IntPtr hwndIntPtr, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var uMsg = (uint)msg;
        var hwnd = (HWND)hwndIntPtr;

        switch (uMsg)
        {
            case PInvoke.WM_ACTIVATE:
            {
                if (_msgActive is not null)
                {
                    return _msgActive(hwnd, uMsg, wParam, lParam, out handled);
                }

                break;
            }

            case PInvoke.WM_DWMCOLORIZATIONCOLORCHANGED:
            {
                var colorizationColor = (COLORREF)(uint)wParam;
                UpdateSystemAccentColor(colorizationColor);

                break;
            }

            // 窗口在不同DPI的监视器之间移动，会收到
            case PInvoke.WM_DPICHANGED:
            {
                // 更新窗口大小
                const SET_WINDOW_POS_FLAGS swpFlag
                    = SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;

                var rc = Marshal.PtrToStructure<RECT>(lParam);
                PInvoke.SetWindowPos(hwnd, HWND.Null, rc.left, rc.top, rc.Width, rc.Height, swpFlag);

                break;
            }

            case PInvoke.WM_SETTINGCHANGE:
            {
                // gpt 调试输出 WM_SETTINGCHANGE 消息，以及wParam(uint), lParam(string)
                Debug.WriteLine($"[WM_SETTINGCHANGE] wParam: {wParam:x8}, lParam: \"{Marshal.PtrToStringAuto(lParam)}\"");

                if (lParam != IntPtr.Zero)
                {
                    var spiStr = Marshal.PtrToStringAuto(lParam);
                    if (spiStr == "ImmersiveColorSet")
                    {
                        IsAccentOnTitleBarAndBorder = ApplicationAccentColorManager.IsAccentColorOnTitleBarsEnabled;
                    }
                }

                break;
            }

            case PInvoke.WM_SIZE:
            case PInvoke.WM_STYLECHANGED:
            case PInvoke.WM_WINDOWPOSCHANGED:
            {
                ActualWindowState = Utilities.GetHwndState(hwnd);
                break;
            }

            case PInvoke.WM_NCACTIVATE:
            {
                IsNcActive = wParam != IntPtr.Zero;

                // 可以消除在没有 WS_CAPTION 时激活瞬间绘制原生标题栏的问题（包括系统按钮）
                // https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-ncactivate
                handled = true;
                return PInvoke.DefWindowProc(hwnd, uMsg, new WPARAM((uint)wParam), -1);
            }

            // 窗口处理调整尺寸边框和相关逻辑，没有结果再将消息转发给标题栏。
            case PInvoke.WM_NCHITTEST:
            {
                var isMaximized = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

                if ((ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip) && !isMaximized)
                {
                    var point   = LParamToPoint(lParam);
                    var wndRect = GetWindowRect(hwnd);

                    var ht = HitTestResizeBorder(wndRect, point);

                    if ((uint)ht is >= PInvoke.HTLEFT and <= PInvoke.HTBOTTOMRIGHT)
                    {
                        // The resize border has been hit and the hit-test result is finalized.
                        // Notify other components participating in non-client interaction
                        // so they can update their state accordingly.
                        PInvoke.SendMessage(hwnd, WM_NCHITTEST_RESOLVED, 0, 0);

                        handled = true;
                        return ht;
                    }
                }

                // 没有结果，将消息转发给标题栏，让标题栏处理拖动窗口、图标、和系统按钮的命中测试。
                // 标题栏在有结果的情况下应该处理 WM_NCHITTEST_RESOLVED 消息。
                var tbResult = PInvoke.SendMessage(hwnd, WM_NCHITTEST_REQUEST, 0, lParam);
                if (tbResult != IntPtr.Zero)
                {
                    handled = true;
                    return tbResult;
                }

                // No specific non-client element was hit during this pass.
                // Still send WM_NCHITTEST_RESOLVED as a completion notification,
                // allowing title bar buttons and other participants to reset
                // their visual state if necessary.
                PInvoke.SendMessage(hwnd, WM_NCHITTEST_RESOLVED, 0, 0);
                break;
            }

            // 非客户端尺寸计算，经测试，目前只有Win11工作正常。
            // Win10 则只能更改三条边，如果更改全部四条边，则会使DWM绘制出一个完整的假标题栏（包括系统按钮）但无法交互。
            // todo 此行为需要进一步测试，尝试不更改（返回非零值），并使用DwmExtendFrameIntoClientArea
            case PInvoke.WM_NCCALCSIZE:
                return _msgNcCalcSize(hwnd, uMsg, wParam, lParam, out handled);
        }

        return IntPtr.Zero;

        static System.Drawing.Point LParamToPoint(IntPtr lParam)
        {
            var yx = lParam.ToInt64();
            var x  = (short)yx;
            var y  = (short)(yx >> 16);
            return new System.Drawing.Point(x, y);
        }
    }

    // 暂时在窗口上处理主题色的监听，将来使用主题管理器来管理
    private void UpdateSystemAccentColor()
    {
        var hResult = PInvoke.DwmGetColorizationColor(out uint colorizationColor, out BOOL opaqueBlend);
        if (hResult.Succeeded)
        {
            UpdateSystemAccentColor((COLORREF)colorizationColor);
        }
    }

    private void UpdateSystemAccentColor(COLORREF color)
    {
        var v = color.Value;

        var a = (byte)((v >> 24) & 0xFF);
        var r = (byte)((v >> 16) & 0xFF);
        var g = (byte)((v >> 8) & 0xFF);
        var b = (byte)(v & 0xFF);

        var accentColor = Color.FromArgb(a, r, g, b).UpdateBrightness(6f);

        Resources["SystemAccentColor"]      = accentColor;
        Resources["SystemAccentColorBrush"] = accentColor.ToBrush();
    }

    private delegate IntPtr MessageHandler(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam, out bool handled);
}