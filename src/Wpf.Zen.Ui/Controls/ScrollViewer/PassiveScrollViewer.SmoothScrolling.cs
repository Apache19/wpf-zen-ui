// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Wpf.Zen.Ui.Controls;

/// <summary>Contains the frame-driven wheel motion and native-input handoff behavior.</summary>
public partial class PassiveScrollViewer
{
    /// <summary>Identifies the <see cref="IsSmoothScrollingEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsSmoothScrollingEnabledProperty =
        DependencyProperty.RegisterAttached(
            nameof(IsSmoothScrollingEnabled),
            typeof(bool),
            typeof(PassiveScrollViewer),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.None,
                OnSmoothScrollingSettingChanged,
                null,
                true
            )
        );

    /// <summary>
    /// Gets or sets a value indicating whether vertical mouse-wheel input uses continuous pixel motion. The default is
    /// <see langword="true"/>. System client-area animation preferences and native spill mode take precedence.
    /// </summary>
    /// <remarks>
    /// This property does not inherit, is not animated, and does not itself invalidate layout. A template
    /// host forwards its value unless the viewer has its own setting. Changing it cancels current motion.
    /// Item-scrolling and unknown providers keep native behavior; set VirtualizingPanel.ScrollUnit to Pixel
    /// on a virtualizing ItemsControl to enable smooth scrolling without disabling virtualization.
    /// </remarks>
    public bool IsSmoothScrollingEnabled
    {
        get => (bool)GetValue(IsSmoothScrollingEnabledProperty);
        set => SetValue(IsSmoothScrollingEnabledProperty, value);
    }

    /// <summary>Configures smooth wheel scrolling for an element's template-owned viewer.</summary>
    /// <param name="element">The viewer or template owner to configure.</param>
    /// <param name="value">Whether smooth scrolling is enabled.</param>
    public static void SetIsSmoothScrollingEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsSmoothScrollingEnabledProperty, value);
    }

    /// <summary>Gets the smooth-scrolling preference; the default is <see langword="true"/>.</summary>
    /// <param name="element">The viewer or template owner to query.</param>
    /// <returns>The element's smooth-scrolling preference.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static bool GetIsSmoothScrollingEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsSmoothScrollingEnabledProperty);
    }

    /// <summary>
    /// Identifies the <see cref="SmoothScrollDuration"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SmoothScrollDurationProperty =
        DependencyProperty.RegisterAttached(
            nameof(SmoothScrollDuration),
            typeof(TimeSpan),
            typeof(PassiveScrollViewer),
            new FrameworkPropertyMetadata(
                TimeSpan.FromMilliseconds(240),
                FrameworkPropertyMetadataOptions.None,
                OnSmoothScrollingSettingChanged,
                null,
                true
            ),
            static value => value is TimeSpan duration
                   && duration >= TimeSpan.FromMilliseconds(1)
                   && duration <= TimeSpan.FromSeconds(10)
        );

    /// <summary>
    /// Gets or sets the approximate time to cover 99% of an isolated wheel step, defaulting to 240 ms.
    /// Valid values range from 1 ms to 10 seconds; smaller values produce a firmer response.
    /// </summary>
    /// <remarks>
    /// This is a damping response time, not a fixed animation deadline. Additional input preserves
    /// forward velocity and extends the destination. The property does not inherit, is not animated,
    /// and does not itself affect layout; changing it cancels current motion at the displayed offset.
    /// </remarks>
    public TimeSpan SmoothScrollDuration
    {
        get => (TimeSpan)GetValue(SmoothScrollDurationProperty);
        set => SetValue(SmoothScrollDurationProperty, value);
    }

    /// <summary>Configures the wheel response time for an element's template-owned viewer.</summary>
    /// <param name="element">The viewer or template owner to configure.</param>
    /// <param name="value">A response time between 1 ms and 10 seconds.</param>
    public static void SetSmoothScrollDuration(DependencyObject element, TimeSpan value)
    {
        element.SetValue(SmoothScrollDurationProperty, value);
    }

    /// <summary>Gets the wheel response time; the default is 240 ms.</summary>
    /// <param name="element">The viewer or template owner to query.</param>
    /// <returns>The configured damping response time.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static TimeSpan GetSmoothScrollDuration(DependencyObject element)
    {
        return (TimeSpan)element.GetValue(SmoothScrollDurationProperty);
    }

    private SmoothScrollMotion? _smoothMotion;
    private IScrollInfo?        _smoothScrollInfo;
    private long                _lastSmoothScrollTimestamp;
    private double              _lastSmoothScrollOffset;

    /// <summary>Gets a value indicating whether this viewer owns a rendering subscription and wheel motion.</summary>
    internal bool IsSmoothScrolling => _smoothMotion is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="PassiveScrollViewer"/> class with input handoff
    /// and rendering-subscription lifetime management.
    /// </summary>
    public PassiveScrollViewer()
    {
        Unloaded += OnSmoothScrollingUnloaded;

        // Observe the request even if a descendant handles it. Native focus/selection navigation must
        // take ownership before WPF queues MakeVisible, including requests that leave the offset unchanged.
        AddHandler(RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler(OnSmoothScrollBringIntoView), true);
        CommandManager.AddPreviewExecutedHandler(this, OnSmoothScrollCommand);
    }

    /// <summary>
    /// Gets a value indicating whether the current provider accepts continuous vertical offsets in device-independent pixels.
    /// Override for a custom pixel-based IScrollInfo; never return true for a provider that rounds to items.
    /// </summary>
    /// <remarks>
    /// IScrollInfo does not expose its coordinate units. Known providers are checked through public APIs;
    /// unknown providers conservatively retain native wheel behavior. No CanContentScroll value is changed.
    /// </remarks>
    protected virtual bool SupportsSmoothScrolling
    {
        get
        {
            if (ScrollInfo is ScrollContentPresenter)
            {
                return true;
            }

            if (ScrollInfo is VirtualizingWrapPanel vwp)
            {
                return vwp.ActualScrollUnit == ScrollUnit.Pixel;
            }

            if (ScrollInfo is VirtualizingStackPanel vsp)
            {
                return vsp.Orientation == Orientation.Horizontal
                       || (ItemsControl.GetItemsOwner(vsp) is { } owner
                           && VirtualizingPanel.GetScrollUnit(owner) == ScrollUnit.Pixel);
            }

            if (ScrollInfo is StackPanel sp)
            {
                return sp.Orientation == Orientation.Horizontal;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether system preferences permit wheel animation. The default respects Windows client-area
    /// animations. An override can apply an application's own reduced-motion preference as well.
    /// </summary>
    protected virtual bool AreScrollAnimationsEnabled => SystemParameters.ClientAreaAnimation;

    /// <inheritdoc />
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        StopSmoothScrolling();
        base.OnPreviewMouseDown(e);
    }

    /// <inheritdoc />
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        StopSmoothScrolling();
        base.OnPreviewKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnPreviewTouchDown(TouchEventArgs e)
    {
        StopSmoothScrolling();
        base.OnPreviewTouchDown(e);
    }

    private static void OnSmoothScrollingSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PassiveScrollViewer viewer)
        {
            viewer.StopSmoothScrolling();
        }
    }

    /// <summary>
    /// Converts full or fractional wheel detents using the Windows line/page preference. WPF's physical
    /// line increment is 16 DIPs; unlike the native sign-only path, delta magnitude is retained here.
    /// </summary>
    internal static double GetSmoothWheelDistance(int delta, int lines, double viewport)
    {
        return -(double)delta / Mouse.MouseWheelDeltaForOneLine * (lines < 0 ? viewport : lines * 16d);
    }

    private bool CanRunSmoothScrolling
    {
        get =>
            IsSmoothScrollingEnabled && IsLoaded && IsVisible && IsEnabled
            && ScrollSpillMode != ScrollSpillMode.Native && AreScrollAnimationsEnabled
            && ScrollInfo is { CanVerticallyScroll: true } && SupportsSmoothScrolling;
    }

    private bool TrySmoothMouseWheel(MouseWheelEventArgs e)
    {
        if (!CanRunSmoothScrolling)
        {
            return false;
        }

        if (_smoothScrollInfo != ScrollInfo)
        {
            StopSmoothScrolling();
        }

        var distance = GetSmoothWheelDistance(e.Delta, SystemParameters.WheelScrollLines, ViewportHeight);
        if (distance == 0)
        {
            // A zero Windows line count disables wheel movement, including propagation to a parent.
            e.Handled = true;
            return true;
        }

        var motion = _smoothMotion ?? new SmoothScrollMotion(VerticalOffset);
        if (!motion.MoveBy(distance, ScrollableHeight))
        {
            e.Handled = ScrollSpillMode == ScrollSpillMode.Disabled;
            return true;
        }

        if (_smoothMotion is null)
        {
            _smoothMotion              = motion;
            _smoothScrollInfo          = ScrollInfo;
            _lastSmoothScrollOffset    = VerticalOffset;
            _lastSmoothScrollTimestamp = Stopwatch.GetTimestamp();

            CompositionTarget.Rendering += OnSmoothScrollRendering;
        }

        e.Handled = true;
        return true;
    }

    private void OnSmoothScrollRendering(object? sender, EventArgs e)
    {
        var timestamp = Stopwatch.GetTimestamp();
        var elapsed   = TimeSpan.FromSeconds((double)(timestamp - _lastSmoothScrollTimestamp) / Stopwatch.Frequency);

        _lastSmoothScrollTimestamp = timestamp;

        AdvanceSmoothScrolling(elapsed);
    }

    /// <summary>
    /// Applies one frame through the same public provider API used by WPF's wheel handling. MediaContext
    /// runs layout again after Rendering, so provider invalidation is committed before drawing this frame.
    /// No UpdateLayout, animation clocks, transforms or per-frame ScrollViewer command queue are needed.
    /// </summary>
    internal void AdvanceSmoothScrolling(TimeSpan elapsed)
    {
        if (_smoothMotion is not { } motion)
        {
            return;
        }

        if (!CanRunSmoothScrolling || _smoothScrollInfo != ScrollInfo)
        {
            StopSmoothScrolling();
            return;
        }

        var previousPosition = motion.Position;
        var continues = motion.Advance(elapsed, SmoothScrollDuration);

        _lastSmoothScrollOffset = motion.Position;
        _smoothScrollInfo!.SetVerticalOffset(motion.Position);

        // Keep ownership until ScrollChanged acknowledges the final write. The provider may still
        // measure a new extent in this frame; releasing here would let layout anchoring undo the
        // final movement. If rounding produces no ScrollChanged, the next frame releases instead.
        if (!continues && previousPosition == motion.Position)
        {
            StopSmoothScrolling();
        }
    }

    /// <summary>
    /// Distinguishes layout feedback from another scroll writer. External offset changes take ownership;
    /// changing extents clamp the running motion when the provider still acknowledges its requested offset.
    /// </summary>
    private void SynchronizeSmoothScrolling(ScrollChangedEventArgs e)
    {
        if (_smoothMotion is not { } motion)
        {
            return;
        }

        if (_smoothScrollInfo != ScrollInfo || !CanRunSmoothScrolling)
        {
            StopSmoothScrolling();
            return;
        }

        // Pixel virtualizing panels may round layout coordinates. Keep subpixel motion state
        // between frames, but permit one physical pixel of layout rounding in provider feedback.
        var tolerance = UseLayoutRounding ? 1 / VisualTreeHelper.GetDpi(this).DpiScaleY : 0.01;
        if (e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0)
        {
            // ScrollChanged batches programmatic offsets with layout changes. An unrelated offset
            // must take ownership even when the extent changes at the same time. IScrollInfo cannot
            // identify the writer; unexpected anchoring shifts also end motion conservatively so
            // we never pull content back toward a destination from a different coordinate frame.
            if (Math.Abs(e.VerticalOffset - Math.Min(_lastSmoothScrollOffset, ScrollableHeight)) > tolerance)
            {
                StopSmoothScrolling();
                return;
            }

            motion.Reconcile(e.VerticalOffset, ScrollableHeight);
            _lastSmoothScrollOffset = e.VerticalOffset;
        }
        else
        {
            if (e.VerticalChange != 0 && Math.Abs(e.VerticalOffset - _lastSmoothScrollOffset) > tolerance)
            {
                StopSmoothScrolling();
            }
        }

        if (motion.Position == motion.Target && motion.Velocity == 0)
        {
            // Commit a coerced physical request as well as its displayed value, so cancelled
            // motion cannot leave the presenter's old out-of-range request waiting for growth.
            _smoothScrollInfo?.SetVerticalOffset(motion.Position);
            StopSmoothScrolling();
        }
    }

    private void OnSmoothScrollingUnloaded(object sender, RoutedEventArgs e)
    {
        StopSmoothScrolling();
    }

    private void OnSmoothScrollBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        StopSmoothScrolling();
    }

    private void OnSmoothScrollCommand(object sender, ExecutedRoutedEventArgs e)
    {
        StopSmoothScrolling();
    }

    /// <summary>
    /// Releases the static rendering event as soon as motion ends or the viewer leaves the live tree.
    /// No destination is committed on cancellation: the new input owner starts at the displayed offset.
    /// </summary>
    private void StopSmoothScrolling()
    {
        if (_smoothMotion is null)
        {
            return;
        }

        CompositionTarget.Rendering -= OnSmoothScrollRendering;

        _smoothMotion     = null;
        _smoothScrollInfo = null;
    }
}
