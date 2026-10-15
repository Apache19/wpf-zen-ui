// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Zen.Ui.Infrastructure;

using NativeContextMenu = System.Windows.Controls.ContextMenu;
using NativeToolTip = System.Windows.Controls.ToolTip;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Shortens WPF's existing <see cref="PopupAnimation.Slide"/> opening animation while preserving the
/// <see cref="Popup"/> lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// WPF remains solely responsible for creating, positioning, showing, hiding, capturing input for, and destroying
/// the popup window. When WPF starts showing the HWND, this behavior temporarily conceals the popup root, lets the
/// opening input stack and WPF's already-queued first render finish, and then replaces only the native vertical Slide
/// clock to limit the travel distance and adjust its duration and easing function.
/// </para>
/// <para>
/// The replacement preserves the direction chosen by WPF's screen-fit algorithm and never travels more than
/// <see cref="MaximumSlideDistance"/> logical pixels. Opacity is used only as a binary first-render gate and is never
/// animated. The behavior does not change horizontal animation, clipping, placement, the public owner's
/// <see cref="Popup.PopupAnimation"/> value, <see cref="Popup.IsOpen"/> metadata, or window teardown behavior.
/// Consumers must set <see cref="Popup.PopupAnimation"/> to
/// <see cref="PopupAnimation.Slide"/> independently. When the effective mode is None, Fade, or Scroll, the behavior
/// is inert and leaves the visual state intact.
/// </para>
/// <para>
/// <see cref="NativeContextMenu"/> and <see cref="NativeToolTip"/> hide their real <see cref="Popup"/> behind a
/// private field and normally force its animation from a system resource. Once WPF naturally creates that Popup, an
/// explicit <see cref="Popup.PopupAnimation"/> value on the public owner is forwarded before WPF calls its private
/// <c>SetupAnimations</c> method. The behavior never creates the private Popup itself. If reflection or the HWND
/// message hook is unavailable on a future WPF version, the controls retain their normal framework behavior.
/// </para>
/// </remarks>
public static class PopupSlideAnimator
{
    /// <summary>
    /// Defines the upper bound, in logical pixels, for the shortened native Slide animation.
    /// </summary>
    public const double MaximumSlideDistance = 90D;

    // ContextMenu and ToolTip expose no public path to the Popup that owns their HWND. These cached, read-only
    // lookups let the behavior inspect an already-created Popup without calling HookupParentPopup or changing its
    // animation value, logical parenting, event wiring, IsOpen binding, or other lifecycle state. Failure simply
    // leaves native WPF behavior intact.
    private static readonly FieldInfo? ContextMenuParentPopupField = GetNonPublicInstanceField(
        typeof(NativeContextMenu),
        "_parentPopup"
    );

    private static readonly FieldInfo? ToolTipParentPopupField = GetNonPublicInstanceField(
        typeof(NativeToolTip),
        "_parentPopup"
    );

    // Popup.UpdatePosition records the direction selected by WPF's screen-fit algorithm in this private bit before
    // PopupRoot.SetupTranslateAnimations creates the native clock. There is no public equivalent: Popup.Placement is
    // only the requested placement and can differ after screen-edge fitting. This cached read is optional; if it is
    // unavailable, the behavior leaves the full native Slide untouched rather than guessing the wrong direction.
    private static readonly PropertyInfo? PopupAnimateFromBottomProperty = GetNonPublicInstanceProperty(
        typeof(Popup),
        "AnimateFromBottom"
    );

    /// <summary>
    /// Identifies the attached property that configures the shortened native Slide animation.
    /// </summary>
    /// <remarks>
    /// Set this property on a <see cref="Popup"/>, <see cref="NativeContextMenu"/>, or
    /// <see cref="NativeToolTip"/>. An explicit value takes precedence over <see cref="IsEnabledProperty"/>.
    /// Enabling this property does not select Slide. The owner must independently have
    /// <see cref="Popup.PopupAnimation"/> set to <see cref="PopupAnimation.Slide"/>; other modes are ignored. For a
    /// ContextMenu or ToolTip, an explicit owner value is forwarded to WPF's private Popup.
    /// </remarks>
    public static readonly DependencyProperty AnimationSettingsProperty =
        DependencyProperty.RegisterAttached(
            "AnimationSettings",
            typeof(PopupAnimationSettings),
            typeof(PopupSlideAnimator),
            new PropertyMetadata(null, OnAnimationBehaviorChanged)
        );

