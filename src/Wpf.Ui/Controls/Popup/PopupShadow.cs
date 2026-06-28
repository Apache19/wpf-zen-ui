// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinContextMenu = System.Windows.Controls.ContextMenu;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides a detached shadow HWND for popup based controls.
/// </summary>
/// <remarks>
/// WPF popups are hosted in their own HWND. This helper creates a transparent companion HWND behind the popup so the
/// shadow can extend outside the popup bounds without reserving transparent padding inside the popup content.
/// </remarks>
public static class PopupShadow
{
    /// <summary>
    /// Identifies the attached property that enables detached shadow rendering for a <see cref="Popup"/> or
    /// <see cref="WinContextMenu"/>.
    /// </summary>
    public static readonly DependencyProperty PopupShadowChromeProperty = DependencyProperty.RegisterAttached(
        "PopupShadowChrome",
        typeof(PopupShadowChrome),
        typeof(PopupShadow),
        new PropertyMetadata(null, OnPopupShadowChromeChanged)
    );

    // Keeps the per-owner controller alive for as long as the attached chrome is set.
    private static readonly DependencyProperty ShadowControllerProperty = DependencyProperty.RegisterAttached(
        "ShadowController",
        typeof(PopupShadowController),
        typeof(PopupShadow),
        new PropertyMetadata(default(PopupShadowController))
    );

    /// <summary>
    /// Helper for getting <see cref="PopupShadowChromeProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="PopupShadowChromeProperty"/> from.
    /// </param>
    /// <returns>PopupShadowChrome property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static PopupShadowChrome? GetPopupShadowChrome(DependencyObject element)
    {
        return (PopupShadowChrome?)element.GetValue(PopupShadowChromeProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="PopupShadowChromeProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="PopupShadowChromeProperty"/> on.
    /// </param>
    /// <param name="value">PopupShadowChrome property value.</param>
    public static void SetPopupShadowChrome(DependencyObject element, PopupShadowChrome? value)
    {
        element.SetValue(PopupShadowChromeProperty, value);
    }

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    private static PopupShadowController? GetShadowController(DependencyObject element)
    {
        return (PopupShadowController?)element.GetValue(ShadowControllerProperty);
    }

    private static void SetShadowController(DependencyObject element, PopupShadowController? value)
    {
        element.SetValue(ShadowControllerProperty, value);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP001:Dispose created",
        Justification = "The controller is stored in an attached property and disposed when PopupShadowChrome is cleared."
    )]
    private static void OnPopupShadowChromeChanged(
        DependencyObject                   d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is not Popup and not WinContextMenu)
        {
            // The implementation depends on Popup/ContextMenu HWND behavior; other elements intentionally opt out.
            return;
        }

        var controller = GetShadowController(d);
        if (e.NewValue is not PopupShadowChrome chrome)
        {
            controller?.Dispose();
            SetShadowController(d, null);
            return;
        }

        // Reuse the existing controller so replacing the chrome does not churn owner event subscriptions.
        if (controller == null)
        {
            controller = new PopupShadowController(d);
            SetShadowController(d, controller);
        }

        controller.Attach(chrome);
    }

    private sealed class PopupShadowController : IDisposable
    {
        private const int WS_POPUP = unchecked((int)0x80000000);

        private const int    WS_EX_TRANSPARENT = 0x00000020;
        private const int    WS_EX_TOOLWINDOW  = 0x00000080;
        private const int    WS_EX_NOACTIVATE  = 0x08000000;
        private const double ChromeInset       = 0.5D;

        private static readonly Duration PopupAnimationDuration = new(TimeSpan.FromMilliseconds(150D));
        private static readonly Duration ShadowFadeInDuration   = new(TimeSpan.FromMilliseconds(80D));

