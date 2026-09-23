// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Windows.Win32;
using Wpf.Zen.Ui.Interop;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Moves its child away from the system touch keyboard while preserving its size whenever possible.
/// </summary>
/// <remarks>
/// <para>
/// Stretch this decorator across the available host and align the child within it. The child is
/// measured against the full host before each avoidance calculation. A ContentPresenter child uses
/// its FrameworkElement content's alignment and size constraints, including Margin. Positive
/// margins reserve keyboard clearance; negative margins retain WPF's overflow semantics.
/// Unobstructed content retains
/// its normal alignment; obstructed content moves the shortest distance above, below, left, or
/// right of the keyboard. When compression is required, slots satisfying fixed/minimum dimensions
/// take priority, followed by retained area. A changed keyboard placement also requests visibility
/// for the focused descendant after layout completes.
/// </para>
/// <para>
/// Child padding or margin supplies clearance from host and keyboard edges. Set
/// <see cref="OcclusionPadding"/> to allow decorative space to overlap the keyboard while retaining
/// the complete child layout slot within the host. Explicit sizes and
/// minimum sizes retain WPF semantics and may cause clipping when the remaining space is too small.
/// Keep modal backdrops outside this decorator so that they continue to cover the complete host.
/// Native monitoring runs only while loaded and visible; no keyboard service means normal layout.
/// </para>
/// </remarks>
public class TouchKeyboardAvoidanceDecorator : Decorator
{
    private readonly Func<HwndSource, Action<Rect>, IDisposable?> _subscribe;

    private IDisposable? _subscription;
    private HwndSource?  _source;
    private Rect _screenBounds        = Rect.Empty;
    private Rect _localBounds         = Rect.Empty;
    private Rect _placement           = Rect.Empty;
    private Rect _lastLoggedPlacement = Rect.Empty;
    private int  _subscriptionVersion;
    private LayoutIntent? _measuredIntent;
    private Rect _arrangedPlacement = Rect.Empty;
    private bool _focusRevealQueued;

    /// <summary>
    /// Identifies the <see cref="OcclusionPadding"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty OcclusionPaddingProperty
        = DependencyProperty.Register(
            nameof(OcclusionPadding),
            typeof(Thickness),
            typeof(TouchKeyboardAvoidanceDecorator),
            new FrameworkPropertyMetadata(
                default(Thickness),
                FrameworkPropertyMetadataOptions.AffectsMeasure
            ),
            IsValidOcclusionPadding
        );

    /// <summary>
    /// Gets or sets the decorative inset of the child's outer layout slot that may overlap the
    /// keyboard. The default is zero; all sides must be finite and nonnegative.
    /// </summary>
    /// <remarks>
    /// Match this inset to presentation padding reserved for shadows or other decoration. It only
    /// changes keyboard collision bounds; measurement and the outer host boundary retain that
    /// padding. When the inset consumes the entire child, no content requires avoidance.
    /// </remarks>
    public Thickness OcclusionPadding
    {
        get => (Thickness)GetValue(OcclusionPaddingProperty);
        set => SetValue(OcclusionPaddingProperty, value);
    }

    /// <summary>
    /// Rejects insets that cannot describe a finite inner collision rectangle.
    /// </summary>
    private static bool IsValidOcclusionPadding(object value)
    {
        var padding = (Thickness)value;

        return padding.Left >= 0 &&
               padding is { Left: < double.PositiveInfinity, Top: >= 0 }
                   and { Top: < double.PositiveInfinity, Right: >= 0 }
                   and { Right: < double.PositiveInfinity, Bottom: >= 0 and < double.PositiveInfinity };
    }

    /// <summary>
    /// Initializes a decorator that observes the system keyboard for its current HWND.
    /// </summary>
    public TouchKeyboardAvoidanceDecorator() : this(SystemInputPaneMonitor.Subscribe)
    {
    }

    /// <summary>
    /// Separates screen-rectangle acquisition from layout so other hosts and tests can supply it.
    /// </summary>
    internal TouchKeyboardAvoidanceDecorator(Func<HwndSource, Action<Rect>, IDisposable?> subscribe)
    {
        _subscribe = subscribe;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;

        IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>
    /// Measures normal content first, then remeasures only if avoiding the keyboard needs less
    /// space.
    /// </summary>
    protected override Size MeasureOverride(Size constraint)
    {
        return MeasurePlacement(constraint);
    }

    /// <summary>
    /// Arranges only the slot established by measurement, leaving render transforms for effects.
    /// Re-measuring at full host size here invalidates descendant scroll/text layout while WPF
    /// is arranging it. Scroll commands and caret positioning must see the reduced measurement.
    /// </summary>
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(_placement.IsEmpty ? new Rect(arrangeSize) : _placement);

        // A keyboard can resize the viewport after the editor already received focus. WPF's
        // original focus request then predates the new viewport. Request visibility once after
        // the changed placement settles, without interfering with subsequent manual scrolling.
        if (_arrangedPlacement != _placement && !_localBounds.IsEmpty)
        {
            QueueFocusedElementReveal();
        }

        _arrangedPlacement = _placement;

        if (_lastLoggedPlacement != _placement)
        {
            if (!_localBounds.IsEmpty || !_lastLoggedPlacement.IsEmpty)
            {
                Debug.WriteLine(
                    $"[TouchKeyboard.Layout] PLACEMENT applied; host={arrangeSize}, "
                    + $"occlusion={_localBounds}, placement={_placement}."
                );
            }

            _lastLoggedPlacement = _placement;
        }

        return arrangeSize;
    }

