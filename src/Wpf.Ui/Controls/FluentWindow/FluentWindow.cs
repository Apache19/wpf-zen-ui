// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Hardware;
using Wpf.Ui.Interop;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

// todo WM_NCMOUSELEAVE 如果要使用Dwm绘制系统按钮，则需要调用DwmDefWindowProc，否则按钮高亮不会去除

/// <summary>
/// FluentWindow
/// </summary>
/// <remarks>
/// 注意不要在此窗口上使用WindowChrome
/// </remarks>
public partial class FluentWindow : Window
{
    static FluentWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(typeof(FluentWindow))
        );

        if (Utilities.IsWin_11_Or_Greater)
        {
            InitializeStatic_Win11();
        }
        else
        {
            // InitializeForLegacyWindows();
        }
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

    #region WindowBackdropType

    /// <summary>
    /// Identifies the <see cref="WindowBackdropType"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty WindowBackdropTypeProperty =
        DependencyProperty.Register(
            nameof(WindowBackdropType),
            typeof(WindowBackdropType),
            typeof(FluentWindow),
            new PropertyMetadata(
                WindowBackdropType.None,
                static (d, e) =>
                {
                    if (d is FluentWindow fw && e.NewValue is WindowBackdropType wbt)
                    {
                        fw._OnWindowBackdropTypeChanged?.Invoke(wbt);
                    }
                }
            )
        );

    /// <summary>
    /// Gets or sets a value determining preferred backdrop type for current <see cref="Window"/>.
    /// </summary>
    public WindowBackdropType WindowBackdropType
    {
        get => (WindowBackdropType)GetValue(WindowBackdropTypeProperty);
        set => SetValue(WindowBackdropTypeProperty, value);
    }

    #endregion

    #region WindowCornerPreference

    /// <summary>
    /// Identifies the <see cref="WindowCornerPreference"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty WindowCornerPreferenceProperty =
        DependencyProperty.Register(
            nameof(WindowCornerPreference),
            typeof(WindowCornerPreference),
            typeof(FluentWindow),
            new PropertyMetadata(
                WindowCornerPreference.Default,
                static (d, e) =>
                {
                    if (d is FluentWindow fw &&
                        e.NewValue is WindowCornerPreference wcp)
                    {
                        fw.OnWindowCornerPreferenceChanged(wcp);
                    }
                }
            )
        );

    /// <summary>
    /// 获取或设置窗口圆角，此选项只在 Windows 11 以上操作系统起作用
    /// </summary>
    public WindowCornerPreference WindowCornerPreference
    {
        get => (WindowCornerPreference)GetValue(WindowCornerPreferenceProperty);
        set => SetValue(WindowCornerPreferenceProperty, value);
    }

    #endregion

    #endregion

    internal static readonly uint WM_NCHITTEST_RESOLVED = PInvoke.RegisterWindowMessage("WM_NCHITTEST_RESOLVED");
    internal static readonly uint WM_NCHITTEST_REQUEST  = PInvoke.RegisterWindowMessage("WM_NCHITTEST_REQUEST");

    #pragma warning disable SA1306 // Field names should begin with lower-case letter

    private readonly MessageHandler?             _MSG_NCCALCSIZE = null;
    private readonly Action<HwndSource>          _OnSourceInitialized;
    private readonly Action<WindowBackdropType>? _OnWindowBackdropTypeChanged;

    #pragma warning restore SA1306 // Field names should begin with lower-case letter

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
            {
                _OnSourceInitialized = OnSourceInitialized_Win10;
                break;
            }

            case OsVersion.Win11:
            default:
            {
                _OnSourceInitialized = OnSourceInitialized_Win11;
                break;
            }
        }

        WeakEventManager<ThemeManager, ThemeChangedEventArgs>
           .AddHandler(
                ThemeManager.Instance,
                nameof(ThemeManager.Changed),
                OnAppThemeChanged
            );

        UpdateSystemAccentColor();
        IsAccentOnTitleBarAndBorder = ApplicationAccentColorManager.IsAccentColorOnTitleBarsEnabled;
    }

    private void OnAppThemeChanged(object? sender, EventArgs e)
    {
    }

    protected override void OnClosed(EventArgs e)
    {
        WeakEventManager<ThemeManager, ThemeChangedEventArgs>
           .RemoveHandler(
                ThemeManager.Instance,
                nameof(ThemeManager.Changed),
                OnAppThemeChanged
            );

        base.OnClosed(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            hwndSource.AddHook(FluentWindowSourceHook);
            _OnSourceInitialized(hwndSource);
        }

        OnWindowCornerPreferenceChanged(WindowCornerPreference);
    }

    private IntPtr FluentWindowSourceHook(IntPtr hwndIntPtr, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var uMsg = (uint)msg;
        var hwnd = (HWND)hwndIntPtr;

        switch (uMsg)
        {
            // 窗口在不同DPI的监视器之间移动，会收到此消息
            case PInvoke.WM_DPICHANGED:
            {
                InvalidateBorderMetricsCache();

                // 更新缓存的DPI
                var yx = wParam.ToInt64();
                _cachedDpi = new DisplayDpi((short)yx, (short)(yx >> 16));

                // 更新窗口大小
                const SET_WINDOW_POS_FLAGS swpFlag
                    = SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;

                var rc = Marshal.PtrToStructure<RECT>(lParam);
                PInvoke.SetWindowPos(hwnd, HWND.Null, rc.left, rc.top, rc.Width, rc.Height, swpFlag);

                break;
            }

            case PInvoke.WM_THEMECHANGED:
                InvalidateBorderMetricsCache();
                break;

            case PInvoke.WM_SETTINGCHANGE:
            {
                Debug.WriteLine($"[WM_SETTINGCHANGE] wParam: {wParam:x8}, lParam: \"{Marshal.PtrToStringAuto(lParam)}\"");

                if (lParam != IntPtr.Zero)
                {
                    var spiStr = Marshal.PtrToStringAuto(lParam);
                    if (spiStr == "ImmersiveColorSet")
                    {
                        IsAccentOnTitleBarAndBorder = ApplicationAccentColorManager.IsAccentColorOnTitleBarsEnabled;
                    }
                    else if (spiStr == "ThemeElement")
                    {
                        InvalidateBorderMetricsCache();
                    }
                }

                if (wParam == (uint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETLOGICALDPIOVERRIDE)
                {
                    InvalidateDpi();
                }

                break;
            }

            case PInvoke.WM_SIZE:
            case PInvoke.WM_WINDOWPOSCHANGED:
            case PInvoke.WM_STYLECHANGED:
            {
                ActualWindowState = Utilities.GetHwndState(hwnd);
                break;
            }

            // 核心消息，此为实现将客户区内容放到标题栏的核心机制。
            // 它能保持完整可调整尺寸边框厚度，并且保持原生边框视觉样式（例如Win10以上系统的边框包含一部分透明区域）。
            // 注意：WindowChrome已过时，请不要在FluentWindow上使用。
            case PInvoke.WM_NCCALCSIZE:
            {
                if (_MSG_NCCALCSIZE != null)
                {
                    return _MSG_NCCALCSIZE(hwnd, uMsg, wParam, lParam, out handled);
                }

                if (wParam.ToInt32() != 0)
                {
                    var isMaximized = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

                    var p = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);

                    // 第二个矩形为原窗口坐标，第三个矩形为原客户区坐标，
                    // 通过计算即可得出窗口边框厚度，可以将这个值存储起来以便后用。
                    // 用这种方法可免去 获取系统指标再经过DPI计算得出窗口边框厚度 的复杂过程。
                    var l = Math.Abs(p.rgrc._1.left - p.rgrc._2.left);
                    var b = Math.Abs(p.rgrc._1.bottom - p.rgrc._2.bottom);

                    var cx = _cxBorder = l > 0 ? l : _cxBorder;
                    var cy = _cyBorder = b > 0 ? b : _cyBorder;

                    // 保留左、下、右三个原边框厚度。
                    // 顶部：
                    // 最大化时则保留顶部边框厚度，可保证客户区显示完整；
                    // 非最大化时则让顶部边框厚度为零，以便移除标题栏，把客户区内容“放进标题区域”。
                    // 关键：不能同时保留四条边框，否则在Win10及以下系统会出现完整的不可交互的假标题栏，
                    // 这个问题从Vista就存在，居然直到Win11才修复。。。
                    p.rgrc._0.left   += cx;
                    p.rgrc._0.right  -= cx;
                    p.rgrc._0.top    += isMaximized ? cy : 0;
                    p.rgrc._0.bottom -= cy;

                    Marshal.StructureToPtr(p, lParam, false);

                    // 设置主题色居然也会触发这个消息，
                    // 此时如果窗口尺寸、位置没有任何改变，返回 WVR_REDRAW 会让系统莫名其妙地把客户区移动并“左对齐”，
                    // 只要返回 0，不让系统后续做任何动作，即可解决。
                    handled = true;
                }

                break;
            }

            // 窗口处理调整尺寸边框和相关逻辑，没有结果再将消息转发给标题栏。
            case PInvoke.WM_NCHITTEST:
            {
                var isMaximized = Utilities.GetHwndState(hwnd) == WindowState.Maximized;

                if ((ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip) && !isMaximized)
                {
                    var point = LParamToPoint(lParam);
                    var rect  = GetWindowRect(hwnd);

                    var ht = HitTestResizeBorder(rect, point);

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
                var tResult = PInvoke.SendMessage(hwnd, WM_NCHITTEST_REQUEST, 0, lParam);
                if (tResult != IntPtr.Zero)
                {
                    handled = true;
                    return tResult;
                }

                // No specific non-client element was hit during this pass.
                // Still send WM_NCHITTEST_RESOLVED as a completion notification,
                // allowing title bar buttons and other participants to reset
                // their visual state if necessary.
                PInvoke.SendMessage(hwnd, WM_NCHITTEST_RESOLVED, 0, 0);
                break;
            }

            case PInvoke.WM_NCACTIVATE:
            {
                IsNcActive = wParam != IntPtr.Zero;

                // 强制刷新渲染队列，以便依赖 IsNcActive 属性的视觉状态及时刷新，
                // 否则在标题栏按下鼠标准备拖动时会有500ms延迟。
                // 例如拖动前标题栏前景色在活动和非活动之间切换
                Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));

                // 可以消除在特定情况（例如没有 WS_CAPTION）时激活瞬间绘制原生标题栏的问题（包括系统按钮）
                // https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-ncactivate
                handled = true;
                return PInvoke.DefWindowProc(hwnd, uMsg, new WPARAM((uint)wParam), -1);
            }

            case PInvoke.WM_DWMCOLORIZATIONCOLORCHANGED:
            {
                UpdateSystemAccentColor();
                InvalidateBorderMetricsCache();

                break;
            }
        }

        return IntPtr.Zero;

        static Point LParamToPoint(IntPtr lParam)
        {
            var yx = lParam.ToInt64();
            return new Point((short)yx, (short)(yx >> 16));
        }
    }

    // 暂时在窗口上处理主题色的监听，将来使用主题管理器来管理
    private void UpdateSystemAccentColor()
    {
        UnsafeNativeMethods.DwmGetColorizationParameters(out var p);
        var c = p.Color;

        var a = (byte)(c >> 24);
        var r = (byte)(c >> 16);
        var g = (byte)(c >> 8);
        var b = (byte)c;

        var accentColor = Color.FromRgb(r, g, b).UpdateBrightness(6f);

        Resources["SystemAccentColor"]      = accentColor;
        Resources["SystemAccentColorBrush"] = accentColor.ToBrush();
    }

    private delegate IntPtr MessageHandler(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam, out bool handled);
}