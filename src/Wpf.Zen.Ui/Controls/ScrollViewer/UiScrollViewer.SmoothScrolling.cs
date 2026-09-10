// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

// ReSharper disable CompareOfFloatsByEqualityOperator
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Contains frame-driven wheel, keyboard and scrollbar motion together with native-input handoff behavior.
/// </summary>
public partial class UiScrollViewer
{
    /// <summary>
    /// Identifies the <see cref="IsSmoothScrollingEnabled"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSmoothScrollingEnabledProperty
        = DependencyProperty.RegisterAttached(
            nameof(IsSmoothScrollingEnabled),
            typeof(bool),
            typeof(UiScrollViewer),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.None,
                OnSmoothScrollingSettingChanged,
                null,
                true
            )
        );

    /// <summary>
    /// Gets or sets a value indicating whether mouse-wheel input, keyboard scrolling and template
    /// scrollbar line/page buttons use continuous pixel motion. The default is <see langword="true"/>.
    /// System client-area animation preferences take precedence; native spill mode bypasses animation
    /// for the wheel only.
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

    /// <summary>
    /// Configures smooth scrolling for an element's template-owned viewer.
    /// </summary>
    /// <param name="element">The viewer or template owner to configure.</param>
    /// <param name="value">Whether smooth scrolling is enabled.</param>
    public static void SetIsSmoothScrollingEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsSmoothScrollingEnabledProperty, value);
    }

    /// <summary>
    /// Gets the smooth-scrolling preference; the default is <see langword="true"/>.
    /// </summary>
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
    public static readonly DependencyProperty SmoothScrollDurationProperty
        = DependencyProperty.RegisterAttached(
            nameof(SmoothScrollDuration),
            typeof(TimeSpan),
            typeof(UiScrollViewer),
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
    /// Gets or sets the approximate time to cover 99% of an isolated input step, defaulting to 240 ms.
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

    /// <summary>
    /// Configures the smooth-scroll response time for an element's template-owned viewer.
    /// </summary>
    /// <param name="element">The viewer or template owner to configure.</param>
    /// <param name="value">A response time between 1 ms and 10 seconds.</param>
    public static void SetSmoothScrollDuration(DependencyObject element, TimeSpan value)
    {
        element.SetValue(SmoothScrollDurationProperty, value);
    }

    /// <summary>
    /// Gets the smooth-scroll response time; the default is 240 ms.
    /// </summary>
    /// <param name="element">The viewer or template owner to query.</param>
    /// <returns>The configured damping response time.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static TimeSpan GetSmoothScrollDuration(DependencyObject element)
    {
        return (TimeSpan)element.GetValue(SmoothScrollDurationProperty);
    }

    // ScrollViewer's pixel-based built-in providers use its internal _scrollLineDelta constant.
    // Repeating that stable 16-DIP contract avoids reflection on every target framework while keeping
    // intercepted line commands equivalent to the native command that they replace.
    private const double ScrollLineDelta = 16d;

    private SmoothScrollMotion? _smoothMotion;
    private IScrollInfo?        _smoothScrollInfo;
    private Orientation         _smoothScrollOrientation;
    private TimeSpan?           _lastSmoothScrollRenderingTime;
    private double              _lastSmoothScrollOffset;

    /// <summary>Gets a value indicating whether this viewer owns a rendering subscription and smooth motion.</summary>
    internal bool IsSmoothScrolling => _smoothMotion is not null;

    /// <summary>
    /// Initializes a new instance of the <see cref="UiScrollViewer"/> class with input handoff
    /// and rendering-subscription lifetime management.
    /// </summary>
    public UiScrollViewer()
    {
        Unloaded += OnSmoothScrollingUnloaded;

        // Observe the request even if a descendant handles it. Native focus/selection navigation must
        // take ownership before WPF queues MakeVisible, including requests that leave the offset unchanged.
        AddHandler(RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler(OnSmoothScrollBringIntoView), true);
        CommandManager.AddPreviewExecutedHandler(this, OnSmoothScrollCommand);
    }

    /// <summary>
    /// Gets a value indicating whether the current provider accepts continuous offsets in
    /// device-independent pixels along <paramref name="orientation"/>. Override for a custom
    /// pixel-based <see cref="IScrollInfo"/>; never return <see langword="true"/> for an axis that
    /// rounds offsets to items.
    /// </summary>
    /// <remarks>
    /// IScrollInfo does not expose its coordinate units. Known providers are checked through public APIs;
    /// unknown providers conservatively retain native scrolling behavior. No CanContentScroll value is changed.
    /// </remarks>
    /// <param name="orientation">The axis whose offset units are being queried.</param>
    /// <returns>
    /// <see langword="true"/> when fractional offsets are safe on the requested axis; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    protected virtual bool SupportsSmoothScrolling(Orientation orientation)
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
            // A stack panel's cross axis is always measured in pixels. Its stacking axis only
            // accepts fractional offsets when the owning ItemsControl selects pixel scroll units.
            var isCrossAxis = vsp.Orientation != orientation;
            return isCrossAxis
                   || (ItemsControl.GetItemsOwner(vsp) is { } owner
                       && VirtualizingPanel.GetScrollUnit(owner) == ScrollUnit.Pixel);
        }

        if (ScrollInfo is StackPanel sp)
        {
            return sp.Orientation != orientation;
        }

        return false;
    }

    /// <summary>
    /// Gets a value indicating whether system preferences permit scroll animation. The default respects Windows client-area
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
        // Descendant input can be consumed before bubbling reaches this viewer. Cancel its
        // pending motion here without claiming the key; repeated viewer-owned scroll keys keep
        // their destination so press-and-hold input continues to accumulate.
        if (!ReferenceEquals(e.OriginalSource, this)
            || !TryGetKeyboardScrollStep(e, out _, out _))
        {
            StopSmoothScrolling();
        }

        base.OnPreviewKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        // Replace the base ScrollViewer operation only during bubbling. Text editors and nested
        // viewers must have the first opportunity to consume their own navigation keys.
        if (TryGetKeyboardScrollStep(e, out var orientation, out var distance)
            && TryAccumulateSmoothScrolling(orientation, distance, out _))
        {
            e.Handled = true;
            return;
        }

        StopSmoothScrolling();

        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnPreviewTouchDown(TouchEventArgs e)
    {
        StopSmoothScrolling();

        base.OnPreviewTouchDown(e);
    }

    private static void OnSmoothScrollingSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UiScrollViewer viewer)
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

    private bool CanRunSmoothScrolling(Orientation orientation)
    {
        return IsSmoothScrollingEnabled && IsLoaded && IsVisible && IsEnabled
               && AreScrollAnimationsEnabled && ScrollInfo is { } scrollInfo
               && (orientation == Orientation.Vertical
                   ? scrollInfo.CanVerticallyScroll
                   : scrollInfo.CanHorizontallyScroll)
               && SupportsSmoothScrolling(orientation);
    }

    private bool TrySmoothMouseWheel(MouseWheelEventArgs e, Orientation orientation)
    {
        if (!CanRunSmoothScrolling(orientation))
        {
            return false;
        }

        // This is still the ordinary vertical wheel: retain its Windows line/page preference even
        // when redirected horizontally, using the selected viewport for page-sized input.
        var viewport = orientation == Orientation.Horizontal ? ViewportWidth : ViewportHeight;
        var distance = GetSmoothWheelDistance(e.Delta, SystemParameters.WheelScrollLines, viewport);

        if (distance == 0)
        {
            // A zero Windows line count disables wheel movement, including propagation to a parent.
            e.Handled = true;
            return true;
        }

        if (!TryAccumulateSmoothScrolling(orientation, distance, out var moved))
        {
            return false;
        }

        if (!moved)
        {
            e.Handled = ScrollSpillMode == ScrollSpillMode.Disabled;
            return true;
        }

        e.Handled = true;
        return true;
    }

    /// <summary>
    /// Adds an input step to the active axis. Repeated-button ticks extend the pending destination
    /// instead of restarting the response, which keeps press-and-hold scrolling continuous.
    /// </summary>
    private bool TryAccumulateSmoothScrolling(Orientation orientation, double distance, out bool moved)
    {
        moved = false;

        if (!CanRunSmoothScrolling(orientation))
        {
            return false;
        }

        if (_smoothScrollInfo != ScrollInfo
            || (_smoothMotion is not null && _smoothScrollOrientation != orientation))
        {
            StopSmoothScrolling();
        }

        var offset  = orientation == Orientation.Vertical ? VerticalOffset : HorizontalOffset;
        var maximum = orientation == Orientation.Vertical ? ScrollableHeight : ScrollableWidth;

        var motion = _smoothMotion ?? new SmoothScrollMotion(offset);
        moved = motion.MoveBy(distance, maximum);

        if (_smoothMotion is null && moved)
        {
            _smoothMotion                  = motion;
            _smoothScrollInfo              = ScrollInfo;
            _smoothScrollOrientation       = orientation;
            _lastSmoothScrollOffset        = offset;
            _lastSmoothScrollRenderingTime = null;

            CompositionTarget.Rendering += OnSmoothScrollRendering;
        }

        return true;
    }

    private void OnSmoothScrollRendering(object? sender, EventArgs e)
    {
        AdvanceSmoothScrollingTo(((RenderingEventArgs)e).RenderingTime);
    }

    /// <summary>
    /// Advances on WPF's presentation clock, so time spent in layout before the callback does not
    /// perturb frame spacing. The first frame establishes the clock origin; input timestamps belong
    /// to a different clock and must not be subtracted from presentation timestamps.
    /// </summary>
    internal void AdvanceSmoothScrollingTo(TimeSpan renderingTime)
    {
        if (_smoothMotion is null)
        {
            return;
        }

        var previousTime = _lastSmoothScrollRenderingTime;
        if (previousTime is null)
        {
            _lastSmoothScrollRenderingTime = renderingTime;
            return;
        }

        // Multiple callbacks for the same presentation must not write another offset. Ignore an
        // older timestamp as well, without moving the baseline back and counting time twice.
        if (renderingTime <= previousTime.Value)
        {
            return;
        }

        _lastSmoothScrollRenderingTime = renderingTime;
        AdvanceSmoothScrolling(renderingTime - previousTime.Value);
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

        if (!CanRunSmoothScrolling(_smoothScrollOrientation) || _smoothScrollInfo != ScrollInfo)
        {
            StopSmoothScrolling();
            return;
        }

        var previousPosition = motion.Position;
        var continues = motion.Advance(elapsed, SmoothScrollDuration);

        _lastSmoothScrollOffset = motion.Position;
        SetSmoothScrollOffset(motion.Position);

        // Keep ownership until ScrollChanged acknowledges the final write. The provider may still
        // measure a new extent in this frame, so the final request must still participate in range
        // reconciliation. If rounding produces no ScrollChanged, the next frame releases instead.
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

        if (_smoothScrollInfo != ScrollInfo || !CanRunSmoothScrolling(_smoothScrollOrientation))
        {
            StopSmoothScrolling();
            return;
        }

        // Pixel virtualizing panels may round layout coordinates. Keep subpixel motion state
        // between frames, but permit one physical pixel of layout rounding in provider feedback.
        var vertical       = _smoothScrollOrientation == Orientation.Vertical;

        var offset         = vertical ? e.VerticalOffset : e.HorizontalOffset;
        var offsetChange   = vertical ? e.VerticalChange : e.HorizontalChange;
        var extentChange   = vertical ? e.ExtentHeightChange : e.ExtentWidthChange;
        var viewportChange = vertical ? e.ViewportHeightChange : e.ViewportWidthChange;
        var maximum        = vertical ? ScrollableHeight : ScrollableWidth;

        var dpiScale = vertical
            ? VisualTreeHelper.GetDpi(this).DpiScaleY
            : VisualTreeHelper.GetDpi(this).DpiScaleX;

        var tolerance = UseLayoutRounding ? 1 / dpiScale : 0.01;

        if (extentChange != 0 || viewportChange != 0)
        {
            // ScrollChanged batches programmatic offsets with layout changes. An unrelated offset
            // must take ownership even when the extent changes at the same time. IScrollInfo cannot
            // identify the writer; unexpected anchoring shifts also end motion conservatively so
            // we never pull content back toward a destination from a different coordinate frame.
            if (Math.Abs(offset - Math.Min(_lastSmoothScrollOffset, maximum)) > tolerance)
            {
                StopSmoothScrolling();
                return;
            }

            // Feedback within tolerance acknowledges the request; it is not a new motion origin.
            // Feeding rounded offsets back into the integrator would discard fractional progress
            // whenever a virtualizing panel refines its extent. Only removed range changes motion.
            motion.ClampToRange(maximum);
            _lastSmoothScrollOffset = motion.Position;
        }
        else
        {
            if (offsetChange != 0 && Math.Abs(offset - _lastSmoothScrollOffset) > tolerance)
            {
                StopSmoothScrolling();
            }
        }

        if (motion.Position == motion.Target && motion.Velocity == 0)
        {
            // Commit a coerced physical request as well as its displayed value, so cancelled
            // motion cannot leave the presenter's old out-of-range request waiting for growth.
            SetSmoothScrollOffset(motion.Position);
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
        if (TryGetSmoothScrollCommandStep(e, out var orientation, out var distance)
            && TryAccumulateSmoothScrolling(orientation, distance, out _))
        {
            // Handling the preview event prevents ScrollViewer's class command handler from
            // issuing the corresponding immediate IScrollInfo Line*/Page* operation. A Thumb uses
            // ScrollTo*/DeferScrollTo* commands and therefore deliberately remains direct-manipulation.
            e.Handled = true;
            return;
        }

        StopSmoothScrolling();
    }

    /// <summary>
    /// Converts line/page commands raised by this viewer or its template scrollbars. Restricting the
    /// source prevents a nested scrolling surface's preview command from being claimed by an ancestor.
    /// Component page commands are included because WPF registers them as keyboard-facing aliases.
    /// </summary>
    private bool TryGetSmoothScrollCommandStep(
        ExecutedRoutedEventArgs e,
        out Orientation orientation,
        out double distance)
    {
        orientation = Orientation.Vertical;
        distance    = 0;

        if (e.Command == ScrollBar.LineUpCommand)
        {
            orientation = Orientation.Vertical;
            distance    = -ScrollLineDelta;
        }
        else if (e.Command == ScrollBar.LineDownCommand)
        {
            orientation = Orientation.Vertical;
            distance    = ScrollLineDelta;
        }
        else if (e.Command == ScrollBar.PageUpCommand || e.Command == ComponentCommands.ScrollPageUp)
        {
            orientation = Orientation.Vertical;
            distance    = -ViewportHeight;
        }
        else if (e.Command == ScrollBar.PageDownCommand || e.Command == ComponentCommands.ScrollPageDown)
        {
            orientation = Orientation.Vertical;
            distance    = ViewportHeight;
        }
        else if (e.Command == ScrollBar.LineLeftCommand)
        {
            orientation = Orientation.Horizontal;
            distance    = -ScrollLineDelta;
        }
        else if (e.Command == ScrollBar.LineRightCommand)
        {
            orientation = Orientation.Horizontal;
            distance    = ScrollLineDelta;
        }
        else if (e.Command == ScrollBar.PageLeftCommand)
        {
            orientation = Orientation.Horizontal;
            distance    = -ViewportWidth;
        }
        else if (e.Command == ScrollBar.PageRightCommand)
        {
            orientation = Orientation.Horizontal;
            distance    = ViewportWidth;
        }
        else
        {
            return false;
        }

        // Unrelated commands never need a source-tree lookup. Only this viewer and its own
        // template scrollbar visuals are eligible; content elements retain native command routing.
        return ReferenceEquals(e.OriginalSource, this)
               || (e.OriginalSource is DependencyObject source
                   && TryFindOwningTemplateScrollBar(source, out var scrollBar)
                   && scrollBar.Orientation == orientation);
    }

    /// <summary>
    /// Maps the keys for which WPF's ScrollViewer performs scrolling. Arrow keys are intercepted only
    /// when the viewer itself owns keyboard focus; arrows raised by descendants must remain available
    /// to WPF's directional focus-navigation algorithm. Page and edge keys are eligible only after
    /// descendant handlers have left the bubbling event unhandled.
    /// </summary>
    private bool TryGetKeyboardScrollStep(
        KeyEventArgs e,
        out Orientation orientation,
        out double distance)
    {
        orientation = Orientation.Vertical;
        distance    = 0;

        if ((e.KeyboardDevice.Modifiers & ModifierKeys.Alt) != 0
            || TemplatedParent is Control)
        {
            // A template-owning control may implement its own keyboard scrolling through
            // Control.HandlesScrolling. That protected-internal contract is not publicly queryable
            // on another Control instance, so retaining native routing is the safe fallback.
            return false;
        }

        var viewerOwnsKey = ReferenceEquals(e.OriginalSource, this);

        switch (e.Key)
        {
            case Key.Left when viewerOwnsKey:
                orientation = Orientation.Horizontal;
                distance = FlowDirection == FlowDirection.RightToLeft ? ScrollLineDelta : -ScrollLineDelta;
                return true;

            case Key.Right when viewerOwnsKey:
                orientation = Orientation.Horizontal;
                distance = FlowDirection == FlowDirection.RightToLeft ? -ScrollLineDelta : ScrollLineDelta;
                return true;

            case Key.Up when viewerOwnsKey:
                distance = -ScrollLineDelta;
                return true;

            case Key.Down when viewerOwnsKey:
                distance = ScrollLineDelta;
                return true;

            case Key.PageUp:
                distance = -ViewportHeight;
                return true;

            case Key.PageDown:
                distance = ViewportHeight;
                return true;

            case Key.Home:
                if ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0)
                {
                    distance = -ScrollableHeight;
                }
                else
                {
                    orientation = Orientation.Horizontal;
                    distance = -ScrollableWidth;
                }

                return true;

            case Key.End:
                if ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0)
                {
                    distance = ScrollableHeight;
                }
                else
                {
                    orientation = Orientation.Horizontal;
                    distance = ScrollableWidth;
                }

                return true;

            default:
                return false;
        }
    }

    private bool TryFindOwningTemplateScrollBar(
        DependencyObject source,
        out ScrollBar scrollBar)
    {
        var current = source;

        // ContentElement is a valid routed-command source, but is not accepted by GetParent.
        // Such sources cannot be template scrollbar visuals and fall back to native routing.
        while (current is Visual && current != this)
        {
            if (current is ScrollBar candidate)
            {
                scrollBar = candidate;
                return ReferenceEquals(candidate.TemplatedParent, this);
            }

            current = VisualTreeHelper.GetParent(current);
        }

        scrollBar = null!;
        return false;
    }

    private void SetSmoothScrollOffset(double offset)
    {
        if (_smoothScrollOrientation == Orientation.Vertical)
        {
            _smoothScrollInfo?.SetVerticalOffset(offset);
        }
        else
        {
            _smoothScrollInfo?.SetHorizontalOffset(offset);
        }
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
