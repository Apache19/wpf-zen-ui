// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Extensions.Wpf;
using Wpf.Ui.Hardware;
using Wpf.Ui.Interop;
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
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(typeof(FluentWindow))
        );

        OverridesDefaultStyleProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(true)
        );
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            hwndSource.AddHook(FluentWindowSourceHook);
        }
    }

    private IntPtr FluentWindowSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var uMsg = (uint)msg;

        switch (uMsg)
        {
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

            case PInvoke.WM_DPICHANGED:
                InvalidateDpi();
                break;

            case PInvoke.WM_WININICHANGE:
                InvalidateSystemMetricCache();
                break;

            // WindowChromeWorker 说lParam是-1时，不会绘制标题栏，但看起来似乎没有用处
            case PInvoke.WM_NCACTIVATE:
            {
                var b = PInvoke.DefWindowProc(new HWND(hwnd), uMsg, new WPARAM((uint)wParam), -1);
                handled = true;
                // IsNcActive = wParam != IntPtr.Zero;
                return b;
            }

            case PInvoke.WM_NCHITTEST:
            {
                // 如果使用自带的系统按钮，则需要将消息传递给 DwmDefWindowProc 以提供命中测试
                var b = PInvoke.DwmDefWindowProc(new HWND(hwnd), (uint)msg, new WPARAM((uint)wParam), new LPARAM(lParam), out LRESULT plResult);
                if (b)
                {
                    PInvoke.SendMessage((HWND)hwnd, WM_NCHITTEST_RESOLVED, 0, 0);
                    return plResult;
                }

                var screenPoint = LParamToPoint(lParam);

                if (ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip)
                {
                    var dpi       = DpiHelper.GetWindowDpi(hwnd);
                    var wndRect   = GetWindowRect(hwnd);
                    var thickness = GetLogicalWindowBorderThickness(hwnd, WindowBorderKind.Resize);
                    var scaleX    = dpi.DpiScaleX;
                    var scaleY    = dpi.DpiScaleY;

                    var ht = HitTestResizeBorder(
                        DpiHelper.DeviceRectToLogical(wndRect, scaleX, scaleY),
                        DpiHelper.DevicePixelsToLogical(screenPoint, scaleX, scaleY),
                        DpiHelper.DeviceThicknessToLogical(thickness, scaleX, scaleY)
                    );

                    if ((uint)ht is >= PInvoke.HTLEFT and <= PInvoke.HTBOTTOMRIGHT)
                    {
                        // The resize border has been hit and the hit-test result is finalized.
                        // Notify other components participating in non-client interaction
                        // so they can update their state accordingly.
                        PInvoke.SendMessage((HWND)hwnd, WM_NCHITTEST_RESOLVED, 0, 0);

                        handled = true;
                        return ht;
                    }
                }

                // Perform hit testing within the title bar area.
                // If the hit element (or one of its ancestors) defines an ElementIdentity
                // attached property, its semantic identity is mapped to the corresponding
                // non-client hit-test result.
                //
                // When a title bar button is hit, a WM_NCHITTEST_RESOLVED message is broadcast
                // with the identity of the hit button, allowing all title bar buttons to
                // synchronize their visual state.

                // todo 在窗口上使用，可以直接offset
                var relativePoint = PointFromScreen(screenPoint);

                if (InputHitTest(relativePoint) is DependencyObject hitElement)
                {
                    var id = GetElementIdentity(hitElement);

                    if (id != TitleBarElementIdentity.Unknown)
                    {
                        var button = hitElement.FindAncestor<TitleBarButton>();

                        if (button is not null)
                        {
                            // A title bar button has been hit. Broadcast the identity of
                            // the hit button via WM_NCHITTEST_RESOLVED, allowing each
                            // button to determine whether it needs to update its own
                            // visual state.
                            var hash = new LPARAM(button.GetHashCode());
                            PInvoke.SendMessage((HWND)hwnd, WM_NCHITTEST_RESOLVED, 1, hash);
                        }
                        else
                        {
                            PInvoke.SendMessage((HWND)hwnd, WM_NCHITTEST_RESOLVED, 0, 0);
                        }

                        // When icon interaction is disabled, redirect icon hit testing to the caption.
                        // This causes the icon area to behave like a normal title bar region,
                        // allowing dragging but suppressing system-defined icon interactions.
                        if (id == TitleBarElementIdentity.Icon && !IsIconInteractionEnabled)
                        {
                            id = TitleBarElementIdentity.Caption;
                        }

                        handled = true;
                        return (IntPtr)id;
                    }
                }

                // No specific non-client element was hit during this pass.
                // Still send WM_NCHITTEST_RESOLVED as a completion notification,
                // allowing title bar buttons and other participants to reset
                // their visual state if necessary.
                PInvoke.SendMessage((HWND)hwnd, WM_NCHITTEST_RESOLVED, 0, 0);
                break;
            }

            // Intercept non-client mouse interactions for specific hit-test targets
            // (typically system buttons) to suppress the default system rendering and
            // behavior in the title bar.
            //
            // This prevents the window from drawing native system button visuals over
            // the custom title bar implementation.
            case PInvoke.WM_NCLBUTTONDOWN:
            case PInvoke.WM_NCLBUTTONUP:
            {
                handled = (uint)wParam switch
                {
                    PInvoke.HTMAXBUTTON => true, PInvoke.HTMINBUTTON => true,
                    PInvoke.HTCLOSE     => true, PInvoke.HTHELP      => true,
                    _                   => handled
                };

                break;
            }

            case PInvoke.WM_NCCALCSIZE:
            {
                if (Utilities.IsOSWindows11OrNewer)
                {
                    EnsureWindowDpi(hwnd);

                    var dpi          = _cachedDpi;
                    var rcClientArea = Marshal.PtrToStructure<RECT>(lParam);
                    var windowBorder = GetLogicalWindowBorderThickness(hwnd, WindowBorderKind.Layout);
                    var thickness    = DpiHelper.LogicalThicknessToDevice(windowBorder, dpi.DpiScaleX, dpi.DpiScaleY);

                    rcClientArea.left   += (int)Math.Ceiling(thickness.Left);
                    rcClientArea.right  -= (int)Math.Ceiling(thickness.Right);
                    rcClientArea.top    += (int)Math.Ceiling(thickness.Top);
                    rcClientArea.bottom -= (int)Math.Ceiling(thickness.Bottom);

                    Marshal.StructureToPtr(rcClientArea, lParam, false);
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