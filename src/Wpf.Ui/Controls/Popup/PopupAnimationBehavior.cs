// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinContextMenu = System.Windows.Controls.ContextMenu;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides Wpf.Ui popup opening animation behavior based on the realized popup window root.
/// </summary>
/// <remarks>
/// WPF's built-in <see cref="Popup.PopupAnimation"/> can only use a small set of system animations and
/// does not expose easing or distance control. This behavior keeps <see cref="Popup.PopupAnimation"/> set to
/// <see cref="PopupAnimation.None"/> and animates the same internal popup root surface used by WPF's native
/// popup animations. WPF still chooses the final screen position and owns the close-time cleanup path.
/// </remarks>
public static class PopupAnimationBehavior
{
    /// <summary>
    /// Identifies the attached property that configures the custom Wpf.Ui popup opening animation.
    /// </summary>
    /// <remarks>
    /// Set this property on a <see cref="Popup"/> or <see cref="WinContextMenu"/> and keep
    /// <see cref="Popup.PopupAnimation"/> as
    /// <see cref="PopupAnimation.None"/> to enable the custom animation path. Clearing this property detaches
    /// the behavior and restores any animated values that were touched by the controller.
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

    internal static bool IsCustomAnimationActive(DependencyObject owner)
    {
        return GetAnimationSettings(owner) != null &&
               (GetPopupAnimation(owner) == PopupAnimation.None ||
                GetAnimationController(owner)?.IsOpenBootstrapAnimationApplied == true);
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
    /// the attached property is set. The controller stays attached to the popup, waits for <see cref="Popup.Opened"/>,
    /// locates the HWND root visual, and starts the entrance animation on WPF's internal popup animation layer.
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
        private          DispatcherOperation?    _pendingOpenAnimationOperation;
        private          double?                 _currentAnimationStartOffset;
        private          int                     _openVersion;
        private          bool                    _isOpenBootstrapAnimationApplied;
        private          bool                    _isUpdatingPopupAnimation;
        private          bool                    _isAttached;
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

            if (!_isAttached)
            {
                AttachPopupEvents();
                _isAttached = true;
            }

            if (IsOwnerOpen)
            {
                // The property can be attached after the popup is already open, especially when styles or
                // templates are applied late. Play the entrance immediately instead of waiting for the next
                // Opened event so the attached behavior is deterministic from the moment settings exist.
                _openVersion++;
                BeginOpenAnimation();
            }
            else
            {
                PrepareOpenBootstrapAnimation();
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
            if (!_isAttached)
            {
                return;
            }

            DetachPopupEvents();
            RestoreNaturalState();
            _isAttached = false;

            if (_settings != null)
            {
                UnsubscribeSettingsChanged();
                _settings = null;
            }
        }

        private void AttachPopupEvents()
        {
            // Opened is the first public point where WPF has created the popup root and attached it to the HWND.
            // IsOpen=false is observed only as an early signal to invalidate pending open callbacks and prepare
            // WPF's native first-frame hiding path for the next open.
            switch (_owner)
            {
                case Popup popup:
                {
                    popup.Opened += OnPopupOpened;
                    popup.Closed += OnPopupClosed;

                    DependencyPropertyDescriptor
                       .FromProperty(Popup.IsOpenProperty, typeof(Popup))
                      ?.AddValueChanged(popup, OnPopupIsOpenChanged);

                    DependencyPropertyDescriptor
                       .FromProperty(Popup.PopupAnimationProperty, typeof(Popup))
                      ?.AddValueChanged(popup, OnPopupAnimationChanged);

                    break;
                }

                case WinContextMenu contextMenu:
                {
                    contextMenu.Opened += OnPopupOpened;
                    contextMenu.Closed += OnPopupClosed;

                    DependencyPropertyDescriptor
                       .FromProperty(Popup.PopupAnimationProperty, typeof(WinContextMenu))
                      ?.AddValueChanged(contextMenu, OnPopupAnimationChanged);

                    break;
                }
            }
        }

        private void DetachPopupEvents()
        {
            switch (_owner)
            {
                case Popup popup:
                {
                    popup.Opened -= OnPopupOpened;
                    popup.Closed -= OnPopupClosed;

                    DependencyPropertyDescriptor
                       .FromProperty(Popup.IsOpenProperty, typeof(Popup))
                      ?.RemoveValueChanged(popup, OnPopupIsOpenChanged);

                    DependencyPropertyDescriptor
                       .FromProperty(Popup.PopupAnimationProperty, typeof(Popup))
                      ?.RemoveValueChanged(popup, OnPopupAnimationChanged);

                    break;
                }

                case WinContextMenu contextMenu:
                {
                    contextMenu.Opened -= OnPopupOpened;
                    contextMenu.Closed -= OnPopupClosed;

                    DependencyPropertyDescriptor
                       .FromProperty(Popup.PopupAnimationProperty, typeof(WinContextMenu))
                      ?.RemoveValueChanged(contextMenu, OnPopupAnimationChanged);

                    break;
                }
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

        private void OnPopupOpened(object? sender, EventArgs e)
        {
            // Every open/close cycle owns its own asynchronous callbacks. Incrementing the version lets delayed
            // Loaded/Render operations and animation completions detect that they belong to an older popup life.
            _openVersion++;
            RestoreOpenBootstrapAnimation();
            ApplyRootPopupAnimationMode();
            BeginOpenAnimation();
        }

        private void OnPopupIsOpenChanged(object? sender, EventArgs e)
        {
            if (IsOwnerOpen)
            {
                return;
            }

            // IsOpen=false is the earliest public close signal for a normal Popup. Do not touch visible close
            // state here; only invalidate the current open.
            _openVersion++;
            CancelPendingOpenAnimation();
            ReleaseAnimationReferences();
            PrepareOpenBootstrapAnimation();
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            if (IsOwnerOpen)
            {
                return;
            }

            // Closing invalidates any queued start operation or completion handler from the previous open. This
            // is especially important for popups that are rapidly reopened by pointer or keyboard interaction.
            _openVersion++;
            CancelPendingOpenAnimation();
            ReleaseAnimationReferences();
            PrepareOpenBootstrapAnimation();
        }

        private void OnPopupAnimationChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingPopupAnimation)
            {
                return;
            }

            if (_isOpenBootstrapAnimationApplied && GetPopupAnimation(_owner) != PopupAnimation.Fade)
            {
                _isOpenBootstrapAnimationApplied = false;
            }

            if (!IsCustomAnimationRequested)
            {
                RestoreNaturalState();
                return;
            }

            if (IsOwnerOpen)
            {
                // Re-entering the custom path while already open should still show the configured entrance
                // state instead of leaving the popup wherever a native or previous animation path stopped.
                BeginOpenAnimation();
            }
            else
            {
                PrepareOpenBootstrapAnimation();
            }
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            if (IsOwnerOpen && GetPopupAnimation(_owner) == PopupAnimation.None)
            {
                // Treat mutable setting changes as a new entrance rather than trying to retarget running
                // animations in place. Restarting keeps duration, easing, and start offset consistent.
                BeginOpenAnimation();
            }
        }

        private void BeginOpenAnimation()
        {
            BeginOpenAnimation(_openVersion);
        }

        private void BeginOpenAnimation(int openVersion)
        {
            if (_isDisposed || !IsOwnerOpen)
            {
                return;
            }

            if (openVersion != _openVersion)
            {
                return;
            }

            if (!IsCustomAnimationRequested || _settings is not { } settings)
            {
                RestoreNaturalState();
                return;
            }

            RestoreOpenBootstrapAnimation();
            ApplyRootPopupAnimationMode();

            if (GetPopupRoot() is not UIElement popupRoot)
            {
                // Popup.Opened can fire before PresentationSource exposes the root visual. Retry at Loaded
                // priority with the same version token so a quick close/reopen cannot animate a stale popup.
                CancelPendingOpenAnimation();
                _pendingOpenAnimationOperation = _owner.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        _pendingOpenAnimationOperation = null;
                        BeginOpenAnimation(openVersion);
                    }),
                    DispatcherPriority.Loaded
                );

