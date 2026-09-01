// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Hosts the collapsible content used by expander templates and coordinates a directional slide
/// toward or away from the expander header.
/// </summary>
/// <remarks>
/// WPF does not measure an element while its <see cref="UIElement.Visibility"/> is
/// <see cref="Visibility.Collapsed"/>. Consequently, an entrance animation that snapshots
/// <see cref="FrameworkElement.ActualWidth"/> or <see cref="FrameworkElement.ActualHeight"/>
/// when expansion is requested can capture zero. This host first makes its content surface visible
/// behind an opacity gate, waits for layout, and only then reveals the surface from its closed
/// position.
/// <para>
/// The animated value is a normalized, linear transition progress: <c>0</c> is fully open and
/// <c>1</c> is fully closed. A cubic curve is applied only when that progress is converted to a
/// pixel offset. Keeping time linear means that reversing after half of a configured interval uses
/// only half of the opposite interval, while the stable cubic mapping keeps the rendered position
/// continuous. The content surface's current render size and <see cref="ExpandDirection"/> are
/// applied on every progress change, so the offset also remains correct when content changes size.
/// </para>
/// <para>
/// <see cref="IsExpanded"/> requests a state transition, while
/// <see cref="UIElement.IsVisibleChanged"/> confirms that the host and its ancestors are actually
/// visible. A collapse retains the layout slot until the exit completes, then sets the host to
/// <see cref="Visibility.Collapsed"/>.
/// </para>
/// <para>
/// The control template must provide <c>PART_ContentSurface</c>. The host itself remains stationary
/// and clips to its layout bounds; only that template part is translated. A template that omits the
/// part still presents content, but expansion and collapse fall back to immediate state changes.
/// </para>
/// </remarks>
[TemplatePart(Name = ContentSurfacePartName, Type = typeof(UIElement))]
internal sealed class ExpanderContentHost : ContentControl
{
    private const string ContentSurfacePartName = "PART_ContentSurface";

    private const double FullyRevealedProgress   = 0.0d;
    private const double FullyHiddenProgress     = 1.0d;
    private const double MinimumAnimatedDistance = 0.1d;

    private static readonly DependencyProperty RevealProgressProperty =
        DependencyProperty.Register(
            "RevealProgress",
            typeof(double),
            typeof(ExpanderContentHost),
            new FrameworkPropertyMetadata(FullyHiddenProgress, OnRevealProgressChanged)
        );

    private DispatcherOperation? _pendingEntrance;
    private UIElement?           _animatedSurface;
    private Transform?           _originalRenderTransform;
    private TranslateTransform?  _translation;
    private double               _currentRevealProgress = FullyHiddenProgress;
    private double               _opacityBeforeGate     = 1.0d;
    private int                  _animationVersion;
    private bool                 _isEntrancePending;
    private bool                 _isOpacityGated;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpanderContentHost"/> class.
    /// </summary>
    public ExpanderContentHost()
    {
        ClipToBounds = true;
        Visibility = Visibility.Collapsed;

        IsVisibleChanged += OnHostIsVisibleChanged;
        Loaded += OnHostLoaded;
        Unloaded += OnHostUnloaded;
    }

    /// <summary>
    /// Identifies the <see cref="IsExpanded"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded),
        typeof(bool),
        typeof(ExpanderContentHost),
        new FrameworkPropertyMetadata(false, OnIsExpandedChanged)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the hosted content should occupy layout space and
    /// remain visible.
    /// </summary>
    /// <remarks>
    /// Setting this property to <see langword="true"/> requests an entrance after the host becomes
    /// actually visible. Setting it to <see langword="false"/> starts an exit from the current
    /// reveal progress and collapses the host only after that animation completes.
    /// </remarks>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ExpandDirection"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ExpandDirectionProperty =
        Expander.ExpandDirectionProperty.AddOwner(
            typeof(ExpanderContentHost),
            new FrameworkPropertyMetadata(
                ExpandDirection.Down,
                OnExpandDirectionChanged
            )
        );

