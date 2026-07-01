// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using WinContextMenu = System.Windows.Controls.ContextMenu;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides Wpf.Ui popup opening animation behavior based on the realized popup window root.
/// </summary>
/// <remarks>
/// WPF's built-in <see cref="Popup.PopupAnimation"/> can only use a small set of system animations and
/// does not expose easing or distance control. This behavior runs ahead of WPF's internal
/// <see cref="Popup.IsOpen"/> changed callback, suppresses WPF's forced first-frame popup-root opacity, and then
/// starts a custom timeline on the same internal popup root surface. WPF still chooses the final screen position,
/// while quick close/reopen cycles force WPF's pending window destroy to complete so an old popup HWND surface is
/// not reused for the next opening frame.
/// </remarks>
public static class PopupAnimationBehavior
{
    private const string PopupRootTypeName = "System.Windows.Controls.Primitives.PopupRoot";

    private static readonly FieldInfo? PropertyChangedCallbackField = typeof(PropertyMetadata).GetField(
        "_propertyChangedCallback",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo? CoerceValueCallbackField = typeof(PropertyMetadata).GetField(
        "_coerceValueCallback",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly Type? PopupRootType = typeof(Popup).Assembly.GetType(PopupRootTypeName);
    private static readonly MethodInfo? PopupForceCloseMethod = typeof(Popup).GetMethod(
        "ForceClose",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    [ThreadStatic]
    private static Stack<PopupAnimationController>? openingControllers;

    static PopupAnimationBehavior()
    {
        OverridePopupRootOpacityMetadata();
        PrependIsOpenChangedCallback(Popup.IsOpenProperty, typeof(Popup));
        PrependIsOpenChangedCallback(WinContextMenu.IsOpenProperty, typeof(WinContextMenu));
    }

    /// <summary>
    /// Identifies the attached property that configures the custom Wpf.Ui popup opening animation.
    /// </summary>
    /// <remarks>
    /// Set this property on a <see cref="Popup"/> or <see cref="WinContextMenu"/> and keep
    /// <see cref="Popup.PopupAnimation"/> as
    /// <see cref="PopupAnimation.None"/> to enable the custom animation path. Clearing this property detaches the
    /// behavior and restores any animated values that were touched by the controller.
    /// </remarks>
    public static readonly DependencyProperty AnimationSettingsProperty =
        DependencyProperty.RegisterAttached(
            "AnimationSettings",
            typeof(PopupAnimationSettings),
            typeof(PopupAnimationBehavior),
            new PropertyMetadata(null, OnAnimationSettingsChanged)
        );

    private static readonly DependencyProperty AnimationControllerProperty =
        DependencyProperty.RegisterAttached(
            "AnimationController",
            typeof(PopupAnimationController),
            typeof(PopupAnimationBehavior),
            new PropertyMetadata(default(PopupAnimationController))
        );

    /// <summary>
    /// Helper for getting <see cref="AnimationSettingsProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="AnimationSettingsProperty"/> from.
    /// </param>
    /// <returns>AnimationSettings property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    [AttachedPropertyBrowsableForType(typeof(WinContextMenu))]
    public static PopupAnimationSettings? GetAnimationSettings(DependencyObject element)
    {
        return (PopupAnimationSettings?)element.GetValue(AnimationSettingsProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="AnimationSettingsProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="AnimationSettingsProperty"/> on.
    /// </param>
    /// <param name="value">AnimationSettings property value.</param>
    public static void SetAnimationSettings(DependencyObject element, PopupAnimationSettings? value)
    {
        element.SetValue(AnimationSettingsProperty, value);
    }

    [AttachedPropertyBrowsableForType(typeof(Popup))]
    [AttachedPropertyBrowsableForType(typeof(WinContextMenu))]
    private static PopupAnimationController? GetAnimationController(DependencyObject element)
    {
        return (PopupAnimationController?)element.GetValue(AnimationControllerProperty);
    }

    private static void SetAnimationController(DependencyObject element, PopupAnimationController? value)
    {
        element.SetValue(AnimationControllerProperty, value);
    }

    private static void PrependIsOpenChangedCallback(
        DependencyProperty property,
        Type ownerType)
    {
        if (PropertyChangedCallbackField is null)
        {
            return;
        }

        var metadata = property.GetMetadata(ownerType);
        var originalCallback = (PropertyChangedCallback?)PropertyChangedCallbackField.GetValue(metadata);
        if (originalCallback is null)
        {
            return;
        }

        PropertyChangedCallbackField.SetValue(
            metadata,
            new PropertyChangedCallback(
                (d, e) =>
                {
                    // DependencyPropertyDescriptor and Popup.Opened are both too late: WPF has already called
                    // Popup.ShowWindow by then. Replace the metadata callback in place so the controller can mark
                    // the opening transition before WPF's own OnIsOpenChanged creates and shows the popup HWND.
                    var controller = GetAnimationController(d);
                    var completeCustomOpen = controller?.OnIsOpenChanging(e) == true;

                    try
                    {
                        originalCallback?.Invoke(d, e);
                    }
                    finally
                    {
                        if (controller != null && e.NewValue is not true)
                        {
                            // Popup.HideWindow normally leaves a zero-delay DispatcherTimer that destroys the HWND
                            // later. A close followed immediately by open cancels that timer and reuses the old HWND
                            // surface, which was the source of the full-opacity flash. ForceClose is only safe for
                            // real Popup owners; ContextMenu keeps its private root Popup for its whole lifetime.
                            controller.ForceClosePendingPopupWindow();
                        }

                        if (completeCustomOpen)
                        {
                            GetAnimationController(d)?.OnIsOpenChanged(e);
                        }
                    }
                }
            )
        );
    }

    private static void OverridePopupRootOpacityMetadata()
    {
        if (PopupRootType is null)
        {
            return;
        }

        var baseMetadata  = UIElement.OpacityProperty.GetMetadata(typeof(UIElement));
        var originalCoerce = (CoerceValueCallback?)CoerceValueCallbackField?.GetValue(baseMetadata);

        try
        {
            // Popup.ShowWindow always sets _popupRoot.Value.Opacity = 1.0 immediately before it calls the native
            // ShowWindow wrapper. Override metadata only for WPF's internal PopupRoot so that write can be coerced
            // to 0 during our armed open window without changing Opacity semantics for normal UIElements.
            UIElement.OpacityProperty.OverrideMetadata(
                PopupRootType,
                new UIPropertyMetadata(
                    baseMetadata.DefaultValue,
                    null,
                    (d, baseValue) =>
                    {
                        var value = originalCoerce?.Invoke(d, baseValue) ?? baseValue;
                        return CoercePopupRootOpacity(d, value);
                    }
                )
            );
        }
        catch (InvalidOperationException)
        {
            // PopupRoot metadata may already be sealed if an application created a Popup before Wpf.Ui initialized
            // this behavior. In that case, leave WPF's metadata untouched instead of broadening the hook to UIElement.
        }
    }

    private static object? CoercePopupRootOpacity(DependencyObject d, object? baseValue)
    {
        if (baseValue is double opacity &&
            opacity > 0D &&
            TryGetOpeningController(d, out var controller) &&
            controller.ShouldSuppressPopupRootOpacity(d))
        {
            // This is intentionally a short-lived suppression, not the animation itself. As soon as WPF finishes
            // its internal IsOpen callback, the controller removes this coerce window and starts the real 0 -> 1
            // custom opacity animation.
            return 0D;
        }

        return baseValue;
    }

    private static bool TryGetOpeningController(
        DependencyObject popupRoot,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PopupAnimationController? controller)
    {
        // Usually PopupRoot.Parent points back to the Popup that owns the controller. During creation or internal
        // ContextMenu plumbing that relationship can be temporarily incomplete, so keep a thread-local stack of
        // controllers that are currently inside the WPF IsOpen callback.
        if (popupRoot is FrameworkElement { Parent: DependencyObject owner } &&
            GetAnimationController(owner) is { } ownerController &&
            ownerController.ShouldSuppressPopupRootOpacity(popupRoot))
        {
            controller = ownerController;
            return true;
        }

        if (openingControllers != null)
        {
            foreach (var openingController in openingControllers)
            {
                if (openingController.ShouldSuppressPopupRootOpacity(popupRoot))
                {
                    controller = openingController;
                    return true;
                }
            }
        }

        controller = null;
        return false;
    }

    private static void PushOpeningController(PopupAnimationController controller)
    {
        openingControllers ??= new Stack<PopupAnimationController>();
        openingControllers.Push(controller);
    }

    private static void RemoveOpeningController(PopupAnimationController controller)
    {
        if (openingControllers is not { Count: > 0 })
        {
            return;
        }

        if (ReferenceEquals(openingControllers.Peek(), controller))
        {
            openingControllers.Pop();
            return;
        }

        var controllers = openingControllers.ToArray();
        openingControllers.Clear();

        for (var i = controllers.Length - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(controllers[i], controller))
            {
                openingControllers.Push(controllers[i]);
            }
        }
    }

    internal static bool IsCustomAnimationActive(DependencyObject owner)
    {
        return GetAnimationSettings(owner) != null &&
               GetPopupAnimation(owner) == PopupAnimation.None;
    }

    internal static Duration GetAnimationDuration(DependencyObject owner)
    {
        return new Duration(GetAnimationSettings(owner)?.GetDuration() ?? TimeSpan.Zero);
    }

    internal static IEasingFunction? GetAnimationEasingFunction(DependencyObject owner)
    {
        return GetAnimationSettings(owner)?.GetEasingFunction();
    }

    internal static bool TryGetAnimationStartOffset(
        DependencyObject owner,
        UIElement popupRoot,
        out double startOffset)
    {
        if (GetAnimationController(owner)?.TryGetCurrentAnimationStartOffset(popupRoot, out startOffset) == true)
        {
            return true;
        }

        if (GetAnimationSettings(owner) is not { } settings)
        {
            startOffset = 0D;
            return false;
        }

        startOffset = GetEffectiveStartOffset(settings, popupRoot, ResolveActualVerticalPlacement(owner, popupRoot));
        return true;
    }

    private static PopupAnimation GetPopupAnimation(DependencyObject owner)
    {
        return owner.GetValue(Popup.PopupAnimationProperty) is PopupAnimation animation
            ? animation
            : PopupAnimation.None;
    }

    private static double GetEffectiveStartOffset(
        PopupAnimationSettings settings,
        UIElement popupRoot,
        PopupVerticalPlacement popupPlacement)
    {
        // The requested offset is capped by the popup height so a compact flyout never starts completely
        // outside its own bounds. Positive and negative offsets are preserved, then flipped when WPF actually
        // placed the popup above the target. This makes "slide from target" feel consistent when Popup flips
        // because of screen edges.
        var height = popupRoot.RenderSize.Height;
        if (height <= 0D && popupRoot is FrameworkElement frameworkElement)
        {
            height = frameworkElement.ActualHeight;
        }

        var offsetLimit = Math.Abs(settings.StartOffset);
        var offset = height > 0D
            ? Math.Min(offsetLimit, height)
            : offsetLimit;

        var direction = Math.Sign(settings.StartOffset);
        if (direction == 0)
        {
            direction = -1;
        }

        var startOffset = offset * direction;

        return popupPlacement == PopupVerticalPlacement.AboveTarget
            ? -startOffset
            : startOffset;
    }

    private static PopupVerticalPlacement ResolveActualVerticalPlacement(
        DependencyObject owner,
        UIElement popupRoot)
    {
        // Requested Placement is only a hint: Popup may flip to the opposite side to stay inside the screen.
        // Compare realized screen bounds first, and use the requested placement only when the visual has not
        // been measured or transformed into a reliable screen rectangle yet.
        if (GetPlacementTarget(owner) is UIElement target &&
            TryGetScreenBounds(target, out var targetBounds) &&
            TryGetScreenBounds(popupRoot, out var popupBounds))
        {
            const double Tolerance = 1D;
            if (popupBounds.Bottom <= targetBounds.Top + Tolerance)
            {
                return PopupVerticalPlacement.AboveTarget;
            }

            if (popupBounds.Top >= targetBounds.Bottom - Tolerance)
            {
                return PopupVerticalPlacement.BelowTarget;
            }

            var verticalOffset = GetCenterY(popupBounds) - GetCenterY(targetBounds);
            if (Math.Abs(verticalOffset) > Tolerance)
            {
                return verticalOffset < 0D
                    ? PopupVerticalPlacement.AboveTarget
                    : PopupVerticalPlacement.BelowTarget;
            }
        }

        return ResolveRequestedVerticalPlacement(GetPlacement(owner));
    }

    private static UIElement? GetPlacementTarget(DependencyObject owner)
    {
        return owner switch
        {
            Popup popup                => popup.PlacementTarget,
            WinContextMenu contextMenu => contextMenu.PlacementTarget,
            _                          => null,
        };
    }

    private static PlacementMode GetPlacement(DependencyObject owner)
    {
        return owner switch
        {
            Popup popup                => popup.Placement,
            WinContextMenu contextMenu => contextMenu.Placement,
            _                          => PlacementMode.Bottom,
        };
    }

    private static PopupVerticalPlacement ResolveRequestedVerticalPlacement(PlacementMode placement)
    {
        return placement == PlacementMode.Top
            ? PopupVerticalPlacement.AboveTarget
            : PopupVerticalPlacement.BelowTarget;
    }

    private static bool TryGetScreenBounds(UIElement element, out Rect bounds)
    {
        bounds = Rect.Empty;

        var size = GetRenderSize(element);
        if (size.Width <= 0D || size.Height <= 0D)
        {
            return false;
        }

        try
        {
            // PointToScreen returns physical pixels; scale the WPF size by the visual DPI so both values use
            // the same coordinate space before comparing popup and placement target rectangles.
            var dpi     = VisualTreeHelper.GetDpi(element);
            var topLeft = element.PointToScreen(default);

            bounds = new Rect(
                topLeft,
                new Size(size.Width * dpi.DpiScaleX, size.Height * dpi.DpiScaleY)
            );

            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static Size GetRenderSize(UIElement element)
    {
        var size = element.RenderSize;
        if (element is not FrameworkElement frameworkElement)
        {
            return size;
        }

        if (size.Width <= 0D)
        {
            size.Width = frameworkElement.ActualWidth;
        }

        if (size.Height <= 0D)
        {
            size.Height = frameworkElement.ActualHeight;
        }

        return size;
    }

    private static double GetCenterY(Rect rect)
    {
        return rect.Top + (rect.Height / 2D);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP001:Dispose created",
        Justification = "The controller is stored in an attached property "
                      + "and disposed when AnimationSettings is cleared.")]
    private static void OnAnimationSettingsChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup and not WinContextMenu)
        {
            return;
        }

        var controller = GetAnimationController(d);
        if (e.NewValue is not PopupAnimationSettings settings)
        {
            // The controller owns event subscriptions and transient animation state, but its lifetime is tied
            // to the attached property rather than to a public disposable object.
            controller?.Dispose();
            SetAnimationController(d, null);
            return;
        }

        if (controller == null)
        {
            controller = new PopupAnimationController(d);
            SetAnimationController(d, controller);
        }

        controller.Attach(settings);
    }

    private enum PopupVerticalPlacement
    {
        BelowTarget,
        AboveTarget,
    }

    /// <summary>
    /// Coordinates the custom opening animation for one popup-style owner.
    /// </summary>
    /// <remarks>
    /// A popup creates its actual window content lazily, so the behavior cannot prepare the target visual when
    /// the attached property is set. The controller temporarily lets WPF's own opening path hide the first frame,
    /// then locates the HWND root visual and starts the custom entrance on WPF's internal animation layer.
    /// The close path is deliberately left to WPF: custom values are applied only for opening, and stale callbacks
    /// are invalidated by version tokens instead of trying to animate or reposition content while the popup closes.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0041:Set mutable dependency properties using SetCurrentValue",
        Justification = "The controller writes only transient popup animation state on WPF's internal popup "
                      + "animation surface, then leaves close-time cleanup to PopupRoot.StopAnimations.")]
    private sealed class PopupAnimationController : IDisposable
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly DependencyObject        _owner;
        private          PopupAnimationSettings? _settings;
        private          UIElement?              _popupRoot;
        private          TranslateTransform?     _translateTransform;
        private          double?                 _currentAnimationStartOffset;
        // Each open/close transition advances the version, so stale opening work cannot run against a newer popup life.
        private          int                     _openVersion;
        private          bool                    _isOpeningWithCustomAnimation;
        private          bool                    _isSuppressingPopupRootOpacity;
        private          Popup?                  _pendingContextMenuRootPopup;
        private          bool                    _isSettingsChangedSubscribed;
        private          bool                    _isDisposed;

        public PopupAnimationController(DependencyObject owner)
        {
            _owner = owner;
        }

        public void Attach(PopupAnimationSettings settings)
        {
            #if NET5_0_OR_GREATER
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            #else

            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(PopupAnimationController));
            }