                return;
            }

            if (!TryGetAnimationLayer(popupRoot, out var animationLayer))
            {
                // If WPF changes PopupRoot enough that we cannot find its native animation layer, fall back to a
                // fade-only entrance rather than placing a translation where PopupRoot.StopAnimations cannot clear it.
                animationLayer = null;
            }

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

            CancelPendingOpenAnimation();
            InitializeOpenAnimationBaseValues(popupRoot, translateTransform, startOffset);

            if (duration <= TimeSpan.Zero || translateTransform == null || Math.Abs(startOffset) < 0.1D)
            {
                CompleteOpenAnimation();
                return;
            }

            StartOpenAnimation(openVersion, popupRoot, translateTransform, startOffset, duration);
        }

        private void StartOpenAnimation(
            int openVersion,
            UIElement popupRoot,
            TranslateTransform translateTransform,
            double startOffset,
            TimeSpan duration)
        {
            _pendingOpenAnimationOperation = null;

            if (_isDisposed || !IsOwnerOpen || openVersion != _openVersion)
            {
                // Render-priority work is allowed to become stale. Returning without cleanup is intentional
                // here: close/dispose/new-open paths already reset state through their lifecycle handlers.
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
            translateAnimation.Completed += (_, _) => OnOpenAnimationCompleted(openVersion);

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

        private void OnOpenAnimationCompleted(int openVersion)
        {
            if (openVersion != _openVersion)
            {
                // A completion from an older animation must not reset a newer opening sequence.
                return;
            }

            // The animations hold their final value until we can commit final base values and remove the clocks.
            CompleteOpenAnimation();
        }

        private static void InitializeOpenAnimationBaseValues(
            UIElement popupRoot,
            TranslateTransform? translateTransform,
            double startOffset)
        {
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
            CancelPendingOpenAnimation();
            CommitFinalOpenState();

            ReleaseAnimationReferences();
        }

        private void RestoreNaturalState()
        {
            CancelPendingOpenAnimation();
            RestoreOpenBootstrapAnimation();
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
            (GetPopupAnimation(_owner) == PopupAnimation.None || _isOpenBootstrapAnimationApplied);

        internal bool IsOpenBootstrapAnimationApplied => _isOpenBootstrapAnimationApplied;

        private void PrepareOpenBootstrapAnimation()
        {
            if (_owner is not Popup popup ||
                _settings == null ||
                _isOpenBootstrapAnimationApplied ||
                popup.IsOpen ||
                !popup.AllowsTransparency ||
                GetPopupAnimation(popup) != PopupAnimation.None)
            {
                return;
            }

            // Popup.SetRootVisualToPopupRoot hides the root before the HWND is shown only when PopupAnimation is
            // non-None. For templated popups, such as ComboBox, the actual Popup value often comes from a
            // TemplateBinding, so change the templated parent value that feeds the Popup instead of only changing
            // the already-bound target.
            SetPopupAnimation(GetPopupAnimationBootstrapTarget(popup), PopupAnimation.Fade);
            _isOpenBootstrapAnimationApplied = true;
        }

        private void RestoreOpenBootstrapAnimation()
        {
            if (!_isOpenBootstrapAnimationApplied || _owner is not Popup popup)
            {
                return;
            }

            SetPopupAnimation(GetPopupAnimationBootstrapTarget(popup), PopupAnimation.None);
            popup.SetCurrentValue(Popup.PopupAnimationProperty, PopupAnimation.None);
            _isOpenBootstrapAnimationApplied = false;
        }

        private static DependencyObject GetPopupAnimationBootstrapTarget(Popup popup)
        {
            return popup.TemplatedParent is DependencyObject templatedParent
                ? templatedParent
                : popup;
        }

        private void SetPopupAnimation(DependencyObject owner, PopupAnimation animation)
        {
            _isUpdatingPopupAnimation = true;

            try
            {
                owner.SetCurrentValue(Popup.PopupAnimationProperty, animation);
            }
            finally
            {
                _isUpdatingPopupAnimation = false;
            }
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

        private void CancelPendingOpenAnimation()
        {
            if (_pendingOpenAnimationOperation == null)
            {
                return;
            }

            if (_pendingOpenAnimationOperation.Status is DispatcherOperationStatus.Pending)
            {
                // Abort only while the dispatcher operation has not run yet. Once it is executing or completed,
                // the version checks and lifecycle handlers own the cleanup.
                _pendingOpenAnimationOperation.Abort();
            }

            _pendingOpenAnimationOperation = null;
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
