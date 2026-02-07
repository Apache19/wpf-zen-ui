// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Hardware;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

// todo 对于Win8.1 以下，直接使用窗口边框色，无视是否开启
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

        OverridesDefaultStyleProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(true)
        );
    }

    #region ActualWindowState

    /// <summary>
    /// Identifies the <see cref="ActualWindowState"/> dependency property.
    /// </summary>
    private static readonly DependencyPropertyKey ActualWindowStatePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActualWindowState),
            typeof(WindowState),
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(
                WindowState.Normal,
                static (d, e) =>
                {
                    if (d is FluentWindow window)
                    {
                        window.OnActualWindowStateChanged((WindowState)e.NewValue);
                    }
                }
            )
        );

    /// <summary>
    /// Identifies the <see cref="ActualWindowState"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActualWindowStateProperty =
        ActualWindowStatePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets 获取窗口的真实状态（只读）。
    /// 这个属性反映 Windows 原生窗口的实际状态，通过窗口消息处理更新。
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
            new PropertyMetadata(
                false,
                static (d, e) =>
                {
                    ((FluentWindow)d)._isNcActivePropertyChanged((bool)e.NewValue);
                }
            )
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

    /// <summary>
    /// 标识操作系统版本的依赖属性。
    /// </summary>
    private static readonly DependencyPropertyKey OsVersionPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(OsVersion),
            typeof(OsVersion),
            typeof(FluentWindow),
            new PropertyMetadata(OsVersion.Unknown)
        );

    /// <summary>
    /// 操作系统版本的依赖属性标识符。
    /// </summary>
    public static readonly DependencyProperty OsVersionProperty = OsVersionPropertyKey.DependencyProperty;

    /// <summary>
    /// 获取当前操作系统的版本。
    /// </summary>
    public OsVersion OsVersion
    {
        get { return (OsVersion)GetValue(OsVersionProperty); }
        private set { SetValue(OsVersionPropertyKey, value); }
    }

    #endregion

    #region IsAccentOnTitleBarAndBorder

    /// <summary>
    /// 标识是否在标题栏和窗口边框上使用主题色的依赖属性。
    /// </summary>
    private static readonly DependencyPropertyKey IsAccentOnTitleBarAndBorderPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsAccentOnTitleBarAndBorder),
            typeof(bool),
            typeof(FluentWindow),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// 标识是否在标题栏和窗口边框上使用主题色的依赖属性。
    /// </summary>
    public static readonly DependencyProperty IsAccentOnTitleBarAndBorderProperty =
        IsAccentOnTitleBarAndBorderPropertyKey.DependencyProperty;

    /// <summary>
    /// 获取一个值，该值指示是否在标题栏和窗口边框上使用主题色。
    /// </summary>
    public bool IsAccentOnTitleBarAndBorder
    {
        get { return (bool)GetValue(IsAccentOnTitleBarAndBorderProperty); }
        private set { SetValue(IsAccentOnTitleBarAndBorderPropertyKey, value); }
    }

    #endregion

    #region 事件

    /// <summary>
    /// 当 ActualWindowState 属性发生变化时发生。
    /// </summary>
    public event EventHandler<WindowStateChangedEventArgs>? ActualWindowStateChanged;

    /// <summary>
    /// 引发 ActualWindowStateChanged 事件。
    /// </summary>
    /// <param name="newState">新的窗口状态。</param>
    protected virtual void OnActualWindowStateChanged(WindowState newState)
    {
        ActualWindowStateChanged?.Invoke(this, new WindowStateChangedEventArgs(newState));
    }

    #endregion

    internal static readonly uint WM_NCHITTEST_RESOLVED = PInvoke.RegisterWindowMessage("WM_NCHITTEST_RESOLVED");
    internal static readonly uint WM_NCHITTEST_REQUEST  = PInvoke.RegisterWindowMessage("WM_NCHITTEST_REQUEST");

    private static IntPtr DefaultMessageHandler(IntPtr hwndIntPtr, uint msg, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = false;
        return IntPtr.Zero;
    }

    private readonly MessageHandler           _ncCalcSize;
    private readonly MessageHandler           _sysCommand = DefaultMessageHandler;
    private readonly MessageHandler           _ncPaint    = DefaultMessageHandler;
    private readonly SourceInitializedHandler _sourceInitialized;
    private readonly Action<bool>             _isNcActivePropertyChanged = _ => { };

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
                _ncCalcSize        = NcCalcSizeHandler_Win10;
                _sourceInitialized = OnSourceInitialized_Win10;
                break;

            case OsVersion.Win11:
            default:
                _ncCalcSize        = NcCalcSizeHandler_Win11;
                _sourceInitialized = OnSourceInitialized_Win11;
                break;
        }

        SetResourceReference(StyleProperty, typeof(FluentWindow));

        IsAccentOnTitleBarAndBorder = ApplicationAccentColorManager.IsAccentColorOnTitleBarsEnabled;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            hwndSource.AddHook(FluentWindowSourceHook);
            _sourceInitialized(hwndSource);
        }
    }

    private IntPtr FluentWindowSourceHook(IntPtr hwndIntPtr, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var uMsg = (uint)msg;
        var hwnd = (HWND)hwndIntPtr;

        switch (uMsg)
        {
            case PInvoke.WM_DWMCOLORIZATIONCOLORCHANGED:
                Debug.WriteLine("WM_DWMCOLORIZATIONCOLORCHANGED");
                InvalidateBorderMetricsCache();
                break;

            // 窗口在不同DPI的监视器之间移动，会收到
            case PInvoke.WM_DPICHANGED:
            {
                InvalidateBorderMetricsCache();

                // 更新缓存的DPI
                var yx = wParam.ToInt32();
                _cachedDpi = new DisplayDpi((short)yx, (short)(yx >> 16));

                // 更新窗口大小
                const SET_WINDOW_POS_FLAGS swpFlag
                    = SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;

                var rc = Marshal.PtrToStructure<RECT>(lParam);
                PInvoke.SetWindowPos(hwnd, HWND.Null, rc.left, rc.top, rc.Width, rc.Height, swpFlag);

                break;
            }

            case PInvoke.WM_THEMECHANGED:
                // 比 WM_SETTINGCHANGE 更可靠地反应 SM_CXPADDEDBORDER 的变化
                InvalidateBorderMetricsCache();
                break;

            case PInvoke.WM_SETTINGCHANGE:
            {
                Debug.WriteLine($"[WM_SETTINGCHANGE] wParam: 0x{wParam:X8} ({wParam}), lParam: \"{Marshal.PtrToStringAuto(lParam)}\"");

                if (lParam != IntPtr.Zero)
                {
                    var spiStr = Marshal.PtrToStringAuto(lParam);
                    if (spiStr == "ImmersiveColorSet")
                    {
                        IsAccentOnTitleBarAndBorder = ApplicationAccentColorManager.IsAccentColorOnTitleBarsEnabled;
                    }
                }

                InvalidateBorderMetricsCache();
                break;
            }

            case PInvoke.WM_SYSCOMMAND:
                return _sysCommand(hwnd, uMsg, wParam, lParam, out handled);

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

            // 如果彻底拦截，则临时修改样式后执行SysCommand会导致非客户区无法绘制
            case PInvoke.WM_NCPAINT:
                return _ncPaint(hwnd, uMsg, wParam, lParam, out handled);

            /*
             * todo 在临时修改样式后设置一个标志，让NCCALCSIZE消息重新计算，暂时去掉下边框，这样瞬时变化就不明显了
             */

            // 窗口处理调整尺寸边框和相关逻辑，没有结果再将消息转发给标题栏。
            case PInvoke.WM_NCHITTEST:
            {
                var isMaximized = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

                if ((ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip) && !isMaximized)
                {
                    var point   = LParamToPoint(lParam);
                    var dpi     = DpiHelper.GetWindowDpi(hwnd);
                    var wndRect = GetWindowRect(hwnd);
                    var border  = GetLogicalWindowBorderThickness(hwnd, WindowBorderKind.Resize);

                    var ht = HitTestResizeBorder(
                        DpiHelper.DeviceRectToLogical(wndRect, dpi.DpiScaleX, dpi.DpiScaleY),
                        DpiHelper.DevicePixelsToLogical(point, dpi.DpiScaleX, dpi.DpiScaleY),
                        border
                    );

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
                return _ncCalcSize(hwnd, uMsg, wParam, lParam, out handled);
        }

        return IntPtr.Zero;

        static Point LParamToPoint(IntPtr lParam)
        {
            var yx = lParam.ToInt32();
            var x  = (short)yx;
            var y  = (short)(yx >> 16);
            return new Point(x, y);
        }
    }

    private delegate IntPtr MessageHandler(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam, out bool handled);

    private delegate void SourceInitializedHandler(HwndSource hwndSource);
}