            #endif

            if (_settings != settings)
            {
                // Settings is a Freezable so theme/style changes can update its values without replacing the
                // instance. Frozen settings cannot change, but mutable settings are observed with a weak event
                // to avoid making the behavior itself a source of leaks.
                UnsubscribeSettingsChanged();

                _settings = settings;
                SubscribeSettingsChanged();
            }

            if (IsOwnerOpen)
            {
                // The property can be attached after the popup is already open, especially when styles or
                // templates are applied late. Play the entrance immediately instead of waiting for the next
                // Opened event so the attached behavior is deterministic from the moment settings exist.
                _openVersion++;
                _ = BeginOpenAnimation();
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            Detach();
            _isDisposed = true;
        }

        private void Detach()
        {
            EndPopupRootOpacitySuppression();
            RestoreNaturalState();
            _isOpeningWithCustomAnimation = false;

            if (_settings != null)
            {
                UnsubscribeSettingsChanged();
                _settings = null;
            }
        }

        private void SubscribeSettingsChanged()
        {
            if (_settings is not { IsFrozen: false })
            {
                return;
            }

            WeakEventManager<Freezable, EventArgs>.AddHandler(
                _settings,
                nameof(Freezable.Changed),
                OnSettingsChanged
            );
            _isSettingsChangedSubscribed = true;
        }

