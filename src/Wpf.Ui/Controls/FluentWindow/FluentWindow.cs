// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Hardware;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow
/// </summary>
public partial class FluentWindow : Window
{
    static FluentWindow()
    {
        // DefaultStyleKeyProperty.OverrideMetadata(
        //     typeof(FluentWindow),
        //     new FrameworkPropertyMetadata(typeof(FluentWindow))
        // );
        //
        // OverridesDefaultStyleProperty.OverrideMetadata(
        //     typeof(FluentWindow),
        //     new FrameworkPropertyMetadata(true)
        // );
    }

    #region ActualWindowState 依赖属性

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

    #region ElementIdentity

    /// <summary>
    /// Identifies the semantic role of an element within the title bar.
    /// </summary>
    /// <remarks>
    /// This attached property is used to associate a visual element in the title bar
    /// with a specific non-client area identity. The assigned value is used internally
    /// to participate in non-client hit testing and window interaction logic.
    ///
    /// The property value is inherited, allowing container elements to define an
    /// identity that applies to their visual children.
    ///
    /// This property is not intended for general application logic and should only be
    /// used when integrating custom elements into the title bar interaction model.
    /// </remarks>
    private static readonly DependencyProperty ElementIdentityProperty =
        DependencyProperty.RegisterAttached(
            "ElementIdentity",
            typeof(TitleBarElementIdentity),
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(
                TitleBarElementIdentity.Unknown,
                FrameworkPropertyMetadataOptions.Inherits
            )
        );

    /// <summary>
    /// Helper for getting <see cref="ElementIdentityProperty"/> from <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">
    /// <see cref="DependencyObject"/> to read <see cref="ElementIdentityProperty"/> from.
    /// </param>
    /// <returns>
    /// ElementIdentity property value.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static TitleBarElementIdentity GetElementIdentity(DependencyObject obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        return (TitleBarElementIdentity)obj.GetValue(ElementIdentityProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="ElementIdentityProperty"/> on <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">
    /// <see cref="DependencyObject"/> to set <see cref="ElementIdentityProperty"/> on.
    /// </param>
    /// <param name="value">
    /// ElementIdentity property value.
    /// </param>
    public static void SetElementIdentity(DependencyObject obj, TitleBarElementIdentity value)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        obj.SetValue(ElementIdentityProperty, value);
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            hwndSource.AddHook(FluentWindowSourceHook);

