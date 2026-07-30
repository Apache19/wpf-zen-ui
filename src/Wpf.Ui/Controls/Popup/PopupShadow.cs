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
    /// <see cref="ContextMenu"/> or <see cref="ToolTip"/>.
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
            new PropertyMetadata(null, OnPopupShadowChanged)
        );

    /// <summary>
    /// Identifies the attached property that enables detached popup shadow rendering with the Wpf.Ui default chrome.
    /// </summary>
    /// <remarks>
    /// If <see cref="PopupShadowChromeProperty"/> is set, that explicit chrome is used. Otherwise setting this
    /// property to <see langword="true"/> uses <see cref="PopupShadowChrome.Default"/>.
    /// </remarks>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PopupShadow),
            new PropertyMetadata(false, OnPopupShadowChanged)
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

    /// <summary>
    /// Helper for getting <see cref="IsEnabledProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="IsEnabledProperty"/> from.
    /// </param>
    /// <returns><see langword="true"/> if the Wpf.Ui default detached popup shadow is enabled.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    [AttachedPropertyBrowsableForType(typeof(ContextMenu))]
    [AttachedPropertyBrowsableForType(typeof(ToolTip))]
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
    /// <param name="value">Whether the Wpf.Ui default detached popup shadow is enabled.</param>
    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
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
                      + "and disposed when the effective PopupShadowChrome is cleared.")]
    private static void OnPopupShadowChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup and not ContextMenu and not ToolTip)
        {
            // The implementation depends on popup-style HWND behavior; other elements intentionally opt out.
            return;
        }

        var controller = GetShadowController(d);
        var chrome = GetEffectivePopupShadowChrome(d);
        if (chrome == null)
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

    private static PopupShadowChrome? GetEffectivePopupShadowChrome(DependencyObject element)
    {
        return GetPopupShadowChrome(element) ?? (GetIsEnabled(element) ? PopupShadowChrome.Default : null);
    }

    /// <summary>
    /// Owns the detached shadow window and synchronizes it with one popup-style owner.
    /// </summary>
    /// <remarks>
    /// The owner is a <see cref="Popup"/>, <see cref="ContextMenu"/>, or <see cref="ToolTip"/>. The controller borrows the owner's
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
        private const int WS_EX_TOPMOST     = 0x00000008;
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
        private Border?            _sourceBorder;
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
        private bool               _isSourceBorderCornerRadiusChangedSubscribed;
        private bool               _isRenderingSubscribed;
        private bool               _isUpdatingShadow;
        // Popup HWND movement can generate several window messages for a single open/close transition. Keep only one
        // deferred bounds refresh so shadow positioning cannot monopolize the dispatcher ahead of input processing.
        private bool               _isShadowBoundsUpdatePending;
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

        private void SetSourceBorder(Border? sourceBorder)
        {
            if (_sourceBorder == sourceBorder)
            {
                return;
            }

            UnsubscribeSourceBorderCornerRadiusChanged();
            _sourceBorder = sourceBorder;
            SubscribeSourceBorderCornerRadiusChanged();
        }

        private void SubscribeSourceBorderCornerRadiusChanged()
        {
            if (_sourceBorder == null)
            {
                return;
            }

            DependencyPropertyDescriptor
               .FromProperty(Border.CornerRadiusProperty, typeof(Border))
              ?.AddValueChanged(_sourceBorder, OnSourceBorderCornerRadiusChanged);

            _isSourceBorderCornerRadiusChangedSubscribed = true;
        }

        private void UnsubscribeSourceBorderCornerRadiusChanged()
        {
            if (!_isSourceBorderCornerRadiusChangedSubscribed || _sourceBorder == null)
            {
                return;
            }

            DependencyPropertyDescriptor
               .FromProperty(Border.CornerRadiusProperty, typeof(Border))
              ?.RemoveValueChanged(_sourceBorder, OnSourceBorderCornerRadiusChanged);

            _isSourceBorderCornerRadiusChangedSubscribed = false;
        }

        private bool IsOwnerOpen =>
            _owner switch
            {
                Popup popup             => popup.IsOpen,
                ContextMenu contextMenu => contextMenu.IsOpen,
                ToolTip toolTip         => toolTip.IsOpen,
                _                       => false,
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

                    DependencyPropertyDescriptor
                       .FromProperty(Popup.HasDropShadowProperty, typeof(Popup))
                      ?.AddValueChanged(popup, OnOwnerHasDropShadowChanged);

                    break;
                }

                case ContextMenu contextMenu:
                {
                    contextMenu.Opened += OnOwnerOpened;
                    contextMenu.Closed += OnOwnerClosed;

                    DependencyPropertyDescriptor
                       .FromProperty(ContextMenu.IsOpenProperty, typeof(ContextMenu))
                      ?.AddValueChanged(contextMenu, OnOwnerIsOpenChanged);

                    DependencyPropertyDescriptor
                       .FromProperty(ContextMenu.HasDropShadowProperty, typeof(ContextMenu))
                      ?.AddValueChanged(contextMenu, OnOwnerHasDropShadowChanged);

                    break;
                }

                case ToolTip toolTip:
                {
                    toolTip.Opened += OnOwnerOpened;
                    toolTip.Closed += OnOwnerClosed;

                    DependencyPropertyDescriptor
                       .FromProperty(ToolTip.IsOpenProperty, typeof(ToolTip))
                      ?.AddValueChanged(toolTip, OnOwnerIsOpenChanged);

                    DependencyPropertyDescriptor
                       .FromProperty(ToolTip.HasDropShadowProperty, typeof(ToolTip))
                      ?.AddValueChanged(toolTip, OnOwnerHasDropShadowChanged);

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

                    DependencyPropertyDescriptor
                       .FromProperty(Popup.HasDropShadowProperty, typeof(Popup))
                      ?.RemoveValueChanged(popup, OnOwnerHasDropShadowChanged);

                    break;
                }

                case ContextMenu contextMenu:
                {
                    contextMenu.Opened -= OnOwnerOpened;
                    contextMenu.Closed -= OnOwnerClosed;

                    DependencyPropertyDescriptor
                       .FromProperty(ContextMenu.IsOpenProperty, typeof(ContextMenu))
                      ?.RemoveValueChanged(contextMenu, OnOwnerIsOpenChanged);

                    DependencyPropertyDescriptor
                       .FromProperty(ContextMenu.HasDropShadowProperty, typeof(ContextMenu))
                      ?.RemoveValueChanged(contextMenu, OnOwnerHasDropShadowChanged);

                    break;
                }

                case ToolTip toolTip:
                {
                    toolTip.Opened -= OnOwnerOpened;
                    toolTip.Closed -= OnOwnerClosed;

                    DependencyPropertyDescriptor
                       .FromProperty(ToolTip.IsOpenProperty, typeof(ToolTip))
                      ?.RemoveValueChanged(toolTip, OnOwnerIsOpenChanged);

                    DependencyPropertyDescriptor
                       .FromProperty(ToolTip.HasDropShadowProperty, typeof(ToolTip))
                      ?.RemoveValueChanged(toolTip, OnOwnerHasDropShadowChanged);

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

        private void OnSourceBorderCornerRadiusChanged(object? sender, EventArgs e)
        {
            if (_shadowSource == null)
            {
                return;
            }

            ApplySourceBorderCornerRadius();
            UpdateShadowContentBounds(default);
        }

        private void OnOwnerHasDropShadowChanged(object? sender, EventArgs e)
        {
            if (!IsDropShadowAllowed())
            {
                Close();
                return;
            }

            if (IsOwnerOpen)
            {
                _isOpenAnimationPending = true;
                Open();
            }
        }

        private void Open()
        {
            if (_chrome == null || !IsOwnerOpen)
            {
                return;
            }

            if (!IsDropShadowAllowed())
            {
                Close();
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
            UpdateShadowBounds(updateZOrder: true);
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
            SetSourceBorder(null);
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

        private bool IsDropShadowAllowed()
        {
            return _owner switch
            {
                // ContextMenu exposes HasDropShadow as part of its public visual contract. Honor the actual value,
                // including values transferred to the popup that WPF creates internally for the menu.
                ContextMenu contextMenu => contextMenu.HasDropShadow,
                ToolTip toolTip         => toolTip.HasDropShadow,

                // Wpf.Ui templates opt into the detached shadow by setting PopupShadowChrome, while most Popup
                // instances leave HasDropShadow at WPF's default false. Treat the default as "not specified" for
                // compatibility, but honor explicit false values from local values, styles, templates, or bindings.
                Popup popup => popup.HasDropShadow || IsDefaultHasDropShadowValue(popup),
                _           => true,
            };
        }

        private static bool IsDefaultHasDropShadowValue(Popup popup)
        {
            var valueSource = DependencyPropertyHelper.GetValueSource(popup, Popup.HasDropShadowProperty);

            return valueSource is
            {
                BaseValueSource: BaseValueSource.Default,
                IsAnimated: false,
                IsCoerced: false,
                IsExpression: false
            };
        }

        private HwndSource? GetOwnerHwndSource()
        {
            // Popup itself is not in the visual tree after opening; its Child is. ContextMenu, by contrast, is the
            // root visual of its popup source. Resolve each owner through the visual that WPF actually presents.
            return _owner switch
            {
                Popup { Child: Visual child } => PresentationSource.FromVisual(child) as HwndSource,
                ContextMenu contextMenu       => PresentationSource.FromVisual(contextMenu) as HwndSource,
                ToolTip toolTip               => PresentationSource.FromVisual(toolTip) as HwndSource,
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
                ContextMenu contextMenu       => contextMenu,
                ToolTip toolTip               => toolTip,
                _                             => null,
            };
        }

        private void EnsureShadowSource()
        {
            if (_shadowSource != null)
            {
                return;
            }

            // Keep the companion HWND in the same z-order band as WPF popup HWNDs. WPF creates normal popups with
            // WS_EX_TOPMOST; without that style, Windows keeps the shadow behind every topmost popup in a cascade.
            const int exStyle = WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;

            var parameters = new HwndSourceParameters("Wpf.Ui.PopupShadow")
            {
                Width                      = 1,
                Height                     = 1,
                WindowStyle                = WS_POPUP,
                ExtendedWindowStyle        = exStyle,

                // WPF popup HWNDs are owned by their placement root or by the previous popup in a cascade. The shadow must
                // share that owner instead of being owned by the popup itself; otherwise Win32's owned-window z-order rules
                // force the shadow above its popup. Sharing the owner lets SetWindowPos place it as the popup's sibling.
                ParentWindow               = GetShadowOwnerWindow(),

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

        private IntPtr GetShadowOwnerWindow()
        {
            // WPF popups form an owner chain: each new popup is owned by the previous popup's HWND (or by the
            // placement root for the first popup). If the shadow window has no owner, Windows treats it as an
            // independent topmost window — when a new popup level opens and refreshes the cascade, the window
            // manager can reorder all topmost windows by owner chain and push earlier shadows behind unrelated
            // popups, visibly detaching them from their menus.
            //
            // By setting the shadow's owner to the same HWND that owns the popup (GW_OWNER), the shadow becomes a
            // sibling of its popup under their shared owner. On the initial open, SetWindowPos places the shadow
            // directly after the popup in z-order. Subsequent bounds updates use SWP_NOZORDER to keep that relative
            // position, so a later cascade normalization can never peel the shadow away from its popup.

            var popupHwnd = _popupSource?.Handle ?? IntPtr.Zero;
            if (popupHwnd == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return PInvoke.GetWindow(new HWND(popupHwnd), GET_WINDOW_CMD.GW_OWNER);
        }

        private void ApplyChrome()
        {
            if (_chrome == null || _shadowBorder == null)
            {
                return;
            }

            SetSourceBorder(GetOwnerRootVisual().FindDescendant<Border>(includeSelf: true));
            ApplySourceBorderCornerRadius();

            var chromeColorBrush = new SolidColorBrush(_chrome.Color);
            chromeColorBrush.Freeze();

            // Tint the mirrored border with the chrome color at the configured opacity so the
            // border and background share the same hue. This prevents a faint background-color
            // leak through the anti-aliased edges of the rounded shadow clip.
            var opacityBorderBrush = new SolidColorBrush(_chrome.Color)
            {
                Opacity = _chrome.Opacity
            };
            opacityBorderBrush.Freeze();

            // Fill the mirrored Border with the chrome shadow color so that, after the Border is clipped out
            // of the shadow surface, anti-aliased corner pixels blend with the shadow hue instead of the
            // default transparent background. Without this fill, rounded corners appear to have jagged
            // colored artifacts.
            _shadowBorder.Background      = chromeColorBrush;
            _shadowBorder.BorderBrush     = opacityBorderBrush;
            _shadowBorder.BorderThickness = _sourceBorder?.BorderThickness ?? default;
            _shadowBorder.Effect          = _chrome.CreateEffect();
        }

        private void ApplySourceBorderCornerRadius()
        {
            if (_shadowBorder == null)
            {
                return;
            }

            if (_sourceBorder != null)
            {
                // Mirror the popup corner radius so the shadow clip matches the visible chrome.
                _shadowBorder.CornerRadius = _sourceBorder.CornerRadius;
                return;
            }

            // Some popups do not use a Border as their template root. Fall back to square chrome rather than
            // carrying stale radius from a previous owner/template.
            _shadowBorder.CornerRadius = default;
        }

        private void UpdateShadowBounds()
        {
            // All updates after Open() are bounds-only. Re-inserting the shadow into the z-order on every
            // move, resize, or DPI change would trigger cascade normalization and can separate earlier shadows
            // from their popups in a multi-level menu.
            UpdateShadowBounds(updateZOrder: false);
        }

        private void QueueShadowBoundsUpdate()
        {
            if (_isShadowBoundsUpdatePending || _isDisposed)
            {
                return;
            }

            _isShadowBoundsUpdatePending = true;

            // Do not use Send here. Fast ComboBox/menu churn can produce a burst of WM_WINDOWPOSCHANGED messages, and
            // Send-priority shadow work can make the UI feel frozen while WPF is still closing/reopening popup HWNDs.
            _ = _dispatcher.BeginInvoke(
                (Action)(() =>
                {
                    _isShadowBoundsUpdatePending = false;

                    if (!_isDisposed)
                    {
                        UpdateShadowBounds();
                    }
                }),
                DispatcherPriority.Background
            );
        }

        private void UpdateShadowBounds(bool updateZOrder)
        {
            // SetWindowPos triggers WM_WINDOWPOSCHANGED, which PopupWndProc picks up and re-dispatches
            // right back to UpdateShadowBounds. The _isUpdatingShadow guard breaks that feedback loop so a
            // single bounds refresh does not chain into itself.
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

            // SWP_NOOWNERZORDER prevents Windows from adjusting z-order based on the owner chain. Without it,
            // even a bounds-only SetWindowPos call can cause the window manager to re-sort owned windows and
            // push an earlier shadow behind a later popup in a cascade.
            var flags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER;

            if (PInvoke.IsWindowVisible(popupHwnd))
            {
                flags |= SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;
            }
            else
            {
                flags |= SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW;
            }

            if (!updateZOrder)
            {
                // Only the initial open declares the companion's z-order. Popup movement, resizing, visibility, and
                // DPI changes must refresh bounds without re-inserting the shadow into the cascade, otherwise Windows
                // may normalize the popup owner tree and separate earlier shadows from their popups.
                flags |= SET_WINDOW_POS_FLAGS.SWP_NOZORDER;
            }

            _isUpdatingShadow = true;

            try
            {
                // Initial creation uses the popup HWND as insert-after so the shadow starts directly beneath the popup.
                // Later bounds refreshes keep the existing z-order untouched to avoid disturbing a popup cascade.
                _ = PInvoke.SetWindowPos(
                    (HWND)_shadowSource.Handle,
                    updateZOrder ? popupHwnd : HWND.Null,
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
            // Start hidden every time. Each opening strategy decides when the shadow becomes visible, while content
            // clipping is transient animation state and must be re-established for this open cycle.
            _shadowRoot.Opacity = 0D;
            UpdateShadowContentBounds(default);

            if (PopupSlideAnimator.IsAnimationActive(_owner))
            {
                // Check the lightweight path first so explicitly opted-in popups do not initialize
                // PopupAnimator's legacy static metadata hooks merely because a shadow is present.
                //
                // PopupSlideAnimator replaces WPF's native 150ms linear Y clock with the configured Wpf.Ui clock,
                // but keeps it on PopupRoot's native animation layer. Sample that real transform just like ordinary
                // Slide while using the replacement duration so the detached shadow does not finish 17ms early.
                var duration = PopupSlideAnimator.GetAnimationDuration(_owner);
                if (!PopupSlideAnimator.HasAnimatedContentBounds(_owner))
                {
                    // The behavior is active but its clock commits the final transform immediately. Treat this like
                    // a non-animated popup; subscribing to Rendering would only rebuild an unchanged clip each frame.
                    BeginDeferredFadeIn(TimeSpan.Zero, ShadowFadeInDuration);
                }
                else if (_chrome.AnimationMode == PopupShadowAnimationMode.DynamicContentBounds)
                {
                    BeginDynamicContentBoundsAnimation(duration);
                }
                else
                {
                    BeginDeferredFadeIn(duration.TimeSpan);
                }

                return;
            }

            if (PopupAnimator.IsCustomAnimationActive(_owner))
            {
                // The content is driven by Wpf.Ui's custom opening timeline, so the detached shadow follows the
                // same duration and reveal offset instead of treating the popup as a native WPF animation.
                var duration = PopupAnimator.GetAnimationDuration(_owner);
                if (!PopupAnimator.HasAnimatedContentBounds(_owner))
                {
                    // A custom animation with no effective translation has a stable content rectangle. Mirroring its
                    // opacity (when present) needs only a WPF animation clock and no per-frame geometry reconstruction.
                    BeginRootFadeIn(duration);
                }
                else if (_chrome.AnimationMode == PopupShadowAnimationMode.DynamicContentBounds)
                {
                    if (TryBeginParallelDynamicContentBoundsAnimation(_owner, duration))
                    {
                        return;
                    }

                    BeginDynamicContentBoundsAnimation(duration);
                }
                else
                {
                    // In fixed-bounds mode the full shadow would look detached during a slide, so reveal it
                    // after the content animation duration instead.
                    BeginDeferredFadeIn(duration.TimeSpan);
                }

                return;
            }

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
                    // No native or custom content animation exists. Use a small fade so creating the companion
                    // HWND does not produce a one-frame shadow flash.
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
            UpdateShadowContentBounds(GetPopupRootAnimationOffset());
            SubscribeRendering();

            // The animated clip already reveals only the shadow belonging to visible popup content. A concurrent
            // opacity clock adds composition work without providing another useful visual transition.
            _shadowRoot.Opacity = 1D;
        }

        private bool TryBeginParallelDynamicContentBoundsAnimation(DependencyObject owner, Duration duration)
        {
            if (_shadowRoot == null ||
                _popupSource?.RootVisual is not UIElement popupRoot ||
                !PopupAnimator.TryGetAnimationStartOffset(owner, popupRoot, out var startOffset))
            {
                return false;
            }

            _contentBoundsAnimationDuration       = duration;
            _contentBoundsAnimationEasingFunction = PopupAnimator.GetAnimationEasingFunction(owner);
            _contentBoundsAnimationStartOffset    = startOffset;
            _isParallelContentBoundsAnimation     = true;
            _shadowAnimationStartedAt             = DateTime.UtcNow;

            // The custom animation starts by moving the popup root from startOffset back to zero. Apply the matching
            // offset before the first render tick so the shadow never shows around content that is still hidden.
            UpdateShadowContentBounds(new Vector(0D, startOffset));
            SubscribeRendering();

            // The bounds animation itself is the reveal. Keep the root fully opaque instead of running a second,
            // parallel opacity animation on the per-pixel-alpha companion window.
            _shadowRoot.Opacity = 1D;

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
            // Only Fade gets an animated close — both the popup and the shadow fade together and WPF keeps the
            // HWND alive for the duration. Slide and Scroll tear down the popup immediately after the close
            // begins, so the shadow must also disappear at once to avoid an orphaned halo.

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

            // This recreates the same "start offset to zero" timeline used by PopupAnimator instead of
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

            var chromeCornerRadius = DeflateCornerRadius(_sourceBorder?.CornerRadius ?? default, ChromeInset);
            _shadowBorder.CornerRadius = chromeCornerRadius;

            // Clip out the visible popup area, leaving only the outside shadow rendered by DropShadowEffect.
            // This is the key trick: the companion window is larger than the popup, but anything where the real popup
            // would draw content is punched out so the two HWNDs do not double-paint chrome or intercept visuals.
            var clipSize = new Size(_shadowRoot.Width, _shadowRoot.Height);
            _shadowRoot.Clip = CreateShadowOnlyClip(clipSize, chromeBounds, chromeCornerRadius);
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

        private static CornerRadius DeflateCornerRadius(CornerRadius cornerRadius, double inset)
        {
            if (inset <= 0D)
            {
                return cornerRadius;
            }

            return new CornerRadius(
                Math.Max(0D, cornerRadius.TopLeft     - inset),
                Math.Max(0D, cornerRadius.TopRight    - inset),
                Math.Max(0D, cornerRadius.BottomRight - inset),
                Math.Max(0D, cornerRadius.BottomLeft  - inset)
            );
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
            switch ((uint)msg)
            {
                case PInvoke.WM_SHOWWINDOW:
                case PInvoke.WM_WINDOWPOSCHANGED:
                case PInvoke.WM_MOVE:
                case PInvoke.WM_SIZE:
                case PInvoke.WM_DPICHANGED:
                    // These messages can arrive in bursts during fast popup open/close cycles. Coalesce them at a
                    // lower priority so shadow repositioning does not starve input while the HWND state settles.
                    QueueShadowBoundsUpdate();
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