    /// <summary>
    /// Identifies the attached property that enables the shortened native Slide animation with
    /// <see cref="PopupAnimationSettings.Default"/>.
    /// </summary>
    /// <remarks>
    /// WPF requires <see cref="Popup.AllowsTransparency"/> for native popup animation. When transparency is not
    /// available, WPF safely coerces <see cref="Popup.PopupAnimation"/> to <see cref="PopupAnimation.None"/> and this
    /// behavior does not attempt to bypass that framework decision. Enabling the animator also never changes the
    /// animation mode; <see cref="Popup.PopupAnimation"/> must independently resolve to
    /// <see cref="PopupAnimation.Slide"/> for an opening to be refined.
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
    /// Gets the Slide animation settings associated with the specified popup-style owner.
    /// </summary>
    /// <param name="element">The object from which to read the attached property.</param>
    /// <returns>The explicitly configured settings, or <see langword="null"/>.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    [AttachedPropertyBrowsableForType(typeof(NativeContextMenu))]
    [AttachedPropertyBrowsableForType(typeof(NativeToolTip))]
    public static PopupAnimationSettings? GetAnimationSettings(DependencyObject element)
    {
        return (PopupAnimationSettings?)element.GetValue(AnimationSettingsProperty);
    }

    /// <summary>
    /// Sets the Slide animation settings associated with the specified popup-style owner.
    /// </summary>
    /// <param name="element">The object on which to set the attached property.</param>
    /// <param name="value">The settings to apply, or <see langword="null"/> to clear explicit settings.</param>
    public static void SetAnimationSettings(DependencyObject element, PopupAnimationSettings? value)
    {
        element.SetValue(AnimationSettingsProperty, value);
    }

    /// <summary>
    /// Gets whether the default shortened Slide animation is enabled for the specified popup-style owner.
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
    /// Enables or disables the default shortened Slide animation for the specified popup-style owner.
    /// </summary>
    /// <param name="element">The object on which to set the attached property.</param>
    /// <param name="value"><see langword="true"/> to use the default settings; otherwise, <see langword="false"/>.</param>
    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// Returns whether the native Slide clock was refined for the current opening.
    /// </summary>
    /// <remarks>
    /// <see cref="PopupShadow"/> uses this visual-only state to track the same duration as the popup content. It does
    /// not expose or modify any Popup lifecycle state.
    /// </remarks>
    internal static bool IsAnimationActive(DependencyObject owner)
    {
        return GetAnimationController(owner)?.IsCurrentOpeningRefined == true;
    }

    /// <summary>
    /// Gets the effective duration used by the shortened Slide animation.
    /// </summary>
    internal static Duration GetAnimationDuration(DependencyObject owner)
    {
        return new Duration(GetEffectiveAnimationSettings(owner)?.GetDuration() ?? TimeSpan.Zero);
    }

    /// <summary>
    /// Returns whether the configured clock changes the popup content bounds over time.
    /// </summary>
    internal static bool HasAnimatedContentBounds(DependencyObject owner)
    {
        return GetEffectiveAnimationSettings(owner) is { } settings &&
               settings.GetDuration() > TimeSpan.Zero &&
               Math.Min(settings.StartOffset, MaximumSlideDistance) >= 0.1D;
    }

    /// <summary>
    /// Registers a one-shot callback for the point at which the deferred Slide clock is attached but the popup root
    /// is still concealed.
    /// </summary>
    /// <remarks>
    /// <see cref="PopupShadow"/> uses this narrow synchronization point because its detached HWND is not affected by
    /// PopupRoot opacity. Returning <see langword="true"/> means the callback owns the pending transition: it receives
    /// <see langword="true"/> immediately before the popup is revealed, or <see langword="false"/> if that opening is
    /// cancelled. Returning <see langword="false"/> means no reveal gate currently exists and the caller should start
    /// normally. Keeping this coordination internal avoids exposing animation lifecycle as public API.
    /// </remarks>
    internal static bool TryRegisterOpeningRevealCallback(
        DependencyObject owner,
        Action<bool> callback)
    {
        return GetAnimationController(owner)?.TryRegisterOpeningRevealCallback(callback) == true;
    }

