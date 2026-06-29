// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media.Animation;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Describes the opening animation applied by <see cref="PopupAnimationBehavior"/>.
/// </summary>
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
        new PropertyMetadata(-90D),
        value => value is double doubleValue && IsValidDouble(doubleValue)
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

    /// <summary>
    /// Gets or sets the starting vertical offset, in logical pixels, for downward popup placement.
    /// </summary>
    public double StartOffset
    {
        get { return (double)GetValue(StartOffsetProperty); }
        set { SetValue(StartOffsetProperty, value); }
    }

    /// <summary>
    /// Gets or sets the opening animation duration, in milliseconds.
    /// </summary>
    public double Duration
    {
        get { return (double)GetValue(DurationProperty); }
        set { SetValue(DurationProperty, value); }
    }

    /// <summary>
    /// Gets or sets the easing function used by the opening animation.
    /// </summary>
    public IEasingFunction EasingFunction
    {
        get { return (IEasingFunction)GetValue(EasingFunctionProperty); }
        set { SetValue(EasingFunctionProperty, value); }
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
        if (EasingFunction is Freezable freezable)
        {
            return (IEasingFunction)freezable.CloneCurrentValue();
        }

        return EasingFunction;
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
