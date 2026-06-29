// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides Wpf.Ui popup opening animation behavior.
/// </summary>
public static class PopupAnimationBehavior
{
    /// <summary>
    /// Identifies the attached property that configures Wpf.Ui popup opening animation.
    /// </summary>
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
    /// <returns>Popup animation settings property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
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
    /// <param name="value">Popup animation settings property value.</param>
    public static void SetAnimationSettings(DependencyObject element, PopupAnimationSettings? value)
    {
        element.SetValue(AnimationSettingsProperty, value);
    }

    [AttachedPropertyBrowsableForType(typeof(Popup))]
    private static PopupAnimationController? GetAnimationController(DependencyObject element)
    {
        return (PopupAnimationController?)element.GetValue(AnimationControllerProperty);
    }

    private static void SetAnimationController(DependencyObject element, PopupAnimationController? value)
    {
        element.SetValue(AnimationControllerProperty, value);
    }

    internal static bool IsCustomAnimationActive(Popup popup)
    {
        return popup.PopupAnimation == PopupAnimation.None && GetAnimationSettings(popup) != null;
    }

    internal static Duration GetAnimationDuration(Popup popup)
    {
        return new Duration(GetAnimationSettings(popup)?.GetDuration() ?? TimeSpan.Zero);
    }

    internal static IEasingFunction? GetAnimationEasingFunction(Popup popup)
    {
        return GetAnimationSettings(popup)?.GetEasingFunction();
    }

    internal static bool TryGetAnimationStartOffset(Popup popup, UIElement popupRoot, out double startOffset)
    {
        if (GetAnimationSettings(popup) is not { } settings)
        {
            startOffset = 0D;
            return false;
        }

        startOffset = GetEffectiveStartOffset(settings, popupRoot, ResolveActualVerticalPlacement(popup, popupRoot));
        return true;
    }