    /// <summary>
    /// Lets descendant scroll viewers expose the current focus after keyboard-driven layout.
    /// The deferred request never changes focus and is discarded after hiding or unloading.
    /// </summary>
    private void QueueFocusedElementReveal()
    {
        if (_focusRevealQueued)
        {
            return;
        }

        _focusRevealQueued = true;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _focusRevealQueued = false;

            if (IsLoaded && IsVisible && !_localBounds.IsEmpty && Child is { } child
                && Keyboard.FocusedElement is FrameworkElement focused
                && (ReferenceEquals(child, focused) || child.IsAncestorOf(focused)))
            {
                focused.BringIntoView();
            }
        }));
    }

    /// <summary>
    /// Uses the full host as a stable baseline, so keyboard movement and hiding never accumulate
    /// offsets or preserve a previously compressed desired size.
    /// </summary>
    private Size MeasurePlacement(Size constraint)
    {
        _placement = Rect.Empty;
        _measuredIntent = null;

        if (Child is null)
        {
            return default;
        }

        var element = GetLayoutElement(Child);
        _measuredIntent = GetLayoutIntent(element);

        Child.Measure(constraint);

        var desired = Child.DesiredSize;

        var host = new Size(
            double.IsInfinity(constraint.Width) ? desired.Width : constraint.Width,
            double.IsInfinity(constraint.Height) ? desired.Height : constraint.Height
        );

        var preferred = GetPreferredBounds(Child, host);
        var placement = KeyboardAvoidanceLayout.Place(
            new Rect(host), preferred, _localBounds, OcclusionPadding, GetMinimumSize(element)
        );

        _placement = placement;

        if (placement.Width < preferred.Width || placement.Height < preferred.Height)
        {
            Child.Measure(placement.Size);
        }

        // Keep the decorator's own desired size stable. Only the child receives the reduced slot.
        return desired;
    }

    /// <summary>
    /// Predicts the aligned outer child rectangle from its full-host measurement. FrameworkElement
    /// enforces its own explicit/minimum sizes and clipping when arranged in the selected slot.
    /// </summary>
    private static Rect GetPreferredBounds(UIElement child, Size host)
    {
        // A ContentPresenter preserves the host's logical ownership and provides an animation
        // surface. Its content owns layout constraints; its measured size includes content Margin.
        if (GetLayoutElement(child) is not { } element)
        {
            return new Rect(host);
        }

        var margin = element.Margin;
        var width  = child.DesiredSize.Width;
        var height = child.DesiredSize.Height;

        if (element is { HorizontalAlignment: HorizontalAlignment.Stretch, Width: double.NaN })
        {
            width = Math.Max(width, Math.Min(host.Width, element.MaxWidth + margin.Left + margin.Right));
        }

        if (element is { VerticalAlignment: VerticalAlignment.Stretch, Height: double.NaN })
        {
            height = Math.Max(height, Math.Min(host.Height, element.MaxHeight + margin.Top + margin.Bottom));
        }

        width  = Math.Min(host.Width, width);
        height = Math.Min(host.Height, height);

        var x = element.HorizontalAlignment switch
        {
            HorizontalAlignment.Left  => 0,
            HorizontalAlignment.Right => host.Width - width,
            _ => (host.Width - width) / 2,
        };

        var y = element.VerticalAlignment switch
        {
            VerticalAlignment.Top    => 0,
            VerticalAlignment.Bottom => host.Height - height,
            _ => (host.Height - height) / 2,
        };

        return new Rect(x, y, width, height);
    }

    /// <summary>
    /// Reads layout intent from presented content while preserving its logical ownership.
    /// </summary>
    private static FrameworkElement? GetLayoutElement(UIElement? child)
    {
        return child is ContentPresenter { Content: FrameworkElement content }
            ? content
            : child as FrameworkElement;
    }

    /// <summary>
    /// Captures properties that can change while a compressed child's DesiredSize stays constant.
    /// Record equality treats Auto (NaN) as equal, so ordinary layout never invalidates itself.
    /// </summary>
    private static LayoutIntent? GetLayoutIntent(FrameworkElement? element)
    {
        return element is null
            ? null
            : new LayoutIntent(
                element.HorizontalAlignment,
                element.VerticalAlignment,
                element.Width,
                element.Height,
                element.MinWidth,
                element.MinHeight,
                element.MaxWidth,
                element.MaxHeight,
                element.Margin,
                element.LayoutTransform?.Value ?? Matrix.Identity
            );
    }

    /// <summary>
    /// Keeps placement synchronized with runtime bindings even when WPF clips their desired size.
    /// </summary>
    private readonly record struct LayoutIntent(
        HorizontalAlignment Horizontal,
        VerticalAlignment Vertical,
        double Width,
        double Height,
        double MinWidth,
        double MinHeight,
        double MaxWidth,
        double MaxHeight,
        Thickness Margin,
        Matrix Transform
    );

    /// <summary>
    /// Includes fixed dimensions and margins in the smallest slot WPF can display without clipping.
    /// Prefer a usable region over a larger strip narrower than the dialog's minimum width.
    /// </summary>
    private static Size GetMinimumSize(FrameworkElement? element)
    {
        if (element is null)
        {
            return default;
        }

        var width = double.IsNaN(element.Width)
            ? element.MinWidth
            : Math.Max(element.MinWidth, Math.Min(element.Width, element.MaxWidth));

        var height = double.IsNaN(element.Height)
            ? element.MinHeight
            : Math.Max(element.MinHeight, Math.Min(element.Height, element.MaxHeight));

        var bounds = element.LayoutTransform?.TransformBounds(new Rect(0, 0, width, height))
            ?? new Rect(0, 0, width, height);

        var margin = element.Margin;

        return new Size(
            Math.Max(0, bounds.Width + margin.Left + margin.Right),
            Math.Max(0, bounds.Height + margin.Top + margin.Bottom)
        );
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        PresentationSource.AddSourceChangedHandler(this, OnSourceChanged);
        LayoutUpdated += OnLayoutUpdated;
        UpdateSubscription();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        PresentationSource.RemoveSourceChangedHandler(this, OnSourceChanged);
        LayoutUpdated -= OnLayoutUpdated;
        StopSubscription();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        UpdateSubscription();
    }

    private void OnSourceChanged(object sender, SourceChangedEventArgs args)
    {
        UpdateSubscription();
    }

    /// <summary>
    /// Rebinds when the presentation moves between HWNDs, including embedded HwndSource hosts.
    /// Versioning prevents a delayed callback from an old subscription restoring stale occlusion.
    /// </summary>
    private void UpdateSubscription()
    {
        var source = IsLoaded && IsVisible
            ? PresentationSource.FromVisual(this) as HwndSource
            : null;

        if (source == _source)
        {
            return;
        }

        StopSubscription();

        _source = source;

        if (source is not null)
        {
            source.AddHook(OnWindowMessage);

            var version = _subscriptionVersion;

            _subscription = _subscribe(
                source,
                bounds =>
                {
                    if (version == _subscriptionVersion)
                    {
                        _screenBounds = bounds;
                        UpdateLocalBounds();
                    }
                }
            );
        }
    }

    private void StopSubscription()
    {
        _subscriptionVersion++;
        _subscription?.Dispose();
        _subscription = null;

        _source?.RemoveHook(OnWindowMessage);
        _source = null;

        _screenBounds = Rect.Empty;

        UpdateLocalBounds();
    }

    private void OnLayoutUpdated(object? sender, EventArgs args)
    {
        UpdateLocalBounds();

        // Alignment can invalidate only arrange; constraints can leave clipped DesiredSize
        // unchanged. Neither case necessarily propagates measurement to this decorator.
        if (GetLayoutIntent(GetLayoutElement(Child)) != _measuredIntent)
        {
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Reprojects stationary keyboard bounds when the window moves or changes DPI. LayoutUpdated
    /// additionally handles movement of this decorator within the same HWND.
    /// </summary>
    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message is (int)PInvoke.WM_WINDOWPOSCHANGED or (int)PInvoke.WM_DPICHANGED)
        {
            InvalidateMeasure();
            Dispatcher.BeginInvoke(new Action(UpdateLocalBounds));
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Converts screen points separately to local DIPs, including monitor DPI and window offset.
    /// Comparing points in one coordinate space avoids treating a screen vector as a position.
    /// </summary>
    private void UpdateLocalBounds()
    {
        var bounds = Rect.Empty;

        if (!_screenBounds.IsEmpty && PresentationSource.FromVisual(this) is not null)
        {
            var topLeft     = PointFromScreen(_screenBounds.TopLeft);
            var topRight    = PointFromScreen(_screenBounds.TopRight);
            var bottomLeft  = PointFromScreen(_screenBounds.BottomLeft);
            var bottomRight = PointFromScreen(_screenBounds.BottomRight);

            bounds = new Rect(topLeft, bottomRight);
            bounds.Union(topRight);
            bounds.Union(bottomLeft);
        }

        if (_localBounds != bounds)
        {
            _localBounds = bounds;
            InvalidateMeasure();
        }
    }
}