    /// <summary>
    /// Gets or sets the direction in which content is revealed from the header. The default is
    /// <see cref="ExpandDirection.Down"/>.
    /// </summary>
    /// <remarks>
    /// The direction determines both the animated axis and the closed-side sign. Down and Right
    /// content are hidden using negative offsets; Up and Left content use positive offsets so all
    /// four variants move toward their header while closing.
    /// </remarks>
    public ExpandDirection ExpandDirection
    {
        get => (ExpandDirection)GetValue(ExpandDirectionProperty);
        set => SetValue(ExpandDirectionProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ExpandDuration"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ExpandDurationProperty = DependencyProperty.Register(
        nameof(ExpandDuration),
        typeof(TimeSpan),
        typeof(ExpanderContentHost),
        new FrameworkPropertyMetadata(TimeSpan.FromMilliseconds(167.0d)),
        IsValidDuration
    );

    /// <summary>
    /// Gets or sets the duration of a complete transition from fully closed to fully open.
    /// </summary>
    /// <remarks>
    /// Reversing a running transition scales this duration by the remaining linear transition
    /// progress. For example, reversing halfway through an entrance consumes half of this duration.
    /// </remarks>
    public TimeSpan ExpandDuration
    {
        get => (TimeSpan)GetValue(ExpandDurationProperty);
        set => SetValue(ExpandDurationProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="CollapseDuration"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CollapseDurationProperty = DependencyProperty.Register(
        nameof(CollapseDuration),
        typeof(TimeSpan),
        typeof(ExpanderContentHost),
        new FrameworkPropertyMetadata(TimeSpan.FromMilliseconds(167.0d)),
        IsValidDuration
    );

    /// <summary>
    /// Gets or sets the duration of a complete transition from fully open to fully closed.
    /// </summary>
    /// <remarks>
    /// The host remains visible and occupies its expanded layout slot during this interval. It is
    /// collapsed only after the reveal progress reaches its closed endpoint.
    /// </remarks>
    public TimeSpan CollapseDuration
    {
        get => (TimeSpan)GetValue(CollapseDurationProperty);
        set => SetValue(CollapseDurationProperty, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        // A ControlTemplate can be replaced while an animation or opacity gate is active. Restore
        // the outgoing surface before WPF detaches it so transforms supplied by that template are
        // never left wrapped in the host's private animation transform.
        RestorePreviousContentSurface();
        base.OnApplyTemplate();

        if (_isEntrancePending)
        {
            GateContentSurfaceUntilEntrance();
        }
        else
        {
            ApplyTranslation();
        }
    }

    private static bool IsValidDuration(object? value) =>
        value is TimeSpan duration && duration >= TimeSpan.Zero;

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (ExpanderContentHost)d;

        if ((bool)e.NewValue)
        {
            host.RequestExpansion();
        }
        else
        {
            host.RequestCollapse();
        }
    }

    private static void OnExpandDirectionChanged(
        DependencyObject                   d,
        DependencyPropertyChangedEventArgs e)
    {
        ((ExpanderContentHost)d).ApplyTranslation();
    }

    private static void OnRevealProgressChanged(
        DependencyObject                   d,
        DependencyPropertyChangedEventArgs e)
    {
        var host = (ExpanderContentHost)d;

        // e.NewValue is the linear transition position produced for this animation tick. Retaining
        // it gives a reversal an exact timeline origin even while WPF replaces the old clock.
        host._currentRevealProgress = ClampProgress((double)e.NewValue);
        host.ApplyTranslation();
    }

    private void OnHostLoaded(object sender, RoutedEventArgs e)
    {
        if (!IsExpanded)
        {
            CollapseImmediately();
            return;
        }

        if (Visibility != Visibility.Visible)
        {
            RequestExpansion();
        }
        else if (_isEntrancePending && IsVisible)
        {
            QueueEntranceAfterLayout();
        }
    }

    private void OnHostUnloaded(object sender, RoutedEventArgs e)
    {
        CancelPendingEntrance();

        if (!IsExpanded)
        {
            CollapseImmediately();
            return;
        }

        // An entrance that was already presented must not replay merely because an ancestor was
        // temporarily removed from the visual tree. Only a never-presented, opacity-gated entrance
        // remains pending for the next load.
        if (!_isEntrancePending)
        {
            CommitOpenVisualState();
        }
    }

    private void OnHostIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            if (IsExpanded && _isEntrancePending)
            {
                QueueEntranceAfterLayout();
            }

            return;
        }

        // IsVisible includes ancestor visibility. Abort only the queued dispatcher operation and
        // preserve the pending state so the entrance can resume when its ancestors become visible.
        if (_isEntrancePending)
        {
            CancelPendingEntrance();
        }
    }

    private void OnContentSurfaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // RevealProgress is dimensionless. Reapplying its eased value against the new RenderSize
        // preserves the same visible phase when content changes size during a transition.
        ApplyTranslation();
    }