            if (!Utilities.IsOSWindows11OrNewer)
            {
                var margins = new MARGINS()
                {
                    cxLeftWidth    = 10,
                    cxRightWidth   = 10,
                    cyBottomHeight = 10,
                    cyTopHeight    = 10,
                };

                PInvoke.DwmExtendFrameIntoClientArea((HWND)hwndSource.Handle, in margins);
            }
        }
    }

    private IntPtr FluentWindowSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var uMsg = (uint)msg;

        switch (uMsg)
        {
            case PInvoke.WM_DPICHANGED:
                InvalidateDpi();
                break;

            case PInvoke.WM_WININICHANGE:
                InvalidateSystemMetricCache();
                break;

            case PInvoke.WM_STYLECHANGED:
            {
                if ((wParam & (nint)WINDOW_LONG_PTR_INDEX.GWL_STYLE) != 0)
                {
                    var ss    = Marshal.PtrToStructure<STYLESTRUCT>(lParam);
                    var style = ss.styleNew;

                    var actualState =
                        (style & (nint)WINDOW_STYLE.WS_MINIMIZE) != 0 ? WindowState.Minimized :
                        (style & (nint)WINDOW_STYLE.WS_MAXIMIZE) != 0 ? WindowState.Maximized :
                                                                        WindowState.Normal;

                    // 只更新 ActualWindowState，不触碰 WindowState
                    ActualWindowState = actualState;
                }

                break;
            }

            // WRONG: Do not rely on WM_SIZE for WindowState synchronization.
            //
            // Using WM_SIZE to infer WindowState is unreliable.
            //
            // After a window has been maximized, Windows may still allow limited resizing
            // (e.g. via resize borders). In this situation, the WS_MAXIMIZE style can be
            // cleared while WPF's Window.WindowState remains Maximized.
            // This creates a mismatch between the actual native window state and WPF's
            // logical WindowState.
            //
            // This example is intentionally kept here as a reference for what *not* to do.
            // case PInvoke.WM_SIZE:
            // {
            //     WindowState = (uint)wParam switch
            //     {
            //         PInvoke.SIZE_RESTORED  => WindowState.Normal,
            //         PInvoke.SIZE_MAXIMIZED => WindowState.Maximized,
            //         PInvoke.SIZE_MINIMIZED => WindowState.Minimized,
            //         _                      => WindowState
            //     };
            // }
            //
            // CORRECT: Determine the actual state by inspecting native window styles.
            //
            // We listen to WM_WINDOWPOSCHANGED and determine the effective WindowState
            // by inspecting native window styles *after* the position/state change
            // has been committed.
            case PInvoke.WM_WINDOWPOSCHANGED:
            {
                var style = PInvoke.GetWindowLong((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);

                var actualState =
                    (style & (nint)WINDOW_STYLE.WS_MINIMIZE) != 0 ? WindowState.Minimized :
                    (style & (nint)WINDOW_STYLE.WS_MAXIMIZE) != 0 ? WindowState.Maximized :
                                                                    WindowState.Normal;

                // 只更新 ActualWindowState，不触碰 WindowState
                ActualWindowState = actualState;
                break;
            }

            case PInvoke.WM_NCACTIVATE:
            {
                IsNcActive = wParam != IntPtr.Zero;

                // WindowChromeWorker 说lParam是-1时不会绘制标题栏，但看起来似乎没有用处，需要进一步调查
                // todo 调查使用 DwmDefWindowProc 会不会有效果
                var b = PInvoke.DefWindowProc(new HWND(hwnd), uMsg, new WPARAM((uint)wParam), -1);
                handled = true;
                return b;
            }

            // 窗口处理调整尺寸边框和相关逻辑，没有结果再将消息转发给标题栏。
            case PInvoke.WM_NCHITTEST:
            {
                var screenPoint = LParamToPoint(lParam);

                if (ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip)
                {
                    var dpi     = DpiHelper.GetWindowDpi(hwnd);
                    var wndRect = GetWindowRect(hwnd);
                    var border  = GetLogicalWindowBorderThickness(hwnd, WindowBorderKind.Resize);

                    var ht = HitTestResizeBorder(
                        DpiHelper.DeviceRectToLogical(wndRect, dpi.DpiScaleX, dpi.DpiScaleY),
                        DpiHelper.DevicePixelsToLogical(screenPoint, dpi.DpiScaleX, dpi.DpiScaleY),
                        border
                    );

                    if ((uint)ht is >= PInvoke.HTLEFT and <= PInvoke.HTBOTTOMRIGHT)
                    {
                        // The resize border has been hit and the hit-test result is finalized.
                        // Notify other components participating in non-client interaction
                        // so they can update their state accordingly.
                        PInvoke.PostMessage((HWND)hwnd, WM_NCHITTEST_RESOLVED, 0, 0);

                        handled = true;
                        return ht;
                    }
                }

                // 没有结果，将消息转发给标题栏，让标题栏处理拖动窗口、图标、和系统按钮的命中测试。
                // 标题栏在有结果的情况下应该处理 WM_NCHITTEST_RESOLVED 消息。
                var lResult = PInvoke.SendMessage((HWND)hwnd, WM_NCHITTEST_REQUEST, 0, lParam);
                if (lResult != IntPtr.Zero)
                {
                    handled = true;
                    return lResult;
                }

                // No specific non-client element was hit during this pass.
                // Still send WM_NCHITTEST_RESOLVED as a completion notification,
                // allowing title bar buttons and other participants to reset
                // their visual state if necessary.
                PInvoke.PostMessage((HWND)hwnd, WM_NCHITTEST_RESOLVED, 0, 0);

                break;
            }

            // 非客户端尺寸计算，经测试，目前只有Win11工作正常。
            // Win10 则只能更改三条边，如果更改全部四条边，则会使DWM绘制出一个完整的假标题栏（包括系统按钮）但无法交互。
            // 此行为需要进一步测试，尝试不更改（返回非零的其他值），而使用DwmExtendFrameIntoClientArea
            case PInvoke.WM_NCCALCSIZE:
            {
                handled = true;

                if (Utilities.IsOSWindows11OrNewer)
                {
                    var rcClientArea = Marshal.PtrToStructure<RECT>(lParam);

                    EnsureWindowDpi(hwnd);

                    var dpi          = _cachedDpi;
                    var windowBorder = GetLogicalWindowBorderThickness(hwnd, WindowBorderKind.Layout);
                    var thickness    = DpiHelper.LogicalThicknessToDevice(windowBorder, dpi.DpiScaleX, dpi.DpiScaleY);

                    rcClientArea.left   += (int)Math.Ceiling(thickness.Left);
                    rcClientArea.right  -= (int)Math.Ceiling(thickness.Right);
                    rcClientArea.top    += (int)Math.Ceiling(thickness.Top);
                    rcClientArea.bottom -= (int)Math.Ceiling(thickness.Bottom);

                    Marshal.StructureToPtr(rcClientArea, lParam, false);

                    IntPtr retVal = IntPtr.Zero;
                    if (wParam.ToInt32() != 0) // wParam == TRUE
                    {
                        retVal = new IntPtr(PInvoke.WVR_REDRAW);
                    }

                    return retVal;
                }

                break;
            }
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
}