        private void UnsubscribeSettingsChanged()
        {
            if (!_isSettingsChangedSubscribed || _settings == null)
            {
                return;
            }

            WeakEventManager<Freezable, EventArgs>.RemoveHandler(
                _settings,
                nameof(Freezable.Changed),
                OnSettingsChanged
            );
            _isSettingsChangedSubscribed = false;
        }

        internal bool OnIsOpenChanging(DependencyPropertyChangedEventArgs e)
        {
            if (_isDisposed || _settings == null)
            {
                return false;
            }

            _openVersion++;

            if (e.NewValue is not true)
            {
                // Close invalidates any pending/opening work. Do not commit the final visual state here: WPF's
                // PopupRoot.StopAnimations will clear animated values as part of its close path, and real Popup
                // owners are force-closed after WPF has queued its normal async destroy.
                _isOpeningWithCustomAnimation = false;
                EndPopupRootOpacitySuppression();
                DetachPendingContextMenuRootPopup();
                ReleaseAnimationReferences();
                return false;
            }

            _isOpeningWithCustomAnimation = IsCustomAnimationRequested;
            if (!_isOpeningWithCustomAnimation)
            {
                return false;
            }

            // WPF's ShowWindow forces PopupRoot.Opacity to 1 immediately before showing the HWND. Keep that write
            // coerced to 0 only for the duration of WPF's internal IsOpen callback; the custom timeline starts
            // synchronously as soon as WPF returns, before the dispatcher can render a fully opaque frame.
            BeginPopupRootOpacitySuppression();
            return true;
        }

