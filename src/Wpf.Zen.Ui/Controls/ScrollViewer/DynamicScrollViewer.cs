// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides a scroll viewer that tracks scrolling and pointer interaction,
/// allowing its template to present scroll bars only while they are active.
/// </summary>
[DefaultEvent(nameof(ScrollChanged))]
[TemplatePart(Name = PartVerticalScrollBar, Type = typeof(ScrollBar))]
[TemplatePart(Name = PartHorizontalScrollBar, Type = typeof(ScrollBar))]
public sealed class DynamicScrollViewer : PassiveScrollViewer
{
    private const string PartVerticalScrollBar = "PART_VerticalScrollBar";
    private const string PartHorizontalScrollBar = "PART_HorizontalScrollBar";

    #region Read-only DP: IsScrollingVertically

    private static readonly DependencyPropertyKey IsScrollingVerticallyPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(IsScrollingVertically),
            typeof(bool),
            typeof(DynamicScrollViewer),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="IsScrollingVertically"/> dependency property.</summary>
    public static readonly DependencyProperty IsScrollingVerticallyProperty
        = IsScrollingVerticallyPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether vertical scrolling occurred during the current activity period.
    /// </summary>
    public bool IsScrollingVertically => (bool)GetValue(IsScrollingVerticallyProperty);

    #endregion

    #region Read-only DP: IsScrollingHorizontally

    private static readonly DependencyPropertyKey IsScrollingHorizontallyPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(IsScrollingHorizontally),
            typeof(bool),
            typeof(DynamicScrollViewer),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="IsScrollingHorizontally"/> dependency property.</summary>
    public static readonly DependencyProperty IsScrollingHorizontallyProperty
        = IsScrollingHorizontallyPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether horizontal scrolling occurred during the current activity period.
    /// </summary>
    public bool IsScrollingHorizontally => (bool)GetValue(IsScrollingHorizontallyProperty);

    #endregion

    #region Read-only Attached DP: IsVerticalScrollBarActive

