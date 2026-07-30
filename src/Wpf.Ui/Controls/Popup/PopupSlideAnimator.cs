// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using NativeContextMenu = System.Windows.Controls.ContextMenu;
using NativeToolTip = System.Windows.Controls.ToolTip;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Refines WPF's native <see cref="PopupAnimation.Slide"/> opening animation without taking ownership of the
/// <see cref="Popup"/> window lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// WPF is deliberately left responsible for creating, positioning, showing, hiding, capturing input for, and
/// destroying the popup HWND. It also establishes the clipped first frame and determines the slide direction from
/// the placement that actually fit on screen. Once WPF raises <c>Opened</c>, this behavior replaces only the native
/// vertical animation clock with the configured distance, duration, and easing function.
/// </para>
/// <para>
/// The default settings retain Wpf.Ui's 90 logical-pixel movement, 167 millisecond duration, and
/// <see cref="CircleEase"/> ease-out curve. No opacity animation is applied. This smaller ownership boundary avoids
/// changing <see cref="Popup.IsOpen"/> metadata, coercing the internal popup-root opacity, or forcing HWND teardown.
/// </para>
/// <para>
/// <see cref="NativeContextMenu"/> and <see cref="NativeToolTip"/> create a private root <see cref="Popup"/> only
/// during their first open. This behavior reads that Popup after WPF has created it, but never invokes the private
/// lifecycle method that creates or roots it. The first opening receives the custom movement before rendering, and
/// the private Popup retains <see cref="PopupAnimation.Slide"/> for subsequent openings. If a future WPF version
/// removes the private field, WPF's normal popup behavior remains intact.
/// </para>
/// </remarks>
public static class PopupSlideAnimator
{
    // ContextMenu and ToolTip do not bind PopupAnimation to their private Popup. The field lookup is read-only:
    // invoking HookupParentPopup outside WPF's IsOpen callback would also change logical parenting, event wiring, and
    // the IsOpen binding, making a visual effect capable of breaking the control's window lifecycle.
    private static readonly FieldInfo? ContextMenuParentPopupField = GetNonPublicInstanceField(
        typeof(NativeContextMenu),
        "_parentPopup"
    );

    private static readonly FieldInfo? ToolTipParentPopupField = GetNonPublicInstanceField(
        typeof(NativeToolTip),
        "_parentPopup"
    );

    // Popup.UpdatePosition records the direction selected by WPF's screen-fit algorithm here before it installs the
    // native Slide clock. Reading the cached PropertyInfo avoids approximating a placement that may have flipped at
    // a monitor edge. If reflection is unavailable, the active native transform and requested placement are used.
    private static readonly PropertyInfo? PopupAnimateFromBottomProperty = GetNonPublicInstanceProperty(
        typeof(Popup),
        "AnimateFromBottom"
    );

    /// <summary>
    /// Identifies the attached property that configures the Wpf.Ui slide refinement.
    /// </summary>
    /// <remarks>
    /// Set this property on a <see cref="Popup"/>, <see cref="NativeContextMenu"/>, or <see cref="NativeToolTip"/>.
    /// The behavior selects <see cref="PopupAnimation.Slide"/> and replaces only its opening movement clock. Clearing
    /// the property detaches the behavior and restores the animation mode observed when it was attached.
    /// </remarks>
    public static readonly DependencyProperty AnimationSettingsProperty =
        DependencyProperty.RegisterAttached(
            "AnimationSettings",
            typeof(PopupAnimationSettings),
            typeof(PopupSlideAnimator),
            new PropertyMetadata(null, OnAnimationBehaviorChanged)
        );

