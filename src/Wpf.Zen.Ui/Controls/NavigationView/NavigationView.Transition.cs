// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media.Animation;
using Wpf.Zen.Ui.Hardware;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <content>
/// Implements entrance transitions for content presented by <see cref="NavigationView"/>.
/// </content>
public partial class NavigationView
{
    private const double NavigationTransitionDecelerationRatio = 0.7D;
    private const int MinimumNavigationTransitionDuration = 10;
    private const int MaximumNavigationTransitionDuration = 10_000;

    /// <summary>Applies a navigation entrance transition to the control-owned frame.</summary>
    /// <remarks>
    /// The frame is the stable presentation surface across navigations. Animating it avoids
    /// replacing or retaining animation clocks on consumer-owned <see cref="UIElement.Opacity"/>
    /// and <see cref="UIElement.RenderTransform"/> properties.
    ///
    /// Existing clocks are removed before the new policy is evaluated because the frame survives
    /// content changes. Without this reset, a rapid navigation configured with
    /// <see cref="NavigationTransition.None"/> could continue the preceding destination's
    /// transition and visually animate content that explicitly opted out.
    /// </remarks>
    private void ApplyTransitionEffectToNavigationFrame(
        NavigationTransitionSettings transitionSettings)
    {
        ResetNavigationFrameTransition();

        if (
            transitionSettings.Transition == NavigationTransition.None
            || transitionSettings.Duration < MinimumNavigationTransitionDuration
            || !HardwareAcceleration.IsSupported(RenderingTier.PartialAcceleration)
        )
        {
            return;
        }

        var duration = new Duration(
            TimeSpan.FromMilliseconds(
                Math.Min(transitionSettings.Duration, MaximumNavigationTransitionDuration)
            )
        );

        switch (transitionSettings.Transition)
        {
            case NavigationTransition.FadeIn:
                BeginNavigationFrameOpacityTransition(duration);
                break;

            case NavigationTransition.FadeInWithSlide:
                BeginNavigationFrameTranslation(TranslateTransform.YProperty, 30, duration);
                BeginNavigationFrameOpacityTransition(duration);
                break;

            case NavigationTransition.SlideBottom:
                BeginNavigationFrameTranslation(TranslateTransform.YProperty, 30, duration);
                break;

            case NavigationTransition.SlideRight:
                BeginNavigationFrameTranslation(TranslateTransform.XProperty, 50, duration);
                break;

            case NavigationTransition.SlideLeft:
                BeginNavigationFrameTranslation(TranslateTransform.XProperty, -50, duration);
                break;
        }
    }

    /// <summary>Removes clocks left on the stable frame by the preceding navigation.</summary>
    private void ResetNavigationFrameTransition()
    {
        _navigationFrame.BeginAnimation(UIElement.OpacityProperty, null);

        if (_navigationFrame.RenderTransform is TranslateTransform translateTransform)
        {
            translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, null);
        }
    }

    /// <summary>Starts the opacity component of a navigation entrance transition.</summary>
    private void BeginNavigationFrameOpacityTransition(Duration duration)
    {
        _navigationFrame.BeginAnimation(
            UIElement.OpacityProperty,
            CreateNavigationFrameAnimation(0, 1, duration)
        );
    }

    /// <summary>Starts one translation component on the control-owned frame.</summary>
    /// <remarks>
    /// A dedicated transform can safely replace the frame's transform because the frame is created
    /// and owned entirely by <see cref="NavigationView"/>. Application content is hosted beneath
    /// that frame and retains its own transform independently.
    /// </remarks>
    private void BeginNavigationFrameTranslation(
        DependencyProperty property,
        double offset,
        Duration duration)
    {
        if (_navigationFrame.RenderTransform is not TranslateTransform translateTransform)
        {
            translateTransform = new TranslateTransform();
            _navigationFrame.SetCurrentValue(
                UIElement.RenderTransformProperty,
                translateTransform
            );
        }

        translateTransform.BeginAnimation(
            property,
            CreateNavigationFrameAnimation(offset, 0, duration)
        );
    }

    /// <summary>Creates the shared timing curve used by built-in navigation transitions.</summary>
    private static DoubleAnimation CreateNavigationFrameAnimation(
        double from,
        double to,
        Duration duration)
    {
        return new DoubleAnimation
        {
            Duration = duration,
            DecelerationRatio = NavigationTransitionDecelerationRatio,
            From = from,
            To = to,
        };
    }
}