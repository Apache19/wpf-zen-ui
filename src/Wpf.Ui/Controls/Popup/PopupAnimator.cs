// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using NativeContextMenu = System.Windows.Controls.ContextMenu;
using NativeToolTip = System.Windows.Controls.ToolTip;

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
public static class PopupAnimator
{
    private const string PopupRootTypeName = "System.Windows.Controls.Primitives.PopupRoot";

    // Reads and replaces WPF's private DependencyProperty metadata callback so this behavior can run before
    // Popup.ShowWindow. Public change notifications are raised too late to hide the first popup-root frame.
    private static readonly FieldInfo? PropertyChangedCallbackField = typeof(PropertyMetadata).GetField(
        "_propertyChangedCallback",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    // Preserves UIElement.Opacity's existing private coerce callback when overriding metadata for PopupRoot.
    // Without reflecting it, the custom coerce hook could accidentally skip WPF's original value normalization.
    private static readonly FieldInfo? CoerceValueCallbackField = typeof(PropertyMetadata).GetField(
        "_coerceValueCallback",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    // Identifies WPF's internal window root type so only PopupRoot opacity is coerced. Applying this hook to
    // UIElement globally would change opacity behavior for normal controls and templates.
    private static readonly Type? PopupRootType = typeof(Popup).Assembly.GetType(PopupRootTypeName);

    // Completes Popup's queued asynchronous HWND destroy after close. This prevents a fast reopen from reusing
    // a stale popup surface before the next custom opening animation reaches the render thread.
    private static readonly MethodInfo? PopupForceCloseMethod = typeof(Popup).GetMethod(
        "ForceClose",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    // Popup.UpdatePosition stores the direction WPF later passes to PopupRoot.SetupTranslateAnimations. Prefer this
    // over approximating placement from public bounds, because it is the actual result of WPF's screen-fit scoring.
    private static readonly PropertyInfo? PopupAnimateFromBottomProperty = typeof(Popup).GetProperty(
        "AnimateFromBottom",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    // ContextMenu owns a private root Popup. Cache the field lookup because menus may open repeatedly, but keep
    // the value optional so newer WPF builds can fall back to the normal "no private popup found" path.
    private static readonly FieldInfo? ContextMenuParentPopupField = typeof(NativeContextMenu).GetField(
        "_parentPopup",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    // ToolTip follows the same private-root-popup pattern as ContextMenu, with WPF creating the Popup lazily when
    // IsOpen first changes to true.
    private static readonly FieldInfo? ToolTipParentPopupField = typeof(NativeToolTip).GetField(
        "_parentPopup",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    // PopupRoot currently exposes the same adorner decorator that WPF uses for native Slide/Scroll animations.
    // Cache the lookup for each open attempt; if it disappears, the visual-tree fallback below keeps behavior safe.
    private static readonly FieldInfo? PopupRootAdornerDecoratorField = PopupRootType?.GetField(
        "_adornerDecorator",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    // Popup opening can be nested on the same UI thread, for example when a menu opens another popup while WPF is
    // still inside IsOpen metadata callbacks. A stack models that LIFO call flow and lets PopupRoot opacity coerce
    // resolve the innermost active controller first without crossing dispatcher threads.
    [ThreadStatic]
    private static Stack<PopupAnimationController>? openingControllers;

    static PopupAnimator()
    {
        OverridePopupRootOpacityMetadata();
        PrependIsOpenChangedCallback(Popup.IsOpenProperty, typeof(Popup));
        PrependIsOpenChangedCallback(NativeContextMenu.IsOpenProperty, typeof(NativeContextMenu));
        PrependIsOpenChangedCallback(NativeToolTip.IsOpenProperty, typeof(NativeToolTip));
    }

    /// <summary>
    /// Identifies the attached property that configures the custom Wpf.Ui popup opening animation.
    /// </summary>
    /// <remarks>
    /// Set this property on a <see cref="Popup"/>, <see cref="NativeContextMenu"/>, or <see cref="NativeToolTip"/> and keep
    /// <see cref="Popup.PopupAnimation"/> as
    /// <see cref="PopupAnimation.None"/> to enable the custom animation path. Clearing this property detaches the
    /// behavior and restores any animated values that were touched by the controller.
    /// </remarks>
    public static readonly DependencyProperty AnimationSettingsProperty =
        DependencyProperty.RegisterAttached(
            "AnimationSettings",
            typeof(PopupAnimationSettings),
            typeof(PopupAnimator),
            new PropertyMetadata(null, OnAnimationBehaviorChanged)
        );

    /// <summary>
    /// Identifies the attached property that enables Wpf.Ui popup opening animation with the default settings.
    /// </summary>
    /// <remarks>
    /// If <see cref="AnimationSettingsProperty"/> is set, those explicit settings are used. Otherwise setting this
    /// property to <see langword="true"/> uses <see cref="PopupAnimationSettings.Default"/>.
    /// </remarks>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PopupAnimator),
            new PropertyMetadata(false, OnAnimationBehaviorChanged)
        );

    private static readonly DependencyProperty AnimationControllerProperty =
        DependencyProperty.RegisterAttached(
            "AnimationController",
            typeof(PopupAnimationController),
            typeof(PopupAnimator),
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
    [AttachedPropertyBrowsableForType(typeof(NativeContextMenu))]
    [AttachedPropertyBrowsableForType(typeof(NativeToolTip))]
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

    /// <summary>
    /// Helper for getting <see cref="IsEnabledProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="IsEnabledProperty"/> from.
    /// </param>
    /// <returns><see langword="true"/> if the Wpf.Ui default popup opening animation is enabled.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    [AttachedPropertyBrowsableForType(typeof(NativeContextMenu))]
    [AttachedPropertyBrowsableForType(typeof(NativeToolTip))]
    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="IsEnabledProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="IsEnabledProperty"/> on.
    /// </param>
    /// <param name="value">Whether the Wpf.Ui default popup opening animation is enabled.</param>
    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    [AttachedPropertyBrowsableForType(typeof(Popup))]
    [AttachedPropertyBrowsableForType(typeof(NativeContextMenu))]
    [AttachedPropertyBrowsableForType(typeof(NativeToolTip))]
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

        // DependencyPropertyDescriptor and Popup.Opened are both too late: WPF has already called
        // Popup.ShowWindow by then. Replace the metadata callback in place so the controller can mark
        // the opening transition before WPF's own OnIsOpenChanged creates and shows the popup HWND.
        PropertyChangedCallbackField.SetValue(
            metadata,
            new PropertyChangedCallback(
                (d, e) =>
                {
                    var controller = GetAnimationController(d);
                    var completeCustomOpen = controller?.OnIsOpenChanging(e) == true;

                    try
                    {
                        originalCallback(d, e);
                    }
                    finally
                    {
                        if (controller != null && e.NewValue is not true)
                        {
                            // Popup.HideWindow normally leaves a zero-delay DispatcherTimer that destroys the HWND
                            // later. A close followed immediately by open cancels that timer and reuses the old HWND
                            // surface, which was the source of the full-opacity flash. ContextMenu is handled through
                            // its private root Popup, matching the same queued-destroy boundary.
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
        if (baseValue is > 0D &&
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
            GetAnimationController(owner) is { } ownerController)
        {
            if (ownerController.ShouldSuppressPopupRootOpacity(popupRoot))
            {
                controller = ownerController;
                return true;
            }

            // PopupRoot.Parent identifies a public Popup unambiguously. If that Popup has its own controller but is
            // not inside its opening suppression window, do not let another controller on the thread-local stack
            // claim the root while its opacity animation is being evaluated.
            controller = null;
            return false;
        }

        if (openingControllers != null)
        {
            foreach (var openingController in openingControllers)
            {
                if (openingController.ShouldSuppressPopupRootOpacity(popupRoot))
                {
                    // One PopupRoot belongs to exactly one opening controller. Once a controller accepts it,
                    // suppressing additional instances would only risk hiding unrelated nested popups.
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

        // Push before WPF enters ShowWindow so PopupRoot.Opacity coercion can find this controller even if
        // PopupRoot.Parent has not been wired back to the owner yet.
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
            // The normal path mirrors nested IsOpen callbacks: the most recent opener leaves first.
            openingControllers.Pop();
            return;
        }

        var controllers = openingControllers.ToArray();
        openingControllers.Clear();

        // Defensive cleanup for unusual reentrancy or exception paths. Remove this controller wherever it sits
        // while preserving the remaining stack order for other openings still inside WPF callbacks.
        for (var i = controllers.Length - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(controllers[i], controller))
            {
                openingControllers.Push(controllers[i]);
            }
        }
    }

    /// <summary>
    /// Returns whether the owner uses the Wpf.Ui custom opening animation instead of native WPF popup animation.
    /// </summary>
    /// <remarks>
    /// This internal surface is for <see cref="PopupShadow"/> so the detached shadow can mirror the content popup.
    /// The behavior itself uses controller state such as <c>IsCustomAnimationRequested</c>, because the content
    /// animation also depends on the current open lifecycle and suppression window.
    /// </remarks>
    internal static bool IsCustomAnimationActive(DependencyObject owner)
    {
        return GetEffectiveAnimationSettings(owner) != null &&
               GetPopupAnimation(owner) == PopupAnimation.None;
    }

    /// <summary>
    /// Gets the custom opening duration for the detached shadow animation.
    /// </summary>
    /// <remarks>
    /// This is scoped to <see cref="PopupShadow"/> synchronization. The popup content animation reads settings
    /// directly from its controller so both sides can share values without sharing transient animation state.
    /// </remarks>
    internal static Duration GetAnimationDuration(DependencyObject owner)
    {
        return new Duration(GetEffectiveAnimationSettings(owner)?.GetDuration() ?? TimeSpan.Zero);
    }

    /// <summary>
    /// Gets the custom opening easing function for the detached shadow animation.
    /// </summary>
    /// <remarks>
    /// This is part of the shadow synchronization surface only. The content popup still gets its easing from the
    /// active controller settings when it creates the real movement clock.
    /// </remarks>
    internal static IEasingFunction? GetAnimationEasingFunction(DependencyObject owner)
    {
        return GetEffectiveAnimationSettings(owner)?.GetEasingFunction();
    }

    /// <summary>
    /// Gets the start offset that the shadow should use to reveal content in parallel with the custom popup slide.
    /// </summary>
    /// <remarks>
    /// This method is exposed only for <see cref="PopupShadow"/>. It lets the shadow reproduce the same
    /// "start offset to zero" movement as the content popup, so the shadow does not outline content that is still
    /// clipped or translated during the first frames. The controller itself already owns the real transform.
    /// </remarks>
    internal static bool TryGetAnimationStartOffset(
        DependencyObject owner,
        UIElement popupRoot,
        out double startOffset)
    {
        if (GetAnimationController(owner)?.TryGetCurrentAnimationStartOffset(popupRoot, out startOffset) == true)
        {
            return true;
        }

        if (GetEffectiveAnimationSettings(owner) is not { } settings)
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

    private static PopupAnimationSettings? GetEffectiveAnimationSettings(DependencyObject element)
    {
        return GetAnimationSettings(element) ?? (GetIsEnabled(element) ? PopupAnimationSettings.Default : null);
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
        // First try to read the actual placement from Popup's internal AnimateFromBottom flag.
        // If that fails, fall back to comparing the realized popup screen bounds against the
        // placement target bounds to infer the real vertical direction.
        if (TryResolveNativeVerticalPlacement(owner, out var nativePlacement))
        {
            return nativePlacement;
        }

        // Requested Placement is only a hint: Popup may flip to the opposite side to stay inside the screen.
        // If WPF's internal direction bit is unavailable, compare the realized popup with the same anchor rectangle
        // that WPF uses for placement. For point-based context menus this is the cursor/offset point, not the full
        // placement target control.
        if (TryGetPlacementAnchorBounds(owner, out var anchorBounds) &&
            TryGetScreenBounds(popupRoot, out var popupBounds))
        {
            const double Tolerance = 1D;
            if (popupBounds.Bottom <= anchorBounds.Top + Tolerance)
            {
                return PopupVerticalPlacement.AboveTarget;
            }

            if (popupBounds.Top >= anchorBounds.Bottom - Tolerance)
            {
                return PopupVerticalPlacement.BelowTarget;
            }

            var verticalOffset = GetCenterY(popupBounds) - GetCenterY(anchorBounds);
            if (Math.Abs(verticalOffset) > Tolerance)
            {
                return verticalOffset < 0D
                    ? PopupVerticalPlacement.AboveTarget
                    : PopupVerticalPlacement.BelowTarget;
            }
        }

        // All resolution steps failed. Fall back to the requested Placement as a best guess —
        // it will be wrong when WPF flips the popup due to screen edges, but it is the only
        // remaining signal.
        return ResolveRequestedVerticalPlacement(GetPlacement(owner));
    }

    private static bool TryResolveNativeVerticalPlacement(
        DependencyObject owner,
        out PopupVerticalPlacement placement)
    {
        placement = default;

        if (PopupAnimateFromBottomProperty == null ||
            TryGetRootPopup(owner) is not { } popup)
        {
            return false;
        }

        try
        {
            if (PopupAnimateFromBottomProperty.GetValue(popup) is not bool animateFromBottom)
            {
                return false;
            }

            placement = animateFromBottom
                ? PopupVerticalPlacement.AboveTarget
                : PopupVerticalPlacement.BelowTarget;

            return true;
        }
        catch (TargetInvocationException)
        {
            return false;
        }
        catch (MethodAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static Popup? TryGetRootPopup(DependencyObject owner)
    {
        return owner switch
        {
            Popup popup                   => popup,
            NativeContextMenu contextMenu => TryGetPrivateRootPopup(contextMenu),
            NativeToolTip toolTip         => TryGetPrivateRootPopup(toolTip),
            _                             => null,
        };
    }

    private static Popup? TryGetPrivateRootPopup(DependencyObject owner)
    {
        return owner switch
        {
            NativeContextMenu contextMenu => ContextMenuParentPopupField?.GetValue(contextMenu) as Popup,
            NativeToolTip toolTip         => ToolTipParentPopupField?.GetValue(toolTip) as Popup,
            _                             => null,
        };
    }

    private static bool TryGetPlacementAnchorBounds(DependencyObject owner, out Rect bounds)
    {
        bounds = Rect.Empty;

        var placement = GetPlacement(owner);
        var target = GetPlacementTarget(owner);
        if (target == null)
        {
            return false;
        }

        if (placement is PlacementMode.Mouse or PlacementMode.MousePoint)
        {
            var mousePosition = Mouse.GetPosition(target);
            return TryTransformTargetRectToScreen(target, new Rect(mousePosition, new Size(0D, 0D)), out bounds);
        }

        if (IsAbsolutePlacementMode(placement))
        {
            return false;
        }

        var placementRect = GetPlacementRectangle(owner);
        if (placementRect == Rect.Empty)
        {
            placementRect = placement is PlacementMode.Relative or PlacementMode.RelativePoint
                ? new Rect(new Point(GetHorizontalOffset(owner), GetVerticalOffset(owner)), new Size(0D, 0D))
                : new Rect(default, GetRenderSize(target));
        }
        else
        {
            placementRect.Offset(GetHorizontalOffset(owner), GetVerticalOffset(owner));
        }

        return TryTransformTargetRectToScreen(target, placementRect, out bounds);
    }

    private static UIElement? GetPlacementTarget(DependencyObject owner)
    {
        return owner switch
        {
            Popup popup                   => popup.PlacementTarget,
            NativeContextMenu contextMenu => contextMenu.PlacementTarget,
            NativeToolTip toolTip         => toolTip.PlacementTarget,
            _                             => null,
        };
    }

    private static PlacementMode GetPlacement(DependencyObject owner)
    {
        return owner switch
        {
            Popup popup                   => popup.Placement,
            NativeContextMenu contextMenu => contextMenu.Placement,
            NativeToolTip toolTip         => toolTip.Placement,
            _                             => PlacementMode.Bottom,
        };
    }

    private static Rect GetPlacementRectangle(DependencyObject owner)
    {
        return owner switch
        {
            Popup popup                   => popup.PlacementRectangle,
            NativeContextMenu contextMenu => contextMenu.PlacementRectangle,
            NativeToolTip toolTip         => toolTip.PlacementRectangle,
            _                             => Rect.Empty,
        };
    }

    private static double GetHorizontalOffset(DependencyObject owner)
    {
        return owner switch
        {
            Popup popup                   => popup.HorizontalOffset,
            NativeContextMenu contextMenu => contextMenu.HorizontalOffset,
            NativeToolTip toolTip         => toolTip.HorizontalOffset,
            _                             => 0D,
        };
    }

    private static double GetVerticalOffset(DependencyObject owner)
    {
        return owner switch
        {
            Popup popup                   => popup.VerticalOffset,
            NativeContextMenu contextMenu => contextMenu.VerticalOffset,
            NativeToolTip toolTip         => toolTip.VerticalOffset,
            _                             => 0D,
        };
    }

    private static PopupVerticalPlacement ResolveRequestedVerticalPlacement(PlacementMode placement)
    {
        return placement == PlacementMode.Top
            ? PopupVerticalPlacement.AboveTarget
            : PopupVerticalPlacement.BelowTarget;
    }

    private static bool IsAbsolutePlacementMode(PlacementMode placement)
    {
        return placement is PlacementMode.Absolute
            or PlacementMode.AbsolutePoint
            or PlacementMode.Mouse
            or PlacementMode.MousePoint;
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

    private static bool TryTransformTargetRectToScreen(
        UIElement target,
        Rect targetRect,
        out Rect bounds)
    {
        bounds = Rect.Empty;

        if (targetRect.Width < 0D || targetRect.Height < 0D)
        {
            return false;
        }

        try
        {
            var dpi     = VisualTreeHelper.GetDpi(target);
            var topLeft = target.PointToScreen(targetRect.TopLeft);

            bounds = new Rect(
                topLeft,
                new Size(targetRect.Width * dpi.DpiScaleX, targetRect.Height * dpi.DpiScaleY)
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
                      + "and disposed when the effective AnimationSettings are cleared.")]
    private static void OnAnimationBehaviorChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup and not NativeContextMenu and not NativeToolTip)
        {
            return;
        }

        var controller = GetAnimationController(d);
        var settings = GetEffectiveAnimationSettings(d);
        if (settings == null)
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
        private readonly DependencyObject        _owner;
        private          PopupAnimationSettings? _settings;
        private          UIElement?              _popupRoot;
        private          TranslateTransform?     _translateTransform;
        private          double?                 _currentAnimationStartOffset;
        // Each open/close transition advances the version, so stale opening work cannot run against a newer popup life.
        private          int                     _openVersion;
        private          bool                    _isOpeningWithCustomAnimation;
        private          bool                    _isSuppressingPopupRootOpacity;
        private          Popup?                  _pendingPrivateRootPopup;
        // Remembers the PopupRoot whose first-frame opacity was actually coerced. If the custom open path aborts
        // before it installs real clocks, this lets us re-coerce or restore that specific root instead of leaving
        // a reused popup HWND visually stuck at opacity 0.
        private          UIElement?              _suppressedPopupRoot;
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
            DetachPendingPrivateRootPopup();
            ResetOpenAnimationState();
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
                DetachPendingPrivateRootPopup();
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
            if (_isDisposed || _settings == null)
            {
                return;
            }

            if (GetPopupAnimation(_owner) != PopupAnimation.None)
            {
                return;
            }

            var popup = GetForceCloseTargetPopup();
            if (popup == null)
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

            // Normal Popup owners finish ShowWindow inside this same IsOpen metadata callback, so suppression can end
            // before BeginOpenAnimation starts. ContextMenu is different: its IsOpen value is copied to a private root
            // Popup through a binding, and that private Popup may not call ShowWindow until after this callback returns.
            // If we release suppression at the ContextMenu boundary, Popup.ShowWindow forces PopupRoot.Opacity to 1 and
            // the stale fully-visible surface can render for one frame before the private Popup.Opened handler runs.
            var keepPopupRootOpacitySuppression = false;

            try
            {
                ApplyRootPopupAnimationMode();

                if (_settings != null)
                {
                    if (IsPrivateRootPopupOwner)
                    {
                        // ContextMenu and ToolTip open through a private root Popup whose ShowWindow timing can lag behind
                        // the public IsOpen property, so they need the guarded path that waits until WPF stops clearing clocks.
                        keepPopupRootOpacitySuppression = BeginPrivateRootPopupOpenAnimation(_openVersion);
                    }
                    else
                    {
                        EndPopupRootOpacitySuppression();
                        if (!BeginOpenAnimation(_openVersion))
                        {
                            // If WPF created the popup HWND but we could not locate the animation surface, prefer a
                            // non-animated visible popup over preserving the suppressed first frame indefinitely.
                            RestoreOpenPopupRootOpacity();
                        }
                    }
                }
            }
            finally
            {
                if (!keepPopupRootOpacitySuppression)
                {
                    // For real Popup owners, and for ContextMenu cases where the private Popup is already open or
                    // unavailable, all first-frame writes have happened synchronously. Leaving the suppression stack
                    // now avoids hiding unrelated nested popups opened later on the same dispatcher turn.
                    EndPopupRootOpacitySuppression();
                    _isOpeningWithCustomAnimation = false;
                }
            }
        }

        private bool BeginPrivateRootPopupOpenAnimation(int openVersion)
        {
            if (TryGetPrivateRootPopup(_owner) is not { } rootPopup)
            {
                // Without WPF's private root Popup there is nothing asynchronous to guard. The caller will fall back to
                // the normal cleanup path and the owner simply opens without the extra private-Popup protection.
                return false;
            }

            if (rootPopup.IsOpen && GetPopupRoot() is UIElement)
            {
                // If the private Popup is already open, its SetupAnimations(true) has already called
                // PopupRoot.StopAnimations. Starting now is safe and will not be cleared by WPF immediately after.
                if (!BeginOpenAnimation(openVersion))
                {
                    // ContextMenu/ToolTip can reuse their private root Popup. If the reused root was hidden by the
                    // suppression hook but cannot be animated this cycle, explicitly return it to visible state.
                    RestoreOpenPopupRootOpacity();
                }

                // Returning false tells OnIsOpenChanged that the suppression window was fully consumed here; no
                // pending private Popup.Opened event needs to own it.
                return false;
            }

            // ContextMenu/ToolTip IsOpen opens a private Popup via binding. The public IsOpen callback can
            // finish before that private Popup reaches ShowWindow/SetupAnimations(true). Starting our timeline too
            // early lets PopupRoot.StopAnimations wipe the clocks, and ending the popup-root opacity suppression here
            // lets Popup.ShowWindow force a full-opacity frame before Opened fires. Wait for the private Popup.Opened
            // event while keeping suppression armed only for this owner's root visual.
            DetachPendingPrivateRootPopup();
            _pendingPrivateRootPopup = rootPopup;
            rootPopup.Opened += OnPrivateRootPopupOpened;
            rootPopup.Closed += OnPendingPrivateRootPopupClosed;

            // Returning true intentionally transfers the suppression lifetime from the owner IsOpen callback to the private
            // Popup.Opened event. That event is the first point where WPF has finished ShowWindow and StopAnimations.
            return true;
        }

        private void OnPrivateRootPopupOpened(object? sender, EventArgs e)
        {
            // Opened is raised after the private Popup's ShowWindow path, so WPF's own StopAnimations call is behind
            // us and the custom timeline can own the PopupRoot until the owner closes.
            DetachPendingPrivateRootPopup();
            var didBeginOpenAnimation = false;

            try
            {
                // BeginOpenAnimation establishes the real 0 -> 1 opacity clock synchronously. The suppression can be
                // released immediately afterwards because the popup root now has explicit hidden base values/clocks.
                didBeginOpenAnimation = BeginOpenAnimation(_openVersion);
            }
            finally
            {
                EndPopupRootOpacitySuppression();
                _isOpeningWithCustomAnimation = false;

                if (!didBeginOpenAnimation)
                {
                    // The private Popup has now opened, so there is no more WPF ShowWindow work to guard. Falling
                    // back to opacity 1 avoids the "shadow only" failure mode when root discovery races with close.
                    RestoreOpenPopupRootOpacity();
                }
            }
        }

        private void OnPendingPrivateRootPopupClosed(object? sender, EventArgs e)
        {
            // Defensive cleanup for interrupted opens: if the owner closes before the private Popup.Opened event fires,
            // release the thread-local suppression stack and remove both event handlers in one place.
            DetachPendingPrivateRootPopup();
            EndPopupRootOpacitySuppression();
            _isOpeningWithCustomAnimation = false;
        }

        private void DetachPendingPrivateRootPopup()
        {
            if (_pendingPrivateRootPopup == null)
            {
                return;
            }

            // The private root Popup can be long-lived and reused, so every temporary handler must be
            // removed explicitly. Otherwise later opens would run stale animation callbacks for old versions.
            _pendingPrivateRootPopup.Opened -= OnPrivateRootPopupOpened;
            _pendingPrivateRootPopup.Closed -= OnPendingPrivateRootPopupClosed;
            _pendingPrivateRootPopup = null;
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
            // Step 1: reject stale work before touching visuals. Open and close events can invalidate a pending
            // animation request while WPF is still completing the previous callback chain.
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
                // Step 2: if custom animation is no longer active, leave the popup in its natural visible state
                // and release any references captured by an earlier opening attempt.
                ResetOpenAnimationState();

                return false;
            }

            // Step 3: make the real root Popup use no native PopupAnimation before the custom timeline starts.
            ApplyRootPopupAnimationMode();

            // Step 4: locate the realized PopupRoot. It exists only after WPF has created the popup window.
            if (GetPopupRoot() is not UIElement popupRoot)
            {
                return false;
            }

            // Step 5: locate WPF's native animation layer. Translating this layer lets PopupRoot.StopAnimations
            // clean up the transform on close, just like it does for built-in popup animations.
            if (!TryGetAnimationLayer(popupRoot, out var animationLayer))
            {
                // If WPF changes PopupRoot enough that we cannot find its native animation layer, avoid placing a
                // translation where PopupRoot.StopAnimations cannot clear it.
                animationLayer = null;
            }

            // Step 6: compute duration and movement from the realized placement rather than the requested placement.
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

            // Step 7: keep just enough state for cleanup and for PopupShadow to mirror the same opening offset.
            _popupRoot                   = popupRoot;
            _currentAnimationStartOffset = translateTransform != null
                ? startOffset
                : 0D;

            // Step 8: establish a hidden first frame synchronously. PopupRoot.Opacity may have just been forced to 1
            // by WPF ShowWindow, so the base value must be reset before any Dispatcher/render work can observe it.
            InitializeOpenAnimationBaseValues(popupRoot, translateTransform, startOffset);

            // Step 9: no movement or no duration still counts as a completed open; commit the final visible state.
            if (duration <= TimeSpan.Zero || translateTransform == null || Math.Abs(startOffset) < 0.1D)
            {
                ResetOpenAnimationState();
                return true;
            }

            // Step 10: start the opacity and translation clocks for the current open version.
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
                ResetOpenAnimationState();
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

        private static void InitializeOpenAnimationBaseValues(
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

        private void ResetOpenAnimationState()
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
            if (_isDisposed ||
                _settings == null ||
                !_isOpeningWithCustomAnimation ||
                !_isSuppressingPopupRootOpacity ||
                !IsPopupRootSuppressionTarget(popupRoot))
            {
                return false;
            }

            if (popupRoot is UIElement popupRootElement)
            {
                // Track only roots that passed the suppression-target check. This avoids later recovery touching an
                // unrelated nested popup that happened to open during the same dispatcher turn.
                _suppressedPopupRoot = popupRootElement;
            }

            return true;
        }

        private bool IsPopupRootSuppressionTarget(DependencyObject popupRoot)
        {
            var rootPopup = _pendingPrivateRootPopup ?? TryGetRootPopup(_owner);
            if (rootPopup == null)
            {
                return false;
            }

            // PopupRoot is a logical child of its owning Popup before ShowWindow writes Opacity = 1. Prefer that
            // direct relationship, then fall back to the HwndSource root for framework versions where Parent is not
            // exposed at this point in the opening lifecycle. Every controller must prove ownership here: opacity
            // animations on already-open PopupRoots can be re-evaluated while a later Popup is inside ShowWindow.
            // Accepting an arbitrary root in that window would make the later controller force the earlier one to 0.
            return (
                       popupRoot is FrameworkElement { Parent: Popup parentPopup } &&
                       ReferenceEquals(parentPopup, rootPopup)
                   )
                 ||
                   (
                       rootPopup.Child is Visual child &&
                       ReferenceEquals(
                           (PresentationSource.FromVisual(child) as HwndSource)?.RootVisual,
                           popupRoot
                       )
                   );
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
            RestoreSuppressedPopupRootOpacity();
        }

        private void RestoreSuppressedPopupRootOpacity()
        {
            var popupRoot = _suppressedPopupRoot;
            _suppressedPopupRoot = null;

            if (popupRoot == null || ReferenceEquals(_popupRoot, popupRoot))
            {
                return;
            }

            // Ending suppression should make WPF's last written base value observable again. When no custom clock was
            // installed, this is the narrow recovery path that prevents a suppressed PopupRoot from staying invisible.
            popupRoot.CoerceValue(UIElement.OpacityProperty);
        }

        private void RestoreOpenPopupRootOpacity()
        {
            if (!IsOwnerOpen || GetPopupRoot() is not UIElement popupRoot)
            {
                return;
            }

            // This is a deliberate fallback for failed setup only. The normal path still starts the real 0 -> 1
            // animation synchronously, preserving the old fast-close ForceClose protection against one-frame flashes.
            popupRoot.BeginAnimation(UIElement.OpacityProperty, null);
            popupRoot.Opacity = 1D;
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
                NativeContextMenu contextMenu => (PresentationSource.FromVisual(contextMenu) as HwndSource)?.RootVisual,
                NativeToolTip toolTip         => (PresentationSource.FromVisual(toolTip) as HwndSource)?.RootVisual,
                _                             => null,
            };
        }

        private bool IsOwnerOpen =>
            _owner switch
            {
                Popup popup                   => popup.IsOpen,
                NativeContextMenu contextMenu => contextMenu.IsOpen,
                NativeToolTip toolTip         => toolTip.IsOpen,
                _                             => false,
            };

        private void ApplyRootPopupAnimationMode()
        {
            if (!IsPrivateRootPopupOwner ||
                GetPopupAnimation(_owner) != PopupAnimation.None ||
                TryGetPrivateRootPopup(_owner) is not { } rootPopup)
            {
                return;
            }

            // ContextMenu and ToolTip host themselves in a private Popup created by WPF. Popup.CreateRootPopup binds
            // IsOpen and placement properties, but not PopupAnimation; without this, the private Popup keeps the system
            // animation and remains visible briefly after PopupRoot.StopAnimations clears our opening transform.
            // Only change the private Popup's animation mode. Its managed instance still belongs to the owner; the
            // close path only completes WPF's pending HWND destroy timer when one is queued, mirroring Popup.ForceClose.
            // ComboBox does not need this path because its template owns the real Popup and binds PopupAnimation
            // directly, so the behavior can observe and update the same instance that WPF opens.
            rootPopup.SetCurrentValue(Popup.PopupAnimationProperty, PopupAnimation.None);
        }

        private Popup? GetForceCloseTargetPopup()
        {
            // Public Popup owners can be force-closed directly. ContextMenu owns only the menu object publicly; WPF
            // stores the real HWND owner in _parentPopup, so the queued async destroy lives on that private Popup.
            return _owner switch
            {
                Popup popup                => popup,
                NativeContextMenu contextMenu => TryGetPrivateRootPopup(contextMenu),
                NativeToolTip toolTip         => TryGetPrivateRootPopup(toolTip),
                _                          => null,
            };
        }

        private bool IsPrivateRootPopupOwner => _owner is NativeContextMenu or NativeToolTip;

        private static bool TryGetAnimationLayer(UIElement popupRoot, out UIElement animationLayer)
        {
            animationLayer = null!;

            if (PopupRootAdornerDecoratorField?.GetValue(popupRoot) is UIElement reflectedAnimationLayer)
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