    private static PopupAnimationSettings? GetEffectiveAnimationSettings(DependencyObject element)
    {
        return GetAnimationSettings(element) ?? (GetIsEnabled(element) ? PopupAnimationSettings.Default : null);
    }

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
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
        var settings   = GetEffectiveAnimationSettings(d);

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
    /// Conceals WPF's first popup frame and starts the shortened clock after its first queued render work.
    /// </summary>
    /// <remarks>
    /// WPF installs the native transform immediately before calling Win32 <c>ShowWindow</c>. A shortened offset cannot
    /// hide the whole popup the way WPF's full-height offset does, and Windows may present that partially translated
    /// surface while the remainder of the opening input stack is still doing one-time layout, template, resource, or
    /// composition work. Merely replacing the clock in <c>WM_SHOWWINDOW</c> therefore exposes that cold work as a
    /// frozen first frame. The message hook now uses that point to set a binary opacity gate, stop the native clock,
    /// and prepare the replacement timeline. A Loaded-priority operation runs after already-queued Render work,
    /// attaches the replacement clock while the root is still transparent, fixes its base value at the requested
    /// first offset, and reveals it atomically.
    /// </remarks>
    private sealed class PopupSlideAnimationController : IDisposable
    {
        private readonly DependencyPropertySubscriptions _propertySubscriptions = new();

        private const int WindowMessageShowWindow = 0x0018;

        private readonly DependencyObject _owner;

        private PopupAnimationSettings _settings;
        private IInputElement?         _watchedElement;
        private HwndSource?            _popupSource;
        private Popup?                 _overriddenPrivatePopup;
        private DispatcherOperation?   _pendingAnimationStart;
        private UIElement?             _openingPopupRoot;
        private TranslateTransform?    _openingTransform;
        private Action<bool>?          _openingRevealCallbacks;
        private int                    _openingVersion;
        private bool                   _isAttached;
        private bool                   _isDisposed;

        public PopupSlideAnimationController(DependencyObject owner)
        {
            _owner    = owner;
            _settings = PopupAnimationSettings.Default;
        }

        internal bool IsCurrentOpeningRefined { get; private set; }

        internal bool TryRegisterOpeningRevealCallback(Action<bool> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (_pendingAnimationStart?.Status != DispatcherOperationStatus.Pending ||
                _openingPopupRoot == null)
            {
                return false;
            }

            _openingRevealCallbacks += callback;
            return true;
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

            _settings = settings;

            if (!_isAttached)
            {
                AttachOwnerEvents();
                _isAttached = true;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_isAttached)
            {
                DetachOwnerEvents();
                _isAttached = false;
            }

            DetachPopupSource();
            RestorePrivatePopupAnimationResource();

            // Disabling while an opening is deferred must restore the binary opacity gate and the transform base
            // value immediately. A private ContextMenu/ToolTip value changed by this controller is restored to WPF's
            // original system resource so disabling the behavior also restores framework semantics.
            CancelCurrentOpening(restoreVisualState: true);
            IsCurrentOpeningRefined = false;
            _isDisposed             = true;
        }

        private void OnOwnerClosed(object? sender, EventArgs e)
        {
            CancelCurrentOpening(restoreVisualState: true);
            IsCurrentOpeningRefined = false;
        }

        private bool IsSlideSelected(Popup popup)
        {
            if (popup.PopupAnimation != PopupAnimation.Slide)
            {
                return false;
            }

            // ContextMenu and ToolTip do not bind their public PopupAnimation attached value to the private Popup.
            // Require both values to opt into Slide: the public value remains the application-facing contract, while
            // the private value proves that WPF actually created the native transform clock that can be refined.
            return _owner is Popup ||
                   _owner.GetValue(Popup.PopupAnimationProperty) is PopupAnimation.Slide;
        }

        /// <summary>
        /// Observes child replacement or private-popup animation inputs for the current owner.
        /// </summary>
        private void AttachOwnerEvents()
        {
            switch (_owner)
            {
                case Popup popup:
                {
                    popup.Closed += OnOwnerClosed;

                    _propertySubscriptions.Add(popup, Popup.ChildProperty, OnPopupChildChanged);

                    WatchElement(popup.Child);
                    break;
                }

                case NativeContextMenu contextMenu:
                {
                    contextMenu.Closed += OnOwnerClosed;
                    AddPopupAnimationValueChanged(contextMenu);
                    WatchElement(contextMenu);
                    break;
                }

                case NativeToolTip toolTip:
                {
                    toolTip.Closed += OnOwnerClosed;
                    AddPopupAnimationValueChanged(toolTip);
                    WatchElement(toolTip);
                    break;
                }
            }
        }