        internal void ForceClosePendingPopupWindow()
        {
            if (_isDisposed || _settings == null || _owner is not Popup popup)
            {
                return;
            }

            if (GetPopupAnimation(_owner) != PopupAnimation.None)
            {
                return;
            }

            try
            {
                // This mirrors Popup.ForceClose's intended internal purpose: if HideWindow queued an async destroy,
                // complete it now. It prevents a fast reopen from CancelAsyncDestroy-ing the old HWND and showing a
                // stale compositor surface before the new opening animation reaches the render thread.
                PopupForceCloseMethod?.Invoke(popup, null);
            }
            catch (TargetInvocationException)
            {
                // If WPF changes this internal method, fall back to the normal async destroy path.
            }
            catch (MethodAccessException)
            {
                // If WPF changes this internal method, fall back to the normal async destroy path.
            }
            catch (ArgumentException)
            {
                // If WPF changes this internal method, fall back to the normal async destroy path.
            }
        }

        internal void OnIsOpenChanged(DependencyPropertyChangedEventArgs e)
        {
            if (_isDisposed)
            {
                return;
            }

            if (e.NewValue is not true)
            {
                _isOpeningWithCustomAnimation = false;
                return;
            }

            try
            {
                EndPopupRootOpacitySuppression();
                ApplyRootPopupAnimationMode();

                if (_settings != null)
                {
                    if (_owner is WinContextMenu)
                    {
                        BeginContextMenuOpenAnimation(_openVersion);
                    }
                    else
                    {
                        _ = BeginOpenAnimation(_openVersion);
                    }
                }
            }
            finally
            {
                EndPopupRootOpacitySuppression();
                _isOpeningWithCustomAnimation = false;
            }
        }

