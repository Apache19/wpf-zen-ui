// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Owns one modal presentation scope, its backdrop, input isolation and transitions.</summary>
/// <remarks>
/// Place last in a finite-sized parent panel, alongside the content to cover. Only ContentDialog
/// content is accepted. ShowAsync rejects a busy host. Direct Content replacement is an external
/// removal and immediately completes the old dialog with None.
/// PART_TransitionRoot is an optional FrameworkElement wrapping only the dialog presenter.
/// The host owns that part's Opacity and RenderTransform, never the dialog's transforms.
/// PART_Backdrop is an optional sibling FrameworkElement covering the presentation scope; the host
/// owns its Opacity. It must not contain the dialog, or their opacity animations would multiply.
/// Each part animates independently when present. Without either part, with animations disabled,
/// or when detached, transitions complete immediately. The template must retain hit testing
/// throughout the fade so pointer input cannot reach the covered content before cleanup.
/// </remarks>
[TemplatePart(Name = "PART_TransitionRoot", Type = typeof(FrameworkElement))]
[TemplatePart(Name = "PART_Backdrop", Type = typeof(FrameworkElement))]
public class ContentDialogHost : ContentControl
{
    private readonly ContentDialogHostController _controller;
    private FrameworkElement? _transitionRoot;
    private FrameworkElement? _backdrop;
    private ScaleTransform? _scale;
    private Action? _finishTransition;

    #region DP: IsDisableSiblingsEnabled