        /// <summary>
        /// Silently stops property observation before releasing presentation-source tracking.
        /// </summary>
        private void DetachOwnerEvents()
        {
            switch (_owner)
            {
                case Popup popup:
                {
                    popup.Closed -= OnOwnerClosed;

                    _propertySubscriptions.Remove(popup, Popup.ChildProperty, OnPopupChildChanged);

                    break;
                }

                case NativeContextMenu contextMenu:
                {
                    contextMenu.Closed -= OnOwnerClosed;
                    RemovePopupAnimationValueChanged(contextMenu);
                    break;
                }

                case NativeToolTip toolTip:
                {
                    toolTip.Closed -= OnOwnerClosed;
                    RemovePopupAnimationValueChanged(toolTip);
                    break;
                }
            }

            WatchElement(null);
        }

        private Popup? GetRootPopup()
        {
            return _owner as Popup ?? TryGetPrivateRootPopup(_owner);
        }

        private void OnPopupChildChanged(object? sender, EventArgs e)
        {
            WatchElement((_owner as Popup)?.Child);
        }

        private void OnOwnerPopupAnimationChanged(object? sender, EventArgs e)
        {
            // Before the first opening there is no private Popup, so this is intentionally a no-op and SourceChanged
            // will forward the latest value later. Once WPF has created it, keeping the value synchronized also
            // covers rapid close/reopen cycles in which WPF reuses the same HWND and raises no new SourceChanged.
            ForwardExplicitPrivatePopupAnimation();
        }

        /// <summary>
        /// Observes later animation choices without changing the owner's original value source.
        /// </summary>
        private void AddPopupAnimationValueChanged(DependencyObject owner)
        {
            _propertySubscriptions.Add(owner, Popup.PopupAnimationProperty, OnOwnerPopupAnimationChanged);
        }

        /// <summary>
        /// Stops animation forwarding without invoking it during controller teardown.
        /// </summary>
        private void RemovePopupAnimationValueChanged(DependencyObject owner)
        {
            _propertySubscriptions.Remove(owner, Popup.PopupAnimationProperty, OnOwnerPopupAnimationChanged);
        }

        private void WatchElement(IInputElement? element)
        {
            if (ReferenceEquals(_watchedElement, element))
            {
                return;
            }

            if (_watchedElement != null)
            {
                PresentationSource.RemoveSourceChangedHandler(_watchedElement, OnPresentationSourceChanged);
            }

            DetachPopupSource();

            _watchedElement = element;
            if (_watchedElement == null)
            {
                return;
            }

            PresentationSource.AddSourceChangedHandler(_watchedElement, OnPresentationSourceChanged);

            if (_watchedElement is Visual visual)
            {
                AttachPopupSource(PresentationSource.FromVisual(visual) as HwndSource);
            }
        }

        private void OnPresentationSourceChanged(object sender, SourceChangedEventArgs e)
        {
            AttachPopupSource(e.NewSource as HwndSource);
        }

        private void AttachPopupSource(HwndSource? source)
        {
            if (ReferenceEquals(_popupSource, source))
            {
                return;
            }

            DetachPopupSource();

            _popupSource = source;
            if (_popupSource == null)
            {
                return;
            }

            // ContextMenu and ToolTip assign a DynamicResource to their private Popup and expose no public forwarding
            // API. SourceChanged occurs during Popup.SetRootVisualToPopupRoot: the private Popup now exists, but
            // Popup.ShowWindow has not called SetupAnimations yet. Forwarding here therefore affects the very first
            // opening without prematurely invoking HookupParentPopup or disturbing its IsOpen binding lifecycle.
            ForwardExplicitPrivatePopupAnimation();
            _popupSource.AddHook(OnPopupWindowMessage);
        }

        private void DetachPopupSource()
        {
            CancelCurrentOpening(restoreVisualState: true);
            _popupSource?.RemoveHook(OnPopupWindowMessage);
            _popupSource = null;
        }