        private void BeginContextMenuOpenAnimation(int openVersion)
        {
            if (_owner is not WinContextMenu contextMenu ||
                TryGetContextMenuRootPopup(contextMenu) is not { } rootPopup)
            {
                return;
            }

            if (rootPopup.IsOpen && GetPopupRoot() is UIElement)
            {
                // If the private Popup is already open, its SetupAnimations(true) has already called
                // PopupRoot.StopAnimations. Starting now is safe and will not be cleared by WPF immediately after.
                _ = BeginOpenAnimation(openVersion);
                return;
            }

            // ContextMenu.IsOpen opens a long-lived private Popup via binding. The ContextMenu IsOpen callback can
            // finish before that private Popup reaches ShowWindow/SetupAnimations(true). Starting our timeline too
            // early lets PopupRoot.StopAnimations wipe the clocks, leaving the menu stuck at the first frame while
            // the detached shadow continues. Wait for the private Popup.Opened event instead.
            DetachPendingContextMenuRootPopup();
            _pendingContextMenuRootPopup = rootPopup;
            rootPopup.Opened += OnContextMenuRootPopupOpened;
        }

        private void OnContextMenuRootPopupOpened(object? sender, EventArgs e)
        {
            // Opened is raised after the private Popup's ShowWindow path, so WPF's own StopAnimations call is behind
            // us and the custom timeline can own the PopupRoot until the menu closes.
            DetachPendingContextMenuRootPopup();
            _ = BeginOpenAnimation(_openVersion);
        }

