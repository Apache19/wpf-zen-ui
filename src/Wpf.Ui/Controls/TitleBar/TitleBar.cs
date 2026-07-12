// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Extensions.Wpf;
using Wpf.Ui.Win32;

// ReSharper disable SuspiciousTypeConversion.Global
// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a customizable window title bar that integrates native
/// non-client title bar behavior with WPF rendering and layout.
/// </summary>
/// <remarks>
/// <see cref="TitleBar"/> enables a WPF control to function as the visual
/// and interactive representation of a window's title bar while preserving
/// native window behaviors such as caption buttons, window dragging,
/// and system commands.
/// <para/>
/// The control participates in non-client hit testing and propagates
/// relevant window state information (such as activation and window state)
/// to its visual elements, allowing caption buttons and other title bar
/// components to behave consistently with the operating system.
/// <para/>
/// <see cref="TitleBar"/> is designed to be used as part of a window's
/// title bar infrastructure. Using it outside a window context or without
/// the associated window message handling may result in undefined behavior.
/// <para/>
/// This control coordinates with the underlying window and message loop
/// rather than attempting to reimplement the system title bar logic.
/// </remarks>
public class TitleBar : ContentControl
{
    static TitleBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TitleBar),
            new FrameworkPropertyMetadata(typeof(TitleBar))
        );

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

    #region DP: LeftContent, Icon, Title, IsIconInteractionEnabled, IsUserContentEnabled, ElementIdentity

    #region LeftContent

    /// <summary>
    /// Identifies the <see cref="LeftContent"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register(
        nameof(LeftContent),
        typeof(object),
        typeof(TitleBar),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets optional content displayed at the leading edge of the title bar,
    /// before the application icon and title.
    /// </summary>
    /// <remarks>
    /// This slot allows another control to contribute interactive title-bar content
    /// without depending on the title bar's template structure or modifying its layout
    /// properties. The slot measures to zero when the value is <see langword="null"/>.
    /// Interactive content is treated as user content during non-client hit testing so
    /// that it receives normal WPF input instead of initiating a window drag.
    /// </remarks>
    public object? LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    #endregion

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

    #region IsUserContentEnabled

    /// <summary>
    /// Identifies the <see cref="IsUserContentEnabled"/> dependency property.
    /// </summary>
    internal static readonly DependencyProperty IsUserContentEnabledProperty =
        DependencyProperty.Register(
            nameof(IsUserContentEnabled),
            typeof(bool),
            typeof(TitleBar),
            new PropertyMetadata(true)
        );

    /// <summary>
    /// Gets a value indicating whether user-provided content is enabled.
    ///
    /// This property is intended for internal coordination between <see cref="TitleBar"/>
    /// and <see cref="ContentDialog"/> to support input isolation while a dialog is shown.
    /// Exposing this behavior publicly has no meaningful use case, as consumers are expected
    /// to manage the enabled state of their own content directly.
    /// </summary>
    public bool IsUserContentEnabled
    {
        get => (bool)GetValue(IsUserContentEnabledProperty);
        internal set => SetValue(IsUserContentEnabledProperty, value);
    }

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
    private static readonly DependencyProperty ElementIdentityProperty =
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

    #endregion

    private Window? _window;

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
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            return;
        }

        _window = Window.GetWindow(this);

        if (_window is not null)
        {
            var hwndSource = PresentationSource.FromVisual(_window) as HwndSource;
            hwndSource?.AddHook(OwnerWindowSourceHook);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            var hwndSource = PresentationSource.FromVisual(_window) as HwndSource;
            hwndSource?.RemoveHook(OwnerWindowSourceHook);
            _window = null;
        }
    }

    private IntPtr OwnerWindowSourceHook(IntPtr hwndIntPtr, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var hwnd = (HWND)hwndIntPtr;
        var uMsg = (uint)msg;

        if (uMsg == FluentWindow.WM_NCHITTEST_REQUEST)
        {
            var lyx         = lParam.ToInt64();
            var screenPoint = new Point((short)lyx, (short)(lyx >> 16));
            var localPoint  = PointFromScreen(screenPoint);

            if (localPoint is { X: >= 0, Y: >= 0 } && localPoint.X < ActualWidth && localPoint.Y < ActualHeight)
            {
                if (InputHitTest(localPoint) is DependencyObject hitElement)
                {
                    var id = GetElementIdentity(hitElement);

                    if (id != TitleBarElementIdentity.Unknown)
                    {
                        var button = hitElement.FindAncestor<TitleBarButton>();
                        if (button is not null)
                        {
                            var hash = button.GetHashCode();
                            PInvoke.SendMessage(hwnd, FluentWindow.WM_NCHITTEST_RESOLVED, 1, new LPARAM(hash));
                        }
                        else
                        {
                            PInvoke.SendMessage(hwnd, FluentWindow.WM_NCHITTEST_RESOLVED, 0, 0);
                        }

                        if (id == TitleBarElementIdentity.Icon && !IsIconInteractionEnabled)
                        {
                            id = TitleBarElementIdentity.Caption;
                        }

                        handled = true;
                        return (IntPtr)id;
                    }
                }
            }

            return IntPtr.Zero;
        }

        switch (uMsg)
        {
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

            case PInvoke.WM_NCRBUTTONUP:
            {
                if (wParam.ToInt32() != PInvoke.HTCAPTION)
                {
                    break;
                }

                var yx = lParam.ToInt64();
                ShowSystemMenuPhysicalCoordinates(hwnd, (short)yx, (short)(yx >> 16));

                break;
            }
        }

        return IntPtr.Zero;
    }

    private static void ShowSystemMenuPhysicalCoordinates(HWND hwnd, short x, short y)
    {
        const TRACK_POPUP_MENU_FLAGS flag
            = TRACK_POPUP_MENU_FLAGS.TPM_LEFTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON |
              TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD;

        var hmenu = PInvoke.GetSystemMenu(hwnd, false);
        var style = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var state = Utilities.GetHwndState(hwnd);

        UpdateSystemMenu(hmenu, style, state);

        unsafe
        {
            var cmd = PInvoke.TrackPopupMenuEx(hmenu, (uint)flag, x, y, hwnd);
            if (cmd)
            {
                PInvoke.PostMessage(hwnd, PInvoke.WM_SYSCOMMAND, (nuint)cmd.Value, 0);
            }
        }
    }

    private static void UpdateSystemMenu(HMENU hmenu, WINDOW_STYLE style, WindowState state)
    {
        const MENU_ITEM_FLAGS en = MENU_ITEM_FLAGS.MF_ENABLED | MENU_ITEM_FLAGS.MF_BYCOMMAND;
        const MENU_ITEM_FLAGS di = MENU_ITEM_FLAGS.MF_GRAYED | MENU_ITEM_FLAGS.MF_DISABLED | MENU_ITEM_FLAGS.MF_BYCOMMAND;

        var canMinimize = (style & WINDOW_STYLE.WS_MINIMIZEBOX) != 0;
        var canMaximize = (style & WINDOW_STYLE.WS_MAXIMIZEBOX) != 0;
        var canSize     = (style & WINDOW_STYLE.WS_THICKFRAME) != 0;

        switch (state)
        {
            case WindowState.Maximized:
            {
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_RESTORE,  en);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_MOVE,     di);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_SIZE,     di);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_MINIMIZE, canMinimize ? en : di);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_MAXIMIZE, di);
                break;
            }

            case WindowState.Minimized:
            {
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_RESTORE,  en);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_MOVE,     di);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_SIZE,     di);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_MINIMIZE, di);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_MAXIMIZE, canMaximize ? en : di);
                break;
            }

            case WindowState.Normal:
            default:
            {
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_RESTORE,  di);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_MOVE,     en);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_SIZE,     canSize ? en : di);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_MINIMIZE, canMinimize ? en : di);
                PInvoke.EnableMenuItem(hmenu, PInvoke.SC_MAXIMIZE, canMaximize ? en : di);
                break;
            }
        }
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
        if (_window is FluentWindow fluentWindow)
        {
            if (fluentWindow.ActualWindowState == WindowState.Normal)
            {
                SystemCommands.MaximizeWindow(fluentWindow);
            }
            else
            {
                SystemCommands.RestoreWindow(fluentWindow);
            }
        }
        else if (_window is not null)
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
}