        private IntPtr OnPopupWindowMessage(
            IntPtr hwnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled)
        {
            if (msg != WindowMessageShowWindow || wParam == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            CancelCurrentOpening(restoreVisualState: true);
            IsCurrentOpeningRefined = false;

            var popup = GetRootPopup();
            if (popup != null && IsSlideSelected(popup))
            {
                // Windows can expose the HWND during ShowWindow, so a shortened transform is not sufficient to hide
                // cold opening work. Prepare an opacity-gated opening here, but do not attach the custom clock until
                // the current input stack and WPF's already-queued initial render have both had a chance to finish.
                IsCurrentOpeningRefined = TryDeferSlideAnimation(popup, _settings);
            }

            return IntPtr.Zero;
        }

        private bool TryDeferSlideAnimation(
            Popup popup,
            PopupAnimationSettings settings)
        {
            if (!TryGetSlideAnimationState(
                    popup,
                    settings,
                    out var popupRoot,
                    out var translateTransform,
                    out var startOffset,
                    out var duration))
            {
                return false;
            }

            // WPF has just installed its full-height native clock. Stop it before hiding the root so it cannot consume
            // part of its duration while the deferred first-render gate is active. Its zero base value is restored
            // explicitly because a prior interrupted custom opening may have left a non-zero base value behind.
            translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
            translateTransform.SetCurrentValue(TranslateTransform.YProperty, 0D);
            popupRoot.SetCurrentValue(UIElement.OpacityProperty, 0D);

            var version = ++_openingVersion;
            _openingPopupRoot = popupRoot;
            _openingTransform = translateTransform;

            // Build the complete Timeline while the opacity gate is active. Public CreateClock cannot be used as a
            // harmless preload because it immediately joins the TimeManager and begins on its next tick; an early
            // clock would therefore consume time before the popup is revealed. BeginAnimation below is intentionally
            // the only deferred part, and WPF's native clock has already paid the timing-system cold-start cost.
            try
            {
                DoubleAnimation? animation = null;

                if (duration > TimeSpan.Zero && Math.Abs(startOffset) >= 0.1D)
                {
                    animation = new DoubleAnimation(
                        startOffset,
                        0D,
                        new Duration(duration),
                        FillBehavior.HoldEnd
                    )
                    {
                        EasingFunction = settings.GetEasingFunction(),
                    };
                }

                // Loaded runs below Render priority. Popup.SetRootVisualToPopupRoot has already queued the work
                // required to realize the new HwndSource, so that first pass completes against a transparent root.
                // The prepared timeline is attached while still transparent and opacity is restored only after
                // BeginAnimation has returned, keeping WPF cold work and our clock's first-use work invisible.
                _pendingAnimationStart = popup.Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(
                        () => StartDeferredSlideAnimation(
                            popup,
                            popupRoot,
                            translateTransform,
                            startOffset,
                            animation,
                            version
                        )
                    )
                );
            }
            catch
            {
                // A custom Freezable can throw while cloning, and Dispatcher shutdown can reject BeginInvoke. Never
                // leave an otherwise valid Popup permanently transparent when optional animation preparation fails.
                CancelCurrentOpening(restoreVisualState: true);
                throw;
            }

            return true;
        }

