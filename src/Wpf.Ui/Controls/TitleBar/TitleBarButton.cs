// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

#pragma warning disable SA1512 // Single-line comments should not be followed by blank line
// ReSharper disable SuspiciousTypeConversion.Global
// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a button designed specifically for use within a <see cref="TitleBar"/>.
/// </summary>
/// <remarks>
/// <see cref="TitleBarButton"/> integrates with non-client area input handling
/// to provide native caption button behavior while still supporting WPF
/// interactions such as commands and visual states.
/// <para/>
/// This control is intended to be hosted exclusively within a <see cref="TitleBar"/>.
/// When used outside a TitleBar, its behavior is undefined and may not meet
/// typical button interaction expectations.
/// </remarks>
public class TitleBarButton : ButtonBase
{
    #region Static constructor

    static TitleBarButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TitleBarButton),
            new FrameworkPropertyMetadata(typeof(TitleBarButton))
        );

        // Title bar buttons must not participate in keyboard focus navigation.
        // Non-client interactions are driven by hit testing and system commands;
        // introducing focus or tab stops would desynchronize visual states and
        // conflict with the system-driven non-client interaction model.

        FocusableProperty.OverrideMetadata(
            typeof(TitleBarButton),
            new FrameworkPropertyMetadata(
                false,
                null,
                static (_, _) => false
            )
        );

        IsTabStopProperty.OverrideMetadata(
            typeof(TitleBarButton),
            new FrameworkPropertyMetadata(
                false,
                null,
                static (_, _) => false
            )
        );

        // Title bar buttons must trigger clicks on Release.
        // Hover and Press states must not participate in non-client mouse resolution;
        // otherwise they would interfere with window behaviors such as dragging,
        // resizing, and system command dispatch.
        ClickModeProperty.OverrideMetadata(
            typeof(TitleBarButton),
            new FrameworkPropertyMetadata(
                ClickMode.Release,
                null,
                static (_, _) => ClickMode.Release
            )
        );

        // Ensure a non-null Background by default to preserve hit-testing behavior.
        // A Transparent brush is used as a safe fallback to prevent hit testing from
        // being disabled when Background is explicitly set to null.
        BackgroundProperty.OverrideMetadata(
            typeof(TitleBarButton),
            new FrameworkPropertyMetadata(
                Brushes.Transparent,
                null,
                static (_, baseValue) => baseValue ?? Brushes.Transparent
            )
        );
    }

    #endregion

    #region DependencyProperty: IsMouseActualOver

    private static readonly DependencyPropertyKey IsMouseActualOverPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsMouseActualOver),
            typeof(bool),
            typeof(TitleBarButton),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsMouseActualOver"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsMouseActualOverProperty =
        IsMouseActualOverPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets or sets a value indicating whether the mouse pointer is currently
    /// considered to be over this element.
    /// </summary>
    /// <remarks>
    /// This property represents the final, resolved mouse-over state for the element.
    /// It is independent of mouse capture, button state, keyboard modifiers,
    /// or any other interaction context.
    /// <para/>
    /// This property is only meaningful when the element is hosted within a
    /// <see cref="TitleBar"/>. Outside a TitleBar, the value is not guaranteed
    /// to be accurate or stable.
    /// </remarks>
    public bool IsMouseActualOver
    {
        get => (bool)GetValue(IsMouseActualOverProperty);
        protected set => SetValue(IsMouseActualOverPropertyKey, value);
    }

    #endregion

    private HwndSource? _hwndSource;
    private bool        _isHitTestTarget;

    /// <summary>
    /// Initializes a new instance of the <see cref="TitleBarButton"/> class.
    /// </summary>
    public TitleBarButton()
    {
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Defer adding the HwndSource hook until the window is fully loaded,
        // ensuring that this hook is invoked before the TitleBar processes
        // window messages.
        Dispatcher.BeginInvoke(
            () =>
            {
                var window = Window.GetWindow(this);
                if (window is not null)
                {
                    _hwndSource = PresentationSource.FromVisual(window) as HwndSource;
                    _hwndSource?.AddHook(HwndSourceHook);
                }
            },
            DispatcherPriority.Loaded
        );
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _hwndSource?.RemoveHook(HwndSourceHook);
        _hwndSource = null;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        IsMouseActualOver = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (IsMouseCaptured && Mouse.LeftButton == MouseButtonState.Pressed)
        {
            IsMouseActualOver = IsPressed;
        }
        else
        {
            IsMouseActualOver = IsMouseOver;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        // todo 逻辑似乎有问题，鼠标在别处按下，进入按钮放开鼠标，则会消失背景色
        if (!IsPressed)
        {
            IsMouseActualOver = false;
        }

        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        // 如果工作在标题栏模式，则不需要
        IsMouseActualOver = false;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_hwndSource is not null)
        {
            // Restore non-client mouse leave tracking that was explicitly canceled
            // during client-area interaction, ensuring that subsequent non-client
            // transitions are handled correctly.
            var tme = new TRACKMOUSEEVENT
            {
                cbSize      = (uint)Marshal.SizeOf<TRACKMOUSEEVENT>(),
                dwFlags     = TRACKMOUSEEVENT_FLAGS.TME_NONCLIENT | TRACKMOUSEEVENT_FLAGS.TME_LEAVE,
                hwndTrack   = (HWND)_hwndSource.Handle,
                dwHoverTime = 0
            };
            PInvoke.TrackMouseEvent(ref tme);
        }

        base.OnLostMouseCapture(e);
    }

    private IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var uMsg = (uint)msg;

        if (uMsg == FluentWindow.WM_NCHITTEST_RESOLVED)
        {
            _isHitTestTarget = wParam.ToInt32() == 1 && lParam.ToInt32() == GetHashCode();
            return IntPtr.Zero;
        }

        switch (uMsg)
        {
            // Handle WM_NCMOUSEMOVE to update IsMouseActualOver
            case PInvoke.WM_NCMOUSEMOVE:
            {
                IsMouseActualOver = _isHitTestTarget;
                break;
            }

            // When a non-client mouse button is pressed, capture the mouse to transition the button
            // into client-area interaction mode.
            // This controlled switch between non-client and client behavior allows the button to
            // preserve native system features (such as tooltips and Snap Layout) while still enabling
            // normal WPF click, command, and interaction handling.
            case PInvoke.WM_NCLBUTTONDOWN:
            {
                if (_isHitTestTarget)
                {
                    // Only handle caption button hit targets.
                    var isRelevantHitTest = (uint)wParam switch
                    {
                        PInvoke.HTMAXBUTTON => true, PInvoke.HTMINBUTTON => true,
                        PInvoke.HTCLOSE     => true, PInvoke.HTHELP      => true,
                        _                   => false
                    };

                    if (isRelevantHitTest && Mouse.Captured == null)
                    {
                        // Cancel non-client mouse tracking before capturing the mouse
                        // to prevent spurious WM_NCMOUSELEAVE messages when entering
                        // client-area interaction mode.
                        var tme = new TRACKMOUSEEVENT
                        {
                            cbSize      = (uint)Marshal.SizeOf<TRACKMOUSEEVENT>(),
                            dwFlags     = TRACKMOUSEEVENT_FLAGS.TME_NONCLIENT | TRACKMOUSEEVENT_FLAGS.TME_LEAVE | TRACKMOUSEEVENT_FLAGS.TME_CANCEL,
                            hwndTrack   = (HWND)hwnd,
                            dwHoverTime = 0
                        };
                        PInvoke.TrackMouseEvent(ref tme);

                        // Directly set pressed state and capture mouse.

                        #pragma warning disable WPF0041
                        IsPressed = true;
                        #pragma warning restore WPF0041

                        CaptureMouse();
                    }
                }

                break;
            }

            case PInvoke.WM_NCMOUSELEAVE:
            {
                _isHitTestTarget  = false;
                IsMouseActualOver = false;
                break;
            }
        }

        return IntPtr.Zero;
    }
}
#pragma warning restore SA1512 // Single-line comments should not be followed by blank line