        private void DetachPendingContextMenuRootPopup()
        {
            if (_pendingContextMenuRootPopup == null)
            {
                return;
            }

            _pendingContextMenuRootPopup.Opened -= OnContextMenuRootPopupOpened;
            _pendingContextMenuRootPopup = null;
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            if (IsOwnerOpen && GetPopupAnimation(_owner) == PopupAnimation.None)
            {
                // Treat mutable setting changes as a new entrance rather than trying to retarget running
                // animations in place. Restarting keeps duration, easing, and start offset consistent.
                _ = BeginOpenAnimation();
            }
        }

        private bool BeginOpenAnimation()
        {
            return BeginOpenAnimation(_openVersion);
        }

        private bool BeginOpenAnimation(int openVersion)
        {
            if (_isDisposed || !IsOwnerOpen)
            {
                return false;
            }

            if (openVersion != _openVersion)
            {
                return false;
            }

            if (!IsCustomAnimationRequested || _settings is not { } settings)
            {
                RestoreNaturalState();
                return false;
            }

            ApplyRootPopupAnimationMode();

            if (GetPopupRoot() is not UIElement popupRoot)
            {
                return false;
            }

            if (!TryGetAnimationLayer(popupRoot, out var animationLayer))
            {
                // If WPF changes PopupRoot enough that we cannot find its native animation layer, avoid placing a
                // translation where PopupRoot.StopAnimations cannot clear it.
                animationLayer = null;
            }

            // Requested placement is only a hint. WPF may flip the realized HWND to stay on screen, so offset is
            // calculated only after the popup root exists and its final bounds can be compared with the target.
            var duration = settings.GetDuration();

            var startOffset = GetEffectiveStartOffset(
                settings,
                popupRoot,
                ResolveActualVerticalPlacement(_owner, popupRoot)
            );

            var translateTransform = animationLayer != null
                ? EnsureNativeTranslateTransform(animationLayer)
                : null;

            _popupRoot                   = popupRoot;
            _currentAnimationStartOffset = translateTransform != null
                ? startOffset
                : 0D;

            // Establish the hidden first frame synchronously. PopupRoot.Opacity may have just been forced to 1 by
            // WPF ShowWindow, so the base value must be reset before any Dispatcher/render work can observe it.
            InitializeOpenAnimationBaseValues(popupRoot, translateTransform, startOffset);

            if (duration <= TimeSpan.Zero || translateTransform == null || Math.Abs(startOffset) < 0.1D)
            {
                CompleteOpenAnimation();
                return true;
            }

            StartOpenAnimation(openVersion, popupRoot, translateTransform, startOffset, duration);
            return true;
        }