        private void StartDeferredSlideAnimation(
            Popup popup,
            UIElement popupRoot,
            TranslateTransform translateTransform,
            double startOffset,
            DoubleAnimation? animation,
            int version)
        {
            if (version != _openingVersion || _isDisposed)
            {
                return;
            }

            _pendingAnimationStart = null;

            if (!popup.IsOpen ||
                !ReferenceEquals(_popupSource?.RootVisual, popupRoot) ||
                !IsSlideSelected(popup))
            {
                CancelCurrentOpening(restoreVisualState: true);
                IsCurrentOpeningRefined = false;
                return;
            }

            // WM_SHOWWINDOW occurs before Popup.Opened and before the first queued render/layout pass. An Opened
            // handler or an ActualPlacement trigger can change the Popup's desired size in that interval, causing
            // WPF to run UpdatePosition again and select the opposite candidate. Re-read the cached candidate bit at
            // the last possible point before attaching the replacement clock; otherwise the HWND can finish below
            // its target while the content still animates with the provisional above-target direction.
            if (TryGetAnimateFromBottom(popup, out var animateFromBottom))
            {
                startOffset = animateFromBottom ? Math.Abs(startOffset) : -Math.Abs(startOffset);

                if (animation != null)
                {
                    animation.SetCurrentValue(DoubleAnimation.FromProperty, startOffset);
                }
            }

            if (animation == null)
            {
                translateTransform.SetCurrentValue(TranslateTransform.YProperty, 0D);
                try
                {
                    NotifyOpeningRevealCallbacks(isAnimationStarting: true);
                }
                finally
                {
                    // A detached visual collaborator must never be able to strand the real popup behind its opacity
                    // gate. Preserve the original exception, but restore WPF's primary visual state first.
                    popupRoot.SetCurrentValue(UIElement.OpacityProperty, 1D);
                    ClearOpeningVisualReferences();
                }

                return;
            }

            animation.Completed += (_, _) => CompleteDeferredSlideAnimation(
                popupRoot,
                translateTransform,
                version
            );

            // A newly attached clock does not receive its begin time until the next TimeManager tick. Keep the base
            // value at the requested offset so restoring opacity below cannot expose an unshifted frame before that
            // tick. HoldEnd protects the opposite edge; completion commits zero as the base value before removing the
            // clock, so neither end of the animation can flash.
            translateTransform.SetCurrentValue(TranslateTransform.YProperty, startOffset);
            translateTransform.BeginAnimation(
                TranslateTransform.YProperty,
                animation,
                HandoffBehavior.SnapshotAndReplace
            );
            // The detached PopupShadow lives in another HWND, so PopupRoot opacity cannot conceal it. Notify one-shot
            // dependants only after the clock and its start value exist, but before revealing the content; both HWNDs
            // then enter their opening state during the same dispatcher callback without an end-position shadow frame.
            try
            {
                NotifyOpeningRevealCallbacks(isAnimationStarting: true);
            }
            finally
            {
                // Callback failures must not turn an optional detached shadow into a permanently invisible Popup.
                popupRoot.SetCurrentValue(UIElement.OpacityProperty, 1D);
            }
        }

        private void CompleteDeferredSlideAnimation(
            UIElement popupRoot,
            TranslateTransform translateTransform,
            int version)
        {
            if (version != _openingVersion ||
                !ReferenceEquals(_openingPopupRoot, popupRoot) ||
                !ReferenceEquals(_openingTransform, translateTransform))
            {
                return;
            }

            translateTransform.SetCurrentValue(TranslateTransform.YProperty, 0D);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, null);

            ClearOpeningVisualReferences();
        }

        private void CancelCurrentOpening(bool restoreVisualState)
        {
            _openingVersion++;

            if (_pendingAnimationStart?.Status == DispatcherOperationStatus.Pending)
            {
                _pendingAnimationStart.Abort();
            }

            _pendingAnimationStart = null;

            if (restoreVisualState)
            {
                if (_openingTransform != null)
                {
                    _openingTransform.SetCurrentValue(TranslateTransform.YProperty, 0D);
                    _openingTransform.BeginAnimation(TranslateTransform.YProperty, null);
                }

                _openingPopupRoot?.SetCurrentValue(UIElement.OpacityProperty, 1D);
            }

            try
            {
                NotifyOpeningRevealCallbacks(isAnimationStarting: false);
            }
            finally
            {
                ClearOpeningVisualReferences();
            }
        }

        private void NotifyOpeningRevealCallbacks(bool isAnimationStarting)
        {
            var callbacks = _openingRevealCallbacks;
            _openingRevealCallbacks = null;
            callbacks?.Invoke(isAnimationStarting);
        }

        private void ClearOpeningVisualReferences()
        {
            _openingPopupRoot = null;
            _openingTransform = null;
        }

        private void ForwardExplicitPrivatePopupAnimation()
        {
            if (_owner is Popup)
            {
                return;
            }

            if (!HasExplicitAnimationValue(_owner))
            {
                RestorePrivatePopupAnimationResource();
                return;
            }

            if (TryGetPrivateRootPopup(_owner) is not { } popup)
            {
                return;
            }

            popup.SetValue(Popup.PopupAnimationProperty, _owner.GetValue(Popup.PopupAnimationProperty));
            _overriddenPrivatePopup = popup;
        }

