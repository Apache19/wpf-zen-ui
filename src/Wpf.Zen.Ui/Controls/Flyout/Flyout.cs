// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Displays a themed, interactive content panel relative to an explicitly selected element.</summary>
/// <remarks>
/// Declare a Flyout in resources or in <see cref="DropDownButton.Flyout"/>, then call
/// <see cref="ShowAt"/> or set <see cref="PlacementTarget"/> and <see cref="IsOpen"/>.
/// The Flyout itself is the content of a private WPF Popup and must not be added to a Panel or
/// another content host. It occupies no space in the owner's layout. Its replaceable template
/// describes only the surface and must present Content; no named parts or visual states are required.
/// Standard ContentControl templates, selectors, formatting, sizing and alignment remain available.
/// Resources and inherited properties resolve through the placement target while open, as with
/// WPF ContextMenu; closed resource instances requiring data must use an explicit binding source.
/// Opening requires a loaded, visible, enabled target connected to a presentation source. An invalid
/// request resets IsOpen to false. Escape, F4 and Alt+Up/Down dismiss the panel; Tab cycles within it. Unloading,
/// hiding or disabling the target or deactivating its Window closes it even when StaysOpen is true.
/// Targets hosted directly in HwndSource use native top-level activation instead of Window events.
/// Closing restores focus only while the owner remains active and focus is still inside this panel.
/// Use ContextMenu directly for menu items and submenus; Flyout does not adapt native popup hosts.
/// </remarks>
public class Flyout : ContentControl
{
    private Popup? _popup;
    private PresentationSource? _popupTargetSource;
    private bool _isUpdatingPopup;
    private int _lifetimeVersion;
    private FrameworkElement? _observedTarget;
    private Window? _ownerWindow;
    private HwndSource? _ownerSource;
    private HwndSource? _panelSource;
    private DispatcherOperation? _pendingActivationCheck;