    private void RequestExpansion()
    {
        var version = ++_animationVersion;
        CancelPendingEntrance();

        if (Visibility != Visibility.Visible)
        {
            _isEntrancePending = true;
            GateContentSurfaceUntilEntrance();

            // Visibility can synchronously raise IsVisibleChanged. That callback owns scheduling
            // when an ancestor is hidden, while the explicit check covers an already visible tree.
            SetCurrentValue(VisibilityProperty, Visibility.Visible);

            if (IsVisible)
            {
                QueueEntranceAfterLayout();
            }

            return;
        }

        if (!IsVisible)
        {
            _isEntrancePending = true;
            GateContentSurfaceUntilEntrance();
            return;
        }

        _isEntrancePending = false;
        StartExpansionFromCurrentProgress(version);
    }

    private void RequestCollapse()
    {
        var version = ++_animationVersion;
        CancelPendingEntrance();
        _isEntrancePending = false;

        // Nothing has been presented while the opacity gate is active. Collapsing immediately is
        // visually equivalent to playing an exit and avoids retaining an unused layout slot.
        if (Visibility != Visibility.Visible || !IsVisible || _isOpacityGated)
        {
            CollapseImmediately();
            return;
        }

        UpdateLayout();

        if (!HasAnimatedDistance())
        {
            CollapseImmediately();
            return;
        }

        var from = GetRevealProgress();
        var duration = ScaleDuration(
            CollapseDuration,
            FullyHiddenProgress - from
        );

        StartProgressAnimation(
            from,
            FullyHiddenProgress,
            duration,
            () =>
            {
                if (version == _animationVersion && !IsExpanded)
                {
                    CollapseImmediately();
                }
            }
        );
    }