        private void RestorePrivatePopupAnimationResource()
        {
            if (_overriddenPrivatePopup == null)
            {
                return;
            }

            _overriddenPrivatePopup.SetResourceReference(
                Popup.PopupAnimationProperty,
                _owner is NativeContextMenu
                    ? SystemParameters.MenuPopupAnimationKey
                    : SystemParameters.ToolTipPopupAnimationKey
            );
            _overriddenPrivatePopup = null;
        }
    }

    /// <summary>
    /// Resolves the native popup animation surface and computes its slide distance and duration.
    /// Leaves the reference outputs null when the native animation structure is unavailable.
    /// </summary>
    private static bool TryGetSlideAnimationState(
        Popup popup,
        PopupAnimationSettings settings,
        [NotNullWhen(true)] out UIElement? popupRoot,
        [NotNullWhen(true)] out TranslateTransform? translateTransform,
        out double startOffset,
        out TimeSpan duration)
    {
        popupRoot          = null;
        translateTransform = null;
        startOffset        = 0D;
        duration           = TimeSpan.Zero;

        if (popup.Child is not Visual child ||
            (PresentationSource.FromVisual(child) as HwndSource)?.RootVisual is not UIElement actualPopupRoot ||
            !TryGetNativeAnimationLayer(actualPopupRoot, out var animationLayer) ||
            animationLayer.RenderTransform is not TranslateTransform actualTranslateTransform)
        {
            return false;
        }

        // PopupRoot.SetupTranslateAnimations creates the transform synchronously, but an AnimationClock does not
        // expose its first current value until WPF's next animation tick. Use the same RenderSize and placement bit
        // consumed by the native method so the replacement is installed before the first rendered frame.
        var nativeDistance = animationLayer.RenderSize.Height;
        if (nativeDistance < 0.1D ||
            !TryGetAnimateFromBottom(popup, out var animateFromBottom))
        {
            return false;
        }

        var distance = Math.Min(
            Math.Min(settings.StartOffset, MaximumSlideDistance),
            nativeDistance
        );

        popupRoot          = actualPopupRoot;
        translateTransform = actualTranslateTransform;
        startOffset        = animateFromBottom ? distance : -distance;
        duration           = settings.GetDuration();

        return true;
    }

    /// <summary>
    /// Finds WPF's native animation layer, returning null when the expected structure is absent.
    /// </summary>
    private static bool TryGetNativeAnimationLayer(
        UIElement popupRoot, [NotNullWhen(true)] out UIElement? animationLayer)
    {
        animationLayer = null;

        // PopupRoot is internal, but its public visual structure mirrors the WPF source: a clipping Decorator whose
        // child is the AdornerDecorator animated by SetupTranslateAnimations. If this contract changes, leave the
        // native clock untouched instead of mutating an application-owned visual.
        if (VisualTreeHelper.GetChildrenCount(popupRoot) > 0 &&
            VisualTreeHelper.GetChild(popupRoot, 0) is Decorator { Child: UIElement nativeAnimationLayer })
        {
            animationLayer = nativeAnimationLayer;
            return true;
        }

        return false;
    }

    private static Popup? TryGetPrivateRootPopup(DependencyObject owner)
    {
        return owner switch
        {
            NativeContextMenu contextMenu => TryGetPrivateRootPopup(contextMenu, ContextMenuParentPopupField),
            NativeToolTip toolTip         => TryGetPrivateRootPopup(toolTip,     ToolTipParentPopupField),
            _                             => null,
        };
    }

    private static bool HasExplicitAnimationValue(DependencyObject owner)
    {
        var valueSource = DependencyPropertyHelper.GetValueSource(owner, Popup.PopupAnimationProperty);
        return valueSource.BaseValueSource != BaseValueSource.Default ||
               valueSource.IsAnimated ||
               valueSource.IsCoerced ||
               valueSource.IsExpression;
    }

    private static bool TryGetAnimateFromBottom(Popup popup, out bool animateFromBottom)
    {
        animateFromBottom = false;
        if (PopupAnimateFromBottomProperty == null)
        {
            return false;
        }

        try
        {
            if (PopupAnimateFromBottomProperty.GetValue(popup) is bool value)
            {
                animateFromBottom = value;
                return true;
            }
        }
        catch (TargetInvocationException)
        {
            return false;
        }
        catch (MemberAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }

        return false;
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
        // Reflection is an optional refinement, not a type-initialization requirement. A missing or inaccessible
        // field must never prevent ContextMenu, ToolTip, or the application itself from starting.
        try
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
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

    private static PropertyInfo? GetNonPublicInstanceProperty(Type type, string name)
    {
        // As with the private parent-Popup fields, placement reflection is a cached optional refinement. A missing
        // member disables distance shortening but cannot break native popup animation or application startup.
        try
        {
            return type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic);
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
