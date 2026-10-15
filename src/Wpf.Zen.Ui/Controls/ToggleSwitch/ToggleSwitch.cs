// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Media.Animation;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents a compact two- or three-state switch whose track and thumb can be styled
/// independently without reimplementing the thumb transition.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ToggleSwitch"/> deliberately does not present the inherited
/// <see cref="ContentControl.Content"/> value. Labels, descriptions, and state-dependent text are
/// application content and can be arranged next to the switch with ordinary WPF panels without
/// coupling that layout to the switch skin.
/// </para>
/// <para>
/// The default template contains <c>PART_Track</c> and <c>PART_Thumb</c>. Their appearances are
/// supplied by <see cref="TrackStyle"/> and <see cref="ThumbStyle"/>. Both default parts are
/// <see cref="ToggleSwitchPart"/> instances, so a replacement style can react to
/// <see cref="ToggleSwitchPart.Orientation"/>, <see cref="ToggleSwitchPart.IsChecked"/>,
/// <see cref="ToggleSwitchPart.IsPointerOver"/>, <see cref="ToggleSwitchPart.IsPressed"/>, and
/// <see cref="UIElement.IsEnabled"/> with ordinary property triggers.
/// </para>
/// <para>
/// The control owns the thumb motion. It measures the resting thumb position relative to the
/// track, mirrors that position across the selected axis, and moves the thumb between both
/// endpoints. A horizontal switch places the checked thumb at its logical end; a vertical switch
/// places it at the top, matching the conventional up-is-on physical-switch metaphor.
/// Consequently, changing track size, thumb size, margin, orientation, or flow direction does not
/// require new pixel coordinates or storyboards. A completely custom control template may use any
/// <see cref="FrameworkElement"/> for either named part; using <see cref="ToggleSwitchPart"/>
/// additionally provides the convenient state properties.
/// </para>
/// <para>
/// <see cref="TransitionProgress"/> is the shared eased timeline for every visual effect. The
/// default track cross-fades persistent unchecked and checked layers with this value, so its color
/// transition stays synchronized with thumb motion and reverses without a second storyboard.
/// </para>
/// </remarks>
[TemplatePart(Name = TrackPartName, Type = typeof(FrameworkElement))]
[TemplatePart(Name = ThumbPartName, Type = typeof(FrameworkElement))]
[TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
[TemplateVisualState(Name = "MouseOver", GroupName = "CommonStates")]
[TemplateVisualState(Name = "Pressed", GroupName = "CommonStates")]
[TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
[TemplateVisualState(Name = "Checked", GroupName = "CheckStates")]
[TemplateVisualState(Name = "Unchecked", GroupName = "CheckStates")]
[TemplateVisualState(Name = "Indeterminate", GroupName = "CheckStates")]
[StyleTypedProperty(Property = nameof(TrackStyle), StyleTargetType = typeof(ToggleSwitchPart))]
[StyleTypedProperty(Property = nameof(ThumbStyle), StyleTargetType = typeof(ToggleSwitchPart))]
public class ToggleSwitch : System.Windows.Controls.Primitives.ToggleButton
{
    /// <summary>The required template-part name for the switch track.</summary>
    public const string TrackPartName = "PART_Track";

    /// <summary>The required template-part name for the movable switch thumb.</summary>
    public const string ThumbPartName = "PART_Thumb";

    private const double UncheckedProgress     = 0.0d;
    private const double IndeterminateProgress = 0.5d;
    private const double CheckedProgress       = 1.0d;

    // WPF animation clocks can target only dependency properties, so this private property acts as
    // the control's internal, linearly advancing timeline. It deliberately stores progress before
    // easing: PublishProgress applies TransitionEasingFunction and exposes the resulting visual
    // position through the read-only TransitionProgress property. Keeping the raw timeline separate
    // lets an interrupted transition restart from its current position without applying a second
    // local easing curve or causing the thumb and template effects to jump.
    private static readonly DependencyProperty RawTransitionProgressProperty =
        DependencyProperty.Register(
            "RawTransitionProgress",
            typeof(double),
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(
                UncheckedProgress,
                OnRawTransitionProgressChanged
            )
        );

    private FrameworkElement?   _track;
    private FrameworkElement?   _thumb;
    private ToggleSwitchPart?   _trackStatePart;
    private ToggleSwitchPart?   _thumbStatePart;
    private Transform?          _originalThumbTransform;
    private TranslateTransform? _thumbTranslation;
    private double              _currentRawTransitionProgress;
    private int                 _transitionVersion;

    static ToggleSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(typeof(ToggleSwitch))
        );
    }

    /// <summary>Initializes a new instance of the <see cref="ToggleSwitch"/> class.</summary>
    public ToggleSwitch()
    {
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
        IsEnabledChanged += OnIsEnabledChanged;
    }

    #region DP: TrackStyle

    /// <summary>
    /// Identifies the <see cref="TrackStyle"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TrackStyleProperty =
        DependencyProperty.Register(
            nameof(TrackStyle),
            typeof(Style),
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the style applied to the default <c>PART_Track</c>
    /// <see cref="ToggleSwitchPart"/>.
    /// </summary>
    /// <remarks>
    /// The style normally targets <see cref="ToggleSwitchPart"/> and may replace its template to
    /// create any track geometry. Width, height, margin, brushes, shadows, and state triggers all
    /// belong in this style. Changing it can affect measurement and arrangement through the
    /// styled part.
    /// </remarks>
    public Style? TrackStyle
    {
        get => (Style?)GetValue(TrackStyleProperty);
        set => SetValue(TrackStyleProperty, value);
    }

    #endregion

    #region DP: ThumbStyle

    /// <summary>
    /// Identifies the <see cref="ThumbStyle"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ThumbStyleProperty =
        DependencyProperty.Register(
            nameof(ThumbStyle),
            typeof(Style),
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the style applied to the default <c>PART_Thumb</c>
    /// <see cref="ToggleSwitchPart"/>.
    /// </summary>
    /// <remarks>
    /// The style controls the thumb's natural size, inset, shape, and state-specific appearance.
    /// The switch recalculates the endpoint whenever the styled thumb changes size, so hover and
    /// pressed styles can resize it without supplying replacement animation coordinates.
    /// </remarks>
    public Style? ThumbStyle
    {
        get => (Style?)GetValue(ThumbStyleProperty);
        set => SetValue(ThumbStyleProperty, value);
    }

    #endregion

    #region DP: Orientation

    /// <summary>
    /// Identifies the <see cref="Orientation"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(
                Orientation.Horizontal,
                OnOrientationChanged
            ),
            ValidateOrientation
        );

    /// <summary>
    /// Gets or sets the axis on which the thumb travels. The default is
    /// <see cref="Orientation.Horizontal"/>.
    /// </summary>
    /// <remarks>
    /// This property does not rotate either visual part. Instead, the selected value is published
    /// to each default <see cref="ToggleSwitchPart"/>, allowing <see cref="TrackStyle"/> and
    /// <see cref="ThumbStyle"/> to select suitable geometry. The built-in styles provide both
    /// horizontal and vertical forms. In horizontal mode, checked is the logical end (right in a
    /// left-to-right layout); in vertical mode, checked is the top and unchecked is the bottom.
    /// Changing the value reapplies the current position immediately.
    /// </remarks>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    #endregion

    #region DP: TransitionDuration

    /// <summary>
    /// Identifies the <see cref="TransitionDuration"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TransitionDurationProperty =
        DependencyProperty.Register(
            nameof(TransitionDuration),
            typeof(TimeSpan),
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(TimeSpan.FromMilliseconds(167.0d)),
            ValidateTransitionDuration
        );

    /// <summary>
    /// Gets or sets the duration of a complete unchecked-to-checked thumb transition.
    /// </summary>
    /// <remarks>
    /// The default is 167 milliseconds. An interrupted or partial transition is scaled by its
    /// remaining normalized distance, which keeps reversals responsive. A zero duration disables
    /// motion without disabling state updates.
    /// </remarks>
    public TimeSpan TransitionDuration
    {
        get => (TimeSpan)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    #endregion

    #region DP: TransitionEasingFunction

    /// <summary>
    /// Identifies the <see cref="TransitionEasingFunction"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TransitionEasingFunctionProperty =
        DependencyProperty.Register(
            nameof(TransitionEasingFunction),
            typeof(IEasingFunction),
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(
                CreateDefaultEasingFunction(),
                OnTransitionEasingFunctionChanged
            )
        );

    /// <summary>
    /// Gets or sets the easing function that maps logical transition progress to thumb position.
    /// </summary>
    /// <remarks>
    /// The default is a symmetric cubic ease-in/ease-out curve. The function is evaluated over one
    /// stable zero-to-one timeline rather than attached to each replacement animation, so reversing
    /// a running transition preserves its rendered position. Results are clamped to the track
    /// endpoints; use a replacement control template for intentional overshoot effects.
    /// </remarks>
    public IEasingFunction? TransitionEasingFunction
    {
        get => (IEasingFunction?)GetValue(TransitionEasingFunctionProperty);
        set => SetValue(TransitionEasingFunctionProperty, value);
    }

    #endregion

    #region DP: IsAnimationEnabled

    /// <summary>
    /// Identifies the <see cref="IsAnimationEnabled"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsAnimationEnabledProperty =
        DependencyProperty.Register(
            nameof(IsAnimationEnabled),
            typeof(bool),
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(true, OnIsAnimationEnabledChanged)
        );

    /// <summary>
    /// Gets or sets a value indicating whether state changes may animate the thumb. The default is
    /// <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// WPF's <see cref="SystemParameters.ClientAreaAnimation"/> setting is also respected. Setting
    /// this property to <see langword="false"/> while a transition is running commits the requested
    /// state immediately.
    /// </remarks>
    public bool IsAnimationEnabled
    {
        get => (bool)GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    #endregion

    #region Read-Only DP: TransitionProgress

    private static readonly DependencyPropertyKey TransitionProgressPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(TransitionProgress),
            typeof(double),
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(UncheckedProgress)
        );

    /// <summary>
    /// Identifies the <see cref="TransitionProgress"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TransitionProgressProperty =
        TransitionProgressPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the current rendered transition progress, where 0 is unchecked, 0.5 is indeterminate,
    /// and 1 is checked.
    /// </summary>
    /// <remarks>
    /// This read-only value includes <see cref="TransitionEasingFunction"/> and changes on animation
    /// ticks. It is the shared visual timeline for the complete switch rather than a thumb-only
    /// value: custom templates may bind track opacity, color layers, highlights, shadows, or thumb
    /// scale to it without defining a second animation. The value is always clamped to the
    /// inclusive range from 0 through 1 and does not affect layout by itself.
    /// </remarks>
    public double TransitionProgress
    {
        get => (double)GetValue(TransitionProgressProperty);
    }

    #endregion

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        RestoreTemplateParts();

        base.OnApplyTemplate();

        _track = GetTemplateChild(TrackPartName) as FrameworkElement;
        _thumb = GetTemplateChild(ThumbPartName) as FrameworkElement;

        _trackStatePart = _track as ToggleSwitchPart;
        _thumbStatePart = _thumb as ToggleSwitchPart;

        if (_track is not null)
        {
            _track.SizeChanged += OnPartSizeChanged;
        }

        if (_thumb is not null)
        {
            _thumb.SizeChanged += OnPartSizeChanged;
            InstallThumbTranslation(_thumb);
        }

        UpdatePartStates();
        PublishProgress();
    }

    /// <inheritdoc />
    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);

        UpdatePartStates();
        TransitionTo(CheckedProgress);
    }

    /// <inheritdoc />
    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);

        UpdatePartStates();
        TransitionTo(UncheckedProgress);
    }

    /// <inheritdoc />
    protected override void OnIndeterminate(RoutedEventArgs e)
    {
        base.OnIndeterminate(e);

        UpdatePartStates();
        TransitionTo(IndeterminateProgress);
    }

    private static bool ValidateOrientation(object? value)
    {
        return value is Orientation.Horizontal or Orientation.Vertical;
    }

    private static bool ValidateTransitionDuration(object? value)
    {
        return value is TimeSpan duration && duration >= TimeSpan.Zero;
    }

    private static void OnRawTransitionProgressChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var toggleSwitch = (ToggleSwitch)d;
        toggleSwitch._currentRawTransitionProgress = ClampProgress((double)e.NewValue);
        toggleSwitch.PublishProgress();
    }

    private static void OnTransitionEasingFunctionChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        ((ToggleSwitch)d).PublishProgress();
    }

    private static void OnOrientationChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var toggleSwitch = (ToggleSwitch)d;

        // Orientation participates in part styles as well as thumb positioning. Publish the new
        // snapshot before recalculating the transform so a style-driven size change can invalidate
        // layout and the SizeChanged callbacks can refine the endpoint with final dimensions.
        toggleSwitch.UpdatePartStates();
        toggleSwitch.PublishProgress();
    }

    private static void OnIsAnimationEnabledChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue)
        {
            var toggleSwitch = (ToggleSwitch)d;
            toggleSwitch.SetProgressImmediately(toggleSwitch.GetRequestedProgress());
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initial XAML state must be rendered at its endpoint. Enter animations on first layout
        // otherwise make a persisted checked value look briefly unchecked as a view appears.
        SetProgressImmediately(GetRequestedProgress());
        UpdatePartStates();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // A disconnected control cannot present intermediate frames. Commit its requested state so
        // virtualization or reparenting does not keep an unnecessary clock alive or replay stale
        // motion when the same instance returns to a visible tree.
        SetProgressImmediately(GetRequestedProgress());
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsPressedProperty || e.Property == IsMouseOverProperty)
        {
            UpdatePartStates();
        }
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdatePartStates();
    }

    private void OnPartSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyThumbPosition(TransitionProgress);
    }

    private void UpdatePartStates()
    {
        _trackStatePart?.SetOwnerState(Orientation, IsChecked, IsMouseOver, IsPressed);
        _thumbStatePart?.SetOwnerState(Orientation, IsChecked, IsMouseOver, IsPressed);
    }

    private void TransitionTo(double targetProgress)
    {
        targetProgress = ClampProgress(targetProgress);

        if (!IsLoaded
            || !IsVisible
            || !IsAnimationEnabled
            || !SystemParameters.ClientAreaAnimation
            || TransitionDuration <= TimeSpan.Zero)
        {
            SetProgressImmediately(targetProgress);
            return;
        }

        var from = _currentRawTransitionProgress;
        var remainingDistance = Math.Abs(targetProgress - from);

        if (remainingDistance < double.Epsilon)
        {
            SetProgressImmediately(targetProgress);
            return;
        }

        var version  = ++_transitionVersion;
        var duration = TimeSpan.FromTicks(
            Math.Max(1L, (long)(TransitionDuration.Ticks * remainingDistance))
        );

        // The private property advances linearly across the complete switch timeline. Easing is
        // applied only while publishing the visual position, so replacing a clock during reversal
        // cannot restart a second local easing curve or jump away from the displayed point.
        SetRawTransitionProgressImmediately(from);

        var animation = new DoubleAnimation(
            from,
            targetProgress,
            new Duration(duration),
            FillBehavior.HoldEnd
        );

        animation.Completed += (_, _) =>
        {
            if (version == _transitionVersion)
            {
                SetProgressImmediately(targetProgress);
            }
        };

        BeginAnimation(
            RawTransitionProgressProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace
        );
    }

    private void SetProgressImmediately(double progress)
    {
        _transitionVersion++;
        SetRawTransitionProgressImmediately(progress);
    }

    private void SetRawTransitionProgressImmediately(double progress)
    {
        var normalizedProgress = ClampProgress(progress);

        // Removing the clock exposes its old base value. Commit the captured effective value in
        // the same dispatcher callback so a reversal never presents the obsolete endpoint. This
        // private property intentionally uses SetValue: it has no consumer-owned value source to
        // preserve, and a concrete animation base must exist before the replacement clock starts.

        BeginAnimation(RawTransitionProgressProperty, null);

#pragma warning disable WPF0041 // SetCurrentValue does not establish the required replacement-clock base.
        SetValue(RawTransitionProgressProperty, normalizedProgress);
#pragma warning restore WPF0041

        _currentRawTransitionProgress = normalizedProgress;

        PublishProgress();
    }

    private void PublishProgress()
    {
        var progress = _currentRawTransitionProgress;

        if (TransitionEasingFunction is not null)
        {
            var easedProgress = TransitionEasingFunction.Ease(progress);

            // A third-party easing implementation can return NaN or infinity. Falling back to the
            // linear phase keeps the template renderable and preserves the requested endpoint.
            if (!double.IsNaN(easedProgress) && !double.IsInfinity(easedProgress))
            {
                progress = easedProgress;
            }
        }

        progress = ClampProgress(progress);

        SetValue(TransitionProgressPropertyKey, progress);

        _trackStatePart?.SetOwnerTransitionProgress(progress);
        _thumbStatePart?.SetOwnerTransitionProgress(progress);

        ApplyThumbPosition(progress);
    }

    private double GetRequestedProgress()
    {
        return IsChecked switch
        {
            true  => CheckedProgress,
            false => UncheckedProgress,
            null  => IndeterminateProgress,
        };
    }

    private void InstallThumbTranslation(FrameworkElement thumb)
    {
        _originalThumbTransform = thumb.RenderTransform;
        _thumbTranslation = new TranslateTransform();

        if (_originalThumbTransform is null ||
            ReferenceEquals(_originalThumbTransform, Transform.Identity))
        {
            thumb.SetCurrentValue(RenderTransformProperty, _thumbTranslation);
            return;
        }

        // PART_Thumb.RenderTransform is the motion layer owned by ToggleSwitch. Preserve a
        // template-supplied transform ahead of that layer so skins can still rotate or scale the
        // complete thumb. Stateful transforms are best placed inside the part template, where a
        // style trigger cannot replace this private translation after OnApplyTemplate.
        var transforms = new TransformGroup();

        transforms.Children.Add(_originalThumbTransform);
        transforms.Children.Add(_thumbTranslation);

        thumb.SetCurrentValue(RenderTransformProperty, transforms);
    }

    private void ApplyThumbPosition(double progress)
    {
        if (_track is null || _thumb is null || _thumbTranslation is null)
        {
            return;
        }

        Point transformedOrigin;

        try
        {
            transformedOrigin = _thumb.TranslatePoint(default, _track);
        }
        catch (InvalidOperationException)
        {
            // A malformed template can temporarily place the named parts in disconnected visual
            // trees. The switch remains functional and retries naturally after the next template,
            // size, or progress update instead of turning a cosmetic contract failure into a crash.
            return;
        }

        if (Orientation == Orientation.Horizontal)
        {
            var restingX = transformedOrigin.X - _thumbTranslation.X;
            var travel = _track.RenderSize.Width - _thumb.RenderSize.Width - (2.0d * restingX);

            _thumbTranslation.SetCurrentValue(TranslateTransform.XProperty, travel * progress);
            _thumbTranslation.SetCurrentValue(TranslateTransform.YProperty, 0.0d);
        }
        else
        {
            var restingY = transformedOrigin.Y - _thumbTranslation.Y;
            var travel = _track.RenderSize.Height - _thumb.RenderSize.Height - (2.0d * restingY);

            // WPF's positive Y axis points downward, but a vertical switch follows the familiar
            // physical convention that up means on. TransitionProgress remains a logical
            // unchecked-to-checked timeline for track effects and animation; only its spatial
            // projection is reversed so false, null, and true map to bottom, center, and top.
            _thumbTranslation.SetCurrentValue(TranslateTransform.XProperty, 0.0d);
            _thumbTranslation.SetCurrentValue(
                TranslateTransform.YProperty,
                travel * (CheckedProgress - progress)
            );
        }
    }

    private void RestoreTemplateParts()
    {
        if (_track is not null)
        {
            _track.SizeChanged -= OnPartSizeChanged;
        }

        if (_thumb is not null)
        {
            _thumb.SizeChanged -= OnPartSizeChanged;
            _thumb.SetCurrentValue(
                RenderTransformProperty,
                _originalThumbTransform ?? Transform.Identity
            );
        }

        _track = null;
        _thumb = null;

        _trackStatePart = null;
        _thumbStatePart = null;

        _originalThumbTransform = null;
        _thumbTranslation       = null;
    }

    private static double ClampProgress(double value)
    {
        return Math.Max(UncheckedProgress, Math.Min(CheckedProgress, value));
    }

    private static IEasingFunction CreateDefaultEasingFunction()
    {
        var easing = new CubicEase
        {
            EasingMode = EasingMode.EaseInOut
        };

        easing.Freeze();

        return easing;
    }
}
