// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media.Animation;

namespace Wpf.Ui.Animations;

/// <summary>
/// A double animation that preserves easing continuity when interrupted.
/// <para/>
/// When a new animation replaces a currently running one, this animation
/// automatically reconstructs the correct easing timeline position from the
/// current animated value, ensuring visually continuous motion speed and
/// easing rhythm.
/// </summary>
/// <remarks>
/// Only monotonic easing functions are supported.
/// Non-monotonic easing functions such as BounceEase and ElasticEase
/// are rejected because they are not mathematically invertible.
/// </remarks>
public class InterruptibleDoubleAnimation : DoubleAnimationBase
{
    #region Dependency Properties

    /// <summary>Identifies the <see cref="From"/> dependency property.</summary>
    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register(
            nameof(From),
            typeof(double),
            typeof(InterruptibleDoubleAnimation)
        );

    /// <summary>Identifies the <see cref="To"/> dependency property.</summary>
    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register(
            nameof(To),
            typeof(double),
            typeof(InterruptibleDoubleAnimation)
        );

    /// <summary>Identifies the <see cref="EasingFunction"/> dependency property.</summary>
    public static readonly DependencyProperty EasingFunctionProperty =
        DependencyProperty.Register(
            nameof(EasingFunction),
            typeof(IEasingFunction),
            typeof(InterruptibleDoubleAnimation),
            new PropertyMetadata(
                null,
                null,
                CoerceEasingFunction
            )
        );

    #endregion

    #region Properties

    public double From
    {
        get => (double)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public double To
    {
        get => (double)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction
    {
        get => (IEasingFunction?)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    #endregion

    #region Animation

    protected override double GetCurrentValueCore(double defaultOriginValue, double defaultDestinationValue, AnimationClock animationClock)
    {
        if (animationClock is null)
        {
            throw new ArgumentNullException(nameof(animationClock));
        }

        var progress = animationClock.CurrentProgress;

        if (progress is null)
            return defaultOriginValue;

        var from = From;
        var to   = To;

        var displacement = to - from;

        if (displacement == 0)
            return to;

        // Current displayed value from previous animation.
        // Reconstruct easing fraction.
        var currentFraction = Clamp((defaultOriginValue - from) / displacement, 0.0, 1.0);

        // Convert easing output back into normalized time.
        var currentTime = InverseEasingFunction(currentFraction);

        // Advance from current time at original speed.
        // Clamp so we never overshoot the end.
        var continuedTime = Math.Min(currentTime + progress.Value, 1.0);

        double easedProgress;

        if (EasingFunction is null)
        {
            easedProgress = continuedTime;
        }
        else
        {
            easedProgress = EasingFunction.Ease(continuedTime);
        }

        return from + (displacement * easedProgress);
    }

    #endregion

    #region Easing Validation

    private static object? CoerceEasingFunction(DependencyObject d, object? baseValue)
    {
        if (baseValue is null)
            return null;

        if (baseValue is not IEasingFunction easing)
        {
            throw new InvalidOperationException(
                "EasingFunction must implement IEasingFunction."
            );
        }

        if (!IsSupportedEasing(easing))
        {
            throw new InvalidOperationException(
                $"{nameof(InterruptibleDoubleAnimation)} only supports " +
                "monotonic easing functions."
            );
        }

        return baseValue;
    }

    private static bool IsSupportedEasing(IEasingFunction easing)
    {
        return easing switch
        {
            BounceEase      => false,
            ElasticEase     => false,
            BackEase        => true,
            CircleEase      => true,
            CubicEase       => true,
            ExponentialEase => true,
            PowerEase       => true,
            QuadraticEase   => true,
            QuarticEase     => true,
            QuinticEase     => true,
            SineEase        => true,
            _               => false
        };
    }

    #endregion

    #region Inverse Easing

    private double InverseEasingFunction(double fraction)
    {
        if (fraction <= 0) return 0;
        if (fraction >= 1) return 1;

        if (EasingFunction is null) return fraction;

        var low  = 0d;
        var high = 1d;

        const int iterations = 24;

        for (var i = 0; i < iterations; i++)
        {
            var mid   = (low + high) * 0.5;
            var value = EasingFunction.Ease(mid);

            if (value < fraction)
                low = mid;
            else
                high = mid;
        }

        return (low + high) * 0.5;
    }

    #endregion

    #region Helpers

    private static double Clamp(double value, double min, double max)
    {
        return value < min ? min : value > max ? max : value;
    }

    #endregion

    #region Instance

    protected override Freezable CreateInstanceCore()
    {
        return new InterruptibleDoubleAnimation();
    }

    #endregion
}