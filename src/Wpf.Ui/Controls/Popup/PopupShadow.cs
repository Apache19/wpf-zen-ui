// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics.CodeAnalysis;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Extensions.Wpf;
using WinContextMenu = System.Windows.Controls.ContextMenu;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides a detached shadow HWND for popup based controls.
/// </summary>
/// <remarks>
/// WPF popups are hosted in their own HWND and are clipped to that window. Drawing a large
/// <see cref="System.Windows.Media.Effects.DropShadowEffect"/> inside the popup would either be clipped at the popup edge
/// or require artificial transparent padding around every popup template. This helper instead creates a transparent companion HWND,
/// sizes it to the popup plus the requested shadow thickness, and keeps it immediately beneath the popup in the z-order.
/// The companion window renders only the outside part of the shadow; the popup content remains in the real popup HWND
/// so hit testing, focus, placement, and layout keep their normal WPF behavior.
/// </remarks>
public static class PopupShadow
{
    /// <summary>
    /// Identifies the attached property that enables detached shadow rendering for a <see cref="Popup"/> or
    /// <see cref="WinContextMenu"/>.
    /// </summary>
    /// <remarks>
    /// The value is intentionally a <see cref="Freezable"/> chrome object rather than a set of primitive attached
    /// properties. That lets styles share and animate the chrome as one object, while the controller can subscribe to
    /// its <see cref="Freezable.Changed"/> event and update the detached HWND without recreating owner subscriptions.
    /// Setting this property on any other element is ignored because the implementation depends on popup HWND lifetime
    /// and bounds messages.
    /// </remarks>
    public static readonly DependencyProperty PopupShadowChromeProperty =
        DependencyProperty.RegisterAttached(
            "PopupShadowChrome",
            typeof(PopupShadowChrome),
            typeof(PopupShadow),
            new PropertyMetadata(null, OnPopupShadowChromeChanged)
        );

