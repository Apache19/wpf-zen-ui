// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Presents a content dialog and owns the visual lifetime of its entrance and exit transitions.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ContentDialogHost"/> owns modal-session semantics, while this control owns only the
/// rendered backdrop and dialog surface. Keeping those responsibilities separate lets the host await
/// a transition without inspecting a template storyboard or depending on
/// <see cref="VisualStateManager"/>'s internal clock bookkeeping.
/// </para>
/// <para>
/// The template may provide <c>PART_Backdrop</c> and <c>PART_DialogRoot</c>. Both parts are optional:
/// a missing part simply omits that visual effect, and a template with neither part completes every
/// transition immediately. The presenter owns the parts' opacity while it is applied. It appends a
/// private <see cref="ScaleTransform"/> to <c>PART_DialogRoot</c> and restores the template-supplied
/// transform when the template changes. The standard template also uses
/// <see cref="DialogSizingMode"/> to choose which axes of <c>PART_DialogRoot</c> fill the host.
/// </para>
/// <para>
/// One private, controllable <see cref="AnimationClock"/> drives the whole transition. A direction
/// change snapshots the values already rendered, removes the old clock synchronously, and continues
/// from that snapshot. This keeps backdrop opacity, dialog opacity, and scale on the same completion
/// boundary and prevents a quickly closed dialog from jumping through the fully open pose.
/// </para>
/// </remarks>
[TemplatePart(Name = BackdropPartName, Type = typeof(UIElement))]
[TemplatePart(Name = DialogRootPartName, Type = typeof(UIElement))]
public class ContentDialogTransitionPresenter : Control
{
    internal const string BackdropPartName   = "PART_Backdrop";
    internal const string DialogRootPartName = "PART_DialogRoot";

    private const double ClosedOpacity = 0.0d;
    private const double OpenOpacity   = 1.0d;
    private const double OpenScale     = 1.0d;

    private static readonly DependencyProperty TransitionProgressProperty =
        DependencyProperty.Register(
            "TransitionProgress",
            typeof(double),
            typeof(ContentDialogTransitionPresenter),
            new FrameworkPropertyMetadata(0.0d, OnTransitionProgressChanged)
        );

    private UIElement?          _backdrop;
    private UIElement?          _dialogRoot;
    private Transform?          _originalDialogTransform;
    private ScaleTransform?     _dialogScale;
    private AnimationClock?     _animationClock;
    private EventHandler?       _clockCompletedHandler;
    private TaskCompletionSource<object?>? _transitionCompletion;

    private double _currentBackdropOpacity = ClosedOpacity;
    private double _currentDialogOpacity   = ClosedOpacity;
    private double _currentScale;
    private double _startBackdropOpacity;
    private double _startDialogOpacity;
    private double _startScale;
    private double _targetBackdropOpacity;
    private double _targetDialogOpacity;
    private double _targetScale;
    private IEasingFunction? _activeEasingFunction;
    private int  _transitionVersion;
    private bool _isOpenTarget;
    private bool _suppressProgressUpdates;
    private StablePose _stablePose = StablePose.PreparedClosed;

    private enum StablePose
    {
        PreparedClosed,
        Open,
        Closed,
    }

