// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media.Animation;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Describes how <see cref="PopupSlideAnimator"/> shortens WPF's native Slide opening animation.
/// </summary>
/// <remarks>
/// These settings affect only the native vertical Slide clock. WPF continues to determine the actual direction,
/// placement, clipping, opacity, and popup-window lifecycle.
/// </remarks>
public sealed class PopupAnimationSettings : Freezable
{
    private static readonly IEasingFunction DefaultEasingFunction = CreateDefaultEasingFunction();

    /// <summary>
    /// Identifies the <see cref="StartOffset"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty StartOffsetProperty = DependencyProperty.Register(
        nameof(StartOffset),
        typeof(double),
        typeof(PopupAnimationSettings),
        new PropertyMetadata(PopupSlideAnimator.MaximumSlideDistance),
        value => value is double doubleValue && IsValidDouble(doubleValue) && doubleValue >= 0D
    );

    /// <summary>
    /// Identifies the <see cref="Duration"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
        nameof(Duration),
        typeof(double),
        typeof(PopupAnimationSettings),
        new PropertyMetadata(167D),
        value => value is double doubleValue && IsValidDouble(doubleValue) && doubleValue >= 0D
    );

    /// <summary>
    /// Identifies the <see cref="EasingFunction"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty EasingFunctionProperty = DependencyProperty.Register(
        nameof(EasingFunction),
        typeof(IEasingFunction),
        typeof(PopupAnimationSettings),
        new PropertyMetadata(DefaultEasingFunction),
        value => value is IEasingFunction
    );

    private static readonly PopupAnimationSettings DefaultInstance = CreateFrozenDefault();

    /// <summary>
    /// Gets the frozen Wpf.Zen.Ui default popup opening animation settings.
    /// </summary>
    /// <remarks>
    /// Use this shared instance when the settings do not need to be modified. Use <see cref="CreateDefault"/> when
    /// per-popup values need to be changed.
    /// </remarks>
    public static PopupAnimationSettings Default
    {
        get { return DefaultInstance; }
    }

    /// <summary>
    /// Gets or sets the requested Slide travel distance, in logical pixels.
    /// </summary>
    /// <remarks>
    /// The default is 90. The effective distance is also limited by the native full-height offset and
    /// <see cref="PopupSlideAnimator.MaximumSlideDistance"/>. WPF's native clock determines direction, so this value
    /// is non-negative and does not encode placement direction.
    /// </remarks>
    public double StartOffset
    {
        get { return (double)GetValue(StartOffsetProperty); }
        set { SetValue(StartOffsetProperty, value); }
    }

    /// <summary>
    /// Gets or sets the opening animation duration, in milliseconds.
    /// </summary>
    /// <remarks>The default is 167 milliseconds. A value of zero completes the native Slide immediately.</remarks>
    public double Duration
    {
        get { return (double)GetValue(DurationProperty); }
        set { SetValue(DurationProperty, value); }
    }

    /// <summary>
    /// Gets or sets the easing function applied to the shortened vertical Slide clock.
    /// </summary>
    /// <remarks>
    /// The default is a frozen <see cref="CircleEase"/> configured with <see cref="EasingMode.EaseOut"/>. The
    /// animator clones a mutable easing object before assigning it to a new clock, so a bound or shared settings
    /// object is never frozen or otherwise mutated as a side effect of opening a Popup.
    /// </remarks>
    public IEasingFunction EasingFunction
    {
        get { return (IEasingFunction)GetValue(EasingFunctionProperty); }
        set { SetValue(EasingFunctionProperty, value); }
    }

    /// <summary>
    /// Creates a mutable copy of the Wpf.Zen.Ui default popup opening animation settings.
    /// </summary>
    /// <returns>A new <see cref="PopupAnimationSettings"/> instance configured with Wpf.Zen.Ui default values.</returns>
    public static PopupAnimationSettings CreateDefault()
    {
        return new PopupAnimationSettings();
    }

    /// <inheritdoc />
    protected override Freezable CreateInstanceCore()
    {
        return new PopupAnimationSettings();
    }

    internal TimeSpan GetDuration()
    {
        return TimeSpan.FromMilliseconds(Duration);
    }

    internal IEasingFunction GetEasingFunction()
    {
        // AnimationTimeline.Freeze may recursively freeze its easing function. Clone Freezable implementations so
        // callers can continue changing a shared or data-bound settings object after an opening has completed.
        if (EasingFunction is Freezable freezable)
        {
            return (IEasingFunction)freezable.CloneCurrentValue();
        }

        return EasingFunction;
    }

    private static PopupAnimationSettings CreateFrozenDefault()
    {
        var settings = CreateDefault();
        settings.Freeze();

        return settings;
    }

    private static IEasingFunction CreateDefaultEasingFunction()
    {
        var easingFunction = new CircleEase
        {
            EasingMode = EasingMode.EaseOut,
        };

        easingFunction.Freeze();
        return easingFunction;
    }

    private static bool IsValidDouble(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