    /// <summary>
    /// Identifies the attached property that enables the native slide refinement with
    /// <see cref="PopupAnimationSettings.Default"/>.
    /// </summary>
    /// <remarks>
    /// An explicit <see cref="AnimationSettingsProperty"/> value takes precedence. The behavior requires a
    /// transparent Popup for WPF to enable native popup animation; when transparency is unavailable, WPF safely
    /// coerces <see cref="Popup.PopupAnimation"/> back to <see cref="PopupAnimation.None"/>.
    /// </remarks>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PopupSlideAnimator),
            new PropertyMetadata(false, OnAnimationBehaviorChanged)
        );

    private static readonly DependencyProperty AnimationControllerProperty =
        DependencyProperty.RegisterAttached(
            "AnimationController",
            typeof(PopupSlideAnimationController),
            typeof(PopupSlideAnimator),
            new PropertyMetadata(default(PopupSlideAnimationController))
        );

    /// <summary>
    /// Gets the slide animation settings associated with the specified popup-style owner.
    /// </summary>
    /// <param name="element">The object from which to read the attached property.</param>
    /// <returns>The explicitly configured animation settings, or <see langword="null"/>.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    [AttachedPropertyBrowsableForType(typeof(NativeContextMenu))]
    [AttachedPropertyBrowsableForType(typeof(NativeToolTip))]
    public static PopupAnimationSettings? GetAnimationSettings(DependencyObject element)
    {
        return (PopupAnimationSettings?)element.GetValue(AnimationSettingsProperty);
    }

    /// <summary>
    /// Sets the slide animation settings associated with the specified popup-style owner.
    /// </summary>
    /// <param name="element">The object on which to set the attached property.</param>
    /// <param name="value">The settings to apply, or <see langword="null"/> to clear explicit settings.</param>
    public static void SetAnimationSettings(DependencyObject element, PopupAnimationSettings? value)
    {
        element.SetValue(AnimationSettingsProperty, value);
    }

    /// <summary>
    /// Gets whether the default Wpf.Ui slide refinement is enabled for the specified popup-style owner.
    /// </summary>
    /// <param name="element">The object from which to read the attached property.</param>
    /// <returns><see langword="true"/> when the default settings are enabled; otherwise, <see langword="false"/>.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    [AttachedPropertyBrowsableForType(typeof(NativeContextMenu))]
    [AttachedPropertyBrowsableForType(typeof(NativeToolTip))]
    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    /// <summary>
    /// Enables or disables the default Wpf.Ui slide refinement for the specified popup-style owner.
    /// </summary>
    /// <param name="element">The object on which to set the attached property.</param>
    /// <param name="value"><see langword="true"/> to use the default settings; otherwise, <see langword="false"/>.</param>
    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// Returns whether the lightweight slide refinement is active for an owner.
    /// </summary>
    /// <remarks>
    /// This internal surface lets <see cref="PopupShadow"/> sample the real native movement for the same duration as
    /// the replacement clock. It intentionally exposes no Popup lifetime or reflection details.
    /// </remarks>
    internal static bool IsAnimationActive(DependencyObject owner)
    {
        if (GetEffectiveAnimationSettings(owner) == null)
        {
            return false;
        }

        return owner is NativeContextMenu or NativeToolTip ||
               owner is Popup { PopupAnimation: PopupAnimation.Slide };
    }

    /// <summary>
    /// Gets the effective duration used by the lightweight slide refinement.
    /// </summary>
    internal static Duration GetAnimationDuration(DependencyObject owner)
    {
        return new Duration(GetEffectiveAnimationSettings(owner)?.GetDuration() ?? TimeSpan.Zero);
    }

    /// <summary>
    /// Returns whether the replacement slide clock can change the popup content bounds.
    /// </summary>
    /// <remarks>
    /// This is intentionally separate from <see cref="IsAnimationActive"/>. The behavior may be attached and select
    /// native <see cref="PopupAnimation.Slide"/>, yet a zero duration or negligible offset causes the controller to
    /// commit the final transform immediately. The detached shadow must not perform render-tick sampling in that case.
    /// </remarks>
    internal static bool HasAnimatedContentBounds(DependencyObject owner)
    {
        return GetEffectiveAnimationSettings(owner) is { } settings &&
               settings.GetDuration() > TimeSpan.Zero &&
               Math.Abs(settings.StartOffset) >= 0.1D;
    }

    private static PopupAnimationSettings? GetEffectiveAnimationSettings(DependencyObject element)
    {
        return GetAnimationSettings(element) ?? (GetIsEnabled(element) ? PopupAnimationSettings.Default : null);
    }

    private static PopupSlideAnimationController? GetAnimationController(DependencyObject element)
    {
        return (PopupSlideAnimationController?)element.GetValue(AnimationControllerProperty);
    }

    private static void SetAnimationController(DependencyObject element, PopupSlideAnimationController? value)
    {
        element.SetValue(AnimationControllerProperty, value);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP001:Dispose created",
        Justification = "The controller is stored in an attached property and disposed when the effective settings are cleared.")]
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
            controller?.Dispose();
            SetAnimationController(d, null);
            return;
        }

        if (controller == null)
        {
            controller = new PopupSlideAnimationController(d);
            SetAnimationController(d, controller);
        }

        controller.Attach(settings);
    }

    /// <summary>
    /// Coordinates native Slide selection and one replacement movement clock for a popup-style owner.
    /// </summary>
    /// <remarks>
    /// The controller never observes <see cref="Popup.IsOpen"/> metadata and never closes a Popup. Its only transient
    /// state is a settings subscription and event handlers on the owner. WPF's PopupRoot.StopAnimations remains the
    /// authority that clears the replacement clock during close or a later open.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0041:Set mutable dependency properties using SetCurrentValue",
        Justification = "SetCurrentValue preserves bindings and styles while the behavior temporarily selects native Slide.")]
    private sealed class PopupSlideAnimationController : IDisposable
    {
        private readonly DependencyObject _owner;

        private PopupAnimationSettings? _settings;
        private Popup?                  _privateRootPopup;
        private PopupAnimation          _originalOwnerAnimation;
        private PopupAnimation          _originalPrivateRootAnimation;
        private bool                    _isOwnerAnimationCaptured;
        private bool                    _isPrivateRootAnimationCaptured;
        private bool                    _isSettingsChangedSubscribed;
        private bool                    _isAttached;
        private bool                    _isDisposed;

        public PopupSlideAnimationController(DependencyObject owner)
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
                throw new ObjectDisposedException(nameof(PopupSlideAnimationController));
            }

            #endif

            if (_settings != settings)
            {
                UnsubscribeSettingsChanged();
                _settings = settings;
                SubscribeSettingsChanged();
            }

            if (!_isAttached)
            {
                AttachOwnerEvents();
                _isAttached = true;
            }

            ConfigureNativeSlide();

            if (IsOwnerOpen)
            {
                ReplaceNativeSlideClock();
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
            StopReplacementAnimation();

            if (_isAttached)
            {
                DetachOwnerEvents();
                _isAttached = false;
            }

            UnsubscribeSettingsChanged();
            _settings = null;

            if (_isPrivateRootAnimationCaptured && _privateRootPopup != null)
            {
                _privateRootPopup.SetCurrentValue(
                    Popup.PopupAnimationProperty,
                    _originalPrivateRootAnimation
                );
            }

            if (_isOwnerAnimationCaptured)
            {
                _owner.SetCurrentValue(Popup.PopupAnimationProperty, _originalOwnerAnimation);
            }

            _privateRootPopup                 = null;
            _isPrivateRootAnimationCaptured  = false;
            _isOwnerAnimationCaptured        = false;
        }

        private void ConfigureNativeSlide()
        {
            if (_owner is Popup popup)
            {
                if (!_isOwnerAnimationCaptured)
                {
                    _originalOwnerAnimation = popup.PopupAnimation;
                    _isOwnerAnimationCaptured = true;
                }

                popup.SetCurrentValue(Popup.PopupAnimationProperty, PopupAnimation.Slide);

                return;
            }

            var privateRootPopup = TryGetPrivateRootPopup(_owner);
            if (privateRootPopup == null)
            {
                return;
            }

            if (!ReferenceEquals(_privateRootPopup, privateRootPopup))
            {
                _privateRootPopup = privateRootPopup;
                _originalPrivateRootAnimation = privateRootPopup.PopupAnimation;
                _isPrivateRootAnimationCaptured = true;
            }

            // ContextMenu and ToolTip use this private Popup as their real HWND owner. WPF has already completed the
            // first ShowWindow by the time the owner raises Opened, so this value prepares the native first frame on
            // every later open. ReplaceNativeSlideClock supplies the equivalent movement for the current first open.
            privateRootPopup.SetCurrentValue(Popup.PopupAnimationProperty, PopupAnimation.Slide);
        }

        private void AttachOwnerEvents()
        {
            switch (_owner)
            {
                case Popup popup:
                    popup.Opened += OnOwnerOpened;
                    break;

                case NativeContextMenu contextMenu:
                    contextMenu.Opened += OnOwnerOpened;
                    break;

                case NativeToolTip toolTip:
                    toolTip.Opened += OnOwnerOpened;
                    break;
            }
        }

        private void DetachOwnerEvents()
        {
            switch (_owner)
            {
                case Popup popup:
                    popup.Opened -= OnOwnerOpened;
                    break;

                case NativeContextMenu contextMenu:
                    contextMenu.Opened -= OnOwnerOpened;
                    break;

                case NativeToolTip toolTip:
                    toolTip.Opened -= OnOwnerOpened;
                    break;
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

        private void OnOwnerOpened(object? sender, EventArgs e)
        {
            // Popup.OnOpened runs synchronously after WPF has installed the native clock and called ShowWindow, but
            // before control returns to the Dispatcher for rendering. Replacing the clock here retains WPF's hidden
            // first-frame preparation without interposing on IsOpen or HWND destruction.
            //
            // ContextMenu and ToolTip create their private Popup inside WPF's IsOpen callback. Opened is the first
            // safe point at which a visual-only behavior may read that object without taking over its lifecycle.
            ConfigureNativeSlide();
            ReplaceNativeSlideClock();
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            ConfigureNativeSlide();
            if (IsOwnerOpen)
            {
                ReplaceNativeSlideClock();
            }
        }

        private void ReplaceNativeSlideClock()
        {
            if (_settings == null ||
                GetPopupRoot() is not UIElement popupRoot ||
                !TryGetNativeAnimationLayer(popupRoot, out var animationLayer))
            {
                return;
            }

            var translateTransform = EnsureNativeTranslateTransform(animationLayer);

            if (_owner is not Popup)
            {
                // The system resource can select Fade, Scroll, or None for the first ContextMenu/ToolTip open. That
                // choice was made before the private Popup became observable. Clear its opacity/horizontal clocks so
                // the requested vertical slide is the sole animation. These surfaces belong to PopupRoot itself, so
                // clearing them cannot remove an application animation declared inside the control template.
                popupRoot.BeginAnimation(UIElement.OpacityProperty, null);
                popupRoot.Opacity = 1D;
                translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
                translateTransform.X = 0D;
            }

            var startOffset = GetEffectiveStartOffset(
                _settings,
                popupRoot,
                translateTransform,
                IsActuallyPlacedAboveTarget()
            );
            var duration = _settings.GetDuration();

            if (duration <= TimeSpan.Zero || Math.Abs(startOffset) < 0.1D)
            {
                translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                translateTransform.Y = 0D;
                return;
            }

            var animation = new DoubleAnimation(
                startOffset,
                0D,
                new Duration(duration),
                FillBehavior.Stop
            )
            {
                EasingFunction = _settings.GetEasingFunction(),
            };

            // SnapshotAndReplace removes WPF's full-height linear Slide clock but leaves its base value and close-time
            // StopAnimations contract untouched. No opacity value or Popup lifetime state is changed here.
            translateTransform.BeginAnimation(
                TranslateTransform.YProperty,
                animation,
                HandoffBehavior.SnapshotAndReplace
            );
        }

        private void StopReplacementAnimation()
        {
            if (GetPopupRoot() is not UIElement popupRoot ||
                !TryGetNativeAnimationLayer(popupRoot, out var animationLayer) ||
                animationLayer.RenderTransform is not TranslateTransform translateTransform)
            {
                return;
            }

            translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
            translateTransform.Y = 0D;
        }

        private bool IsActuallyPlacedAboveTarget()
        {
            var rootPopup = _owner as Popup ?? _privateRootPopup;
            if (rootPopup != null && PopupAnimateFromBottomProperty != null)
            {
                try
                {
                    if (PopupAnimateFromBottomProperty.GetValue(rootPopup) is bool animateFromBottom)
                    {
                        return animateFromBottom;
                    }
                }
                catch (TargetInvocationException)
                {
                    // Fall through to the native transform and requested-placement signals below.
                }
                catch (MemberAccessException)
                {
                    // Fall through to the native transform and requested-placement signals below.
                }
                catch (ArgumentException)
                {
                    // Fall through to the native transform and requested-placement signals below.
                }
                catch (System.Security.SecurityException)
                {
                    // Fall through when the host does not grant access to WPF's private placement state.
                }
            }

            return GetRequestedPlacement(_owner) == PlacementMode.Top;
        }

        private Visual? GetPopupRoot()
        {
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
    }

    private static double GetEffectiveStartOffset(
        PopupAnimationSettings settings,
        UIElement popupRoot,
        TranslateTransform nativeTransform,
        bool isPlacedAboveTarget)
    {
        var height = popupRoot.RenderSize.Height;
        if (height <= 0D && popupRoot is FrameworkElement frameworkElement)
        {
            height = frameworkElement.ActualHeight;
        }

        var offset = height > 0D
            ? Math.Min(Math.Abs(settings.StartOffset), height)
            : Math.Abs(settings.StartOffset);
        var direction = Math.Sign(settings.StartOffset);
        if (direction == 0)
        {
            return 0D;
        }

        var startOffset = offset * direction;

        // Prefer the actual placement bit above. When reflection is unavailable, the native clock's current value
        // still exposes the direction WPF selected. The requested placement is only the final fallback.
        if (Math.Abs(nativeTransform.Y) > 0.1D)
        {
            isPlacedAboveTarget = nativeTransform.Y > 0D;
        }

        return isPlacedAboveTarget ? -startOffset : startOffset;
    }

    private static bool TryGetNativeAnimationLayer(UIElement popupRoot, out UIElement animationLayer)
    {
        animationLayer = null!;

        // PopupRoot is internal and sealed, but its visual structure is observable through public WPF APIs. The first
        // child is the clipping Decorator and its child is the AdornerDecorator that owns native Slide transforms.
        // If WPF changes that structure, leave the native animation untouched instead of animating an unsafe layer.
        if (VisualTreeHelper.GetChildrenCount(popupRoot) > 0 &&
            VisualTreeHelper.GetChild(popupRoot, 0) is Decorator { Child: UIElement nativeAnimationLayer })
        {
            animationLayer = nativeAnimationLayer;
            return true;
        }

        return false;
    }

    private static TranslateTransform EnsureNativeTranslateTransform(UIElement animationLayer)
    {
        if (animationLayer.RenderTransform is TranslateTransform translateTransform)
        {
            return translateTransform;
        }

        // PopupRoot.SetupTranslateAnimations owns this internal AdornerDecorator and performs the same replacement.
        // Creating the transform here is necessary only on a first ContextMenu/ToolTip open whose system animation
        // was Fade or None, before the behavior could select Slide on the newly created private Popup.
        translateTransform = new TranslateTransform();
        animationLayer.SetCurrentValue(UIElement.RenderTransformProperty, translateTransform);

        return translateTransform;
    }

    private static PlacementMode GetRequestedPlacement(DependencyObject owner)
    {
        return owner switch
        {
            Popup popup                   => popup.Placement,
            NativeContextMenu contextMenu => contextMenu.Placement,
            NativeToolTip toolTip         => toolTip.Placement,
            _                             => PlacementMode.Bottom,
        };
    }

    private static Popup? TryGetPrivateRootPopup(DependencyObject owner)
    {
        return owner switch
        {
            NativeContextMenu contextMenu => TryGetPrivateRootPopup(
                contextMenu,
                ContextMenuParentPopupField
            ),
            NativeToolTip toolTip => TryGetPrivateRootPopup(
                toolTip,
                ToolTipParentPopupField
            ),
            _ => null,
        };
    }

    private static Popup? TryGetPrivateRootPopup(
        DependencyObject owner,
        FieldInfo? parentPopupField)
    {
        if (parentPopupField == null)
        {
            return null;
        }

        try
        {
            return parentPopupField.GetValue(owner) as Popup;
        }
        catch (TargetInvocationException)
        {
            return null;
        }
        catch (MemberAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
    }

    private static FieldInfo? GetNonPublicInstanceField(Type type, string name)
    {
        return TryGetReflectionMember(
            () => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        );
    }

    private static PropertyInfo? GetNonPublicInstanceProperty(Type type, string name)
    {
        return TryGetReflectionMember(
            () => type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)
        );
    }

    private static TMember? TryGetReflectionMember<TMember>(Func<TMember?> lookup)
        where TMember : MemberInfo
    {
        // Reflection is an optional refinement, not an initialization requirement. In restricted hosts or a future
        // WPF version with ambiguous/renamed members, returning null keeps type initialization and native Popup
        // behavior usable instead of converting a missing optimization into an application-startup failure.
        try
        {
            return lookup();
        }
        catch (AmbiguousMatchException)
        {
            return null;
        }
        catch (MemberAccessException)
        {
            return null;
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
    }
}