    private void QueueEntranceAfterLayout()
    {
        if (_pendingEntrance?.Status == DispatcherOperationStatus.Pending ||
            Dispatcher.HasShutdownStarted)
        {
            return;
        }

        var version = _animationVersion;

        // Loaded priority runs after the visibility change has received its normal layout work.
        // The opacity gate remains active until the closed transform and animation clock exist.
        _pendingEntrance = Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => StartDeferredEntrance(version))
        );
    }

    private void StartDeferredEntrance(int version)
    {
        _pendingEntrance = null;

        if (version != _animationVersion ||
            !IsExpanded ||
            !_isEntrancePending ||
            Visibility != Visibility.Visible ||
            !IsVisible)
        {
            return;
        }

        _isEntrancePending = false;
        UpdateLayout();

        if (!TryGetAnimationState(out _, out _) || !HasAnimatedDistance())
        {
            CommitOpenVisualState();
            return;
        }

        // Install the closed progress before removing the opacity gate. A newly attached clock can
        // start on the next TimeManager tick, so the base value prevents a one-frame open flash.
        SetRevealProgressImmediately(FullyHiddenProgress);
        StartProgressAnimation(
            FullyHiddenProgress,
            FullyRevealedProgress,
            ExpandDuration,
            () =>
            {
                if (version == _animationVersion && IsExpanded)
                {
                    CommitOpenVisualState();
                }
            }
        );

        RestoreContentSurfaceOpacity();
    }

    private void StartExpansionFromCurrentProgress(int version)
    {
        UpdateLayout();

        if (!TryGetAnimationState(out _, out _) || !HasAnimatedDistance())
        {
            CommitOpenVisualState();
            return;
        }

        var from     = GetRevealProgress();
        var duration = ScaleDuration(ExpandDuration, from);

        StartProgressAnimation(
            from,
            FullyRevealedProgress,
            duration,
            () =>
            {
                if (version == _animationVersion && IsExpanded)
                {
                    CommitOpenVisualState();
                }
            }
        );

        RestoreContentSurfaceOpacity();
    }

    private void StartProgressAnimation(
        double   from,
        double   to,
        TimeSpan duration,
        Action   completed)
    {
        from = ClampProgress(from);
        to = ClampProgress(to);

        // Capture the displayed progress before removing a replaced clock. Committing it as the
        // new base in the same dispatcher callback prevents a reversal from jumping to an endpoint.
        SetRevealProgressImmediately(from);

        if (duration <= TimeSpan.Zero || Math.Abs(to - from) < double.Epsilon)
        {
            SetRevealProgressImmediately(to);
            completed();
            return;
        }

        // Animate the logical phase linearly. ApplyTranslation maps this phase through one stable
        // cubic curve for both directions. If easing lived on each replacement animation instead,
        // a halfway reversal would restart a fresh local curve and could consume nearly the full
        // configured duration even though only half of the original timeline remained.
        var animation = new DoubleAnimation(
            from,
            to,
            new Duration(duration),
            FillBehavior.HoldEnd
        );

        animation.Completed += (_, _) => completed();
        BeginAnimation(
            RevealProgressProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace
        );

        // BeginAnimation can synchronously invalidate the property with its non-animated base
        // before the new clock receives its first tick. Reassert the captured origin so callers
        // observing the transform in this dispatcher turn see the same position as before the
        // reversal; the clock continues normally from that origin on the next tick.
        _currentRevealProgress = from;
        ApplyTranslation();
    }

    private bool TryGetAnimationState(
        out UIElement          surface,
        out TranslateTransform translation)
    {
        if (_animatedSurface is null)
        {
            if (GetTemplateChild(ContentSurfacePartName) is not UIElement currentSurface)
            {
                surface = null!;
                translation = null!;
                return false;
            }

            _animatedSurface = currentSurface;
            _originalRenderTransform = currentSurface.RenderTransform;
            _translation = new TranslateTransform();

            if (_originalRenderTransform is null ||
                ReferenceEquals(_originalRenderTransform, Transform.Identity))
            {
                currentSurface.SetCurrentValue(RenderTransformProperty, _translation);
            }
            else
            {
                // Append the private translation so a transform supplied by the template remains
                // intact and never needs to expose its values to the animation state machine. The
                // host itself must remain stationary because its bounds are the clipping window.
                var transforms = new TransformGroup();
                transforms.Children.Add(_originalRenderTransform);
                transforms.Children.Add(_translation);
                currentSurface.SetCurrentValue(RenderTransformProperty, transforms);
            }

            if (currentSurface is FrameworkElement frameworkElement)
            {
                frameworkElement.SizeChanged += OnContentSurfaceSizeChanged;
            }

            ApplyTranslation(currentSurface, _translation, _currentRevealProgress);
        }

        surface = _animatedSurface;
        translation = _translation!;
        return true;
    }

    private void ApplyTranslation()
    {
        if (TryGetAnimationState(out var surface, out var translation))
        {
            ApplyTranslation(surface, translation, _currentRevealProgress);
        }
    }

    private void ApplyTranslation(
        UIElement          surface,
        TranslateTransform translation,
        double             progress)
    {
        progress = ClampProgress(progress);

        // This single global mapping is equivalent to EaseOut while progress moves from 1 to 0
        // and EaseIn while it moves from 0 to 1. Unlike applying easing to each new DoubleAnimation,
        // it does not change when a running transition is reversed.
        var offsetFactor = progress * progress * progress;

        var x = 0.0d;
        var y = 0.0d;

        switch (ExpandDirection)
        {
            case ExpandDirection.Down:
                y = -surface.RenderSize.Height * offsetFactor;
                break;

            case ExpandDirection.Up:
                y = surface.RenderSize.Height * offsetFactor;
                break;

            case ExpandDirection.Left:
                x = surface.RenderSize.Width * offsetFactor;
                break;

            case ExpandDirection.Right:
                x = -surface.RenderSize.Width * offsetFactor;
                break;
        }

        translation.SetCurrentValue(TranslateTransform.XProperty, x);
        translation.SetCurrentValue(TranslateTransform.YProperty, y);
    }

    private bool HasAnimatedDistance()
    {
        if (!TryGetAnimationState(out var surface, out _))
        {
            return false;
        }

        var distance = ExpandDirection is ExpandDirection.Left or ExpandDirection.Right
            ? surface.RenderSize.Width
            : surface.RenderSize.Height;

        return distance >= MinimumAnimatedDistance;
    }

    private void GateContentSurfaceUntilEntrance()
    {
        if (!TryGetAnimationState(out var surface, out _))
        {
            return;
        }

        SetRevealProgressImmediately(FullyHiddenProgress);

        if (!_isOpacityGated)
        {
            _opacityBeforeGate = surface.Opacity;
            surface.SetCurrentValue(OpacityProperty, 0.0d);
            _isOpacityGated = true;
        }
    }

    private void RestoreContentSurfaceOpacity()
    {
        if (!_isOpacityGated || _animatedSurface is null)
        {
            return;
        }

        _animatedSurface.SetCurrentValue(OpacityProperty, _opacityBeforeGate);
        _isOpacityGated = false;
    }

    private void CommitOpenVisualState()
    {
        SetRevealProgressImmediately(FullyRevealedProgress);
        RestoreContentSurfaceOpacity();
    }

    private void CollapseImmediately()
    {
        CancelPendingEntrance();

        // Collapse while the animation still holds its closed endpoint. Resetting the normalized
        // progress afterwards cannot expose a translated frame and prepares the next entrance.
        SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
        SetRevealProgressImmediately(FullyHiddenProgress);
        RestoreContentSurfaceOpacity();
    }

    private void SetRevealProgressImmediately(double progress)
    {
        var normalizedProgress = ClampProgress(progress);

        // Removing a clock exposes the existing animation base. Commit the captured effective
        // value immediately afterwards; both operations run in one dispatcher callback, so WPF
        // cannot render the intermediate base while a reversal is being installed.
        BeginAnimation(RevealProgressProperty, null);
        SetCurrentValue(RevealProgressProperty, normalizedProgress);
        _currentRevealProgress = normalizedProgress;
        ApplyTranslation();
    }

    private double GetRevealProgress() =>
        _currentRevealProgress;

    private void CancelPendingEntrance()
    {
        if (_pendingEntrance?.Status == DispatcherOperationStatus.Pending)
        {
            _pendingEntrance.Abort();
        }

        _pendingEntrance = null;
    }

    private void RestorePreviousContentSurface()
    {
        if (_animatedSurface is null)
        {
            return;
        }

        if (_animatedSurface is FrameworkElement frameworkElement)
        {
            frameworkElement.SizeChanged -= OnContentSurfaceSizeChanged;
        }

        if (_isOpacityGated)
        {
            _animatedSurface.SetCurrentValue(OpacityProperty, _opacityBeforeGate);
            _isOpacityGated = false;
        }

        _animatedSurface.SetCurrentValue(
            RenderTransformProperty,
            _originalRenderTransform ?? Transform.Identity
        );
        _animatedSurface = null;
        _originalRenderTransform = null;
        _translation = null;
    }

    private static double ClampProgress(double value) =>
        Math.Max(FullyRevealedProgress, Math.Min(FullyHiddenProgress, value));

    private static TimeSpan ScaleDuration(TimeSpan duration, double distanceRatio)
    {
        if (duration <= TimeSpan.Zero || distanceRatio <= 0.0d)
        {
            return TimeSpan.Zero;
        }

        var ratio = Math.Min(1.0d, distanceRatio);
        return TimeSpan.FromTicks(Math.Max(1L, (long)(duration.Ticks * ratio)));
    }
}