        private void StartOpenAnimation(
            int openVersion,
            UIElement popupRoot,
            TranslateTransform translateTransform,
            double startOffset,
            TimeSpan duration)
        {
            if (_isDisposed || !IsOwnerOpen || openVersion != _openVersion)
            {
                // Returning without cleanup is intentional here: close/dispose/new-open paths already reset state
                // through their lifecycle handlers.
                return;
            }

            if (_settings == null || GetPopupAnimation(_owner) != PopupAnimation.None)
            {
                RestoreNaturalState();
                return;
            }

            var durationValue  = new Duration(duration);
            var easingFunction = _settings.GetEasingFunction();

            var translateAnimation = new DoubleAnimation(startOffset, 0D, durationValue, FillBehavior.HoldEnd)
            {
                EasingFunction = easingFunction,
            };

            // Keep the fade linear instead of forcing it to follow the movement easing. Opacity is perceived very
            // differently from displacement: an ease-out fade can look like a sudden appearance because the fast
            // early alpha change is hard to track, while linear opacity gives the softest and most legible fade.
            // It also avoids each movement easing accidentally giving the popup a different fade personality.
            var opacityAnimation = new DoubleAnimation(0D, 1D, durationValue, FillBehavior.HoldEnd)
            {
                EasingFunction = null,
            };

            // The translation is placed on PopupRoot's internal adorner decorator, the same layer that WPF's native
            // Slide/Scroll animation uses. PopupRoot.StopAnimations will clear it synchronously when the popup closes.
            // Do not attach Completed handlers that commit final values; HoldEnd already presents the natural final
            // frame, and close/reopen can otherwise race with a stale completion callback from the previous open.
            translateTransform.BeginAnimation(
                TranslateTransform.YProperty,
                translateAnimation,
                HandoffBehavior.SnapshotAndReplace
            );
            popupRoot.BeginAnimation(
                UIElement.OpacityProperty,
                opacityAnimation,
                HandoffBehavior.SnapshotAndReplace
            );
        }

        private void InitializeOpenAnimationBaseValues(
            UIElement popupRoot,
            TranslateTransform? translateTransform,
            double startOffset)
        {
            // Clear any previous open clock before assigning base values. ContextMenu keeps its private PopupRoot
            // alive between openings, and ComboBox can also reuse transforms unless ForceClose has already destroyed
            // the HWND. Fresh base values keep each open independent of the previous animation.
            popupRoot.BeginAnimation(UIElement.OpacityProperty, null);
            popupRoot.Opacity = 0D;

            if (translateTransform != null)
            {
                translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                translateTransform.Y = startOffset;
            }
        }

        private void CompleteOpenAnimation()
        {
            CommitFinalOpenState();

            ReleaseAnimationReferences();
        }

        private void RestoreNaturalState()
        {
            CommitFinalOpenState();

            ReleaseAnimationReferences();
        }

        private void CommitFinalOpenState()
        {
            if (_popupRoot != null)
            {
                _popupRoot.BeginAnimation(UIElement.OpacityProperty, null);
                _popupRoot.Opacity = 1D;
            }

            if (_translateTransform != null)
            {
                _translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                _translateTransform.Y = 0D;
            }
        }

        private void ReleaseAnimationReferences()
        {
            _translateTransform = null;
            _popupRoot          = null;
            _currentAnimationStartOffset = null;
        }

        private bool IsCustomAnimationRequested =>
            _settings != null &&
            GetPopupAnimation(_owner) == PopupAnimation.None;

