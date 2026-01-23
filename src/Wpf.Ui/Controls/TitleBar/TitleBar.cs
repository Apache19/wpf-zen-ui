// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Security.Policy;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Wpf.Ui.Designer;
using Wpf.Ui.Extensions.Wpf;
using Wpf.Ui.Hardware;
using Wpf.Ui.Win32;

// ReSharper disable SuspiciousTypeConversion.Global
// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a customizable window title bar that bridges non-client
/// area behavior with WPF rendering and interaction.
/// </summary>
/// <remarks>
/// <see cref="TitleBar"/> participates directly in non-client area processing
/// to provide native window behaviors such as caption buttons, window dragging,
/// resizing, and system interactions, while allowing full control over layout
/// and visual presentation using WPF.
/// <para/>
/// The control is responsible for resolving non-client hit testing results
/// and synchronizing window state, activation state, and mouse interaction
/// information with its child elements.
/// <para/>
/// <see cref="TitleBar"/> is intended to be used as part of a window's title
/// bar infrastructure. Using it outside a window context or without the
/// associated non-client handling may result in undefined behavior.
/// <para/>
/// This control complements, rather than replaces, the window's native
/// non-client processing. It coordinates with the underlying window and
/// message loop instead of attempting to reimplement system behavior.
/// </remarks>
public class TitleBar : Control
{
    static TitleBar()
    {
        // Ensure a non-null Background by default to preserve hit-testing behavior.
        // A Transparent brush is used as a safe fallback to prevent hit testing from
        // being disabled when Background is explicitly set to null.
        BackgroundProperty.OverrideMetadata(
            typeof(TitleBar),
            new FrameworkPropertyMetadata(
                Brushes.Transparent,
                null,
                static (_, baseValue) => baseValue ?? Brushes.Transparent
            )
        );

        // Set the default vertical alignment of the TitleBar to Top.
        // This aligns the control with typical window chrome positioning
        // and avoids unintended vertical centering when hosted in layout containers.
        VerticalAlignmentProperty.OverrideMetadata(
            typeof(TitleBar),
            new FrameworkPropertyMetadata(VerticalAlignment.Top)
        );
    }

    #region DP: Icon, Title, UserContent, IsIconInteractionEnabled, IsNcActive, WindowStyle, WindowResizeMode, ElementIdentity

    #region Icon