    // Keeps the per-owner controller alive for as long as the attached chrome is set. The controller owns the
    // companion HWND, Win32 hooks, timers, and render callbacks, so storing it as private attached state ties all of
    // that cleanup to the public attached property without exposing an IDisposable contract to consumers.
    private static readonly DependencyProperty ShadowControllerProperty =
        DependencyProperty.RegisterAttached(
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

    [SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP001:Dispose created",
        Justification = "The controller is stored in an attached property "
                      + "and disposed when PopupShadowChrome is cleared.")]
    private static void OnPopupShadowChromeChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup and not WinContextMenu)
        {
            // The implementation depends on Popup/ContextMenu HWND behavior; other elements intentionally opt out.
            return;
        }

        var controller = GetShadowController(d);
        if (e.NewValue is not PopupShadowChrome chrome)
        {
            // Clearing the chrome means the owner should behave like a normal WPF popup again. Disposing here removes
            // borrowed HWND hooks before the controller reference is dropped, which is the important leak boundary.
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

    /// <summary>
    /// Owns the detached shadow window and synchronizes it with one popup-style owner.
    /// </summary>
    /// <remarks>
    /// The owner is either a <see cref="Popup"/> or a <see cref="WinContextMenu"/>. The controller borrows the owner's
    /// <see cref="HwndSource"/> only long enough to observe HWND movement, size, visibility, DPI, and destruction
    /// messages. It owns a second per-pixel-alpha <see cref="HwndSource"/> for the shadow surface. A common mistake is
    /// to think this shadow is part of the popup visual tree; it is not. It must be positioned with Win32 device-pixel
    /// coordinates, while its internal WPF content is still measured in logical units.
    /// </remarks>
    [SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0041:Set mutable dependency properties using SetCurrentValue",
        Justification = "The target dependency properties are defined on internal types "
                      + "and are not accessible for external binding or animation. "
                      + "Direct assignment is performed within internal control logic "
                      + "and does not affect external consumers.")]
    private sealed class PopupShadowController : IDisposable
    {
        private const int WS_POPUP          = unchecked((int)0x80000000);
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW  = 0x00000080;
        private const int WS_EX_NOACTIVATE  = 0x08000000;

        // The chrome is clipped out of the shadow surface. Pull the clip inward by half a logical pixel so the
        // antialiased edge of the mirrored Border cannot show up as a faint colored seam beside the real popup.
        private const double ChromeInset = 0.5D;

        // WPF's built-in popup animations do not expose their duration. The controller mirrors the platform value
        // used by PopupAnimation.Slide/Scroll/Fade closely enough to keep the detached shadow visually synchronized.
        private static readonly Duration PopupAnimationDuration = new(TimeSpan.FromMilliseconds(150D));

        // When there is no popup movement to follow, the shadow fades in quickly instead of popping on. This is
        // deliberately shorter than the popup animation so the shadow settles before it calls attention to itself.
        private static readonly Duration ShadowFadeInDuration   = new(TimeSpan.FromMilliseconds(80D));

        private readonly DependencyObject _owner;
        private readonly Dispatcher       _dispatcher;

        [SuppressMessage(
            "Usage",
            "CA2213:Disposable fields should be disposed",
            Justification = "This references the owner popup HWND source. "
                          + "PopupShadowController removes its hook but does not own the source.")]
        private HwndSource? _popupSource;

        private HwndSource?        _shadowSource;
        private PopupShadowChrome? _chrome;
        private Border?            _shadowBorder;
        private Grid?              _shadowRoot;
        private DispatcherTimer?   _deferredFadeInTimer;
        private Duration           _contentBoundsAnimationDuration = PopupAnimationDuration;
        private IEasingFunction?   _contentBoundsAnimationEasingFunction;
        private double             _contentBoundsAnimationStartOffset;
        private bool               _isParallelContentBoundsAnimation;
        private bool               _isOpenAnimationPending;
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
                // Frozen instances are safe to share and never raise changes; mutable instances are observed with a
                // weak event because styles can keep the chrome alive much longer than any individual popup.
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
                // The attached property can appear after the popup is already open, for example through a late style
                // application. Resolve the HWND immediately so the visual state matches the property state.
                _isOpenAnimationPending = true;
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
            // Opened/Closed describe the normal popup lifetime, while IsOpen changes are observed as well because
            // WPF can start closing before Closed fires. That earlier signal lets the shadow fade out with native
            // PopupAnimation.Fade instead of disappearing only after the popup is already gone.
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
            _isOpenAnimationPending = true;
            Open();

            // The popup HWND can be recreated or resized by template/layout work after Opened fires.
            // Running Open again at Loaded priority is intentionally idempotent: it either discovers a new owner
            // source or simply refreshes the existing bounds.
            _ = _dispatcher.BeginInvoke(Open, DispatcherPriority.Loaded);
        }

        private void OnOwnerClosed(object? sender, EventArgs e)
        {
            if (!IsOwnerOpen)
            {
                _isOpenAnimationPending = false;
                Close();
            }
        }

        private void OnOwnerIsOpenChanged(object? sender, EventArgs e)
        {
            if (!IsOwnerOpen)
            {
                _isOpenAnimationPending = false;
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
            if (_chrome == null || !IsOwnerOpen)
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
                BeginPendingOpenAnimation();
                return;
            }

            Close();

            _popupSource = popupSource;
            _popupSource.AddHook(PopupWndProc);

            EnsureShadowSource();
            ApplyChrome();
            UpdateShadowBounds();
            BeginPendingOpenAnimation(force: true);
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
            // ContextMenu inherits PopupAnimationProperty metadata through MenuBase plumbing even though the owner
            // type is not Popup. Reading the DP directly keeps Popup and ContextMenu on the same animation path.
            return _owner.GetValue(Popup.PopupAnimationProperty) is PopupAnimation animation
                ? animation
                : PopupAnimation.None;
        }

        private HwndSource? GetOwnerHwndSource()
        {
            // Popup itself is not in the visual tree after opening; its Child is. ContextMenu, by contrast, is the
            // root visual of its popup source. Resolve each owner through the visual that WPF actually presents.
            return _owner switch
            {
                Popup { Child: Visual child } => PresentationSource.FromVisual(child) as HwndSource,
                WinContextMenu contextMenu    => PresentationSource.FromVisual(contextMenu) as HwndSource,
                _                             => null,
            };
        }

        private Visual? GetOwnerRootVisual()
        {
            // This returns the WPF content root, not the HWND root visual. It is used only for DPI and chrome mirroring
            // where the popup template's Border is meaningful.
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

            // Keep the companion HWND non-activating, hidden from Alt+Tab, and transparent to hit testing.
            const int exStyle = WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;

            var parameters = new HwndSourceParameters("Wpf.Ui.PopupShadow")
            {
                Width                      = 1,
                Height                     = 1,
                WindowStyle                = WS_POPUP,
                ExtendedWindowStyle        = exStyle,

                // Required for a transparent WPF HWND. Without per-pixel opacity the companion window would paint an
                // opaque rectangle around the popup instead of only the shadow pixels.
                UsesPerPixelOpacity        = true,

                // The shadow is a visual accessory, never an input root. These flags prevent it from stealing focus or
                // menu-mode behavior from the owner popup.
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

            var sourceBorder = GetOwnerRootVisual().FindDescendant<Border>(includeSelf: true);
            if (sourceBorder != null)
            {
                // Mirror the popup chrome so the shadow matches rounded corners and border thickness.
                // Only the nearest Border is copied; the actual content stays in the owner HWND, and the mirrored
                // Border is later clipped away so only its DropShadowEffect remains visible.
                _shadowBorder.CornerRadius    = sourceBorder.CornerRadius;
                _shadowBorder.BorderThickness = sourceBorder.BorderThickness;
                _shadowBorder.Padding         = sourceBorder.Padding;
            }
            else
            {
                // Some popups do not use a Border as their template root. Fall back to square chrome rather than
                // carrying stale radius/thickness from a previous owner/template.
                _shadowBorder.CornerRadius    = default;
                _shadowBorder.BorderThickness = default;
                _shadowBorder.Padding         = default;
            }

            _shadowBorder.Background  = _chrome.ChromeBrush;
            _shadowBorder.BorderBrush = _chrome.ChromeBrush;
            _shadowBorder.Effect      = _chrome.CreateEffect();
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
                // The cached HwndSource can outlive the native handle during popup teardown. Treat an invalid handle
                // as a close path so hooks and the companion window are released promptly.
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
            // Convert only the thickness: the popup rectangle already came from GetWindowRect in device pixels, while
            // the internal WPF root size is converted back to logical units after the HWND size is computed.
            var deviceThickness = LogicalToDevice(shadowThickness, dpi);
            var shadowLeft      = popupRect.left - (int)Math.Ceiling(deviceThickness.Left);
            var shadowTop       = popupRect.top - (int)Math.Ceiling(deviceThickness.Top);
            var shadowWidth     = popupRect.Width + (int)Math.Ceiling(deviceThickness.Left + deviceThickness.Right);
            var shadowHeight    = popupRect.Height + (int)Math.Ceiling(deviceThickness.Top + deviceThickness.Bottom);

            _shadowRoot.Width  = Math.Max(0D, shadowWidth / dpi.DpiScaleX);
            _shadowRoot.Height = Math.Max(0D, shadowHeight / dpi.DpiScaleY);

            UpdateShadowContentBounds(default);

            var flags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
            flags |= PInvoke.IsWindowVisible(popupHwnd)
                ? SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW
                : SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW;

            _isUpdatingShadow = true;

            try
            {
                // Use the popup HWND as insert-after so the shadow stays directly beneath the popup.
                // SetWindowPos inserts the shadow after popupHwnd in z-order; because top-to-bottom z-order is
                // "previous before next", the companion becomes the next window behind the owner rather than a top
                // window covering it.
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

        private void BeginPendingOpenAnimation(bool force = false)
        {
            if (!force && !_isOpenAnimationPending)
            {
                return;
            }

            _isOpenAnimationPending = false;
            BeginOpenAnimation();
        }

        private void BeginOpenAnimation()
        {
            if (_shadowRoot == null || _chrome == null)
            {
                return;
            }

            StopOpenAnimation();
            // Start hidden every time. Bounds may already be correct from a previous open, but opacity and content
            // clipping are transient animation state and must be re-established for this open cycle.
            _shadowRoot.Opacity = 0D;
            UpdateShadowContentBounds(default);

            var animation = GetPopupAnimation();
            switch (animation)
            {
                case PopupAnimation.Fade:
                    // Fade has no changing content bounds to chase; the popup and detached shadow can fade together.
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
                    if (PopupAnimationBehavior.IsCustomAnimationActive(_owner))
                    {
                        // PopupAnimationBehavior animates the popup HWND root while Popup.PopupAnimation remains None.
                        // The shadow still needs to follow that synthetic movement so both features compose.
                        var duration = PopupAnimationBehavior.GetAnimationDuration(_owner);
                        if (_chrome.AnimationMode == PopupShadowAnimationMode.DynamicContentBounds)
                        {
                            if (TryBeginParallelDynamicContentBoundsAnimation(_owner, duration))
                            {
                                break;
                            }

                            BeginDynamicContentBoundsAnimation(duration);
                        }
                        else
                        {
                            // In fixed-bounds mode the full shadow would look detached during a slide, so reveal it
                            // after the content animation duration instead.
                            BeginDeferredFadeIn(duration.TimeSpan);
                        }
                    }
                    else
                    {
                        // No native or custom content animation exists. Use a small fade so creating the companion
                        // HWND does not produce a one-frame shadow flash.
                        BeginDeferredFadeIn(TimeSpan.Zero, ShadowFadeInDuration);
                    }

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

            // These fields describe the temporary strategy for the current open animation. Reset them together so a
            // later non-animated open cannot accidentally reuse an easing function or start offset from an older one.
            _contentBoundsAnimationDuration       = PopupAnimationDuration;
            _contentBoundsAnimationEasingFunction = null;
            _contentBoundsAnimationStartOffset    = 0D;
            _isParallelContentBoundsAnimation     = false;

            _shadowRoot.BeginAnimation(UIElement.OpacityProperty, null);
            _shadowRoot.Opacity = 0D;

            // Return the shadow surface to the natural full-content clip before clearing transforms. This matters
            // when Close is followed by a quick Open: the first bounds update should not inherit a partially visible
            // clip from the previous slide/scroll animation.
            UpdateShadowContentBounds(default);

            if (_shadowRoot.RenderTransform is TranslateTransform translateTransform)
            {
                translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
                translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                translateTransform.X = 0D;
                translateTransform.Y = 0D;
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
            BeginDynamicContentBoundsAnimation(PopupAnimationDuration);
        }

        private void BeginDynamicContentBoundsAnimation(Duration duration)
        {
            if (_shadowRoot == null)
            {
                return;
            }

            _contentBoundsAnimationDuration = duration;
            _shadowAnimationStartedAt       = DateTime.UtcNow;

            // Native PopupAnimation.Slide/Scroll animates inside WPF's popup root. There is no public clock to bind to,
            // so the shadow samples the rendered transform each frame and shrinks its visible chrome to the currently
            // revealed portion of the popup content.
            SubscribeRendering();

            BeginRootFadeIn(duration);
        }

        private bool TryBeginParallelDynamicContentBoundsAnimation(DependencyObject owner, Duration duration)
        {
            if (_shadowRoot == null ||
                _popupSource?.RootVisual is not UIElement popupRoot ||
                !PopupAnimationBehavior.TryGetAnimationStartOffset(owner, popupRoot, out var startOffset))
            {
                return false;
            }

            _contentBoundsAnimationDuration       = duration;
            _contentBoundsAnimationEasingFunction = PopupAnimationBehavior.GetAnimationEasingFunction(owner);
            _contentBoundsAnimationStartOffset    = startOffset;
            _isParallelContentBoundsAnimation     = true;
            _shadowAnimationStartedAt             = DateTime.UtcNow;

            // The custom animation starts by moving the popup root from startOffset back to zero. Apply the matching
            // offset before the first render tick so the shadow never shows around content that is still hidden.
            UpdateShadowContentBounds(new Vector(0D, startOffset));
            SubscribeRendering();

            BeginRootFadeIn(duration);

            return true;
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

            // DispatcherTimer is used instead of Task.Delay so the callback runs on the owner dispatcher and can be
            // cancelled with the rest of the WPF animation state during close/dispose.
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
            _shadowRoot.Opacity = 0D;
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
            if (elapsed >= _contentBoundsAnimationDuration.TimeSpan)
            {
                // Once the content animation is considered complete, stop sampling and restore the full popup chrome.
                // Keeping the Rendering subscription alive after this point would be wasted per-frame work.
                StopRendering();
                UpdateShadowContentBounds(default);
                return;
            }

            UpdateShadowContentBounds(
                _isParallelContentBoundsAnimation
                    ? GetParallelContentBoundsAnimationOffset(elapsed)
                    : GetPopupRootAnimationOffset()
            );
        }

        private Vector GetParallelContentBoundsAnimationOffset(TimeSpan elapsed)
        {
            var duration = _contentBoundsAnimationDuration.TimeSpan;
            if (duration <= TimeSpan.Zero)
            {
                return default;
            }

            var progress      = Math.Max(0D, Math.Min(1D, elapsed.TotalMilliseconds / duration.TotalMilliseconds));
            var easedProgress = _contentBoundsAnimationEasingFunction?.Ease(progress) ?? progress;

            // This recreates the same "start offset to zero" timeline used by PopupAnimationBehavior instead of
            // reading the popup transform. It is used only when the custom behavior can provide a reliable start
            // offset; otherwise the controller falls back to sampling the visual tree.
            return new Vector(0D, _contentBoundsAnimationStartOffset * (1D - easedProgress));
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
            // WPF's built-in popup animations may place their TranslateTransform on an internal descendant rather
            // than on the HwndSource root itself. Walk the visual tree and use the first meaningful translation as
            // the content reveal offset. The small tolerance filters out layout noise and completed animations.
            if (parent is UIElement { RenderTransform: TranslateTransform translateTransform } &&
                (Math.Abs(translateTransform.X) > 0.1D || Math.Abs(translateTransform.Y) > 0.1D))
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

            // contentOffset is measured in logical units within the shadow root. During a slide/scroll animation it
            // represents where the popup content currently is, while popupBounds represents the final viewport.
            var contentBounds = popupBounds with
            {
                X = popupBounds.Left + contentOffset.X,
                Y = popupBounds.Top + contentOffset.Y
            };

            var visibleBounds = Rect.Intersect(popupBounds, contentBounds);
            if (visibleBounds == Rect.Empty || visibleBounds.Width <= 0D || visibleBounds.Height <= 0D)
            {
                // When the popup content is fully outside its final viewport, there is no matching shadow to show.
                // Hiding both the Border and the root clip avoids leaving an orphaned DropShadowEffect halo.
                _shadowBorder.Width  = 0D;
                _shadowBorder.Height = 0D;

                _shadowRoot.Clip = Geometry.Empty;
                return;
            }

            _shadowBorder.HorizontalAlignment = HorizontalAlignment.Left;
            _shadowBorder.VerticalAlignment   = VerticalAlignment.Top;

            // Nudge inward slightly so anti-aliased chrome pixels do not leak through the shadow-only clip.
            // The real popup draws its own border/background; this mirrored border exists only as the shape that
            // DropShadowEffect casts from, so losing half a logical pixel of its interior is visually harmless.
            var chromeBounds = new Rect(
                visibleBounds.Left + ChromeInset,
                visibleBounds.Top + ChromeInset,
                Math.Max(0D, visibleBounds.Width - (ChromeInset * 2D)),
                Math.Max(0D, visibleBounds.Height - (ChromeInset * 2D))
            );

            _shadowBorder.Margin = new Thickness(chromeBounds.Left, chromeBounds.Top, 0D, 0D);
            _shadowBorder.Width  = chromeBounds.Width;
            _shadowBorder.Height = chromeBounds.Height;

            // Clip out the visible popup area, leaving only the outside shadow rendered by DropShadowEffect.
            // This is the key trick: the companion window is larger than the popup, but anything where the real popup
            // would draw content is punched out so the two HWNDs do not double-paint chrome or intercept visuals.
            var clipSize = new Size(_shadowRoot.Width, _shadowRoot.Height);
            _shadowRoot.Clip = CreateShadowOnlyClip(clipSize, chromeBounds, _shadowBorder.CornerRadius);
        }

        private static GeometryGroup CreateShadowOnlyClip(
            Size shadowSize,
            Rect contentBounds,
            CornerRadius cornerRadius)
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

            // RectangleGeometry supports only a uniform radius. Build the path manually so asymmetric CornerRadius
            // values from the popup template clip the shadow exactly like the real Border.
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
            // Border-like corner radii cannot exceed half of the available side, otherwise adjacent arcs overlap and
            // the EvenOdd punch-out can produce self-intersecting geometry.
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
            // Prefer the owner's visual DPI because the popup can be on a monitor with a different scale than the
            // application main window. Falling back to 1.0 keeps early/lost visual cases from breaking positioning.
            return GetOwnerRootVisual() is Visual visual
                ? VisualTreeHelper.GetDpi(visual)
                : new DpiScale(1D, 1D);
        }

        private static Thickness LogicalToDevice(Thickness thickness, DpiScale dpi)
        {
            // Thickness values in PopupShadowChrome are logical WPF units. SetWindowPos and GetWindowRect use physical
            // pixels, so every side must be scaled independently for per-monitor DPI.
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
                    // WPF can destroy and recreate popup HWNDs while the managed Popup remains alive. Closing the
                    // shadow here drops hooks attached to the old native handle; a later Open call will attach again.
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
                return new IntPtr(PInvoke.HTTRANSPARENT);
            }

            return IntPtr.Zero;
        }
    }
}