        internal bool ShouldSuppressPopupRootOpacity(DependencyObject popupRoot)
        {
            return !_isDisposed &&
                   _settings != null &&
                   _isOpeningWithCustomAnimation &&
                   _isSuppressingPopupRootOpacity;
        }

        private void BeginPopupRootOpacitySuppression()
        {
            if (_isSuppressingPopupRootOpacity)
            {
                return;
            }

            _isSuppressingPopupRootOpacity = true;
            PushOpeningController(this);
        }

        private void EndPopupRootOpacitySuppression()
        {
            if (!_isSuppressingPopupRootOpacity)
            {
                return;
            }

            _isSuppressingPopupRootOpacity = false;
            RemoveOpeningController(this);
        }

        internal bool TryGetCurrentAnimationStartOffset(UIElement popupRoot, out double startOffset)
        {
            if (_popupRoot == popupRoot && _currentAnimationStartOffset.HasValue)
            {
                startOffset = _currentAnimationStartOffset.Value;
                return true;
            }

            startOffset = 0D;
            return false;
        }

        private Visual? GetPopupRoot()
        {
            // Popup.Child belongs to the logical popup content, but the visible window is hosted under the
            // HwndSource root visual. The root provides the opacity surface and contains the host we translate.
            return _owner switch
            {
                Popup { Child: Visual child } => (PresentationSource.FromVisual(child) as HwndSource)?.RootVisual,
                WinContextMenu contextMenu    => (PresentationSource.FromVisual(contextMenu) as HwndSource)?.RootVisual,
                _                             => null,
            };
        }

        private bool IsOwnerOpen =>
            _owner switch
            {
                Popup popup                => popup.IsOpen,
                WinContextMenu contextMenu => contextMenu.IsOpen,
                _                          => false,
            };

        private void ApplyRootPopupAnimationMode()
        {
            if (_owner is not WinContextMenu contextMenu ||
                GetPopupAnimation(_owner) != PopupAnimation.None ||
                TryGetContextMenuRootPopup(contextMenu) is not { } rootPopup)
            {
                return;
            }

            // ContextMenu hosts itself in a private Popup created by WPF. Popup.CreateRootPopup binds IsOpen and
            // placement properties, but not PopupAnimation; without this, the private Popup keeps the system menu
            // animation and remains visible briefly after PopupRoot.StopAnimations clears our opening transform.
            // Only change the private Popup's animation mode. Do not force-close it on menu close: ContextMenu owns
            // and reuses this instance, and destroying its HWND out of band leaves later opens in a half-reset state.
            // ComboBox does not need this path because its template owns the real Popup and binds PopupAnimation
            // directly, so the behavior can observe and update the same instance that WPF opens.
            rootPopup.SetCurrentValue(Popup.PopupAnimationProperty, PopupAnimation.None);
        }

        private static Popup? TryGetContextMenuRootPopup(WinContextMenu contextMenu)
        {
            return typeof(WinContextMenu).GetField("_parentPopup", InstanceNonPublic)?.GetValue(contextMenu) as Popup;
        }

        private static bool TryGetAnimationLayer(UIElement popupRoot, out UIElement animationLayer)
        {
            animationLayer = null!;

            if (popupRoot.GetType().GetField("_adornerDecorator", InstanceNonPublic)?.GetValue(popupRoot)
                is UIElement reflectedAnimationLayer)
            {
                animationLayer = reflectedAnimationLayer;
                return true;
            }

            // Current WPF builds expose PopupRoot -> _transformDecorator -> _adornerDecorator in the visual tree.
            // This keeps the behavior usable if private field reflection is unavailable but the structure remains.
            if (VisualTreeHelper.GetChildrenCount(popupRoot) > 0 &&
                VisualTreeHelper.GetChild(popupRoot, 0) is Decorator { Child: UIElement visualTreeAnimationLayer })
            {
                animationLayer = visualTreeAnimationLayer;
                return true;
            }

            return false;
        }

        private TranslateTransform EnsureNativeTranslateTransform(UIElement animationLayer)
        {
            if (animationLayer.RenderTransform is TranslateTransform translateTransform)
            {
                _translateTransform = translateTransform;
                return translateTransform;
            }

            translateTransform = new TranslateTransform();
            animationLayer.RenderTransform = translateTransform;
            _translateTransform = translateTransform;
            return translateTransform;
        }
    }
}