    static ContentDialogTransitionPresenter()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ContentDialogTransitionPresenter),
            new FrameworkPropertyMetadata(typeof(ContentDialogTransitionPresenter))
        );

        FocusableProperty.OverrideMetadata(
            typeof(ContentDialogTransitionPresenter),
            new FrameworkPropertyMetadata(false)
        );

        IsTabStopProperty.OverrideMetadata(
            typeof(ContentDialogTransitionPresenter),
            new FrameworkPropertyMetadata(false)
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialogTransitionPresenter"/> class.
    /// </summary>
    public ContentDialogTransitionPresenter()
    {
        _currentScale = OpeningScale;
        Unloaded += OnPresenterUnloaded;
    }

    /// <summary>Identifies the <see cref="Backdrop"/> dependency property.</summary>
    public static readonly DependencyProperty BackdropProperty = DependencyProperty.Register(
        nameof(Backdrop),
        typeof(Brush),
        typeof(ContentDialogTransitionPresenter),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the brush painted behind the dialog. The default is <see langword="null"/>.
    /// This property does not affect layout and may be animated or supplied by a dynamic resource.
    /// </summary>
    public Brush? Backdrop
    {
        get => (Brush?)GetValue(BackdropProperty);
        set => SetValue(BackdropProperty, value);
    }

    /// <summary>Identifies the <see cref="Dialog"/> dependency property.</summary>
    public static readonly DependencyProperty DialogProperty = DependencyProperty.Register(
        nameof(Dialog),
        typeof(ContentDialog),
        typeof(ContentDialogTransitionPresenter),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the dialog displayed by the presentation surface. The default is
    /// <see langword="null"/>. The presenter deliberately does not add this value as a logical
    /// child; <see cref="ContentDialogHost"/> retains logical ownership for the whole session.
    /// </summary>
    public ContentDialog? Dialog
    {
        get => (ContentDialog?)GetValue(DialogProperty);
        set => SetValue(DialogProperty, value);
    }

    /// <summary>Identifies the <see cref="DialogSizingMode"/> dependency property.</summary>
    public static readonly DependencyProperty DialogSizingModeProperty = DependencyProperty.Register(
        nameof(DialogSizingMode),
        typeof(ContentDialogSizingMode),
        typeof(ContentDialogTransitionPresenter),
        new FrameworkPropertyMetadata(ContentDialogSizingMode.Auto),
        static value => value is ContentDialogSizingMode.Auto
            or ContentDialogSizingMode.Stretch
            or ContentDialogSizingMode.StretchWidth
            or ContentDialogSizingMode.StretchHeight
    );

    /// <summary>
    /// Gets or sets the sizing mode that the presentation template applies to its dialog surface.
    /// The default is <see cref="ContentDialogSizingMode.Auto"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="ContentDialogHost"/>'s standard template forwards the current dialog's
    /// <see cref="ContentDialog.SizingMode"/> value. The property is separate from
    /// <see cref="Dialog"/> so replacement presenter templates have a direct, bindable layout input
    /// and do not need a nested binding to the dialog instance.
    /// </remarks>
    public ContentDialogSizingMode DialogSizingMode
    {
        get => (ContentDialogSizingMode)GetValue(DialogSizingModeProperty);
        set => SetValue(DialogSizingModeProperty, value);
    }

    /// <summary>Identifies the <see cref="OpenDuration"/> dependency property.</summary>
    public static readonly DependencyProperty OpenDurationProperty = DependencyProperty.Register(
        nameof(OpenDuration),
        typeof(TimeSpan),
        typeof(ContentDialogTransitionPresenter),
        new FrameworkPropertyMetadata(TimeSpan.FromMilliseconds(167.0d)),
        IsValidDuration
    );

    /// <summary>
    /// Gets or sets the duration of a complete transition from the prepared closed pose to the open
    /// pose. The default is 167 milliseconds. A zero duration completes synchronously.
    /// </summary>
    /// <remarks>
    /// A transition redirected from a partially rendered pose uses only the corresponding fraction
    /// of this duration, so reversing does not restart a full-length animation.
    /// </remarks>
    public TimeSpan OpenDuration
    {
        get => (TimeSpan)GetValue(OpenDurationProperty);
        set => SetValue(OpenDurationProperty, value);
    }

    /// <summary>Identifies the <see cref="CloseDuration"/> dependency property.</summary>
    public static readonly DependencyProperty CloseDurationProperty = DependencyProperty.Register(
        nameof(CloseDuration),
        typeof(TimeSpan),
        typeof(ContentDialogTransitionPresenter),
        new FrameworkPropertyMetadata(TimeSpan.FromMilliseconds(167.0d)),
        IsValidDuration
    );

    /// <summary>
    /// Gets or sets the duration of a complete transition from the open pose to the closed pose.
    /// The default is 167 milliseconds. A zero duration completes synchronously.
    /// </summary>
    public TimeSpan CloseDuration
    {
        get => (TimeSpan)GetValue(CloseDurationProperty);
        set => SetValue(CloseDurationProperty, value);
    }

    /// <summary>Identifies the <see cref="OpeningScale"/> dependency property.</summary>
    public static readonly DependencyProperty OpeningScaleProperty = DependencyProperty.Register(
        nameof(OpeningScale),
        typeof(double),
        typeof(ContentDialogTransitionPresenter),
        new FrameworkPropertyMetadata(0.94d, OnScaleChanged),
        IsValidScale
    );

    /// <summary>
    /// Gets or sets the dialog scale used immediately before an entrance. The default is 0.94.
    /// This changes only rendering; it does not affect measurement or arrangement.
    /// </summary>
    public double OpeningScale
    {
        get => (double)GetValue(OpeningScaleProperty);
        set => SetValue(OpeningScaleProperty, value);
    }

    /// <summary>Identifies the <see cref="ClosingScale"/> dependency property.</summary>
    public static readonly DependencyProperty ClosingScaleProperty = DependencyProperty.Register(
        nameof(ClosingScale),
        typeof(double),
        typeof(ContentDialogTransitionPresenter),
        new FrameworkPropertyMetadata(0.97d, OnScaleChanged),
        IsValidScale
    );

    /// <summary>
    /// Gets or sets the dialog scale reached at the end of an exit. The default is 0.97.
    /// This changes only rendering; it does not affect measurement or arrangement.
    /// </summary>
    public double ClosingScale
    {
        get => (double)GetValue(ClosingScaleProperty);
        set => SetValue(ClosingScaleProperty, value);
    }

    /// <summary>Identifies the <see cref="OpeningEasingFunction"/> dependency property.</summary>
    public static readonly DependencyProperty OpeningEasingFunctionProperty = DependencyProperty.Register(
        nameof(OpeningEasingFunction),
        typeof(IEasingFunction),
        typeof(ContentDialogTransitionPresenter),
        new FrameworkPropertyMetadata(CreateDefaultOpeningEasingFunction())
    );

    /// <summary>
    /// Gets or sets the easing applied to dialog scale during an entrance. The backdrop and dialog
    /// opacity remain linear so the surface cannot stay unexpectedly interactive while invisible.
    /// </summary>
    public IEasingFunction? OpeningEasingFunction
    {
        get => (IEasingFunction?)GetValue(OpeningEasingFunctionProperty);
        set => SetValue(OpeningEasingFunctionProperty, value);
    }

    /// <summary>Identifies the <see cref="ClosingEasingFunction"/> dependency property.</summary>
    public static readonly DependencyProperty ClosingEasingFunctionProperty = DependencyProperty.Register(
        nameof(ClosingEasingFunction),
        typeof(IEasingFunction),
        typeof(ContentDialogTransitionPresenter),
        new FrameworkPropertyMetadata(CreateDefaultClosingEasingFunction())
    );

    /// <summary>
    /// Gets or sets the easing applied to dialog scale during an exit. The backdrop and dialog
    /// opacity remain linear. Set this property to <see langword="null"/> for linear scale motion.
    /// </summary>
    public IEasingFunction? ClosingEasingFunction
    {
        get => (IEasingFunction?)GetValue(ClosingEasingFunctionProperty);
        set => SetValue(ClosingEasingFunctionProperty, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        // The presenter can be retemplated independently of its host. Resolve an interrupted
        // transition to its requested endpoint before releasing the waiter; the new surfaces must
        // match that completed transition even when Host.OnApplyTemplate is never called.
        if (_transitionCompletion is not null)
        {
            _stablePose = _isOpenTarget ? StablePose.Open : StablePose.Closed;
        }

        CancelActiveTransition();
        RestoreTemplateParts();

        base.OnApplyTemplate();

        _backdrop = GetTemplateChild(BackdropPartName) as UIElement;
        _dialogRoot = GetTemplateChild(DialogRootPartName) as UIElement;
        AttachDialogScale();
        ApplyStablePose();
    }

    /// <summary>
    /// Applies one synchronized transition frame to the template's visual surfaces.
    /// </summary>
    /// <param name="backdropOpacity">The backdrop opacity for this frame, from 0 through 1.</param>
    /// <param name="dialogOpacity">The dialog-surface opacity for this frame, from 0 through 1.</param>
    /// <param name="dialogScale">
    /// The uniform dialog scale for this frame. A custom easing function may temporarily produce a
    /// value beyond its two stable endpoints.
    /// </param>
    /// <remarks>
    /// Derived presenters may override this method to coordinate additional render-only effects.
    /// Call the base implementation to retain opacity and scale handling for
    /// <c>PART_Backdrop</c> and <c>PART_DialogRoot</c>. This method runs on the Dispatcher thread and
    /// may be called once per WPF timing tick, so implementations must avoid layout changes and
    /// allocations.
    /// </remarks>
    protected virtual void ApplyTransitionVisuals(
        double backdropOpacity,
        double dialogOpacity,
        double dialogScale)
    {
        _backdrop?.SetCurrentValue(OpacityProperty, backdropOpacity);
        _dialogRoot?.SetCurrentValue(OpacityProperty, dialogOpacity);

        if (_dialogScale is not null)
        {
            _dialogScale.SetCurrentValue(ScaleTransform.ScaleXProperty, dialogScale);
            _dialogScale.SetCurrentValue(ScaleTransform.ScaleYProperty, dialogScale);
        }
    }

    internal Task OpenAsync(bool useTransitions) =>
        TransitionToAsync(isOpen: true, useTransitions);

    internal Task CloseAsync(bool useTransitions) =>
        TransitionToAsync(isOpen: false, useTransitions);

    /// <summary>
    /// Redirects an entrance immediately when close wins the host's outcome arbitration. The
    /// presentation pipeline later receives and awaits the same exit task instead of restarting it.
    /// </summary>
    internal void RedirectToClosed(bool useTransitions)
    {
        _ = TransitionToAsync(isOpen: false, useTransitions);
    }

    /// <summary>
    /// Completes the currently requested transition synchronously. This policy is used when
    /// animation is disabled while a session remains otherwise valid.
    /// </summary>
    internal void CompleteTransition()
    {
        if (_transitionCompletion is null)
        {
            return;
        }

        CompleteActiveTransition(_isOpenTarget);
    }

    /// <summary>
    /// Removes every owned clock and prepares an invisible entrance pose. Forced host cleanup uses
    /// this path because no later dispatcher or timing-manager tick may be available.
    /// </summary>
    internal void Reset()
    {
        _isOpenTarget = false;
        CancelActiveTransition();
        _stablePose = StablePose.PreparedClosed;
        ApplyVisualValues(ClosedOpacity, ClosedOpacity, OpeningScale);
    }

    /// <summary>
    /// Synchronizes a newly applied host template without playing a transition.
    /// </summary>
    internal void SnapTo(bool isOpen)
    {
        _isOpenTarget = isOpen;
        CancelActiveTransition();
        _stablePose = isOpen ? StablePose.Open : StablePose.PreparedClosed;
        ApplyStablePose();
    }

    private Task TransitionToAsync(bool isOpen, bool useTransitions)
    {
        VerifyAccess();

        if (_transitionCompletion is not null && _isOpenTarget == isOpen)
        {
            return _transitionCompletion.Task;
        }

        if (_transitionCompletion is null && IsAtStableTarget(isOpen))
        {
            return Task.CompletedTask;
        }

        // The visual fields are updated on every progress invalidation. They remain valid after the
        // progress clock is removed and are therefore a deterministic origin for a reversal.
        var sourceBackdropOpacity = _currentBackdropOpacity;
        var sourceDialogOpacity   = _currentDialogOpacity;
        var sourceScale           = _currentScale;

        CancelActiveTransition();
        _isOpenTarget = isOpen;

        var targetBackdropOpacity = isOpen ? OpenOpacity : ClosedOpacity;
        var targetDialogOpacity   = isOpen ? OpenOpacity : ClosedOpacity;
        var targetScale           = isOpen ? OpenScale : ClosingScale;
        var fullDuration          = isOpen ? OpenDuration : CloseDuration;
        var duration = ScaleDuration(
            fullDuration,
            GetRemainingDistanceRatio(
                isOpen,
                sourceBackdropOpacity,
                sourceDialogOpacity,
                sourceScale
            )
        );

        if (!useTransitions || duration <= TimeSpan.Zero || !HasAnimationSurface())
        {
            SetStableTarget(isOpen);
            return Task.CompletedTask;
        }

        _startBackdropOpacity = sourceBackdropOpacity;
        _startDialogOpacity = sourceDialogOpacity;
        _startScale = sourceScale;
        _targetBackdropOpacity = targetBackdropOpacity;
        _targetDialogOpacity = targetDialogOpacity;
        _targetScale = targetScale;
        _activeEasingFunction = CloneEasingFunction(
            isOpen ? OpeningEasingFunction : ClosingEasingFunction
        );

        var version = ++_transitionVersion;
        var completion = _transitionCompletion =
            new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation(
            0.0d,
            1.0d,
            new Duration(duration),
            FillBehavior.Stop
        );
        var clock = _animationClock = animation.CreateClock();

        _clockCompletedHandler = (_, _) =>
        {
            if (version == _transitionVersion && ReferenceEquals(_animationClock, clock))
            {
                CompleteActiveTransition(isOpen);
            }
        };
        clock.Completed += _clockCompletedHandler;

        SetTransitionProgressBase(0.0d);
        ApplyAnimationClock(
            TransitionProgressProperty,
            clock,
            HandoffBehavior.SnapshotAndReplace
        );

        return completion.Task;
    }

    private void CompleteActiveTransition(bool isOpen)
    {
        var completion = _transitionCompletion;
        RemoveAnimationClock();
        _transitionCompletion = null;
        SetStableTarget(isOpen);
        completion?.TrySetResult(null);
    }

    private void CancelActiveTransition()
    {
        var completion = _transitionCompletion;
        _transitionVersion++;
        RemoveAnimationClock();
        _transitionCompletion = null;
        completion?.TrySetResult(null);
    }

    private void RemoveAnimationClock()
    {
        if (_animationClock is null)
        {
            return;
        }

        if (_clockCompletedHandler is not null)
        {
            _animationClock.Completed -= _clockCompletedHandler;
        }

        var clock = _animationClock;
        _animationClock = null;
        _clockCompletedHandler = null;

        // ApplyAnimationClock(null) removes animation storage synchronously. ClockController.Remove
        // only changes timing state on the next tick, so it cannot be the operation that restores a
        // stable property value during unload or Dispatcher shutdown.
        _suppressProgressUpdates = true;
        try
        {
            ApplyAnimationClock(TransitionProgressProperty, null);
            SetCurrentValue(TransitionProgressProperty, 0.0d);
        }
        finally
        {
            _suppressProgressUpdates = false;
        }

        clock.Controller?.Remove();
    }

    private void SetTransitionProgressBase(double progress)
    {
        _suppressProgressUpdates = true;
        try
        {
            SetCurrentValue(TransitionProgressProperty, progress);
        }
        finally
        {
            _suppressProgressUpdates = false;
        }
    }

    private void ApplyTransitionProgress(double progress)
    {
        progress = Clamp(progress, 0.0d, 1.0d);

        var scaleProgress = progress;
        if (_activeEasingFunction is not null)
        {
            var easedProgress = _activeEasingFunction.Ease(progress);
            if (!double.IsNaN(easedProgress) && !double.IsInfinity(easedProgress))
            {
                scaleProgress = easedProgress;
            }
        }

        ApplyVisualValues(
            Lerp(_startBackdropOpacity, _targetBackdropOpacity, progress),
            Lerp(_startDialogOpacity, _targetDialogOpacity, progress),
            Lerp(_startScale, _targetScale, scaleProgress)
        );
    }

    private void SetStableTarget(bool isOpen)
    {
        _stablePose = isOpen ? StablePose.Open : StablePose.Closed;
        ApplyStablePose();
    }

    private void ApplyStablePose()
    {
        switch (_stablePose)
        {
            case StablePose.Open:
                ApplyVisualValues(OpenOpacity, OpenOpacity, OpenScale);
                break;
            case StablePose.Closed:
                ApplyVisualValues(ClosedOpacity, ClosedOpacity, ClosingScale);
                break;
            default:
                ApplyVisualValues(ClosedOpacity, ClosedOpacity, OpeningScale);
                break;
        }
    }

    private void ApplyVisualValues(
        double backdropOpacity,
        double dialogOpacity,
        double scale)
    {
        _currentBackdropOpacity = backdropOpacity;
        _currentDialogOpacity = dialogOpacity;
        _currentScale = scale;
        ApplyTransitionVisuals(backdropOpacity, dialogOpacity, scale);
    }

    private void AttachDialogScale()
    {
        if (_dialogRoot is null)
        {
            return;
        }

        _originalDialogTransform = _dialogRoot.RenderTransform;
        _dialogScale = new ScaleTransform(OpenScale, OpenScale);

        if (_originalDialogTransform is null ||
            ReferenceEquals(_originalDialogTransform, Transform.Identity))
        {
            _dialogRoot.SetCurrentValue(RenderTransformProperty, _dialogScale);
            return;
        }

        // The private scale is appended, rather than replacing the template transform, so custom
        // templates may still rotate, translate, or otherwise decorate the dialog surface.
        var transforms = new TransformGroup();
        transforms.Children.Add(_originalDialogTransform);
        transforms.Children.Add(_dialogScale);
        _dialogRoot.SetCurrentValue(RenderTransformProperty, transforms);
    }

    private void RestoreTemplateParts()
    {
        if (_dialogRoot is not null && _dialogScale is not null)
        {
            _dialogRoot.SetCurrentValue(
                RenderTransformProperty,
                _originalDialogTransform ?? Transform.Identity
            );
        }

        _backdrop = null;
        _dialogRoot = null;
        _originalDialogTransform = null;
        _dialogScale = null;
    }

    private bool HasAnimationSurface() =>
        _backdrop is not null || _dialogRoot is not null;

    private bool IsAtStableTarget(bool isOpen) =>
        isOpen ? _stablePose == StablePose.Open : _stablePose != StablePose.Open;

    private double GetRemainingDistanceRatio(
        bool   isOpen,
        double backdropOpacity,
        double dialogOpacity,
        double scale)
    {
        var targetOpacity = isOpen ? OpenOpacity : ClosedOpacity;
        var targetScale   = isOpen ? OpenScale : ClosingScale;
        var fullScaleDistance = Math.Abs(
            OpenScale - (isOpen ? OpeningScale : ClosingScale)
        );
        var scaleRatio = fullScaleDistance <= double.Epsilon
            ? 0.0d
            : Math.Abs(targetScale - scale) / fullScaleDistance;

        return Clamp(
            Math.Max(
                Math.Max(
                    Math.Abs(targetOpacity - backdropOpacity),
                    Math.Abs(targetOpacity - dialogOpacity)
                ),
                scaleRatio
            ),
            0.0d,
            1.0d
        );
    }

    private void OnPresenterUnloaded(object sender, RoutedEventArgs e)
    {
        // An unloaded presenter may never receive another timing tick. Resolve any waiter and
        // retain a deterministic pose so reattaching the same instance does not expose a held clock.
        if (_transitionCompletion is not null)
        {
            CompleteActiveTransition(_isOpenTarget);
        }
    }

    private static void OnTransitionProgressChanged(
        DependencyObject                   d,
        DependencyPropertyChangedEventArgs e)
    {
        var presenter = (ContentDialogTransitionPresenter)d;
        if (!presenter._suppressProgressUpdates)
        {
            presenter.ApplyTransitionProgress((double)e.NewValue);
        }
    }

    private static void OnScaleChanged(
        DependencyObject                   d,
        DependencyPropertyChangedEventArgs e)
    {
        var presenter = (ContentDialogTransitionPresenter)d;
        if (presenter._transitionCompletion is null)
        {
            presenter.ApplyStablePose();
        }
    }

    private static bool IsValidDuration(object? value) =>
        value is TimeSpan duration && duration >= TimeSpan.Zero;

    private static bool IsValidScale(object? value) =>
        value is double scale && scale > 0.0d &&
        !double.IsNaN(scale) && !double.IsInfinity(scale);

    private static TimeSpan ScaleDuration(TimeSpan duration, double distanceRatio)
    {
        if (duration <= TimeSpan.Zero || distanceRatio <= 0.0d)
        {
            return TimeSpan.Zero;
        }

        var ratio = Math.Min(1.0d, distanceRatio);
        return TimeSpan.FromTicks(Math.Max(1L, (long)(duration.Ticks * ratio)));
    }

    private static IEasingFunction? CloneEasingFunction(IEasingFunction? easingFunction)
    {
        // Easing functions are usually Freezables and may be backed by DynamicResource. Snapshotting
        // one transition prevents a mid-flight resource mutation from changing its interpolation.
        return easingFunction is Freezable freezable
            ? (IEasingFunction)freezable.CloneCurrentValue()
            : easingFunction;
    }

    private static IEasingFunction CreateDefaultOpeningEasingFunction()
    {
        var easing = new BackEase
        {
            Amplitude = 0.35d,
            EasingMode = EasingMode.EaseOut,
        };
        easing.Freeze();
        return easing;
    }

    private static IEasingFunction CreateDefaultClosingEasingFunction()
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        easing.Freeze();
        return easing;
    }

    private static double Lerp(double from, double to, double progress) =>
        from + ((to - from) * progress);

    private static double Clamp(double value, double minimum, double maximum) =>
        Math.Max(minimum, Math.Min(maximum, value));
}