    private static readonly DependencyPropertyKey IsVerticalScrollBarActivePropertyKey
        = DependencyProperty.RegisterAttachedReadOnly(
            "IsVerticalScrollBarActive",
            typeof(bool),
            typeof(DynamicScrollViewer),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsVerticalScrollBarActiveProperty"/> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty IsVerticalScrollBarActiveProperty
        = IsVerticalScrollBarActivePropertyKey.DependencyProperty;

    /// <summary>
    /// Helper for getting <see cref="IsVerticalScrollBarActiveProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <remarks>
    /// This includes scrolling, pointer presence within the viewer, mouse capture, and the configured hide delay.
    /// </remarks>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="IsVerticalScrollBarActiveProperty"/> from.
    /// </param>
    /// <returns>IsVerticalScrollBarActive property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(ScrollBar))]
    public static bool GetIsVerticalScrollBarActive(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (bool)element.GetValue(IsVerticalScrollBarActiveProperty);
    }

    #endregion

    #region Read-only Attached DP: IsHorizontalScrollBarActive

    private static readonly DependencyPropertyKey IsHorizontalScrollBarActivePropertyKey
        = DependencyProperty.RegisterAttachedReadOnly(
            "IsHorizontalScrollBarActive",
            typeof(bool),
            typeof(DynamicScrollViewer),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsHorizontalScrollBarActiveProperty"/> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty IsHorizontalScrollBarActiveProperty
        = IsHorizontalScrollBarActivePropertyKey.DependencyProperty;

    /// <summary>
    /// Helper for getting <see cref="IsHorizontalScrollBarActiveProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <remarks>
    /// This includes scrolling, pointer presence within the viewer, mouse capture, and the configured hide delay.
    /// </remarks>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="IsHorizontalScrollBarActiveProperty"/> from.
    /// </param>
    /// <returns>IsHorizontalScrollBarActive property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(ScrollBar))]
    public static bool GetIsHorizontalScrollBarActive(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (bool)element.GetValue(IsHorizontalScrollBarActiveProperty);
    }

    #endregion

    #region Read-only Attached DP: IsManagedScrollBar

    private static readonly DependencyPropertyKey IsManagedScrollBarPropertyKey
        = DependencyProperty.RegisterAttachedReadOnly(
            "IsManagedScrollBar",
            typeof(bool),
            typeof(DynamicScrollViewer),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the read-only attached property that marks a scroll bar whose
    /// presentation is managed by a <see cref="DynamicScrollViewer"/>.
    /// </summary>
    public static readonly DependencyProperty IsManagedScrollBarProperty
        = IsManagedScrollBarPropertyKey.DependencyProperty;

    /// <summary>
    /// Helper for getting <see cref="IsManagedScrollBarProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="IsManagedScrollBarProperty"/> from.
    /// </param>
    /// <returns>IsManagedScrollBar property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static bool GetIsManagedScrollBar(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (bool)element.GetValue(IsManagedScrollBarProperty);
    }

    #endregion

    #region DP: MinimalChange

    /// <summary>Identifies the <see cref="MinimalChange"/> dependency property.</summary>
    public static readonly DependencyProperty MinimalChangeProperty = DependencyProperty.Register(
        nameof(MinimalChange),
        typeof(double),
        typeof(DynamicScrollViewer),
        new FrameworkPropertyMetadata(
            40d,
            static (d, e) => ((DynamicScrollViewer)d)._minimalChange = (double)e.NewValue
        ),
        static value => value is double number and >= 0d
                     && !double.IsNaN(number)
                     && !double.IsInfinity(number)
    );

    /// <summary>
    /// Gets or sets the minimum physical offset change required to count as scrolling activity.
    /// </summary>
    /// <remarks>
    /// This threshold applies to pixel-based scrolling. When <see cref="ScrollViewer.CanContentScroll"/>
    /// is enabled, offsets may be expressed in logical item units, so every non-zero change is treated
    /// as scrolling activity.
    /// </remarks>
    public double MinimalChange
    {
        get => (double)GetValue(MinimalChangeProperty);
        set => SetValue(MinimalChangeProperty, value);
    }

    #endregion

    #region DP: Timeout

    /// <summary>Identifies the <see cref="Timeout"/> dependency property.</summary>
    public static readonly DependencyProperty TimeoutProperty = DependencyProperty.Register(
        nameof(Timeout),
        typeof(int),
        typeof(DynamicScrollViewer),
        new FrameworkPropertyMetadata(
            1200,
            static (d, e) => ((DynamicScrollViewer)d).OnTimeoutChanged((int)e.NewValue)
        ),
        static value => value is >= 0
    );

    /// <summary>
    /// Gets or sets the delay, in milliseconds, after which an inactive scroll bar is hidden.
    /// </summary>
    public int Timeout
    {
        get => (int)GetValue(TimeoutProperty);
        set => SetValue(TimeoutProperty, value);
    }

    #endregion

    private readonly AxisState _verticalState;
    private readonly AxisState _horizontalState;

    // ScrollChanged fires frequently, so cache the values used in its hot path.
    private double _minimalChange
        = (double)MinimalChangeProperty.DefaultMetadata.DefaultValue;

    private int _timeout
        = (int)TimeoutProperty.DefaultMetadata.DefaultValue;

    static DynamicScrollViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DynamicScrollViewer),
            new FrameworkPropertyMetadata(typeof(DynamicScrollViewer))
        );
    }

    public DynamicScrollViewer()
    {
        _verticalState = new AxisState(
            IsScrollingVerticallyPropertyKey,
            IsVerticalScrollBarActivePropertyKey
        );

        _horizontalState = new AxisState(
            IsScrollingHorizontallyPropertyKey,
            IsHorizontalScrollBarActivePropertyKey
        );

        _verticalState.Timer.Tick += OnStateTimerTick;
        _horizontalState.Timer.Tick += OnStateTimerTick;

        UpdateTimerIntervals();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        DetachScrollBar(_verticalState);
        DetachScrollBar(_horizontalState);

        base.OnApplyTemplate();

        AttachScrollBar(
            _verticalState,
            GetTemplateChild(PartVerticalScrollBar) as ScrollBar
        );

        AttachScrollBar(
            _horizontalState,
            GetTemplateChild(PartHorizontalScrollBar) as ScrollBar
        );

        SynchronizeActiveState(_verticalState);
        SynchronizeActiveState(_horizontalState);
    }

    /// <inheritdoc />
    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        base.OnScrollChanged(e);

        if (IsScrollActivity(e.HorizontalChange))
        {
            BeginScrollActivity(_horizontalState);
        }

        if (IsScrollActivity(e.VerticalChange))
        {
            BeginScrollActivity(_verticalState);
        }
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);

        // WinUI-style dynamic scroll bars are revealed when the pointer enters the scrolling
        // surface, not only after it reaches the much narrower scroll-bar hit target. Both axes
        // become active together so the template responds consistently for diagonal overflow.
        BeginPointerInteraction(_verticalState);
        BeginPointerInteraction(_horizontalState);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        EndPointerInteraction(_verticalState);
        EndPointerInteraction(_horizontalState);
    }

    private bool IsScrollActivity(double offsetChange)
    {
        // ScrollChangedEventArgs uses the coordinate system of the active IScrollInfo. Physical scrolling
        // reports pixels, while logical scrolling commonly reports only a few item units per wheel detent.
        return CanContentScroll
            ? offsetChange != 0d
            : Math.Abs(offsetChange) > _minimalChange;
    }

    private void BeginScrollActivity(AxisState state)
    {
        SetState(this, state.ScrollingPropertyKey, true);

        if (state.ScrollBar is not null)
        {
            SetState(state.ScrollBar, state.ActivePropertyKey, true);
        }

        RestartTimer(state.Timer);
    }

    private void OnStateTimerTick(object? sender, EventArgs e)
    {
        var state = GetState(sender);

        if (state is null)
        {
            return;
        }

        state.Timer.Stop();
        SetState(this, state.ScrollingPropertyKey, false);

        if (!IsInteractingWithViewer(state) && state.ScrollBar is not null)
        {
            SetState(state.ScrollBar, state.ActivePropertyKey, false);
        }
    }

    private void BeginPointerInteraction(AxisState state)
    {
        // When the timer represents only a pending hide, pointer interaction cancels it. A timer that
        // still represents scrolling must continue so IsScrolling* can return to false on schedule.
        if (!GetState(this, state.ScrollingPropertyKey))
        {
            state.Timer.Stop();
        }

        if (state.ScrollBar is not null)
        {
            SetState(state.ScrollBar, state.ActivePropertyKey, true);
        }
    }

    private void EndPointerInteraction(AxisState state)
    {
        if (GetState(this, state.ScrollingPropertyKey) || IsInteractingWithViewer(state))
        {
            if (state.ScrollBar is not null)
            {
                SetState(state.ScrollBar, state.ActivePropertyKey, true);
            }

            return;
        }

        RestartTimer(state.Timer);
    }

    private void OnScrollBarCaptureStarted(object sender, MouseEventArgs e)
    {
        if (GetState(sender) is { } state)
        {
            BeginPointerInteraction(state);
        }
    }

    private void OnScrollBarCaptureEnded(object sender, MouseEventArgs e)
    {
        if (GetState(sender) is { } state)
        {
            EndPointerInteraction(state);
        }
    }

    private void AttachScrollBar(AxisState state, ScrollBar? scrollBar)
    {
        state.ScrollBar = scrollBar;

        if (scrollBar is null)
        {
            return;
        }

        scrollBar.SetValue(IsManagedScrollBarPropertyKey, true);

        // Hover is tracked by the containing viewer. Capture remains attached to the individual
        // scroll bar because a thumb can retain capture after the pointer leaves the viewer bounds.
        scrollBar.GotMouseCapture += OnScrollBarCaptureStarted;
        scrollBar.LostMouseCapture += OnScrollBarCaptureEnded;
    }

    private void DetachScrollBar(AxisState state)
    {
        if (state.ScrollBar is not { } scrollBar)
        {
            return;
        }

        scrollBar.GotMouseCapture -= OnScrollBarCaptureStarted;
        scrollBar.LostMouseCapture -= OnScrollBarCaptureEnded;

        scrollBar.ClearValue(IsManagedScrollBarPropertyKey);

        state.ScrollBar = null;
    }

    private void SynchronizeActiveState(AxisState state)
    {
        if (state.ScrollBar is not null)
        {
            SetState(
                state.ScrollBar,
                state.ActivePropertyKey,
                GetState(this, state.ScrollingPropertyKey) || IsInteractingWithViewer(state)
            );
        }
    }

    private AxisState? GetState(object? source)
    {
        if (source is null)
        {
            return null;
        }

        if (ReferenceEquals(source, _verticalState.Timer)
            || ReferenceEquals(source, _verticalState.ScrollBar))
        {
            return _verticalState;
        }

        if (ReferenceEquals(source, _horizontalState.Timer)
            || ReferenceEquals(source, _horizontalState.ScrollBar))
        {
            return _horizontalState;
        }

        return null;
    }

    private static bool GetState(DependencyObject target, DependencyPropertyKey propertyKey)
    {
        return (bool)target.GetValue(propertyKey.DependencyProperty);
    }

    private static void SetState(DependencyObject target, DependencyPropertyKey propertyKey, bool value)
    {
        if (GetState(target, propertyKey) != value)
        {
            target.SetValue(propertyKey, value);
        }
    }

    private void OnTimeoutChanged(int timeout)
    {
        _timeout = timeout;
        UpdateTimerIntervals();

        RestartIfEnabled(_verticalState.Timer);
        RestartIfEnabled(_horizontalState.Timer);
    }

    private void UpdateTimerIntervals()
    {
        // A zero DispatcherTimer interval repeatedly re-enters the dispatcher. One millisecond preserves
        // effectively immediate deactivation while keeping these timers one-shot.
        var interval = TimeSpan.FromMilliseconds(Math.Max(1, _timeout));

        _verticalState.Timer.Interval = interval;
        _horizontalState.Timer.Interval = interval;
    }

    private bool IsInteractingWithViewer(AxisState state)
    {
        return IsMouseOver || state.ScrollBar is { IsMouseCaptureWithin: true };
    }

    private static void RestartTimer(DispatcherTimer timer)
    {
        timer.Stop();
        timer.Start();
    }

    private static void RestartIfEnabled(DispatcherTimer timer)
    {
        if (timer.IsEnabled)
        {
            RestartTimer(timer);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SynchronizeActiveState(_verticalState);
        SynchronizeActiveState(_horizontalState);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ResetState(_verticalState);
        ResetState(_horizontalState);
    }

    private void ResetState(AxisState state)
    {
        state.Timer.Stop();
        SetState(this, state.ScrollingPropertyKey, false);

        if (state.ScrollBar is not null)
        {
            SetState(state.ScrollBar, state.ActivePropertyKey, false);
        }
    }

    private sealed class AxisState
    {
        public AxisState(
            DependencyPropertyKey scrollingPropertyKey,
            DependencyPropertyKey activePropertyKey
        )
        {
            ScrollingPropertyKey = scrollingPropertyKey;
            ActivePropertyKey = activePropertyKey;
        }

        public DependencyPropertyKey ScrollingPropertyKey { get; }

        public DependencyPropertyKey ActivePropertyKey { get; }

        public DispatcherTimer Timer { get; } = new();

        public ScrollBar? ScrollBar { get; set; }
    }
}
