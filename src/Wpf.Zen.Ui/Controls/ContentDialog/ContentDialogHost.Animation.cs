// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media.Animation;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Runs presentation transitions on template parts without changing dialog transforms.</summary>
public partial class ContentDialogHost
{
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

    private static void OnAnimationSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ContentDialogHost)d).FinishTransition();
}