// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media.Animation;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Coordinates the dialog lifecycle with the template-owned transition presenter.</summary>
public partial class ContentDialogHost
{
    /// <summary>Identifies the <see cref="OpenDuration"/> dependency property.</summary>
    public static readonly DependencyProperty OpenDurationProperty =
        ContentDialogTransitionPresenter.OpenDurationProperty.AddOwner(typeof(ContentDialogHost));

    /// <summary>
    /// Gets or sets the duration of a complete dialog entrance. The default is 167 milliseconds.
    /// A zero duration completes synchronously. A redirected transition is shortened according to
    /// the distance remaining from the currently rendered pose.
    /// </summary>
    public TimeSpan OpenDuration
    {
        get => (TimeSpan)GetValue(OpenDurationProperty);
        set => SetValue(OpenDurationProperty, value);
    }

    /// <summary>Identifies the <see cref="CloseDuration"/> dependency property.</summary>
    public static readonly DependencyProperty CloseDurationProperty =
        ContentDialogTransitionPresenter.CloseDurationProperty.AddOwner(typeof(ContentDialogHost));

    /// <summary>
    /// Gets or sets the duration of a complete dialog exit. The default is 167 milliseconds.
    /// A zero duration completes synchronously.
    /// </summary>
    public TimeSpan CloseDuration
    {
        get => (TimeSpan)GetValue(CloseDurationProperty);
        set => SetValue(CloseDurationProperty, value);
    }

    /// <summary>Identifies the <see cref="OpeningScale"/> dependency property.</summary>
    public static readonly DependencyProperty OpeningScaleProperty =
        ContentDialogTransitionPresenter.OpeningScaleProperty.AddOwner(typeof(ContentDialogHost));

    /// <summary>
    /// Gets or sets the dialog scale used at the beginning of an entrance. The default is 0.94.
    /// This affects rendering only and does not change dialog layout.
    /// </summary>
    public double OpeningScale
    {
        get => (double)GetValue(OpeningScaleProperty);
        set => SetValue(OpeningScaleProperty, value);
    }

    /// <summary>Identifies the <see cref="ClosingScale"/> dependency property.</summary>
    public static readonly DependencyProperty ClosingScaleProperty =
        ContentDialogTransitionPresenter.ClosingScaleProperty.AddOwner(typeof(ContentDialogHost));

    /// <summary>
    /// Gets or sets the dialog scale reached at the end of an exit. The default is 0.97.
    /// This affects rendering only and does not change dialog layout.
    /// </summary>
    public double ClosingScale
    {
        get => (double)GetValue(ClosingScaleProperty);
        set => SetValue(ClosingScaleProperty, value);
    }

    /// <summary>Identifies the <see cref="OpeningEasingFunction"/> dependency property.</summary>
    public static readonly DependencyProperty OpeningEasingFunctionProperty =
        ContentDialogTransitionPresenter.OpeningEasingFunctionProperty.AddOwner(
            typeof(ContentDialogHost)
        );

    /// <summary>
    /// Gets or sets the easing applied to dialog scale during an entrance. Backdrop and dialog
    /// opacity remain linear. Set this property to <see langword="null"/> for linear scale motion.
    /// </summary>
    public IEasingFunction? OpeningEasingFunction
    {
        get => (IEasingFunction?)GetValue(OpeningEasingFunctionProperty);
        set => SetValue(OpeningEasingFunctionProperty, value);
    }

    /// <summary>Identifies the <see cref="ClosingEasingFunction"/> dependency property.</summary>
    public static readonly DependencyProperty ClosingEasingFunctionProperty =
        ContentDialogTransitionPresenter.ClosingEasingFunctionProperty.AddOwner(
            typeof(ContentDialogHost)
        );

    /// <summary>
    /// Gets or sets the easing applied to dialog scale during an exit. Backdrop and dialog opacity
    /// remain linear. Set this property to <see langword="null"/> for linear scale motion.
    /// </summary>
    public IEasingFunction? ClosingEasingFunction
    {
        get => (IEasingFunction?)GetValue(ClosingEasingFunctionProperty);
        set => SetValue(ClosingEasingFunctionProperty, value);
    }

    private bool ShouldAnimateTransition =>
        IsAnimationEnabled &&
        SystemParameters.ClientAreaAnimation &&
        IsLoaded &&
        IsVisible;

    private Task OpenDialogAsync() =>
        _dialogPresenter?.OpenAsync(ShouldAnimateTransition) ?? Task.CompletedTask;

    private Task CloseDialogAsync() =>
        _dialogPresenter?.CloseAsync(ShouldAnimateTransition) ?? Task.CompletedTask;

    private void RedirectTransitionToClosed()
    {
        _dialogPresenter?.RedirectToClosed(ShouldAnimateTransition);
    }

    private void AbortTransition()
    {
        _dialogPresenter?.Reset();
    }

    private static void OnIsAnimationEnabledChanged(
        DependencyObject                   d,
        DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue)
        {
            ((ContentDialogHost)d)._dialogPresenter?.CompleteTransition();
        }
    }
}