    /// <summary>
    /// Identifies the <see cref="Icon"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(IconElement),
        typeof(TitleBar),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the icon displayed in the title bar.
    /// </summary>
    /// <remarks>
    /// This property represents the window icon shown in the title bar, following
    /// conventional Windows window icon behavior.
    ///
    /// When the icon is double-clicked, the window may be closed, consistent with
    /// standard system title bar semantics. This behavior is controlled by the
    /// <see cref="IsIconInteractionEnabled"/> property.
    ///
    /// If set to <see langword="null"/>, no icon is displayed and no icon-related
    /// interactions are available.
    /// </remarks>
    public IconElement? Icon
    {
        get => (IconElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion

    #region Title

    /// <summary>
    /// Identifies the <see cref="Title"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(object),
        typeof(TitleBar),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the title content displayed in the title bar.
    /// </summary>
    /// <remarks>
    /// This property represents the content displayed as the window title.
    /// Although commonly set to a <see cref="string"/>, it accepts any object.
    ///
    /// If a <see cref="string"/> is provided, it is automatically rendered as text.
    /// If a UI element is provided, it will be displayed directly, allowing
    /// full customization of the title presentation.
    ///
    /// The layout, alignment, and visual appearance of the title content are
    /// the responsibility of the consumer.
    ///
    /// If set to <see langword="null"/>, no title content is displayed.
    /// </remarks>
    public object? Title
    {
        get => (object?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    #endregion

    #region UserContent

    /// <summary>
    /// Identifies the <see cref="UserContent"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty UserContentProperty = DependencyProperty.Register(
        nameof(UserContent),
        typeof(object),
        typeof(TitleBar),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the custom content displayed in the center area of the TitleBar.
    /// </summary>
    /// <remarks>
    /// This property allows developers to inject arbitrary UI elements into the middle
    /// section of the title bar, such as search boxes, navigation controls, or status indicators.
    ///
    /// The TitleBar does not perform any automatic layout or styling for this content.
    /// Consumers are responsible for arranging, aligning, and styling the provided UI
    /// to ensure visual consistency with the rest of the application.
    ///
    /// If this property is not set, the center area of the title bar remains empty.
    /// </remarks>
    /// <example>
    /// &lt;ui:TitleBar&gt;
    ///     &lt;ui:TitleBar.UserContent&gt;
    ///         &lt;StackPanel Orientation="Horizontal" VerticalAlignment="Center"&gt;
    ///             &lt;Button Content="User Button"/&gt;
    ///             &lt;TextBlock Text="User Content"/&gt;
    ///         &lt;/StackPanel&gt;
    ///     &lt;/ui:TitleBar.UserContent&gt;
    /// &lt;/ui:TitleBar&gt;
    /// </example>
    public object? UserContent
    {
        get => GetValue(UserContentProperty);
        set => SetValue(UserContentProperty, value);
    }

    #endregion

    #region IsIconInteractionEnabled

    /// <summary>
    /// Identifies the <see cref="IsIconInteractionEnabled"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsIconInteractionEnabledProperty =
        DependencyProperty.Register(
            nameof(IsIconInteractionEnabled),
            typeof(bool),
            typeof(TitleBar),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Gets or sets a value indicating whether standard system-defined
    /// interactions for the title bar icon are permitted.
    /// </summary>
    /// <remarks>
    /// When enabled, the title bar does not restrict system-defined
    /// interactions associated with the window icon.
    ///
    /// When disabled, such interactions are not permitted, and the icon
    /// area behaves as a non-interactive part of the title bar.
    ///
    /// The default value is <see langword="false"/>.
    /// </remarks>
    public bool IsIconInteractionEnabled
    {
        get => (bool)GetValue(IsIconInteractionEnabledProperty);
        set => SetValue(IsIconInteractionEnabledProperty, value);
    }

    #endregion

    #region IsNcActive

    private static readonly DependencyPropertyKey IsNcActivePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsNcActive),
            typeof(bool),
            typeof(TitleBar),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsNcActive"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsNcActiveProperty =
        IsNcActivePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets or sets a value indicating whether the window is currently in an active
    /// non-client (title bar) state.
    /// </summary>
    /// <remarks>
    /// This property reflects the activation state of the window as it applies
    /// to the title bar and other non-client visuals.
    ///
    /// The value is updated internally in response to non-client related window
    /// messages (such as <c>WM_NCACTIVATE</c>) and is intended for visual state
    /// updates only.
    ///
    /// This property is read-only and should not be set by consumers.
    /// </remarks>
    public bool IsNcActive
    {
        get => (bool)GetValue(IsNcActiveProperty);
        protected set => SetValue(IsNcActivePropertyKey, value);
    }

    #endregion

    #region WindowStyle

    private static readonly DependencyPropertyKey WindowStylePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(WindowStyle),
            typeof(WindowStyle),
            typeof(TitleBar),
            new PropertyMetadata(WindowStyle.SingleBorderWindow)
        );

    /// <summary>
    /// Identifies the <see cref="WindowStyle"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty WindowStyleProperty =
        WindowStylePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets or sets the current <see cref="System.Windows.WindowStyle"/> of the associated window.
    /// </summary>
    /// <remarks>
    /// This property reflects the window style as observed by the title bar and is
    /// updated internally when the window style changes.
    ///
    /// It is intended for visual state updates, allowing the title bar to adjust its
    /// appearance when the window switches between different styles, such as
    /// a standard window and a tool window.
    ///
    /// This property is read-only and does not control the window style.
    /// </remarks>
    public WindowStyle WindowStyle
    {
        get => (WindowStyle)GetValue(WindowStyleProperty);
        protected set => SetValue(WindowStylePropertyKey, value);
    }

    private static readonly DependencyPropertyDescriptor WindowStylePropertyDescriptor =
        DependencyPropertyDescriptor.FromProperty(
            Window.WindowStyleProperty,
            typeof(Window)
        );

    #endregion

    #region WindowResizeMode

    private static readonly DependencyPropertyKey WindowResizeModePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(WindowResizeMode),
            typeof(ResizeMode),
            typeof(TitleBar),
            new PropertyMetadata(ResizeMode.CanResize)
        );

    /// <summary>
    /// Identifies the <see cref="WindowResizeMode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty WindowResizeModeProperty =
        WindowResizeModePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets or sets the current <see cref="System.Windows.ResizeMode"/> of the associated window.
    /// </summary>
    /// <remarks>
    /// This property reflects the resize mode of the window as observed by the title bar
    /// and is updated internally when the window resize mode changes.
    ///
    /// It is intended for visual state updates, allowing the title bar to adjust its
    /// presentation and available controls. Typical scenarios include disabling the
    /// maximize button, or hiding minimize and maximize buttons when resizing is not
    /// permitted.
    ///
    /// This property is read-only and does not control the window resize mode.
    /// </remarks>
    public ResizeMode WindowResizeMode
    {
        get => (ResizeMode)GetValue(WindowResizeModeProperty);
        protected set => SetValue(WindowResizeModePropertyKey, value);
    }

    private static readonly DependencyPropertyDescriptor WindowResizeModePropertyDescriptor =
        DependencyPropertyDescriptor.FromProperty(
            Window.ResizeModeProperty,
            typeof(Window)
        );

    #endregion

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
    public static readonly DependencyProperty ElementIdentityProperty =
        DependencyProperty.RegisterAttached(
            "ElementIdentity",
            typeof(TitleBarElementIdentity),
            typeof(TitleBar),
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

    #region WindowChrome

    private static readonly DependencyPropertyDescriptor WindowChromePropertyDescriptor =
        DependencyPropertyDescriptor.FromProperty(
            WindowChrome.WindowChromeProperty,
            typeof(Window)
        );

    #endregion

    #endregion

    /// <summary>
    /// A custom window message indicating that a non-client hit test has been resolved.
    /// </summary>
    /// <remarks>
    /// This message is registered and used internally to notify interested components
    /// that the final result of a non-client hit test has been determined.
    ///
    /// It is sent after the hit-test decision is made, allowing title bar elements
    /// (such as system buttons) and other components that have registered an
    /// <see cref="HwndSourceHook"/> to update their visual or interaction state
    /// based on the resolved target.
    ///
    /// This message complements <c>WM_NCHITTEST</c> and is intended for coordination
    /// and state synchronization only; it does not perform hit testing itself.
    ///
    /// <para>
    /// Message parameters:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///     <c>wParam</c>: Indicates whether a title bar button was hit.
    ///     A value of <c>1</c> means a button was hit; any other value means no button was hit.
    ///     This can be used by receivers as a fast-path check.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     <c>lParam</c>: When a button is hit, contains the hash code of the hit button instance;
    ///     otherwise, the value is <c>0</c>.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    internal static readonly uint WM_NCHITTEST_RESOLVED = PInvoke.RegisterWindowMessage("WM_NCHITTEST_RESOLVED");

    private Window?       _window;
    private HwndSource?   _hwndSource;
    private WindowChrome? _chrome;

    /// <summary>
    /// Initializes a new instance of the <see cref="TitleBar"/> class.
    /// </summary>
    public TitleBar()
    {
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DesignerHelper.IsInDesignMode)
        {
            return;
        }

        _window = Window.GetWindow(this);

        if (_window is not null)
        {
            _chrome = WindowChrome.GetWindowChrome(_window);

            IsNcActive       = _window.IsActive;
            WindowStyle      = _window.WindowStyle;
            WindowResizeMode = _window.ResizeMode;

            WindowChromePropertyDescriptor.AddValueChanged(_window, OnWindowChromeChanged);
            WindowStylePropertyDescriptor.AddValueChanged(_window, OnWindowStyleChanged);
            WindowResizeModePropertyDescriptor.AddValueChanged(_window, OnWindowResizeModeChanged);

            _hwndSource = PresentationSource.FromVisual(_window) as HwndSource;
            _hwndSource?.AddHook(OwnerWindowSourceHook);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_hwndSource is not null)
        {
            _hwndSource?.RemoveHook(OwnerWindowSourceHook);
            _hwndSource = null;
        }

        if (_window is not null)
        {
            WindowChromePropertyDescriptor.RemoveValueChanged(_window, OnWindowChromeChanged);
            WindowStylePropertyDescriptor.RemoveValueChanged(_window, OnWindowStyleChanged);
            WindowResizeModePropertyDescriptor.RemoveValueChanged(_window, OnWindowResizeModeChanged);

            _window = null;
        }
    }

    private IntPtr OwnerWindowSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var uMsg = (uint)msg;

        if (uMsg == WM_NCHITTEST_RESOLVED)
        {
            handled = true;
            return IntPtr.Zero;
        }

        switch (uMsg)
        {
            case PInvoke.WM_NCHITTEST:
            {
                var screenPoint = LParamToPoint(lParam);

                // When WindowChrome is in use, WindowChromeWorker prioritizes user content
                // during hit testing. This can cause the resize border area to be covered
                // by client content, making window resizing via the border impossible.
                //
                // To address this, when the window supports resizing, the resize border
                // is hit-tested explicitly here and the corresponding HT value is returned
                // with higher priority.
                //
                // When WindowChrome is not used, this step is unnecessary because the
                // window itself correctly handles non-client resize hit testing.

                if (_chrome is not null && WindowResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip)
                {
                    var dpi     = GetDpi(hwnd);
                    var wndRect = GetWindowRect(hwnd);

                    var ht = HitTestResizeBorder(
                        DpiHelper.DeviceRectToLogical(wndRect, dpi.DpiScaleX, dpi.DpiScaleY),
                        DpiHelper.DevicePixelsToLogical(screenPoint, dpi.DpiScaleX, dpi.DpiScaleY)
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

                var relativePoint = PointFromScreen(screenPoint);

                if (relativePoint is { X: >= 0, Y: >= 0 } &&
                    relativePoint.X <= ActualWidth && relativePoint.Y <= ActualHeight)
                {
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
                }

                // No specific non-client element was hit during this pass.
                // Still send WM_NCHITTEST_RESOLVED as a completion notification,
                // allowing title bar buttons and other participants to reset
                // their visual state if necessary.
                PInvoke.SendMessage((HWND)hwnd, WM_NCHITTEST_RESOLVED, 0, 0);
                break;
            }

            case PInvoke.WM_NCACTIVATE:
                IsNcActive = wParam != IntPtr.Zero;
                break;

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

    private DisplayDpi GetDpi(IntPtr hwnd)
    {
        if (Utilities.IsOSWindows10_14393_OrNewer)
        {
            return DpiHelper.GetWindowDpi(hwnd);
        }

        var dpiScale = VisualTreeHelper.GetDpi(this);
        return new DisplayDpi(dpiScale.DpiScaleX, dpiScale.DpiScaleY);
    }

    private static readonly IntPtr[,] _HitTestBorders = new[,]
    {
        // @formatter:off
        { (IntPtr)PInvoke.HTTOPLEFT,    (IntPtr)PInvoke.HTTOP,     (IntPtr)PInvoke.HTTOPRIGHT    },
        { (IntPtr)PInvoke.HTLEFT,       IntPtr.Zero,               (IntPtr)PInvoke.HTRIGHT       },
        { (IntPtr)PInvoke.HTBOTTOMLEFT, (IntPtr)PInvoke.HTBOTTOM,  (IntPtr)PInvoke.HTBOTTOMRIGHT },
        // @formatter:on
    };

    /// <summary>
    /// Maps the current mouse position to a resize border or corner hit-test result
    /// within the specified window rectangle.
    /// </summary>
    /// <remarks>
    /// Inspired by the internal hit-testing logic used by WPF's WindowChromeWorker.
    /// Requires window chrome to be initialized; otherwise, returns <see cref="IntPtr.Zero"/>.
    /// </remarks>
    /// <param name="wRect">The window rectangle used for resize border hit testing.</param>
    /// <param name="mPoint">The current mouse position.</param>
    /// <returns>
    /// A hit-test handle corresponding to the resize border or corner under the mouse,
    /// or <see cref="IntPtr.Zero"/> if chrome is not available.
    /// </returns>
    private IntPtr HitTestResizeBorder(Rect wRect, Point mPoint)
    {
        if (_chrome is null)
        {
            return IntPtr.Zero;
        }

        var row = 1;
        var col = 1;

        if (mPoint.X >= wRect.Left && mPoint.X < wRect.Left + (int)_chrome.ResizeBorderThickness.Left)
        {
            col = 0; // left
        }
        else if (mPoint.X < wRect.Right && mPoint.X >= wRect.Right - _chrome.ResizeBorderThickness.Right)
        {
            col = 2; // right
        }

        if (mPoint.Y >= wRect.Top && mPoint.Y < wRect.Top + _chrome.ResizeBorderThickness.Top)
        {
            row = 0; // top
        }
        else if (mPoint.Y < wRect.Bottom && mPoint.Y >= wRect.Bottom - (int)_chrome.ResizeBorderThickness.Bottom)
        {
            row = 2; // bottom
        }

        return _HitTestBorders[row, col];
    }

    /// <summary>
    /// Get the bounding rectangle for the window in physical coordinates.
    /// </summary>
    /// <returns>The bounding rectangle for the window.</returns>
    private static Rect GetWindowRect(IntPtr hwnd)
    {
        if (PInvoke.GetWindowRect((HWND)hwnd, out RECT rect))
        {
            return new Rect(rect.left, rect.top, rect.Width, rect.Height);
        }

        throw new Win32Exception();
    }

    private void CloseWindow()
    {
        if (_window is not null)
        {
            SystemCommands.CloseWindow(_window);
        }
    }

    private void MinimizeWindow()
    {
        if (_window is not null)
        {
            SystemCommands.MinimizeWindow(_window);
        }
    }

    private void MaximizeWindow()
    {
        if (_window is not null)
        {
            if (_window.WindowState == WindowState.Normal)
            {
                SystemCommands.MaximizeWindow(_window);
            }
            else
            {
                SystemCommands.RestoreWindow(_window);
            }
        }
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TitleBarButton d)
        {
            var id = GetElementIdentity(d);
            switch (id)
            {
                case TitleBarElementIdentity.MaximizeButton:
                    MaximizeWindow();
                    break;

                case TitleBarElementIdentity.CloseButton:
                    CloseWindow();
                    break;

                case TitleBarElementIdentity.MinimizeButton:
                    MinimizeWindow();
                    break;
            }
        }
    }

    private void OnWindowChromeChanged(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            _chrome = WindowChrome.GetWindowChrome(window);
        }
    }

    private void OnWindowStyleChanged(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            WindowStyle = window.WindowStyle;
        }
    }

    private void OnWindowResizeModeChanged(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            WindowResizeMode = window.ResizeMode;
        }
    }
}