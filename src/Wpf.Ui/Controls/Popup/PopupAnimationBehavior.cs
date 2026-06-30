// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
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
/// <see cref="PopupAnimation.None"/> and animates the popup's HWND root visual directly after the popup opens.
/// The animation is therefore independent from the placement engine: WPF still chooses the final screen
/// position, while this behavior only fades and translates the already-positioned popup content into place.
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
        return GetPopupAnimation(owner) == PopupAnimation.None && GetAnimationSettings(owner) != null;
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
    /// locates the HWND root visual, applies initial opacity and translate values, then starts the real animations
    /// on the next render pass.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0041:Set mutable dependency properties using SetCurrentValue",
        Justification = "The target dependency properties belong to the popup root animation state. "
                      + "They are assigned and restored by this controller while opening the popup, "
                      + "and do not represent externally supplied bindings or local values.")]
    private sealed class PopupAnimationController : IDisposable
    {
        private readonly DependencyObject        _owner;
        private          PopupAnimationSettings? _settings;
        private          UIElement?              _popupRoot;
        private          TranslateTransform?     _translateTransform;
        private          DispatcherOperation?    _pendingOpenAnimationOperation;
        private          double?                 _currentAnimationStartOffset;
        private          int                     _openVersion;
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
                BeginOpenAnimation();
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
            StopOpenAnimation();
            _isAttached = false;

            if (_settings != null)
            {
                UnsubscribeSettingsChanged();
                _settings = null;
            }
        }

        private void AttachPopupEvents()
        {
            Debug.WriteLine("[PopupAnimation] AttachPopupEvents");

            // Placement and PopupAnimation are watched in addition to Opened/Closed because both can change
            // while the popup is alive. A placement change may require a new slide direction, and a switch back
            // to a native PopupAnimation must cancel this custom animation path immediately.
            switch (_owner)
            {
                case Popup popup:
                {
                    popup.Opened += OnPopupOpened;
                    popup.Closed += OnPopupClosed;

                    DependencyPropertyDescriptor
                       .FromProperty(Popup.PlacementProperty, typeof(Popup))
                      ?.AddValueChanged(popup, OnPlacementChanged);

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
                       .FromProperty(WinContextMenu.PlacementProperty, typeof(WinContextMenu))
                      ?.AddValueChanged(contextMenu, OnPlacementChanged);

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
                       .FromProperty(Popup.PlacementProperty, typeof(Popup))
                      ?.RemoveValueChanged(popup, OnPlacementChanged);

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
                       .FromProperty(WinContextMenu.PlacementProperty, typeof(WinContextMenu))
                      ?.RemoveValueChanged(contextMenu, OnPlacementChanged);

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
            BeginOpenAnimation();
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            // Closing invalidates any queued start operation or completion handler from the previous open. This
            // is especially important for popups that are rapidly reopened by pointer or keyboard interaction.
            _openVersion++;
            StopOpenAnimation();
        }

        private void OnPlacementChanged(object? sender, EventArgs e)
        {
            if (IsOwnerOpen)
            {
                // Popup can move while it remains open, for example after layout changes or screen-edge
                // collision handling. Restart so the offset is recalculated against the new realized position.
                BeginOpenAnimation();
            }
        }

        private void OnPopupAnimationChanged(object? sender, EventArgs e)
        {
            if (GetPopupAnimation(_owner) != PopupAnimation.None)
            {
                StopOpenAnimation();
                return;
            }

            if (IsOwnerOpen)
            {
                // Re-entering the custom path while already open should still show the configured entrance
                // state instead of leaving the popup wherever a native or previous animation path stopped.
                BeginOpenAnimation();
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

            if (_settings == null || GetPopupAnimation(_owner) != PopupAnimation.None)
            {
                StopOpenAnimation();
                return;
            }

            if (GetPopupRoot() is not UIElement popupRoot)
            {
                // Popup.Opened can fire before PresentationSource exposes the root visual. Retry at Loaded
                // priority with the same version token so a quick close/reopen cannot animate a stale popup.
                _ = _owner.Dispatcher.BeginInvoke(
                    new Action(() => BeginOpenAnimation(openVersion)),
                    DispatcherPriority.Loaded
                );

                return;
            }

            var duration = _settings.GetDuration();

            var startOffset = GetEffectiveStartOffset(
                _settings,
                popupRoot,
                ResolveActualVerticalPlacement(_owner, popupRoot)
            );

            var translateTransform = EnsureTranslateTransform(popupRoot);

            _popupRoot                   = popupRoot;
            _currentAnimationStartOffset = startOffset;

            // A previous opening attempt may have already scheduled the Render-priority start. Cancel it before
            // writing the new initial frame so only the latest settings, placement, and version can start.
            CancelPendingOpenAnimation();

            // Establish the first visible frame synchronously. Popup has already chosen its final screen
            // placement, but before WPF gets a chance to paint that natural state we immediately pull the root
            // back to the configured offset and make it transparent. Without this write-before-render step the
            // user can see one frame of fully visible content at the final position before the slide begins.
            translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
            translateTransform.Y = startOffset;
            popupRoot.BeginAnimation(UIElement.OpacityProperty, null);
            popupRoot.Opacity = 0D;

            if (duration <= TimeSpan.Zero || Math.Abs(startOffset) < 0.1D)
            {
                // The initial hidden/offset frame was already applied to avoid flicker. If there is no useful
                // motion to play, restore the natural values immediately so callers can use zero-duration
                // settings as an explicit "no animation" state without leaving the popup transparent.
                translateTransform.Y = 0D;
                popupRoot.Opacity    = 1D;
                _currentAnimationStartOffset = null;
                return;
            }

            // Start the actual animations at Render priority, after the initial offset/opacity values have had
            // a layout/render boundary to become the presented starting frame. Starting immediately in the same
            // dispatcher turn can spend part of the animation timeline while the popup content is not yet drawn,
            // which makes the first visible frame appear already in progress or skip the opening motion entirely.
            _pendingOpenAnimationOperation = _owner.Dispatcher.BeginInvoke(
                new Action(() => StartOpenAnimation(openVersion, popupRoot, translateTransform, duration)),
                DispatcherPriority.Render
            );
        }

        private void StartOpenAnimation(
            int openVersion,
            UIElement popupRoot,
            TranslateTransform translateTransform,
            TimeSpan duration)
        {
            _pendingOpenAnimationOperation = null;

            if (_isDisposed || !IsOwnerOpen || openVersion != _openVersion)
            {
                // Render-priority work is allowed to become stale. Returning without cleanup is intentional
                // here: close/dispose/new-open paths already reset state through StopOpenAnimation.
                return;
            }

            if (_settings == null || GetPopupAnimation(_owner) != PopupAnimation.None)
            {
                StopOpenAnimation();
                return;
            }

            var durationValue  = new Duration(duration);
            var easingFunction = _settings.GetEasingFunction();

            // Only the translate animation uses the configured easing. Opacity stays linear so every popup has
            // a predictable fade while the movement curve provides the visual character.
            var translateAnimation = new DoubleAnimation(0D, durationValue, FillBehavior.HoldEnd)
            {
                EasingFunction = easingFunction,
            };
            translateAnimation.Completed += (_, _) => OnOpenAnimationCompleted(openVersion);

            // Keep the fade linear instead of forcing it to follow the movement easing. Opacity is perceived very
            // differently from displacement: an ease-out fade can look like a sudden appearance because the fast
            // early alpha change is hard to track, while linear opacity gives the softest and most legible fade.
            // It also avoids each movement easing accidentally giving the popup a different fade personality.
            var opacityAnimation = new DoubleAnimation(1D, durationValue, FillBehavior.HoldEnd)
            {
                // 测试，看看慢动作下是什么表现
                EasingFunction = _settings.GetEasingFunction(),
            };

            // SnapshotAndReplace deliberately cuts off any previous generated animation. Popup entrance is a
            // state correction to a known final position, not a composable effect where multiple animations
            // should accumulate.
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

            // The animations use FillBehavior.HoldEnd so the final frame stays stable until completion is
            // observed. StopOpenAnimation then clears the animation clocks and writes the same natural values as
            // local state, preventing a held animation from masking future property changes.
            StopOpenAnimation();
        }

        private void StopOpenAnimation()
        {
            CancelPendingOpenAnimation();

            // Always leave the popup root in its natural state. This method is used for normal completion,
            // cancellation, setting changes, native animation handoff, and disposal. After the reset, drop the
            // transient visual references so a future popup HWND/root rebuild starts from freshly resolved state.
            if (_translateTransform != null)
            {
                _translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                _translateTransform.Y = 0D;
            }

            if (_popupRoot != null)
            {
                _popupRoot.BeginAnimation(UIElement.OpacityProperty, null);
                _popupRoot.Opacity = 1D;
            }

            _translateTransform = null;
            _popupRoot          = null;
            _currentAnimationStartOffset = null;
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
                // the version checks and StopOpenAnimation path own the cleanup.
                _pendingOpenAnimationOperation.Abort();
            }

            _pendingOpenAnimationOperation = null;
        }

        private Visual? GetPopupRoot()
        {
            // Popup.Child belongs to the logical popup content, but the visible window is hosted under the
            // HwndSource root visual. Animating the root keeps shadows, chrome, and transformed content moving
            // as one unit without changing the placement target or child layout.
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

        private TranslateTransform EnsureTranslateTransform(UIElement popupRoot)
        {
            // Preserve any transform that the popup root already has. When there is a TransformGroup we reuse
            // an existing TranslateTransform if possible; otherwise we append one so the animation composes with
            // scale/rotate/skew transforms that may already be applied by WPF or another internal behavior.
            if (popupRoot.RenderTransform is TranslateTransform translateTransform)
            {
                _translateTransform = translateTransform;
                return translateTransform;
            }

            if (popupRoot.RenderTransform is TransformGroup transformGroup)
            {
                var childTranslateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
                if (childTranslateTransform == null)
                {
                    childTranslateTransform = new TranslateTransform();
                    transformGroup.Children.Add(childTranslateTransform);
                }

                _translateTransform = childTranslateTransform;
                return childTranslateTransform;
            }

            translateTransform = new TranslateTransform();

            if (popupRoot.RenderTransform == Transform.Identity)
            {
                popupRoot.RenderTransform = translateTransform;
            }
            else
            {
                popupRoot.RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        popupRoot.RenderTransform,
                        translateTransform,
                    },
                };
            }

            _translateTransform = translateTransform;
            return translateTransform;
        }
    }
}