        private readonly DependencyObject _owner;
        private readonly Dispatcher       _dispatcher;

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Usage",
            "CA2213:Disposable fields should be disposed",
            Justification = "This references the owner popup HWND source. PopupShadowController removes its hook but does not own the source."
        )]
        private HwndSource? _popupSource;

        private HwndSource?        _shadowSource;
        private PopupShadowChrome? _chrome;
        private Border?            _shadowBorder;
        private Grid?              _shadowRoot;
        private DispatcherTimer?   _deferredFadeInTimer;
        private DateTime           _shadowAnimationStartedAt;
        private bool               _isAttached;
        private bool               _isChromeChangedSubscribed;
        private bool               _isRenderingSubscribed;
        private bool               _isUpdatingShadow;
        private bool               _isDisposed;

        public PopupShadowController(DependencyObject owner)
        {
            _owner      = owner;
            _dispatcher = owner.Dispatcher;
        }

        public void Attach(PopupShadowChrome chrome)
        {
            #if NET5_0_OR_GREATER

            ObjectDisposedException.ThrowIf(_isDisposed, this);

            #else
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(PopupShadowController));
            }

            #endif

            if (_chrome != chrome)
            {
                // PopupShadowChrome is a Freezable, so mutable instances raise Changed when animated or updated.
                UnsubscribeChromeChanged();

                _chrome = chrome;
                SubscribeChromeChanged();
            }

            if (!_isAttached)
            {
                AttachOwnerEvents();
                _isAttached = true;
            }

            if (IsOwnerOpen)
            {
                Open();
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

        // ReSharper disable once MemberCanBePrivate.Local
        public void Detach()
        {
            if (!_isAttached)
            {
                return;
            }

            DetachOwnerEvents();
            Close();
            _isAttached = false;

            if (_chrome != null)
            {
                // Keep the bookkeeping balanced even though the event is weak.
                UnsubscribeChromeChanged();
                _chrome = null;
            }
        }

        private void SubscribeChromeChanged()
        {
            if (_chrome is not { IsFrozen: false })
            {
                return;
            }

            WeakEventManager<Freezable, EventArgs>.AddHandler(_chrome, nameof(Freezable.Changed), OnChromeChanged);
            _isChromeChangedSubscribed = true;
        }

        private void UnsubscribeChromeChanged()
        {
            if (!_isChromeChangedSubscribed || _chrome == null)
            {
                return;
            }

            WeakEventManager<Freezable, EventArgs>.RemoveHandler(_chrome, nameof(Freezable.Changed), OnChromeChanged);
            _isChromeChangedSubscribed = false;
        }

        private bool IsOwnerOpen =>
            _owner switch
            {
                Popup popup                => popup.IsOpen,
                WinContextMenu contextMenu => contextMenu.IsOpen,
                _                          => false,
            };

        private void AttachOwnerEvents()
        {
            switch (_owner)
            {
                case Popup popup:
                {
                    popup.Opened += OnOwnerOpened;
                    popup.Closed += OnOwnerClosed;
                    DependencyPropertyDescriptor
                       .FromProperty(Popup.IsOpenProperty, typeof(Popup))
                      ?.AddValueChanged(popup, OnOwnerIsOpenChanged);
                    break;
                }

                case WinContextMenu contextMenu:
                {
                    contextMenu.Opened += OnOwnerOpened;
                    contextMenu.Closed += OnOwnerClosed;
                    DependencyPropertyDescriptor
                       .FromProperty(WinContextMenu.IsOpenProperty, typeof(WinContextMenu))
                      ?.AddValueChanged(contextMenu, OnOwnerIsOpenChanged);
                    break;
                }
            }
        }

        private void DetachOwnerEvents()
        {
            switch (_owner)
            {
                case Popup popup:
                {
                    popup.Opened -= OnOwnerOpened;
                    popup.Closed -= OnOwnerClosed;

                    DependencyPropertyDescriptor
                       .FromProperty(Popup.IsOpenProperty, typeof(Popup))
                      ?.RemoveValueChanged(popup, OnOwnerIsOpenChanged);

                    break;
                }

                case WinContextMenu contextMenu:
                {
                    contextMenu.Opened -= OnOwnerOpened;
                    contextMenu.Closed -= OnOwnerClosed;

                    DependencyPropertyDescriptor
                       .FromProperty(WinContextMenu.IsOpenProperty, typeof(WinContextMenu))
                      ?.RemoveValueChanged(contextMenu, OnOwnerIsOpenChanged);

                    break;
                }
            }
        }

        private void OnOwnerOpened(object? sender, EventArgs e)
        {
            Open();
            // The popup HWND can be recreated or resized by template/layout work after Opened fires.
            _ = _dispatcher.BeginInvoke(Open, DispatcherPriority.Loaded);
        }

        private void OnOwnerClosed(object? sender, EventArgs e)
        {
            Close();
        }

        private void OnOwnerIsOpenChanged(object? sender, EventArgs e)
        {
            if (!IsOwnerOpen)
            {
                BeginCloseAnimation();
            }
        }

        private void OnChromeChanged(object? sender, EventArgs e)
        {
            if (_shadowSource == null)
            {
                return;
            }

            ApplyChrome();
            UpdateShadowBounds();
        }

        private void Open()
        {
            if (_chrome == null)
            {
                return;
            }

            var popupSource = GetOwnerHwndSource();
            if (popupSource == null || popupSource.Handle == IntPtr.Zero)
            {
                return;
            }

            if (popupSource == _popupSource)
            {
                // The owner HWND is already hooked; only its bounds may have changed.
                UpdateShadowBounds();
                return;
            }

            Close();

            _popupSource = popupSource;
            _popupSource.AddHook(PopupWndProc);
            EnsureShadowSource();
            ApplyChrome();
            UpdateShadowBounds();
            BeginOpenAnimation();
        }

        private void Close()
        {
            // The owner source is borrowed from WPF. Remove our hook, but do not dispose it.
            _popupSource?.RemoveHook(PopupWndProc);
            _popupSource = null;

            if (_shadowSource != null)
            {
                StopOpenAnimation();
                _shadowSource.RemoveHook(ShadowWndProc);
                _shadowSource.Dispose();
                _shadowSource = null;
            }

            _shadowBorder = null;
            _shadowRoot   = null;
        }

        private PopupAnimation GetPopupAnimation()
        {
            return _owner.GetValue(Popup.PopupAnimationProperty) is PopupAnimation animation
                ? animation
                : PopupAnimation.None;
        }

        private HwndSource? GetOwnerHwndSource()
        {
            return _owner switch
            {
                Popup { Child: Visual child } => PresentationSource.FromVisual(child) as HwndSource,
                WinContextMenu contextMenu    => PresentationSource.FromVisual(contextMenu) as HwndSource,
                _                             => null,
            };
        }

        private Visual? GetOwnerRootVisual()
        {
            return _owner switch
            {
                Popup { Child: Visual child } => child,
                WinContextMenu contextMenu    => contextMenu,
                _                             => null,
            };
        }

        private void EnsureShadowSource()
        {
            if (_shadowSource != null)
            {
                return;
            }

            var parameters = new HwndSourceParameters("Wpf.Ui.PopupShadow")
            {
                Width       = 1,
                Height      = 1,
                WindowStyle = WS_POPUP,
                // Keep the companion HWND non-activating, hidden from Alt+Tab, and transparent to hit testing.
                ExtendedWindowStyle        = WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE,
                UsesPerPixelOpacity        = true,
                AcquireHwndFocusInMenuMode = false,
                TreatAsInputRoot           = false,
            };

            #pragma warning disable IDISP003 // The null guard above prevents replacing an undisposed shadow source.
            _shadowSource = new HwndSource(parameters);
            #pragma warning restore IDISP003
            _shadowSource.AddHook(ShadowWndProc);

            _shadowSource.CompositionTarget?.BackgroundColor = Colors.Transparent;

            // This visual tree is owned by the companion HWND and never participates in app layout.
            _shadowRoot = new Grid
            {
                Background       = Brushes.Transparent,
                IsHitTestVisible = false,
                ClipToBounds     = false,
                Opacity          = 0D,
                RenderTransform  = new TranslateTransform(),
            };

            _shadowBorder = new Border
            {
                IsHitTestVisible    = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch,
                SnapsToDevicePixels = true,
                UseLayoutRounding   = true,
            };

            _shadowRoot.Children.Add(_shadowBorder);
            _shadowSource.RootVisual = _shadowRoot;
        }

        private void ApplyChrome()
        {
            if (_chrome == null || _shadowBorder == null)
            {
                return;
            }

            var sourceBorder = FindFirstVisualChild<Border>(GetOwnerRootVisual());
            if (sourceBorder != null)
            {
                // Mirror the popup chrome so the shadow matches rounded corners and border thickness.
                _shadowBorder.SetCurrentValue(Border.CornerRadiusProperty,    sourceBorder.CornerRadius);
                _shadowBorder.SetCurrentValue(Border.BorderThicknessProperty, sourceBorder.BorderThickness);
                _shadowBorder.SetCurrentValue(Border.PaddingProperty,         sourceBorder.Padding);
            }
            else
            {
                _shadowBorder.SetCurrentValue(Border.CornerRadiusProperty,    default(CornerRadius));
                _shadowBorder.SetCurrentValue(Border.BorderThicknessProperty, default(Thickness));
                _shadowBorder.SetCurrentValue(Border.PaddingProperty,         default(Thickness));
            }

            _shadowBorder.SetCurrentValue(Border.BackgroundProperty,  _chrome.ChromeBrush);
            _shadowBorder.SetCurrentValue(Border.BorderBrushProperty, _chrome.ChromeBrush);
            _shadowBorder.SetCurrentValue(UIElement.EffectProperty,   _chrome.CreateEffect());
        }

        private void UpdateShadowBounds()
        {
            if (_isUpdatingShadow || _chrome == null || _popupSource == null || _shadowSource == null || _shadowRoot == null)
            {
                return;
            }

            var popupHwnd = (HWND)_popupSource.Handle;
            if (popupHwnd == HWND.Null || !PInvoke.IsWindow(popupHwnd))
            {
                Close();
                return;
            }

            if (!PInvoke.GetWindowRect(popupHwnd, out var popupRect))
            {
                return;
            }

            var dpi             = GetDpi();
            var shadowThickness = _chrome.ShadowThickness;
            // Win32 coordinates are device pixels; the public chrome uses WPF logical units.
            var deviceThickness = LogicalToDevice(shadowThickness, dpi);
            var shadowLeft      = popupRect.left - (int)Math.Ceiling(deviceThickness.Left);
            var shadowTop       = popupRect.top - (int)Math.Ceiling(deviceThickness.Top);
            var shadowWidth     = popupRect.Width + (int)Math.Ceiling(deviceThickness.Left + deviceThickness.Right);
            var shadowHeight    = popupRect.Height + (int)Math.Ceiling(deviceThickness.Top + deviceThickness.Bottom);

            _shadowRoot.SetCurrentValue(FrameworkElement.WidthProperty,  Math.Max(0D, shadowWidth / dpi.DpiScaleX));
            _shadowRoot.SetCurrentValue(FrameworkElement.HeightProperty, Math.Max(0D, shadowHeight / dpi.DpiScaleY));

            UpdateShadowContentBounds(default);

            var flags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
            flags |= PInvoke.IsWindowVisible(popupHwnd)
                ? SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW
                : SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW;

            _isUpdatingShadow = true;

            try
            {
                // Use the popup HWND as insert-after so the shadow stays directly beneath the popup.
                _ = PInvoke.SetWindowPos(
                    (HWND)_shadowSource.Handle,
                    popupHwnd,
                    shadowLeft,
                    shadowTop,
                    shadowWidth,
                    shadowHeight,
                    flags
                );
            }
            finally
            {
                _isUpdatingShadow = false;
            }
        }

        private void BeginOpenAnimation()
        {
            if (_shadowRoot == null || _chrome == null)
            {
                return;
            }

            StopOpenAnimation();
            _shadowRoot.SetCurrentValue(UIElement.OpacityProperty, 0D);
            UpdateShadowContentBounds(default);

            var animation = GetPopupAnimation();
            switch (animation)
            {
                case PopupAnimation.Fade:
                    BeginFadeAnimation();
                    break;

                case PopupAnimation.Slide:
                case PopupAnimation.Scroll:
                    if (_chrome.AnimationMode == PopupShadowAnimationMode.DynamicContentBounds)
                    {
                        // Track the popup content's animated clip so the shadow does not appear before the content.
                        BeginDynamicContentBoundsAnimation();
                    }
                    else
                    {
                        // Let the popup animation complete, then fade the shadow in quietly.
                        BeginDeferredFadeIn(PopupAnimationDuration.TimeSpan);
                    }

                    break;

                case PopupAnimation.None:
                default:
                    BeginDeferredFadeIn(TimeSpan.Zero, ShadowFadeInDuration);
                    break;
            }
        }

        private void StopOpenAnimation()
        {
            if (_shadowRoot == null)
            {
                return;
            }

            StopRendering();
            StopDeferredFadeIn();

            _shadowRoot.BeginAnimation(UIElement.OpacityProperty, null);
            _shadowRoot.SetCurrentValue(UIElement.OpacityProperty, 0D);
            UpdateShadowContentBounds(default);

            if (_shadowRoot.RenderTransform is TranslateTransform translateTransform)
            {
                translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
                translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                translateTransform.SetCurrentValue(TranslateTransform.XProperty, 0D);
                translateTransform.SetCurrentValue(TranslateTransform.YProperty, 0D);
            }
        }

        private void BeginFadeAnimation()
        {
            if (_shadowRoot == null)
            {
                return;
            }

            BeginRootFadeIn(PopupAnimationDuration);
        }

        private void BeginDynamicContentBoundsAnimation()
        {
            if (_shadowRoot == null)
            {
                return;
            }

            _shadowAnimationStartedAt = DateTime.UtcNow;
            SubscribeRendering();
            BeginRootFadeIn(PopupAnimationDuration);
        }

        private void BeginDeferredFadeIn(TimeSpan delay)
        {
            BeginDeferredFadeIn(delay, PopupAnimationDuration);
        }

        private void BeginDeferredFadeIn(TimeSpan delay, Duration fadeDuration)
        {
            if (delay <= TimeSpan.Zero)
            {
                BeginRootFadeIn(fadeDuration);
                return;
            }

            StopDeferredFadeIn();

            _deferredFadeInTimer = new DispatcherTimer(DispatcherPriority.Input, _dispatcher)
            {
                Interval = delay,
            };
            _deferredFadeInTimer.Tick += OnDeferredFadeInTimerTick;
            _deferredFadeInTimer.Start();
        }

        private void OnDeferredFadeInTimerTick(object? sender, EventArgs e)
        {
            StopDeferredFadeIn();
            UpdateShadowContentBounds(default);
            BeginRootFadeIn(ShadowFadeInDuration);
        }

        private void BeginRootFadeIn(Duration duration)
        {
            _shadowRoot?.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0D, 1D, duration, FillBehavior.HoldEnd)
            );
        }

        private void BeginCloseAnimation()
        {
            if (GetPopupAnimation() == PopupAnimation.Fade)
            {
                BeginRootFadeOut(PopupAnimationDuration);
                return;
            }

            HideShadowImmediately();
        }

        private void BeginRootFadeOut(Duration duration)
        {
            if (_shadowRoot == null)
            {
                return;
            }

            StopRendering();
            StopDeferredFadeIn();
            _shadowRoot.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(_shadowRoot.Opacity, 0D, duration, FillBehavior.HoldEnd)
            );
        }

        private void HideShadowImmediately()
        {
            if (_shadowRoot == null)
            {
                return;
            }

            StopRendering();
            StopDeferredFadeIn();
            _shadowRoot.BeginAnimation(UIElement.OpacityProperty, null);
            _shadowRoot.SetCurrentValue(UIElement.OpacityProperty, 0D);
        }

        private void SubscribeRendering()
        {
            if (_isRenderingSubscribed)
            {
                return;
            }

            CompositionTarget.Rendering += OnRendering;
            _isRenderingSubscribed      =  true;
        }

        private void StopRendering()
        {
            if (!_isRenderingSubscribed)
            {
                return;
            }

            CompositionTarget.Rendering -= OnRendering;
            _isRenderingSubscribed      =  false;
        }

        private void StopDeferredFadeIn()
        {
            if (_deferredFadeInTimer == null)
            {
                return;
            }

            _deferredFadeInTimer.Stop();
            _deferredFadeInTimer.Tick -= OnDeferredFadeInTimerTick;
            _deferredFadeInTimer      =  null;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            var elapsed = DateTime.UtcNow - _shadowAnimationStartedAt;
            if (elapsed >= PopupAnimationDuration.TimeSpan)
            {
                StopRendering();
                UpdateShadowContentBounds(default);
                return;
            }

            // WPF popup slide/scroll animations move an internal root transform; mirror that visible content offset.
            UpdateShadowContentBounds(GetPopupRootAnimationOffset());
        }

        private Vector GetPopupRootAnimationOffset()
        {
            if (_popupSource?.RootVisual is not DependencyObject rootVisual)
            {
                return default;
            }

            return FindPopupRootTranslateOffset(rootVisual);
        }

        private static Vector FindPopupRootTranslateOffset(DependencyObject parent)
        {
            if (parent is UIElement { RenderTransform: TranslateTransform translateTransform }
             && (Math.Abs(translateTransform.X) > 0.1D || Math.Abs(translateTransform.Y) > 0.1D))
            {
                return new Vector(translateTransform.X, translateTransform.Y);
            }

            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childrenCount; i++)
            {
                var offset = FindPopupRootTranslateOffset(VisualTreeHelper.GetChild(parent, i));
                if (Math.Abs(offset.X) > 0.1D || Math.Abs(offset.Y) > 0.1D)
                {
                    return offset;
                }
            }

            return default;
        }

        private void UpdateShadowContentBounds(Vector contentOffset)
        {
            if (_shadowRoot == null || _shadowBorder == null || _chrome == null)
            {
                return;
            }

            var shadowThickness = _chrome.ShadowThickness;
            var popupWidth      = Math.Max(0D, _shadowRoot.Width - shadowThickness.Left - shadowThickness.Right);
            var popupHeight     = Math.Max(0D, _shadowRoot.Height - shadowThickness.Top - shadowThickness.Bottom);
            var popupBounds     = new Rect(shadowThickness.Left, shadowThickness.Top, popupWidth, popupHeight);
            var contentBounds = popupBounds with
            {
                X = popupBounds.Left + contentOffset.X,
                Y = popupBounds.Top + contentOffset.Y
            };

            var visibleBounds = Rect.Intersect(popupBounds, contentBounds);
            if (visibleBounds == Rect.Empty || visibleBounds.Width <= 0D || visibleBounds.Height <= 0D)
            {
                _shadowBorder.SetCurrentValue(FrameworkElement.WidthProperty,  0D);
                _shadowBorder.SetCurrentValue(FrameworkElement.HeightProperty, 0D);

                _shadowRoot.SetCurrentValue(UIElement.ClipProperty, Geometry.Empty);
                return;
            }

            _shadowBorder.SetCurrentValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            _shadowBorder.SetCurrentValue(FrameworkElement.VerticalAlignmentProperty,   VerticalAlignment.Top);
            // Nudge inward slightly so anti-aliased chrome pixels do not leak through the shadow-only clip.
            var chromeBounds = new Rect(
                visibleBounds.Left + ChromeInset,
                visibleBounds.Top + ChromeInset,
                Math.Max(0D, visibleBounds.Width - (ChromeInset * 2D)),
                Math.Max(0D, visibleBounds.Height - (ChromeInset * 2D))
            );
            var visibleCornerRadius = GetVisibleCornerRadius(visibleBounds, contentBounds, _shadowBorder.CornerRadius);

            _shadowBorder.SetCurrentValue(
                FrameworkElement.MarginProperty,
                new Thickness(chromeBounds.Left, chromeBounds.Top, 0D, 0D)
            );
            _shadowBorder.SetCurrentValue(FrameworkElement.WidthProperty,  chromeBounds.Width);
            _shadowBorder.SetCurrentValue(FrameworkElement.HeightProperty, chromeBounds.Height);

            // Clip out the visible popup area, leaving only the outside shadow rendered by DropShadowEffect.
            _shadowRoot.SetCurrentValue(
                UIElement.ClipProperty,
                CreateShadowOnlyClip(
                    new Size(_shadowRoot.Width, _shadowRoot.Height),
                    chromeBounds,
                    visibleCornerRadius
                )
            );
        }

        private static CornerRadius GetVisibleCornerRadius(
            Rect         visibleBounds,
            Rect         contentBounds,
            CornerRadius sourceCornerRadius
        )
        {
            const double Tolerance = 0.1D;

            var leftVisible   = Math.Abs(visibleBounds.Left - contentBounds.Left) < Tolerance;
            var topVisible    = Math.Abs(visibleBounds.Top - contentBounds.Top) < Tolerance;
            var rightVisible  = Math.Abs(visibleBounds.Right - contentBounds.Right) < Tolerance;
            var bottomVisible = Math.Abs(visibleBounds.Bottom - contentBounds.Bottom) < Tolerance;

            // Hidden edges must not keep rounded corners, otherwise the clipped shadow shows curved cuts mid-animation.
            return new CornerRadius(
                leftVisible && topVisible ? sourceCornerRadius.TopLeft : 0D,
                topVisible && rightVisible ? sourceCornerRadius.TopRight : 0D,
                rightVisible && bottomVisible ? sourceCornerRadius.BottomRight : 0D,
                bottomVisible && leftVisible ? sourceCornerRadius.BottomLeft : 0D
            );
        }

        private static GeometryGroup CreateShadowOnlyClip(
            Size         shadowSize,
            Rect         contentBounds,
            CornerRadius cornerRadius
        )
        {
            var clip = new GeometryGroup
            {
                // EvenOdd subtracts the popup chrome from the full shadow surface.
                FillRule = FillRule.EvenOdd,
            };

            clip.Children.Add(new RectangleGeometry(new Rect(shadowSize)));
            clip.Children.Add(CreateRoundedRectangleGeometry(contentBounds, cornerRadius));

            return clip;
        }

        private static Geometry CreateRoundedRectangleGeometry(Rect bounds, CornerRadius cornerRadius)
        {
            if (bounds.Width <= 0D || bounds.Height <= 0D)
            {
                return Geometry.Empty;
            }

            var topLeft     = NormalizeRadius(cornerRadius.TopLeft,     bounds.Width, bounds.Height);
            var topRight    = NormalizeRadius(cornerRadius.TopRight,    bounds.Width, bounds.Height);
            var bottomRight = NormalizeRadius(cornerRadius.BottomRight, bounds.Width, bounds.Height);
            var bottomLeft  = NormalizeRadius(cornerRadius.BottomLeft,  bounds.Width, bounds.Height);

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(bounds.Left + topLeft,     bounds.Top), true, true);
                context.LineTo(new Point(bounds.Right - topRight,        bounds.Top), true, false);
                AddCorner(context, new Point(bounds.Right,               bounds.Top + topRight), topRight);
                context.LineTo(new Point(bounds.Right,                   bounds.Bottom - bottomRight), true, false);
                AddCorner(context, new Point(bounds.Right - bottomRight, bounds.Bottom), bottomRight);
                context.LineTo(new Point(bounds.Left + bottomLeft,       bounds.Bottom), true, false);
                AddCorner(context, new Point(bounds.Left,                bounds.Bottom - bottomLeft), bottomLeft);
                context.LineTo(new Point(bounds.Left,                    bounds.Top + topLeft), true, false);
                AddCorner(context, new Point(bounds.Left + topLeft,      bounds.Top), topLeft);
            }

            geometry.Freeze();
            return geometry;
        }

        private static double NormalizeRadius(double radius, double width, double height)
        {
            return Math.Max(0D, Math.Min(radius, Math.Min(width, height) / 2D));
        }

        private static void AddCorner(StreamGeometryContext context, Point point, double radius)
        {
            if (radius <= 0D)
            {
                context.LineTo(point, true, false);
                return;
            }

            context.ArcTo(
                point,
                new Size(radius, radius),
                0D,
                false,
                SweepDirection.Clockwise,
                true,
                false
            );
        }

        private DpiScale GetDpi()
        {
            return GetOwnerRootVisual() is Visual visual
                ? VisualTreeHelper.GetDpi(visual)
                : new DpiScale(1D, 1D);
        }

        private static Thickness LogicalToDevice(Thickness thickness, DpiScale dpi)
        {
            return new Thickness(
                thickness.Left * dpi.DpiScaleX,
                thickness.Top * dpi.DpiScaleY,
                thickness.Right * dpi.DpiScaleX,
                thickness.Bottom * dpi.DpiScaleY
            );
        }

        private IntPtr PopupWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            var message = (uint)msg;
            switch (message)
            {
                case PInvoke.WM_SHOWWINDOW:
                case PInvoke.WM_WINDOWPOSCHANGED:
                case PInvoke.WM_MOVE:
                case PInvoke.WM_SIZE:
                case PInvoke.WM_DPICHANGED:
                    // Defer back to WPF's dispatcher to avoid re-entering layout while processing HWND messages.
                    _ = _dispatcher.BeginInvoke(UpdateShadowBounds, DispatcherPriority.Send);
                    break;

                case PInvoke.WM_NCDESTROY:
                    Close();
                    break;
            }

            return IntPtr.Zero;
        }

        private static IntPtr ShadowWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if ((uint)msg == PInvoke.WM_NCHITTEST)
            {
                // HTTRANSPARENT lets mouse input continue to the popup or underlying window.
                handled = true;
                return new IntPtr(-1);
            }

            return IntPtr.Zero;
        }

        private static T? FindFirstVisualChild<T>(DependencyObject? parent)
            where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            if (parent is T target)
            {
                return target;
            }

            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childrenCount; i++)
            {
                if (FindFirstVisualChild<T>(VisualTreeHelper.GetChild(parent, i)) is { } child)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