    private static double GetEffectiveStartOffset(
        PopupAnimationSettings settings,
        UIElement popupRoot,
        PopupVerticalPlacement popupPlacement)
    {
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

    private static PopupVerticalPlacement ResolveActualVerticalPlacement(Popup popup, UIElement popupRoot)
    {
        if (popup.PlacementTarget is UIElement target &&
            TryGetScreenBounds(target,    out var targetBounds) &&
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

        return ResolveRequestedVerticalPlacement(popup.Placement);
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
            var dpi     = VisualTreeHelper.GetDpi(element);
            var topLeft = element.PointToScreen(new Point());

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

    private static void OnAnimationSettingsChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup popup)
        {
            return;
        }

        var controller = GetAnimationController(popup);
        if (e.NewValue is not PopupAnimationSettings settings)
        {
            controller?.Dispose();
            SetAnimationController(popup, null);
            return;
        }

        if (controller == null)
        {
            controller = new PopupAnimationController(popup);
            SetAnimationController(popup, controller);
        }

        controller.Attach(settings);
    }

    private enum PopupVerticalPlacement
    {
        BelowTarget,
        AboveTarget,
    }

    private sealed class PopupAnimationController : IDisposable
    {
        private readonly Popup                   _popup;
        private          PopupAnimationSettings? _settings;
        private          UIElement?              _popupRoot;
        private          TranslateTransform?     _translateTransform;
        private          DispatcherOperation?    _pendingOpenAnimationOperation;
        private          int                     _openVersion;
        private          bool                    _isAttached;
        private          bool                    _isSettingsChangedSubscribed;
        private          bool                    _isDisposed;

        public PopupAnimationController(Popup popup)
        {
            _popup = popup;
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
                UnsubscribeSettingsChanged();

                _settings = settings;
                SubscribeSettingsChanged();
            }

            if (!_isAttached)
            {
                AttachPopupEvents();
                _isAttached = true;
            }

            if (_popup.IsOpen)
            {
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
            _popup.Opened += OnPopupOpened;
            _popup.Closed += OnPopupClosed;

            DependencyPropertyDescriptor
               .FromProperty(Popup.PlacementProperty, typeof(Popup))
              ?.AddValueChanged(_popup, OnPlacementChanged);

            DependencyPropertyDescriptor
               .FromProperty(Popup.PopupAnimationProperty, typeof(Popup))
              ?.AddValueChanged(_popup, OnPopupAnimationChanged);
        }

        private void DetachPopupEvents()
        {
            _popup.Opened -= OnPopupOpened;
            _popup.Closed -= OnPopupClosed;

            DependencyPropertyDescriptor
               .FromProperty(Popup.PlacementProperty, typeof(Popup))
              ?.RemoveValueChanged(_popup, OnPlacementChanged);

            DependencyPropertyDescriptor
               .FromProperty(Popup.PopupAnimationProperty, typeof(Popup))
              ?.RemoveValueChanged(_popup, OnPopupAnimationChanged);
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
            _openVersion++;
            BeginOpenAnimation();
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            _openVersion++;
            StopOpenAnimation();
        }

        private void OnPlacementChanged(object? sender, EventArgs e)
        {
            if (_popup.IsOpen)
            {
                BeginOpenAnimation();
            }
        }

        private void OnPopupAnimationChanged(object? sender, EventArgs e)
        {
            if (_popup.PopupAnimation != PopupAnimation.None)
            {
                StopOpenAnimation();
                return;
            }

            if (_popup.IsOpen)
            {
                BeginOpenAnimation();
            }
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            if (_popup.IsOpen && _popup.PopupAnimation == PopupAnimation.None)
            {
                BeginOpenAnimation();
            }
        }

        private void BeginOpenAnimation()
        {
            BeginOpenAnimation(_openVersion);
        }

        private void BeginOpenAnimation(int openVersion)
        {
            if (_isDisposed || !_popup.IsOpen)
            {
                return;
            }

            if (openVersion != _openVersion)
            {
                return;
            }

            if (_settings == null || _popup.PopupAnimation != PopupAnimation.None)
            {
                StopOpenAnimation();
                return;
            }

            if (GetPopupRoot() is not UIElement popupRoot)
            {
                _ = _popup.Dispatcher.BeginInvoke(
                    new Action(() => BeginOpenAnimation(openVersion)),
                    DispatcherPriority.Loaded
                );
                return;
            }

            var duration = _settings.GetDuration();
            var startOffset = GetEffectiveStartOffset(
                _settings,
                popupRoot,
                ResolveActualVerticalPlacement(_popup, popupRoot)
            );
            var translateTransform = EnsureTranslateTransform(popupRoot);

            _popupRoot = popupRoot;

            CancelPendingOpenAnimation();

            translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
            translateTransform.Y = startOffset;
            popupRoot.BeginAnimation(UIElement.OpacityProperty, null);
            popupRoot.Opacity = 0D;

            if (duration <= TimeSpan.Zero || Math.Abs(startOffset) < 0.1D)
            {
                translateTransform.Y = 0D;
                popupRoot.Opacity    = 1D;
                return;
            }

            _pendingOpenAnimationOperation = _popup.Dispatcher.BeginInvoke(
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

            if (_isDisposed || !_popup.IsOpen || openVersion != _openVersion)
            {
                return;
            }

            if (_settings == null || _popup.PopupAnimation != PopupAnimation.None)
            {
                StopOpenAnimation();
                return;
            }

            var durationValue  = new Duration(duration);
            var easingFunction = _settings.GetEasingFunction();
            var translateAnimation = new DoubleAnimation(0D, durationValue, FillBehavior.HoldEnd)
            {
                EasingFunction = easingFunction,
            };
            translateAnimation.Completed += (_, _) => OnOpenAnimationCompleted(openVersion);

            // 透明度动画采用线性，不与动画设置靠齐。
            var opacityAnimation = new DoubleAnimation(1D, durationValue, FillBehavior.HoldEnd)
            {
                // EasingFunction = _settings.GetEasingFunction(),
            };

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
                return;
            }

            StopOpenAnimation();
        }

        private void StopOpenAnimation()
        {
            CancelPendingOpenAnimation();

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
        }

        private void CancelPendingOpenAnimation()
        {
            if (_pendingOpenAnimationOperation == null)
            {
                return;
            }

            if (_pendingOpenAnimationOperation.Status is DispatcherOperationStatus.Pending)
            {
                _pendingOpenAnimationOperation.Abort();
            }

            _pendingOpenAnimationOperation = null;
        }

        private Visual? GetPopupRoot()
        {
            return _popup.Child is Visual child
                ? (PresentationSource.FromVisual(child) as HwndSource)?.RootVisual as Visual
                : null;
        }

        private TranslateTransform EnsureTranslateTransform(UIElement popupRoot)
        {
            if (popupRoot.RenderTransform is TranslateTransform translateTransform)
            {
                _translateTransform = translateTransform;
                return translateTransform;
            }

            if (popupRoot.RenderTransform is TransformGroup transformGroup)
            {
                translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
                if (translateTransform == null)
                {
                    translateTransform = new TranslateTransform();
                    transformGroup.Children.Add(translateTransform);
                }

                _translateTransform = translateTransform;
                return translateTransform;
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