    static Flyout()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Flyout),
            new FrameworkPropertyMetadata(typeof(Flyout))
            );

        KeyboardNavigation.TabNavigationProperty.OverrideMetadata(
            typeof(Flyout),
            new FrameworkPropertyMetadata(KeyboardNavigationMode.Cycle)
            );

        KeyboardNavigation.ControlTabNavigationProperty.OverrideMetadata(
            typeof(Flyout),
            new FrameworkPropertyMetadata(KeyboardNavigationMode.Cycle)
            );

        // Skip the surface during tab traversal, but keep Focusable=true as a fallback for panels
        // without focusable content so Escape remains available.
        IsTabStopProperty.OverrideMetadata(
            typeof(Flyout),
            new FrameworkPropertyMetadata(false)
            );

        ContentProperty.OverrideMetadata(
            typeof(Flyout),
            new FrameworkPropertyMetadata(null, null, CoerceContent)
            );
    }

    /// <summary>Initializes a new instance of the <see cref="Flyout"/> class with keyboard and input isolation.</summary>
    public Flyout()
    {
        IsEnabledChanged += (_, _) =>
        {
            if (!IsEnabled)
            {
                Hide();
            }
        };

        // Popup's target-based event route reaches the trigger and its ancestors. Let descendants
        // process their input first, then stop unhandled input from clicking the trigger or scrolling
        // the page behind the panel. Routed commands and preview handlers retain their WPF routes.
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, e) => e.Handled = true));

        MouseDown += (_, e) => e.Handled = true;
        MouseUp += (_, e) => e.Handled = true;
        MouseWheel += (_, e) => e.Handled = true;
    }

    /// <summary>Identifies the <see cref="IsOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(Flyout),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnIsOpenChanged
            )
        );

    /// <summary>Gets or sets a value indicating whether the panel is open. Defaults to false; binds two-way by default.</summary>
    /// <remarks>
    /// Does not inherit or affect the target's layout. The callback creates the native host on first
    /// opening and manages its lifetime. Dismissal updates this property without replacing bindings.
    /// Set PlacementTarget before opening; unavailable targets reject the request rather than opening
    /// at the screen origin. Opened and Closed report native window notifications, not merely DP changes.
    /// </remarks>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Identifies the <see cref="PlacementTarget"/> dependency property.</summary>
    public static readonly DependencyProperty PlacementTargetProperty = DependencyProperty.Register(
        nameof(PlacementTarget),
        typeof(FrameworkElement),
        typeof(Flyout),
        new FrameworkPropertyMetadata(null, OnPlacementTargetChanged, CoercePlacementTarget)
        );

    /// <summary>Gets or sets the element anchoring the panel. The default is null.</summary>
    /// <remarks>
    /// Does not inherit or affect target layout. Changes reposition an open popup and transfer target
    /// lifetime subscriptions. Crossing presentation sources replaces the private host so its native owner
    /// follows the new target; IsOpen remains true, and the replacement raises Opened after taking focus.
    /// Self and descendant targets are rejected to prevent a popup ancestry cycle.
    /// An unavailable replacement closes the panel. No implicit layout-parent fallback is used.
    /// </remarks>
    public FrameworkElement? PlacementTarget
    {
        get => (FrameworkElement?)GetValue(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    /// <summary>Identifies the <see cref="Placement"/> dependency property.</summary>
    public static readonly DependencyProperty PlacementProperty = Popup.PlacementProperty.AddOwner(
        typeof(Flyout), new FrameworkPropertyMetadata(PlacementMode.Bottom));

    /// <summary>Gets or sets WPF popup placement. The default is Bottom, relative to PlacementTarget.</summary>
    /// <remarks>Does not inherit or affect owner layout. WPF repositions on change and may flip at screen edges.</remarks>
    public PlacementMode Placement
    {
        get => (PlacementMode)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>Identifies the <see cref="PlacementRectangle"/> dependency property.</summary>
    public static readonly DependencyProperty PlacementRectangleProperty = Popup.PlacementRectangleProperty.AddOwner(
        typeof(Flyout), new FrameworkPropertyMetadata(Rect.Empty));

    /// <summary>Gets or sets the anchor rectangle in target coordinates; Empty uses the target bounds.</summary>
    /// <remarks>Does not inherit or affect layout. Changes reposition the native Popup.</remarks>
    public Rect PlacementRectangle
    {
        get => (Rect)GetValue(PlacementRectangleProperty);
        set => SetValue(PlacementRectangleProperty, value);
    }

    /// <summary>Identifies the <see cref="HorizontalOffset"/> dependency property.</summary>
    public static readonly DependencyProperty HorizontalOffsetProperty = Popup.HorizontalOffsetProperty.AddOwner(
        typeof(Flyout), new FrameworkPropertyMetadata(0D));

    /// <summary>Gets or sets the horizontal offset in device-independent units. The default is zero.</summary>
    /// <remarks>Supports animation, does not inherit or affect target layout, and repositions the native window.</remarks>
    public double HorizontalOffset
    {
        get => (double)GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    /// <summary>Identifies the <see cref="VerticalOffset"/> dependency property.</summary>
    public static readonly DependencyProperty VerticalOffsetProperty = Popup.VerticalOffsetProperty.AddOwner(
        typeof(Flyout), new FrameworkPropertyMetadata(0D));

    /// <summary>Gets or sets the vertical offset in device-independent units. The default is zero.</summary>
    /// <remarks>Supports animation, does not inherit or affect target layout, and repositions the native window.</remarks>
    public double VerticalOffset
    {
        get => (double)GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>Identifies the <see cref="StaysOpen"/> dependency property.</summary>
    public static readonly DependencyProperty StaysOpenProperty = Popup.StaysOpenProperty.AddOwner(
        typeof(Flyout), new FrameworkPropertyMetadata(false));

    /// <summary>Gets or sets a value indicating whether outside mouse input leaves the panel open. Defaults to false.</summary>
    /// <remarks>Does not inherit or affect layout. WPF updates mouse capture when this changes; Escape still closes.</remarks>
    public bool StaysOpen
    {
        get => (bool)GetValue(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }

    /// <summary>Identifies the <see cref="CustomPopupPlacementCallback"/> dependency property.</summary>
    public static readonly DependencyProperty CustomPopupPlacementCallbackProperty =
        Popup.CustomPopupPlacementCallbackProperty.AddOwner(
        typeof(Flyout), new FrameworkPropertyMetadata(null)
        );

    /// <summary>Gets or sets the callback used by Custom placement. The default is null.</summary>
    /// <remarks>Does not inherit or affect layout. WPF invokes it when positioning a Custom popup.</remarks>
    public CustomPopupPlacementCallback? CustomPopupPlacementCallback
    {
        get => (CustomPopupPlacementCallback?)GetValue(CustomPopupPlacementCallbackProperty);
        set => SetValue(CustomPopupPlacementCallbackProperty, value);
    }

    /// <summary>Identifies the <see cref="PopupAnimation"/> dependency property.</summary>
    public static readonly DependencyProperty PopupAnimationProperty = Popup.PopupAnimationProperty.AddOwner(
        typeof(Flyout), new FrameworkPropertyMetadata(PopupAnimation.Slide));

    /// <summary>Gets or sets the native popup animation. The default is Slide.</summary>
    /// <remarks>Does not inherit or affect owner layout. Slide uses the library's bounded native-slide animator.</remarks>
    public PopupAnimation PopupAnimation
    {
        get => (PopupAnimation)GetValue(PopupAnimationProperty);
        set => SetValue(PopupAnimationProperty, value);
    }

    /// <summary>Identifies the bubbling notification raised when the native popup opens.</summary>
    public static readonly RoutedEvent OpenedEvent = EventManager.RegisterRoutedEvent(
        nameof(Opened), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Flyout));

    /// <summary>Occurs after WPF displays the popup window and initial focus is attempted.</summary>
    /// <remarks>
    /// No notification is raised for an opening cancelled or superseded by an initial-focus callback.
    /// A host replacement across presentation sources raises a new notification while IsOpen stays true.
    /// </remarks>
    public event RoutedEventHandler Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    /// <summary>Identifies the bubbling notification raised when the native popup closes.</summary>
    public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent(
        nameof(Closed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Flyout));

    /// <summary>Occurs when WPF destroys the popup window, which can be later than IsOpen becoming false.</summary>
    /// <remarks>Retiring a host to move an open panel to another presentation source does not raise this event.</remarks>
    public event RoutedEventHandler Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    /// <summary>Opens the panel at the specified loaded target, preserving existing property bindings.</summary>
    /// <param name="target">The element whose bounds anchor the panel.</param>
    public void ShowAt(FrameworkElement target)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        SetCurrentValue(PlacementTargetProperty, target);
        Show();
    }

    /// <summary>Opens the panel using its current PlacementTarget; an unavailable target rejects the request.</summary>
    public void Show()
    {
        SetCurrentValue(IsOpenProperty, true);
    }

    /// <summary>Closes the panel without replacing an IsOpen binding.</summary>
    public void Hide()
    {
        SetCurrentValue(IsOpenProperty, false);
    }

    /// <summary>Raises the native opening notification. Derived classes should call the base implementation.</summary>
    /// <param name="e">The opening notification.</param>
    protected virtual void OnOpened(RoutedEventArgs e)
    {
        RaiseEvent(e);
    }

    /// <summary>Raises the native closing notification. Derived classes should call the base implementation.</summary>
    /// <param name="e">The closing notification.</param>
    protected virtual void OnClosed(RoutedEventArgs e)
    {
        RaiseEvent(e);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.Escape or Key.F4
            || ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 && key is Key.Up or Key.Down))
        {
            Hide();
            e.Handled = true;
        }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
                 or Key.PageUp or Key.PageDown or Key.Home or Key.End)
        {
            // Text editors and inner ScrollViewers already had their opportunity to handle these.
            e.Handled = true;
        }
    }

    private static object? CoerceContent(DependencyObject d, object? value)
    {
        // A second popup host would introduce independent visibility, capture and focus lifetimes.
        // Reject before committing Content so the current panel remains intact on an invalid assignment.
        if (value is System.Windows.Controls.ContextMenu or Popup or Flyout)
        {
            throw new ArgumentException("Flyout.Content must be panel content, not another popup host.");
        }

        return value;
    }

    private static object? CoercePlacementTarget(DependencyObject d, object? value)
    {
        if (value is DependencyObject target)
        {
            for (var current = target; current is not null; current = LogicalTreeHelper.GetParent(current))
            {
                if (ReferenceEquals(current, d))
                {
                    throw new ArgumentException("A Flyout cannot be placed relative to itself or its content.");
                }
            }

            if (((Flyout)d).IsAncestorOf(target))
            {
                throw new ArgumentException("A Flyout cannot be placed relative to its visual descendants.");
            }
        }

        return value;
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var flyout = (Flyout)d;
        flyout._lifetimeVersion++;
        if ((bool)e.NewValue)
        {
            if (!flyout.CanOpen())
            {
                flyout.Hide();
                return;
            }

            flyout.ObserveTarget();
            flyout.EnsurePopup();
        }
        else
        {
            // Move focus only while it is still in this panel and the owner remains active. Closing
            // through an outside click must not steal focus from the element the user just chose.
            var focusTarget = flyout.IsKeyboardFocusWithin && flyout.CanOpen() && flyout.IsOwnerActive()
                ? flyout.PlacementTarget
                : null;
            // Focus callbacks can synchronously reopen the panel. Release only this closing lifetime's
            // subscriptions before invoking application code, so a new opening retains its observers.
            flyout.StopObservingTarget();
            focusTarget?.Focus();
        }
    }

    private static void OnPlacementTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var flyout = (Flyout)d;
        if (!flyout.IsOpen)
        {
            return;
        }

        flyout._lifetimeVersion++;
        if (flyout.CanOpen())
        {
            flyout.ObserveTarget();
            flyout.EnsurePopup();
        }
        else
        {
            flyout.Hide();
        }
    }

    private bool CanOpen()
    {
        return IsEnabled
               && PlacementTarget is { IsLoaded: true, IsVisible: true, IsEnabled: true } target
               && PresentationSource.FromVisual(target) is not null;
    }

    /// <summary>Maintains a host for the target's presentation source, independently of the surface template.</summary>
    private void EnsurePopup()
    {
        if (_isUpdatingPopup)
        {
            return;
        }

        // Parenting changes inherited properties and can run bindings that close or retarget the panel.
        // Complete each tree operation before reconciling the latest request; never reparent recursively.
        _isUpdatingPopup = true;
        try
        {
            while (IsOpen && CanOpen())
            {
                var targetSource = PresentationSource.FromVisual(PlacementTarget!);
                if (_popup is not null && ReferenceEquals(_popupTargetSource, targetSource))
                {
                    return;
                }

                ReleasePopup();
                if (!IsOpen || !CanOpen())
                {
                    return;
                }

                CreatePopup();
            }
        }
        finally
        {
            _isUpdatingPopup = false;
        }
    }

    /// <summary>Detaches a host whose HWND must no longer belong to the previous target's source.</summary>
    private void ReleasePopup()
    {
        if (_popup is not { } popup)
        {
            return;
        }

        // WPF assigns the HWND owner in Popup.BuildWindow only. A PlacementTarget binding change
        // repositions the HWND without changing that owner. Use a fresh Popup across source boundaries;
        // toggling IsOpen immediately would cancel WPF's deferred destruction and reuse the old HWND.
        _popup = null;
        _popupTargetSource = null;
        _panelSource?.RemoveHook(OnHostMessage);
        _panelSource = null;
        popup.Opened -= OnPopupOpened;
        popup.Closed -= OnPopupClosed;
        PopupShadow.SetIsEnabled(popup, false);
        PopupSlideAnimator.SetIsEnabled(popup, false);
        PopupPlacementTracker.SetIsEnabled(popup, false);
        BindingOperations.ClearAllBindings(popup);
        popup.IsOpen = false;
        // Popup.OnChildChanged synchronously releases both the visual and logical parent, even
        // while native destruction is pending. The retired host must no longer retain this content.
        popup.Child = null;
        popup.PlacementTarget = null;
    }

    /// <summary>Connects the content and placement bindings before allowing the native host to open.</summary>
    private void CreatePopup()
    {
        if (LogicalTreeHelper.GetParent(this) is not null || VisualTreeHelper.GetParent(this) is not null)
        {
            Hide();
            throw new InvalidOperationException(
                "Declare Flyout in resources or DropDownButton.Flyout, not in the layout tree."
                );
        }

        _popup = new Popup { AllowsTransparency = true };
        _popupTargetSource = PresentationSource.FromVisual(PlacementTarget!);

        _popup.Opened += OnPopupOpened;
        _popup.Closed += OnPopupClosed;

        _popup.SetBinding(Popup.PopupAnimationProperty, new Binding(nameof(PopupAnimation)) { Source = this });

        PopupSlideAnimator.SetIsEnabled(_popup, true);
        PopupShadow.SetIsEnabled(_popup, true);
        PopupPlacementTracker.SetIsEnabled(_popup, true);

        // WPF's public helper connects target inheritance before parenting this control, binds the
        // placement contract, and binds IsOpen last. All handlers must be ready for synchronous Opened.
        Popup.CreateRootPopup(_popup, this);
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        var version = _lifetimeVersion;
        if (!IsCurrentOpening(sender, version))
        {
            return;
        }

        var movedFocus = MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        // Both focus attempts invoke application handlers synchronously. A close, reopen or retarget
        // ends this opening's work; it must not steal restored focus or publish a stale notification.
        if (!IsCurrentOpening(sender, version))
        {
            return;
        }

        if (!movedFocus)
        {
            Focus();
        }

        if (!IsCurrentOpening(sender, version))
        {
            return;
        }

        OnOpened(new RoutedEventArgs(OpenedEvent, this));
        if (IsCurrentOpening(sender, version))
        {
            ObservePanelSource();
        }
    }

    /// <summary>Rejects continuations belonging to a retired host or an earlier lifetime.</summary>
    private bool IsCurrentOpening(object? sender, int version)
    {
        return IsOpen && _popup?.IsOpen == true && ReferenceEquals(sender, _popup)
               && version == _lifetimeVersion;
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        // Popup window destruction is deferred. An obsolete close must not cancel a newer request.
        if (!ReferenceEquals(sender, _popup) || _popup?.IsOpen == true)
        {
            return;
        }

        Hide();
        OnClosed(new RoutedEventArgs(ClosedEvent, this));
    }

    /// <summary>Tracks only the active target so closed resource flyouts do not keep event subscriptions alive.</summary>
    private void ObserveTarget()
    {
        StopObservingTarget();

        _observedTarget = PlacementTarget!;
        _observedTarget.Unloaded += OnTargetUnloaded;
        _observedTarget.IsVisibleChanged += OnTargetAvailabilityChanged;
        _observedTarget.IsEnabledChanged += OnTargetAvailabilityChanged;
        _ownerWindow = Window.GetWindow(_observedTarget);
        if (_ownerWindow is not null)
        {
            _ownerWindow.Deactivated += OnOwnerDeactivated;
        }
        else
        {
            // Interop targets may be hosted in a child HWND (for example ElementHost).
            // Observe both the target source and the panel: keyboard focus moves between
            // these HWNDs while the same native top-level owner remains active.
            _ownerSource = PresentationSource.FromVisual(_observedTarget) as HwndSource;
            _ownerSource?.AddHook(OnHostMessage);
            if (_ownerSource is not null) _ownerSource.Disposed += OnOwnerSourceDisposed;
            ObservePanelSource();
        }
    }

    private void ObservePanelSource()
    {
        if (!IsOpen || _ownerSource is null || _panelSource is not null) return;
        _panelSource = PresentationSource.FromVisual(this) as HwndSource;
        _panelSource?.AddHook(OnHostMessage);
    }

    /// <summary>Tests native top-level activation without activating the host or stealing focus.</summary>
    private bool IsOwnerActive()
    {
        if (_ownerWindow is not null) return _ownerWindow.IsActive;
        if (_ownerSource is null || _ownerSource.IsDisposed || _ownerSource.Handle == IntPtr.Zero) return false;
        var owner = PInvoke.GetAncestor((HWND)_ownerSource.Handle, GET_ANCESTOR_FLAGS.GA_ROOT);
        return owner != default && owner == PInvoke.GetForegroundWindow();
    }

    private IntPtr OnHostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WPF Window raises Deactivated from WM_ACTIVATE. Embedded sources also need
        // WM_KILLFOCUS from the panel, since their native parent owns activation messages.
        // Defer until the native focus transition completes; entering the panel is not dismissal.
        if (message is (int)PInvoke.WM_ACTIVATE or (int)PInvoke.WM_ACTIVATEAPP or (int)PInvoke.WM_KILLFOCUS
            && _pendingActivationCheck is null)
        {
            _pendingActivationCheck = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                _pendingActivationCheck = null;
                if (IsOpen && !IsOwnerActive()) Hide();
            }));
        }

        return IntPtr.Zero;
    }

    private void OnOwnerSourceDisposed(object? sender, EventArgs e) => Hide();

    private void StopObservingTarget()
    {
        _pendingActivationCheck?.Abort();
        _pendingActivationCheck = null;
        _panelSource?.RemoveHook(OnHostMessage);
        _panelSource = null;
        if (_ownerSource is not null)
        {
            _ownerSource.Disposed -= OnOwnerSourceDisposed;
            _ownerSource.RemoveHook(OnHostMessage);
            _ownerSource = null;
        }

        if (_observedTarget is not null)
        {
            _observedTarget.Unloaded -= OnTargetUnloaded;
            _observedTarget.IsVisibleChanged -= OnTargetAvailabilityChanged;
            _observedTarget.IsEnabledChanged -= OnTargetAvailabilityChanged;
            _observedTarget = null;
        }

        if (_ownerWindow is not null)
        {
            _ownerWindow.Deactivated -= OnOwnerDeactivated;
            _ownerWindow = null;
        }
    }

    private void OnTargetUnloaded(object sender, RoutedEventArgs e) => Hide();
    private void OnOwnerDeactivated(object? sender, EventArgs e) => Hide();
    private void OnTargetAvailabilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!CanOpen())
        {
            Hide();
        }
    }
}
