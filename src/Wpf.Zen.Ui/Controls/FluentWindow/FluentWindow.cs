// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.Extensions.Wpf;
using Wpf.Zen.Ui.Interop;
using Wpf.Zen.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides a WPF window whose content extends through the native caption while retaining platform window behavior.
/// </summary>
/// <remarks>
/// <para>
/// FluentWindow implements its frame directly through <c>WM_NCCALCSIZE</c>. Do not attach
/// <see cref="System.Windows.Shell.WindowChrome"/>: both components would rewrite the same client rectangle and
/// compete for non-client hit testing.
/// </para>
/// <para>
/// A hosted <see cref="TitleBar"/> supplies semantic hit-test roles through its visual tree. FluentWindow remains
/// the only HWND hook, which keeps resize borders, caption buttons, Snap Layout and WPF input in one ordered state
/// machine.
/// </para>
/// </remarks>
public partial class FluentWindow : Window
{
    private const int SizeRestored = 0;
    private const int SizeMinimized = 1;
    private const int SizeMaximized = 2;

    static FluentWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(typeof(FluentWindow))
        );

        // A FluentWindow border represents the fallback visual frame, not arbitrary content decoration. Coercing
        // this value keeps custom templates and derived windows synchronized with the active native frame strategy.
        BorderThicknessProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(new Thickness(1), null, CoerceBorderThickness)
        );

        if (Utilities.IsWin_11_Or_Greater)
        {
            InitializeStatic_Win11();
        }
    }

    #region Dependency properties

    private static readonly DependencyPropertyKey ActualWindowStatePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActualWindowState),
            typeof(WindowState),
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(
                WindowState.Normal,
                static (d, _) => d.CoerceValue(BorderThicknessProperty)
            )
        );

    /// <summary>
    /// Identifies the <see cref="ActualWindowState"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActualWindowStateProperty =
        ActualWindowStatePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the state reported by the native <c>WM_SIZE</c> message.
    /// </summary>
    /// <remarks>
    /// Public HwndSource hooks run before WPF synchronizes <see cref="WindowState"/>. This property is retained so
    /// frame templates and caption glyphs can observe the state that Windows is processing without timing races.
    /// </remarks>
    public WindowState ActualWindowState
    {
        get => (WindowState)GetValue(ActualWindowStateProperty);
        private set => SetValue(ActualWindowStatePropertyKey, value);
    }

    private static readonly DependencyPropertyKey IsNcActivePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsNcActive),
        typeof(bool),
        typeof(FluentWindow),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// Identifies the <see cref="IsNcActive"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsNcActiveProperty = IsNcActivePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether Windows currently renders the non-client area as active.
    /// </summary>
    public bool IsNcActive
    {
        get => (bool)GetValue(IsNcActiveProperty);
        private set => SetValue(IsNcActivePropertyKey, value);
    }

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
    /// Gets a value indicating whether the user enabled the Windows setting that applies the accent color to title bars and borders.
    /// </summary>
    public bool IsAccentOnTitleBarAndBorder
    {
        get => (bool)GetValue(IsAccentOnTitleBarAndBorderProperty);
        private set => SetValue(IsAccentOnTitleBarAndBorderPropertyKey, value);
    }

    /// <summary>
    /// Identifies the <see cref="UseNativeWindowBorder"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty UseNativeWindowBorderProperty = DependencyProperty.Register(
        nameof(UseNativeWindowBorder),
        typeof(bool),
        typeof(FluentWindow),
        new FrameworkPropertyMetadata(true, OnUseNativeWindowBorderChanged)
    );

    /// <summary>
    /// Gets or sets a value indicating whether Windows 10 or later retains its usable native side and bottom frame margins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default value is <see langword="true"/>. Setting the property to <see langword="false"/> makes the
    /// normal client area fill the HWND, which avoids the native-frame startup flash seen by some WPF windows.
    /// On Windows 11, FluentWindow retains a minimal DWM frame extension so the system continues to draw the
    /// outline, shadow and rounded corners even when <see cref="WindowBackdropType"/> is <see cref="WindowBackdropType.None"/>.
    /// On Windows 10, FluentWindow replaces the removed native frame with a four-edge WPF border.
    /// </para>
    /// <para>
    /// On Windows 10, <see langword="true"/> selects the three-edge hybrid native frame and
    /// <see langword="false"/> selects the fully client-rendered fallback. The property has no visual effect on
    /// Windows 8.1 and earlier because those systems always require the fallback.
    /// </para>
    /// <para>
    /// The property can be changed after the HWND is shown. FluentWindow requests <c>SWP_FRAMECHANGED</c> so Windows
    /// immediately runs a fresh non-client calculation without changing the window's bounds or activation state.
    /// </para>
    /// </remarks>
    public bool UseNativeWindowBorder
    {
        get => (bool)GetValue(UseNativeWindowBorderProperty);
        set => SetValue(UseNativeWindowBorderProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsTitleBarHitTestVisibleWhenCovered"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsTitleBarHitTestVisibleWhenCoveredProperty =
        DependencyProperty.Register(
            nameof(IsTitleBarHitTestVisibleWhenCovered),
            typeof(bool),
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(true)
        );

    /// <summary>
    /// Gets or sets a value indicating whether title-bar drag regions and caption buttons remain available when
    /// another visual covers them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default value is <see langword="true"/>. In that mode, FluentWindow first honors the ordinary topmost
    /// WPF input hit. If that visual is outside a <see cref="TitleBar"/> and does not declare a
    /// <see cref="TitleBarHitTestRole"/>, non-client hit testing continues through the visual stacking order until it
    /// finds the first title-bar role. This allows full-window overlays such as a <see cref="ContentDialog"/> mask to
    /// retain window dragging and caption-button behavior. A role-less element inside a title bar remains ordinary
    /// client content and is never treated as an overlay.
    /// </para>
    /// <para>
    /// Setting the property to <see langword="false"/> makes the topmost WPF input hit authoritative. A visual with
    /// no title-bar role then blocks the covered title bar. The property affects only native non-client hit testing;
    /// it does not make covered client controls receive WPF mouse or touch input, and it does not change the higher
    /// priority of the window resize border.
    /// </para>
    /// </remarks>
    public bool IsTitleBarHitTestVisibleWhenCovered
    {
        get => (bool)GetValue(IsTitleBarHitTestVisibleWhenCoveredProperty);
        set => SetValue(IsTitleBarHitTestVisibleWhenCoveredProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="WindowBackdropType"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty WindowBackdropTypeProperty = DependencyProperty.Register(
        nameof(WindowBackdropType),
        typeof(WindowBackdropType),
        typeof(FluentWindow),
        new PropertyMetadata(
            WindowBackdropType.None,
            static (d, _) => ((FluentWindow)d)._updateBackdrop?.Invoke()
        )
    );

    /// <summary>
    /// Gets or sets the preferred system backdrop for this window.
    /// </summary>
    public WindowBackdropType WindowBackdropType
    {
        get => (WindowBackdropType)GetValue(WindowBackdropTypeProperty);
        set => SetValue(WindowBackdropTypeProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="WindowCornerPreference"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty WindowCornerPreferenceProperty = DependencyProperty.Register(
        nameof(WindowCornerPreference),
        typeof(WindowCornerPreference),
        typeof(FluentWindow),
        new PropertyMetadata(
            WindowCornerPreference.Default,
            static (d, e) =>
                ((FluentWindow)d).OnWindowCornerPreferenceChanged((WindowCornerPreference)e.NewValue)
        )
    );

    /// <summary>
    /// Gets or sets the Windows 11 DWM corner preference.
    /// </summary>
    public WindowCornerPreference WindowCornerPreference
    {
        get => (WindowCornerPreference)GetValue(WindowCornerPreferenceProperty);
        set => SetValue(WindowCornerPreferenceProperty, value);
    }

    #endregion

    private readonly Action? _updateBackdrop;

    private HwndSource? _hwndSource;
    private HWND _hwnd;
    private HMENU _systemMenu;
    private WindowFrameInsets _resizeFrame;
    private TitleBarButton? _pendingCaptionButton;
    private TitleBarButton? _pressedCaptionButton;
    private TitleBarButton? _hotCaptionButton;
    private bool _isTrackingNonClientMouse;
    private bool _isThemeSubscribed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentWindow"/> class.
    /// </summary>
    public FluentWindow()
    {
        if (Utilities.IsWin_11_Or_Greater)
        {
            _updateBackdrop = UpdateBackdrop_Win11;
        }

        IsAccentOnTitleBarAndBorder = UnsafeNativeMethods.IsAccentColorOnTitleBarsEnabled();
    }

    /// <inheritdoc />
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is not HwndSource source)
        {
            return;
        }

        _hwndSource = source;
        _hwnd = (HWND)source.Handle;
        source.AddHook(FluentWindowSourceHook);

        ActualWindowState = Utilities.GetHwndState(_hwnd);
        IsNcActive = IsActive;

        // Cache the standard menu before removing WS_SYSMENU. GetSystemMenu may no longer return it once the style
        // disappears, but the HWND-owned menu remains valid for the lifetime of the window and can still dispatch
        // standard WM_SYSCOMMAND values.
        _systemMenu = PInvoke.GetSystemMenu(_hwnd, false);

        // WS_SYSMENU makes modern DWM paths paint caption buttons independently of the WTA_NONCLIENT flags. There
        // is no supported policy that disables only those buttons while retaining DWM borders and shadows, so the
        // style is removed and FluentWindow supplies the menu entry points itself.
        _ = UnsafeNativeMethods.RemoveWindowSystemMenuStyle(_hwnd);

        // WTNCA_NODRAWCAPTION remains useful for the native title and icon. It is deliberately only a best-effort
        // supplement; WS_SYSMENU removal above is what guarantees that native caption buttons are absent.
        _ = UnsafeNativeMethods.SuppressWindowCaptionRendering(source.Handle);

        if (!_isThemeSubscribed)
        {
            ThemeManager.Changed += OnAppThemeChanged;
            _isThemeSubscribed = true;
        }

        _updateBackdrop?.Invoke();
        UpdateLegacyFrameExtension();
        OnWindowCornerPreferenceChanged(WindowCornerPreference);
        CoerceValue(BorderThicknessProperty);
        RequestFrameRecalculation();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        if (_isThemeSubscribed)
        {
            ThemeManager.Changed -= OnAppThemeChanged;
            _isThemeSubscribed = false;
        }

        _hwndSource?.RemoveHook(FluentWindowSourceHook);
        _hwndSource = null;
        _hwnd = HWND.Null;
        _systemMenu = default;

        base.OnClosed(e);
    }

    private static object CoerceBorderThickness(DependencyObject d, object? baseValue)
    {
        var window = (FluentWindow)d;
        return FluentWindowNonClientArea.CalculateVisualBorderThickness(
            window.GetFrameStrategy(),
            window.ActualWindowState
        );
    }

    private static void OnUseNativeWindowBorderChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        var window = (FluentWindow)d;

        // BorderThickness is an effective frame output rather than an independent decoration value. Re-coercion
        // must happen even before the HWND exists so templates update immediately when this property is data-bound.
        window.CoerceValue(BorderThicknessProperty);

        if (Utilities.IsWin_10_Or_Greater && window._hwnd != HWND.Null)
        {
            if (Utilities.IsWin_11_Or_Greater)
            {
                // Win11's client-frame bridge depends on both the border and backdrop strategies. Reapplying the
                // backdrop also selects the correct full-window or one-pixel DWM margins for the new frame policy.
                window._updateBackdrop?.Invoke();
            }
            else
            {
                window.UpdateLegacyFrameExtension();
            }

            window.RequestFrameRecalculation();
        }
    }

    private void OnAppThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        _updateBackdrop?.Invoke();
    }

    private FluentWindowFrameStrategy GetFrameStrategy()
    {
        // Before Windows 11, replacing the caption while retaining all four default frame margins can make DWM
        // paint a second, non-interactive caption. Windows 10 tolerates three native edges, so only its top line is
        // supplied by WPF. Older versions use a complete client frame because their native result is less reliable.
        // Windows 11 fixes the false-caption bug, but retaining its complete top resize frame leaves a conspicuous
        // blank strip above WPF content. The native strategy therefore keeps the side and bottom margins while the
        // client covers the top frame; WM_NCHITTEST still supplies the measured top resize zone from inside the HWND.
        return FluentWindowNonClientArea.SelectFrameStrategy(
            Utilities.IsWin_11_Or_Greater,
            Utilities.IsWin_10_Or_Greater,
            UseNativeWindowBorder
        );
    }

    private void RequestFrameRecalculation()
    {
        if (_hwnd == HWND.Null || !PInvoke.IsWindow(_hwnd))
        {
            return;
        }

        const SET_WINDOW_POS_FLAGS flags =
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED
            | SET_WINDOW_POS_FLAGS.SWP_NOMOVE
            | SET_WINDOW_POS_FLAGS.SWP_NOSIZE
            | SET_WINDOW_POS_FLAGS.SWP_NOZORDER
            | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;

        _ = PInvoke.SetWindowPos(_hwnd, HWND.Null, 0, 0, 0, 0, flags);
    }

    private IntPtr FluentWindowSourceHook(
        IntPtr hwndValue,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled
    )
    {
        var hwnd = (HWND)hwndValue;
        var message = (uint)msg;

        switch (message)
        {
            case PInvoke.WM_STYLECHANGING:
                PreventSystemMenuStyle(lParam, wParam);
                break;

            case PInvoke.WM_NCCALCSIZE:
                return HandleNcCalcSize(hwnd, message, wParam, lParam, out handled);

            case PInvoke.WM_NCHITTEST:
                return HandleNcHitTest(hwnd, lParam, out handled);

            case PInvoke.WM_NCMOUSEMOVE:
                SetHotCaptionButton(IsCaptionButtonHit(wParam.ToInt32()) ? _pendingCaptionButton : null);
                BeginNonClientMouseTracking(hwnd);
                break;

            case PInvoke.WM_NCMOUSELEAVE:
                _isTrackingNonClientMouse = false;
                _pendingCaptionButton = null;
                SetHotCaptionButton(null);
                break;

            case PInvoke.WM_NCLBUTTONDOWN:
                if (wParam.ToInt32() == PInvoke.HTSYSMENU)
                {
                    // Fluent Design title bars do not expose the legacy single-click icon menu. Swallowing the
                    // first click, rather than opening a modal TrackPopupMenu loop, allows Windows to synthesize
                    // WM_NCLBUTTONDBLCLK for the optional icon double-click-to-close interaction.
                    handled = true;
                    break;
                }

                if (IsCaptionButtonHit(wParam.ToInt32()) && _pendingCaptionButton is not null)
                {
                    CancelNonClientMouseTracking(hwnd);
                    SetHotCaptionButton(_pendingCaptionButton);
                    _pressedCaptionButton = _pendingCaptionButton;
                    _pendingCaptionButton.BeginNonClientPress();
                    handled = true;
                }

                break;

            case PInvoke.WM_NCLBUTTONUP:
                if (wParam.ToInt32() == PInvoke.HTSYSMENU)
                {
                    // This release belongs to the icon press deliberately consumed above. The system menu remains
                    // available through Alt+Space and a title-bar right click, but never through a left click.
                    handled = true;
                    break;
                }

                if (IsCaptionButtonHit(wParam.ToInt32()) && _pressedCaptionButton is not null)
                {
                    // A non-client press is converted to a captured WPF interaction on button-down. Suppressing the
                    // native button-up prevents DefWindowProc from issuing the same system command a second time.
                    _pressedCaptionButton = null;
                    handled = true;
                }

                break;

            case PInvoke.WM_NCRBUTTONUP:
                var rightButtonHitTest = wParam.ToInt32();
                if (
                    rightButtonHitTest == (int)PInvoke.HTCAPTION
                    || rightButtonHitTest == (int)PInvoke.HTSYSMENU
                )
                {
                    var point = LParamToScreenPoint(lParam);
                    ShowSystemMenuPhysicalCoordinates(hwnd, (int)point.X, (int)point.Y);
                    handled = true;
                }

                break;

            case PInvoke.WM_SYSKEYDOWN:
                // Once WS_SYSMENU is removed DefWindowProc is no longer required to translate Alt+Space into
                // SC_KEYMENU. Handling the system-key message directly also avoids depending on WPF input focus.
                if (wParam.ToInt32() == (int)VIRTUAL_KEY.VK_SPACE && (lParam.ToInt64() & (1L << 29)) != 0)
                {
                    ShowSystemMenuAtWindowCorner(hwnd);
                    handled = true;
                }

                break;

            case PInvoke.WM_NCLBUTTONDBLCLK:
                if (wParam.ToInt32() == PInvoke.HTSYSMENU)
                {
                    // IsIconInteractionEnabled maps the icon to HTSYSMENU only when this opt-in behavior is wanted.
                    // No single-click menu is opened, so the double-click can reach this message normally.
                    _ = PInvoke.PostMessage(hwnd, PInvoke.WM_SYSCOMMAND, new WPARAM(PInvoke.SC_CLOSE), 0);
                    handled = true;
                    break;
                }

                if (IsCaptionButtonHit(wParam.ToInt32()) && _pendingCaptionButton is not null)
                {
                    CancelNonClientMouseTracking(hwnd);
                    SetHotCaptionButton(_pendingCaptionButton);
                    _pressedCaptionButton = _pendingCaptionButton;
                    _pendingCaptionButton.BeginNonClientPress();
                    handled = true;
                }

                break;

            case PInvoke.WM_NCACTIVATE:
                IsNcActive = wParam != IntPtr.Zero;
                UpdateDwmBorderColor_Win11();

                // IsNcActive drives title-bar brushes. A render-priority pass prevents an inactive caption color
                // from lingering while Windows enters its modal move/size loop.
                Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));

                handled = true;
                return PInvoke.DefWindowProc(
                    hwnd,
                    message,
                    new WPARAM((nuint)wParam.ToInt64()),
                    new LPARAM(-1)
                );

            case PInvoke.WM_SIZE:
                UpdateActualWindowState(wParam.ToInt32());
                break;

            case PInvoke.WM_SETTINGCHANGE:
                if (lParam != IntPtr.Zero && Marshal.PtrToStringAuto(lParam) == "ImmersiveColorSet")
                {
                    IsAccentOnTitleBarAndBorder = UnsafeNativeMethods.IsAccentColorOnTitleBarsEnabled();
                }

                // High-contrast and transparency settings also arrive through WM_SETTINGCHANGE. Reapplying the
                // backdrop for every setting notification is inexpensive and guarantees an immediate safe fallback.
                _updateBackdrop?.Invoke();
                UpdateLegacyFrameExtension();

                break;

            case PInvoke.WM_THEMECHANGED:
                _ = UnsafeNativeMethods.SuppressWindowCaptionRendering(hwndValue);
                _updateBackdrop?.Invoke();
                UpdateLegacyFrameExtension();
                RequestFrameRecalculation();
                break;

            case PInvoke.WM_DWMCOMPOSITIONCHANGED:
                // Frame extensions are not persistent across a DWM composition transition. Win11 restores either
                // the backdrop sheet or its one-pixel client-frame bridge; older systems restore the shadow bridge.
                _updateBackdrop?.Invoke();
                UpdateLegacyFrameExtension();
                break;

            case PInvoke.WM_DWMCOLORIZATIONCOLORCHANGED:
                IsAccentOnTitleBarAndBorder = UnsafeNativeMethods.IsAccentColorOnTitleBarsEnabled();
                UpdateDwmBorderColor_Win11();
                break;
        }

        return IntPtr.Zero;
    }

    private IntPtr HandleNcCalcSize(HWND hwnd, uint message, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = true;
        var isMaximized = IsNativeWindowMaximized(hwnd);

        if (wParam == IntPtr.Zero)
        {
            var proposedWindow = Marshal.PtrToStructure<RECT>(lParam);

            // DefWindowProc is the authoritative per-message measurement source. It accounts for the HWND's
            // current styles, DPI and theme without converting a cached SystemParameters thickness to pixels.
            _ = PInvoke.DefWindowProc(hwnd, message, default, new LPARAM(lParam));
            var nativeClient = Marshal.PtrToStructure<RECT>(lParam);

            _resizeFrame = FluentWindowNonClientArea.MeasureFrame(proposedWindow, nativeClient);
            var client = FluentWindowNonClientArea.CalculateClientRect(
                proposedWindow,
                _resizeFrame,
                GetFrameStrategy(),
                isMaximized
            );
            Marshal.StructureToPtr(client, lParam, false);
            return IntPtr.Zero;
        }

        var proposed = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);
        var proposedWindowRect = proposed.rgrc._0;

        // Preserve the proposed window before asking DefWindowProc to replace rgrc[0] with its native client.
        _ = PInvoke.DefWindowProc(
            hwnd,
            message,
            new WPARAM((nuint)wParam.ToInt64()),
            new LPARAM(lParam)
        );
        var native = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);

        _resizeFrame = FluentWindowNonClientArea.MeasureFrame(proposedWindowRect, native.rgrc._0);
        native.rgrc._0 = FluentWindowNonClientArea.CalculateClientRect(
            proposedWindowRect,
            _resizeFrame,
            GetFrameStrategy(),
            isMaximized
        );
        Marshal.StructureToPtr(native, lParam, false);

        // Returning zero avoids the WVR_REDRAW path that can relocate retained WPF content when a theme change
        // produces WM_NCCALCSIZE without a corresponding size or position change.
        return IntPtr.Zero;
    }

    private IntPtr HandleNcHitTest(HWND hwnd, IntPtr lParam, out bool handled)
    {
        var screenPoint = LParamToScreenPoint(lParam);

        // Resize borders deliberately win over title-bar content at the outermost physical pixels. Otherwise a
        // button or custom client element stretched to the edge could make that part of the HWND impossible to size.
        if (
            ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip
            && !IsNativeWindowMaximized(hwnd)
            && PInvoke.GetWindowRect(hwnd, out RECT windowRect)
        )
        {
            var resizeResult = FluentWindowNonClientArea.HitTestResizeBorder(
                windowRect,
                screenPoint,
                _resizeFrame
            );

            if ((uint)resizeResult is >= PInvoke.HTLEFT and <= PInvoke.HTBOTTOMRIGHT)
            {
                _pendingCaptionButton = null;
                handled = true;
                return resizeResult;
            }
        }

        // Native hit testing can reenter while Unloaded handlers remove modal content and the
        // HWND hook is still installed. PointFromScreen requires a connected PresentationSource;
        // once detached, leave the message to the native window procedure instead of touching WPF.
        if (PresentationSource.FromVisual(this) is null)
        {
            _pendingCaptionButton = null;
            handled = false;
            return IntPtr.Zero;
        }

        var localPoint = PointFromScreen(screenPoint);
        if (
            localPoint.X < 0
            || localPoint.Y < 0
            || localPoint.X >= ActualWidth
            || localPoint.Y >= ActualHeight
        )
        {
            _pendingCaptionButton = null;
            handled = false;
            return IntPtr.Zero;
        }

        var hitElement = InputHitTest(localPoint) as DependencyObject;
        if (
            IsTitleBarHitTestVisibleWhenCovered
            && ShouldContinueTitleBarHitTestThrough(hitElement)
        )
        {
            // UIElement.InputHitTest intentionally stops at the topmost input element. That is correct for ordinary
            // routed input, but it differs from the pre-refactor title-bar implementation: each TitleBar used to hit
            // test its own subtree and therefore remained reachable behind sibling overlays. Enumerating raw visual
            // hits only after the ordinary result is a role-less element outside every TitleBar restores that
            // behavior without making title-bar user content non-client or bypassing an explicit Client role on a
            // higher visual.
            hitElement = FindTitleBarHitTestElement(this, localPoint);
        }

        if (hitElement is null)
        {
            _pendingCaptionButton = null;
            handled = false;
            return IntPtr.Zero;
        }

        var role = TitleBar.GetHitTestRole(hitElement);
        if (role == TitleBarHitTestRole.None)
        {
            _pendingCaptionButton = null;
            handled = false;
            return IntPtr.Zero;
        }

        var titleBar = hitElement as TitleBar ?? hitElement.FindAncestor<TitleBar>();
        _pendingCaptionButton = role
            is TitleBarHitTestRole.MinimizeButton
                or TitleBarHitTestRole.MaximizeButton
                or TitleBarHitTestRole.CloseButton
                or TitleBarHitTestRole.HelpButton
            ? hitElement as TitleBarButton ?? hitElement.FindAncestor<TitleBarButton>()
            : null;

        handled = true;
        return FluentWindowNonClientArea.MapTitleBarRole(role, titleBar?.IsIconInteractionEnabled == true);
    }

    /// <summary>
    /// Determines whether non-client hit testing may continue behind the ordinary WPF input hit.
    /// </summary>
    /// <remarks>
    /// <see cref="TitleBarHitTestRole.None"/> has two distinct meanings here. On a window-level sibling such as a
    /// dialog mask it means that the visual has no title-bar semantics and may be skipped when covered hit testing is
    /// enabled. Inside a <see cref="TitleBar"/>, however, it is also the natural value of arbitrary user controls and
    /// must continue to mean <c>HTCLIENT</c>. Treating both cases as an overlay would expose the caption visual below
    /// the control and prevent that control from receiving WPF input even when no mask is present.
    /// </remarks>
    /// <param name="hitElement">The element returned by the ordinary WPF input hit test.</param>
    /// <returns><see langword="true"/> only when lookup may safely continue through the visual stacking order.</returns>
    internal static bool ShouldContinueTitleBarHitTestThrough(DependencyObject? hitElement)
    {
        if (hitElement is null)
        {
            return true;
        }

        if (TitleBar.GetHitTestRole(hitElement) != TitleBarHitTestRole.None)
        {
            return false;
        }

        return hitElement is not TitleBar && hitElement.FindAncestor<TitleBar>() is null;
    }

    /// <summary>
    /// Finds the frontmost visible, input-enabled visual carrying a semantic title-bar hit-test role.
    /// </summary>
    /// <remarks>
    /// WPF's public <see cref="VisualTreeHelper.HitTest(Visual, HitTestFilterCallback, HitTestResultCallback, HitTestParameters)"/>
    /// overload is used because <see cref="UIElement.InputHitTest(Point)"/> deliberately stops after its first result.
    /// The filter mirrors the visibility and hit-test-visibility guards in WPF's internal
    /// <c>UIElement.InputHitTestFilterCallback</c>. Keeping those guards is important: the opt-in continuation is
    /// allowed to cross z-order siblings, but it must never resurrect a title bar disabled by its own visual tree.
    /// </remarks>
    /// <param name="root">The visual at which hit testing starts.</param>
    /// <param name="point">The point expressed in <paramref name="root"/> coordinates.</param>
    /// <returns>The first visual with a non-<see cref="TitleBarHitTestRole.None"/> role, or <see langword="null"/>.</returns>
    internal static DependencyObject? FindTitleBarHitTestElement(Visual root, Point point)
    {
        DependencyObject? hitElement = null;

        VisualTreeHelper.HitTest(
            root,
            static candidate =>
            {
                if (
                    candidate is UIElement element
                    && (element.Visibility != Visibility.Visible || !element.IsHitTestVisible)
                )
                {
                    return HitTestFilterBehavior.ContinueSkipSelfAndChildren;
                }

                return HitTestFilterBehavior.Continue;
            },
            result =>
            {
                var candidate = GetEnabledInputElement(result.VisualHit, root);
                if (
                    candidate is null
                    || TitleBar.GetHitTestRole(candidate) == TitleBarHitTestRole.None
                )
                {
                    return HitTestResultBehavior.Continue;
                }

                hitElement = candidate;
                return HitTestResultBehavior.Stop;
            },
            new PointHitTestParameters(point)
        );

        return hitElement;
    }

    /// <summary>
    /// Resolves a raw visual hit to the enabled WPF input element that owns it.
    /// </summary>
    /// <remarks>
    /// <see cref="VisualTreeHelper.HitTest(Visual, Point)"/> reports disabled visuals, whereas
    /// <see cref="UIElement.InputHitTest(Point)"/> walks upward to the first enabled <see cref="UIElement"/>. Repeating
    /// that step prevents a disabled caption button behind an overlay from regaining its native HT role.
    /// </remarks>
    private static DependencyObject? GetEnabledInputElement(DependencyObject visualHit, Visual root)
    {
        DependencyObject? candidate = visualHit;
        while (candidate is not null)
        {
            if (candidate is UIElement { IsEnabled: true })
            {
                return candidate;
            }

            if (ReferenceEquals(candidate, root))
            {
                break;
            }

            candidate = candidate.GetParent();
        }

        return null;
    }

    private void UpdateActualWindowState(int sizeCode)
    {
        switch (sizeCode)
        {
            case SizeRestored:
                ActualWindowState = WindowState.Normal;
                break;
            case SizeMinimized:
                ActualWindowState = WindowState.Minimized;
                break;
            case SizeMaximized:
                ActualWindowState = WindowState.Maximized;
                break;
        }
    }

    private static bool IsNativeWindowMaximized(HWND hwnd)
    {
        var style = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        return (style & WINDOW_STYLE.WS_MAXIMIZE) != 0;
    }

    private static Point LParamToScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        return new Point((short)value, (short)(value >> 16));
    }

    private static bool IsCaptionButtonHit(int hitTest)
    {
        return hitTest
            is (int)PInvoke.HTMINBUTTON
                or (int)PInvoke.HTMAXBUTTON
                or (int)PInvoke.HTCLOSE
                or (int)PInvoke.HTHELP;
    }

    private void SetHotCaptionButton(TitleBarButton? button)
    {
        if (ReferenceEquals(_hotCaptionButton, button))
        {
            return;
        }

        _hotCaptionButton?.SetNonClientPointerOver(false);
        _hotCaptionButton = button;
        _hotCaptionButton?.SetNonClientPointerOver(true);
    }

    private void BeginNonClientMouseTracking(HWND hwnd)
    {
        if (_isTrackingNonClientMouse)
        {
            return;
        }

        var tracking = new TRACKMOUSEEVENT
        {
            cbSize = (uint)Marshal.SizeOf<TRACKMOUSEEVENT>(),
            dwFlags = TRACKMOUSEEVENT_FLAGS.TME_NONCLIENT | TRACKMOUSEEVENT_FLAGS.TME_LEAVE,
            hwndTrack = hwnd,
        };

        _isTrackingNonClientMouse = PInvoke.TrackMouseEvent(ref tracking);
    }

    private void CancelNonClientMouseTracking(HWND hwnd)
    {
        if (!_isTrackingNonClientMouse)
        {
            return;
        }

        var tracking = new TRACKMOUSEEVENT
        {
            cbSize = (uint)Marshal.SizeOf<TRACKMOUSEEVENT>(),
            dwFlags =
                TRACKMOUSEEVENT_FLAGS.TME_NONCLIENT
                | TRACKMOUSEEVENT_FLAGS.TME_LEAVE
                | TRACKMOUSEEVENT_FLAGS.TME_CANCEL,
            hwndTrack = hwnd,
        };

        _ = PInvoke.TrackMouseEvent(ref tracking);
        _isTrackingNonClientMouse = false;
    }

    /// <summary>
    /// Restores non-client leave tracking after a caption button releases WPF mouse capture.
    /// </summary>
    internal void RestoreNonClientMouseTracking()
    {
        _pressedCaptionButton = null;

        if (_hwnd != HWND.Null)
        {
            _isTrackingNonClientMouse = false;
            BeginNonClientMouseTracking(_hwnd);
        }
    }

    private void ShowSystemMenuAtWindowCorner(HWND hwnd)
    {
        if (!PInvoke.GetWindowRect(hwnd, out RECT windowRect))
        {
            return;
        }

        // The menu is intentionally anchored inside the measured top-left frame rather than using WPF logical
        // coordinates. This stays stable on mixed-DPI monitors and with negative virtual-screen coordinates.
        ShowSystemMenuPhysicalCoordinates(
            hwnd,
            windowRect.left + _resizeFrame.Left,
            windowRect.top + _resizeFrame.Top
        );
    }

    private unsafe void ShowSystemMenuPhysicalCoordinates(HWND hwnd, int x, int y)
    {
        const TRACK_POPUP_MENU_FLAGS flags =
            TRACK_POPUP_MENU_FLAGS.TPM_LEFTBUTTON
            | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON
            | TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD;

        var menu = _systemMenu;
        if (menu.Value == null)
        {
            return;
        }

        var style = (WINDOW_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        UpdateSystemMenu(menu, style, Utilities.GetHwndState(hwnd));

        var command = PInvoke.TrackPopupMenuEx(menu, (uint)flags, x, y, hwnd);
        if (command)
        {
            _ = PInvoke.PostMessage(hwnd, PInvoke.WM_SYSCOMMAND, (nuint)command.Value, 0);
        }
    }

    private static void PreventSystemMenuStyle(IntPtr lParam, IntPtr wParam)
    {
        if (lParam == IntPtr.Zero || wParam.ToInt32() != (int)WINDOW_LONG_PTR_INDEX.GWL_STYLE)
        {
            return;
        }

        // WPF starts every top-level Window with WS_SYSMENU and can rebuild the style when WindowStyle or
        // ResizeMode changes. Editing STYLESTRUCT during WM_STYLECHANGING prevents a later property change from
        // silently reintroducing DWM caption buttons and avoids a second SetWindowLong recursion.
        var styles = Marshal.PtrToStructure<STYLESTRUCT>(lParam);
        styles.styleNew &= ~(uint)WINDOW_STYLE.WS_SYSMENU;
        Marshal.StructureToPtr(styles, lParam, false);
    }

    private static void UpdateSystemMenu(HMENU menu, WINDOW_STYLE style, WindowState state)
    {
        const MENU_ITEM_FLAGS enabled = MENU_ITEM_FLAGS.MF_ENABLED | MENU_ITEM_FLAGS.MF_BYCOMMAND;
        const MENU_ITEM_FLAGS disabled =
            MENU_ITEM_FLAGS.MF_GRAYED | MENU_ITEM_FLAGS.MF_DISABLED | MENU_ITEM_FLAGS.MF_BYCOMMAND;

        var canMinimize = (style & WINDOW_STYLE.WS_MINIMIZEBOX) != 0;
        var canMaximize = (style & WINDOW_STYLE.WS_MAXIMIZEBOX) != 0;
        var canSize = (style & WINDOW_STYLE.WS_THICKFRAME) != 0;

        switch (state)
        {
            case WindowState.Maximized:
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_RESTORE, enabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_MOVE, disabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_SIZE, disabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_MINIMIZE, canMinimize ? enabled : disabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_MAXIMIZE, disabled);
                break;

            case WindowState.Minimized:
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_RESTORE, enabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_MOVE, disabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_SIZE, disabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_MINIMIZE, disabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_MAXIMIZE, canMaximize ? enabled : disabled);
                break;

            default:
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_RESTORE, disabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_MOVE, enabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_SIZE, canSize ? enabled : disabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_MINIMIZE, canMinimize ? enabled : disabled);
                _ = PInvoke.EnableMenuItem(menu, PInvoke.SC_MAXIMIZE, canMaximize ? enabled : disabled);
                break;
        }
    }

    private static bool IsSupportedBackdrop(WindowBackdropType backdropType)
    {
        return backdropType switch
        {
            WindowBackdropType.Auto => Utilities.IsWin_11_22H2_Or_Greater,
            WindowBackdropType.Mica_Alt => Utilities.IsWin_11_22H2_Or_Greater,
            WindowBackdropType.Mica => Utilities.IsWin_10_Or_Greater,
            WindowBackdropType.Acrylic => Utilities.IsWin_7_Or_Greater,
            WindowBackdropType.None => true,
            _ => false,
        };
    }
}