    /// <summary>Identifies the <see cref="IsDisableSiblingsEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsDisableSiblingsEnabledProperty = DependencyProperty.Register(
        nameof(IsDisableSiblingsEnabled),
        typeof(bool),
        typeof(ContentDialogHost),
        new PropertyMetadata(true, OnIsDisableSiblingsEnabledChanged)
    );

    /// <summary>Gets or sets a value indicating whether application siblings are disabled during presentation. Default true. Changing it updates input isolation immediately; no layout or inheritance effect.</summary>
    public bool IsDisableSiblingsEnabled
    {
        get => (bool)GetValue(IsDisableSiblingsEnabledProperty);
        set => SetValue(IsDisableSiblingsEnabledProperty, value);
    }

    #endregion

    #region DP: IsAnimationEnabled

    /// <summary>Identifies the <see cref="IsAnimationEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsAnimationEnabledProperty = DependencyProperty.Register(
        nameof(IsAnimationEnabled),
        typeof(bool),
        typeof(ContentDialogHost),
        new PropertyMetadata(true, OnAnimationSettingChanged)
    );

    /// <summary>Gets or sets a value indicating whether host transitions are enabled. Default true; system client-area animation settings are also honored. Turning it off finishes an active transition immediately.</summary>
    public bool IsAnimationEnabled
    {
        get => (bool)GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    #endregion

    #region DP: TransitionDuration

    /// <summary>Identifies the <see cref="TransitionDuration"/> dependency property.</summary>
    public static readonly DependencyProperty TransitionDurationProperty = DependencyProperty.Register(
        nameof(TransitionDuration),
        typeof(TimeSpan),
        typeof(ContentDialogHost),
        new PropertyMetadata(TimeSpan.FromMilliseconds(220), OnAnimationSettingChanged),
        value => (TimeSpan)value >= TimeSpan.Zero
    );

    /// <summary>Gets or sets the transition duration. Default 220 ms; zero finishes immediately. Negative durations are rejected. Changing it finishes the current transition.</summary>
    public TimeSpan TransitionDuration
    {
        get => (TimeSpan)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    #endregion

    static ContentDialogHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ContentDialogHost), new FrameworkPropertyMetadata(typeof(ContentDialogHost)));
        ContentProperty.OverrideMetadata(typeof(ContentDialogHost), new FrameworkPropertyMetadata(null, null, CoerceContent));
    }

    /// <summary>Initializes a new instance of the <see cref="ContentDialogHost"/> class. Input isolation begins when content is attached.</summary>
    public ContentDialogHost()
    {
        _controller = new ContentDialogHostController(this) { IsDisableSiblingsEnabled = true };
        Loaded += (_, _) =>
        {
            if (Content is ContentDialog)
                _controller.HandleDialogAdded();
        };
        Unloaded += (_, _) =>
        {
            // Unloaded cannot be vetoed. Release animation waiters and modal state even when
            // WPF retains Content while removing the host's entire presentation source.
            (Content as ContentDialog)?.NotifyHostRemoved();
            FinishTransition();
            _controller.HandleDialogRemoved();
        };
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                (Content as ContentDialog)?.NotifyHostRemoved();
                FinishTransition();
            }
        };
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        FinishTransition();
        base.OnApplyTemplate();
        _transitionRoot = GetTemplateChild("PART_TransitionRoot") as FrameworkElement;
        _backdrop = GetTemplateChild("PART_Backdrop") as FrameworkElement;
        _backdrop?.SetCurrentValue(OpacityProperty, 1d);
        _scale = null;
        if (_transitionRoot is not null)
        {
            _scale = new ScaleTransform(1, 1);
            _transitionRoot.SetCurrentValue(OpacityProperty, 1d);
            _transitionRoot.SetCurrentValue(RenderTransformOriginProperty, new Point(.5, .5));
            _transitionRoot.SetCurrentValue(RenderTransformProperty, _scale);
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        // An obsolete animation tree may never tick again, so finish its waiter before replacing it.
        FinishTransition();
        _transitionRoot = null;
        _backdrop = null;
        _scale = null;
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <inheritdoc />
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        FinishTransition();
        _transitionRoot?.SetCurrentValue(OpacityProperty, 1d);
        _backdrop?.SetCurrentValue(OpacityProperty, 1d);
        _scale?.SetCurrentValue(ScaleTransform.ScaleXProperty, 1d);
        _scale?.SetCurrentValue(ScaleTransform.ScaleYProperty, 1d);
        if (oldContent is ContentDialog oldDialog && !ReferenceEquals(oldContent, newContent))
            oldDialog.NotifyHostRemoved();

        if (oldContent is null && newContent is not null)
            _controller.HandleDialogAdded();
        try
        {
            base.OnContentChanged(oldContent, newContent);
        }
        finally
        {
            if (newContent is null)
                _controller.HandleDialogRemoved();
        }
    }

    internal Task OpenDialogAsync(ContentDialog dialog)
    {
        ApplyTemplate();
        return ReferenceEquals(Content, dialog) ? AnimateAsync(true) : Task.CompletedTask;
    }

    internal Task CloseDialogAsync(ContentDialog dialog) =>
        ReferenceEquals(Content, dialog) ? AnimateAsync(false) : Task.CompletedTask;

    private Task AnimateAsync(bool opening)
    {
        // Capture both current opacities before finishing an interrupted entrance. A close request
        // must fade from what is actually visible, not jump to the entrance's completed pose.
        // Consumer properties remain untouched; SnapshotAndReplace prevents accumulating clocks.
        double fromOpacity = opening ? 0 : _transitionRoot?.Opacity ?? 1;
        double fromBackdropOpacity = opening ? 0 : _backdrop?.Opacity ?? 1;
        double fromScale = opening ? .94 : _scale?.ScaleX ?? 1;
        FinishTransition();
        var root = _transitionRoot;
        var scale = _scale;
        var backdrop = _backdrop;
        double endOpacity = opening ? 1 : 0;
        double endScale = opening ? 1 : .97;

        // Use the same final pose for normal completion and disabled/interrupted transitions.
        // This also supports custom templates that supply just one of the optional parts.
        void SetFinalPose()
        {
            root?.SetCurrentValue(OpacityProperty, endOpacity);
            backdrop?.SetCurrentValue(OpacityProperty, endOpacity);
            scale?.SetCurrentValue(ScaleTransform.ScaleXProperty, endScale);
            scale?.SetCurrentValue(ScaleTransform.ScaleYProperty, endScale);
        }

        if ((root is null && backdrop is null) || !IsLoaded || !IsVisible
            || !IsAnimationEnabled || !SystemParameters.ClientAreaAnimation || TransitionDuration == TimeSpan.Zero)
        {
            SetFinalPose();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var opacity = new DoubleAnimation(fromOpacity, endOpacity, new Duration(TransitionDuration));
        var backdropOpacity = new DoubleAnimation(fromBackdropOpacity, endOpacity, new Duration(TransitionDuration));
        var size = new DoubleAnimation(fromScale, endScale, new Duration(TransitionDuration))
        {
            EasingFunction = opening
                ? new BackEase { Amplitude = .35, EasingMode = EasingMode.EaseOut }
                : new CubicEase { EasingMode = EasingMode.EaseIn },
        };
        bool completed = false;
        void Complete()
        {
            if (completed)
                return;
            completed = true;
            _finishTransition = null;
            // Keep the final exit pose until Content is removed; restoring opacity here could
            // flash the dialog between animation completion and the asynchronous close continuation.
            // Clearing clocks prevents them from later mutating a reused host.
            root?.BeginAnimation(OpacityProperty, null);
            backdrop?.BeginAnimation(OpacityProperty, null);
            scale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            SetFinalPose();
            completion.TrySetResult(true);
        }

        _finishTransition = Complete;
        // All clocks have the same duration. One completion releases the shared presentation
        // waiter and removes every clock, including when only the backdrop exists in a template.
        (root is not null ? opacity : backdropOpacity).Completed += (_, _) => Complete();
        root?.BeginAnimation(OpacityProperty, opacity);
        backdrop?.BeginAnimation(OpacityProperty, backdropOpacity);
        scale?.BeginAnimation(ScaleTransform.ScaleXProperty, size);
        scale?.BeginAnimation(ScaleTransform.ScaleYProperty, size);
        return completion.Task;
    }

    private void FinishTransition() => _finishTransition?.Invoke();

    private static object? CoerceContent(DependencyObject d, object? value)
    {
        if (value is not null and not ContentDialog)
            throw new ArgumentException("ContentDialogHost only accepts ContentDialog content.");
        return value;
    }

    private static void OnIsDisableSiblingsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ContentDialogHost)d)._controller.IsDisableSiblingsEnabled = (bool)e.NewValue;

    private static void OnAnimationSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ContentDialogHost)d).FinishTransition();
}
