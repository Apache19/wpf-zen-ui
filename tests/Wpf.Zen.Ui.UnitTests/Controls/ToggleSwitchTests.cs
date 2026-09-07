// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ToggleSwitchTests
{
    [Fact]
    public void DefaultTemplate_ContainsOnlyStyleableSwitchParts()
    {
        WpfTestThread.Run(() =>
        {
            var toggleSwitch = new ToggleSwitch { Content = "Application label" };
            var window = new Window
            {
                Width = 120.0d,
                Height = 80.0d,
                ShowInTaskbar = false,
                Content = toggleSwitch,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                Assert.IsType<ToggleSwitchPart>(
                    toggleSwitch.Template.FindName(ToggleSwitch.TrackPartName, toggleSwitch)
                );
                Assert.IsType<ToggleSwitchPart>(
                    toggleSwitch.Template.FindName(ToggleSwitch.ThumbPartName, toggleSwitch)
                );
                Assert.NotNull(toggleSwitch.TrackStyle);
                Assert.NotNull(toggleSwitch.ThumbStyle);
                Assert.DoesNotContain(
                    EnumerateVisualDescendants(toggleSwitch),
                    element => element is ContentPresenter
                );
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void LegacyContentProperties_AreNotDeclaredByToggleSwitch()
    {
        Assert.Null(typeof(ToggleSwitch).GetProperty("OffContent"));
        Assert.Null(typeof(ToggleSwitch).GetProperty("OnContent"));
        Assert.Null(typeof(ToggleSwitch).GetProperty("ContentPlacement"));
    }

    [Fact]
    public void SelectionState_UpdatesReadOnlyProgressWithoutAVisualTemplate()
    {
        WpfTestThread.Run(() =>
        {
            var toggleSwitch = new ToggleSwitch { IsAnimationEnabled = false };

            Assert.Equal(0.0d, toggleSwitch.TransitionProgress);

            toggleSwitch.IsChecked = true;
            Assert.Equal(1.0d, toggleSwitch.TransitionProgress);

            toggleSwitch.IsChecked = null;
            Assert.Equal(0.5d, toggleSwitch.TransitionProgress);

            toggleSwitch.IsChecked = false;
            Assert.Equal(0.0d, toggleSwitch.TransitionProgress);
        });
    }

    [Fact]
    public void DefaultTrack_CrossFadesCheckedVisualWithSharedTransitionProgress()
    {
        WpfTestThread.Run(() =>
        {
            var toggleSwitch = new ToggleSwitch { IsAnimationEnabled = false };
            var window = new Window
            {
                Width = 120.0d,
                Height = 80.0d,
                ShowInTaskbar = false,
                Content = toggleSwitch,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                var track = Assert.IsType<ToggleSwitchPart>(
                    toggleSwitch.Template.FindName(ToggleSwitch.TrackPartName, toggleSwitch)
                );
                var checkedVisual = Assert.IsAssignableFrom<Border>(
                    track.Template.FindName("CheckedVisual", track)
                );

                Assert.Equal(0.0d, checkedVisual.Opacity);

                toggleSwitch.IsChecked = null;
                Assert.Equal(0.5d, checkedVisual.Opacity);

                toggleSwitch.IsChecked = true;
                Assert.Equal(1.0d, checkedVisual.Opacity);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DefaultStyle_AdaptsTrackAndPressedThumbGeometryToOrientation()
    {
        WpfTestThread.Run(() =>
        {
            var toggleSwitch = new ToggleSwitch { IsAnimationEnabled = false };
            var window = new Window
            {
                Width = 120.0d,
                Height = 120.0d,
                ShowInTaskbar = false,
                Content = toggleSwitch,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                var track = Assert.IsType<ToggleSwitchPart>(
                    toggleSwitch.Template.FindName(ToggleSwitch.TrackPartName, toggleSwitch)
                );
                var thumb = Assert.IsType<ToggleSwitchPart>(
                    toggleSwitch.Template.FindName(ToggleSwitch.ThumbPartName, toggleSwitch)
                );

                Assert.Equal(Orientation.Horizontal, track.Orientation);
                Assert.Equal(40.0d, track.ActualWidth);
                Assert.Equal(20.0d, track.ActualHeight);

                toggleSwitch.Orientation = Orientation.Vertical;
                window.UpdateLayout();

                Assert.Equal(Orientation.Vertical, track.Orientation);
                Assert.Equal(Orientation.Vertical, thumb.Orientation);
                Assert.Equal(20.0d, track.ActualWidth);
                Assert.Equal(40.0d, track.ActualHeight);

                thumb.SetOwnerState(Orientation.Vertical, false, false, true);
                window.UpdateLayout();

                Assert.Equal(14.0d, thumb.ActualWidth);
                Assert.Equal(17.0d, thumb.ActualHeight);

                thumb.SetOwnerState(Orientation.Horizontal, false, false, true);
                window.UpdateLayout();

                Assert.Equal(17.0d, thumb.ActualWidth);
                Assert.Equal(14.0d, thumb.ActualHeight);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void HorizontalTemplate_ComputesTravelFromCurrentPartSizes()
    {
        WpfTestThread.Run(() =>
        {
            var toggleSwitch = CreateArrangedSwitch(horizontal: true);
            var thumb = GetPart(toggleSwitch, ToggleSwitch.ThumbPartName);

            toggleSwitch.IsChecked = true;

            var translation = GetOwnedTranslation(thumb);
            Assert.Equal(20.0d, translation.X, precision: 6);
            Assert.Equal(0.0d, translation.Y, precision: 6);

            // A pressed-style width change must retain the same trailing inset without any
            // replacement storyboard coordinates.
            thumb.Width = 17.0d;
            toggleSwitch.Measure(new Size(100.0d, 100.0d));
            toggleSwitch.Arrange(new Rect(toggleSwitch.DesiredSize));
            toggleSwitch.UpdateLayout();

            Assert.Equal(15.0d, translation.X, precision: 6);
        });
    }

    [Fact]
    public void VerticalTemplate_MapsUncheckedCenterAndCheckedFromBottomToTop()
    {
        WpfTestThread.Run(() =>
        {
            var toggleSwitch = CreateArrangedSwitch(horizontal: false);
            var thumb = GetPart(toggleSwitch, ToggleSwitch.ThumbPartName);
            var translation = GetOwnedTranslation(thumb);

            Assert.Equal(0.0d, translation.X, precision: 6);
            Assert.Equal(20.0d, translation.Y, precision: 6);

            toggleSwitch.IsThreeState = true;
            toggleSwitch.IsChecked = null;

            Assert.Equal(10.0d, translation.Y, precision: 6);

            toggleSwitch.IsChecked = true;

            Assert.Equal(0.0d, translation.X, precision: 6);
            Assert.Equal(0.0d, translation.Y, precision: 6);
        });
    }

    [Fact]
    public void RightToLeftTemplate_UsesLogicalStartToEndTravel()
    {
        WpfTestThread.Run(() =>
        {
            var toggleSwitch = new ToggleSwitch
            {
                FlowDirection = FlowDirection.RightToLeft,
                IsAnimationEnabled = false,
                Template = ParseTemplate(horizontal: true, includeScaleTransform: false),
            };

            Assert.True(toggleSwitch.ApplyTemplate());
            toggleSwitch.Measure(new Size(100.0d, 100.0d));
            toggleSwitch.Arrange(new Rect(toggleSwitch.DesiredSize));
            toggleSwitch.UpdateLayout();

            var thumb = GetPart(toggleSwitch, ToggleSwitch.ThumbPartName);
            toggleSwitch.IsChecked = true;

            // WPF mirrors the template coordinate space for RTL. Retaining positive logical
            // start-to-end travel therefore renders from the physical right edge to the left.
            Assert.Equal(20.0d, GetOwnedTranslation(thumb).X, precision: 6);
        });
    }

    [Fact]
    public void ToggleSwitchParts_ReceiveOwningSelectionState()
    {
        WpfTestThread.Run(() =>
        {
            var toggleSwitch = CreateArrangedSwitch(horizontal: true);
            var track = Assert.IsType<ToggleSwitchPart>(
                toggleSwitch.Template.FindName(ToggleSwitch.TrackPartName, toggleSwitch)
            );
            var thumb = Assert.IsType<ToggleSwitchPart>(
                toggleSwitch.Template.FindName(ToggleSwitch.ThumbPartName, toggleSwitch)
            );

            toggleSwitch.IsChecked = true;

            Assert.True(track.IsChecked);
            Assert.True(thumb.IsChecked);
            Assert.Equal(1.0d, track.TransitionProgress);
            Assert.Equal(1.0d, thumb.TransitionProgress);

            toggleSwitch.IsChecked = null;

            Assert.Null(track.IsChecked);
            Assert.Null(thumb.IsChecked);
            Assert.Equal(0.5d, track.TransitionProgress);
            Assert.Equal(0.5d, thumb.TransitionProgress);
        });
    }

    [Fact]
    public void LoadedTransition_CanReverseWithoutJumpingToAnEndpoint()
    {
        WpfTestThread.Run(async () =>
        {
            if (!SystemParameters.ClientAreaAnimation)
            {
                return;
            }

            var toggleSwitch = new ToggleSwitch
            {
                TransitionDuration = TimeSpan.FromMilliseconds(400.0d),
                TransitionEasingFunction = null,
            };
            var window = new Window
            {
                Width = 120.0d,
                Height = 80.0d,
                ShowInTaskbar = false,
                Content = toggleSwitch,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                toggleSwitch.IsChecked = true;
                await Task.Delay(100, TestContext.Current.CancellationToken);

                var progressBeforeReversal = toggleSwitch.TransitionProgress;
                Assert.InRange(progressBeforeReversal, 0.01d, 0.99d);
                var track = Assert.IsType<ToggleSwitchPart>(
                    toggleSwitch.Template.FindName(ToggleSwitch.TrackPartName, toggleSwitch)
                );
                var checkedVisual = Assert.IsAssignableFrom<Border>(
                    track.Template.FindName("CheckedVisual", track)
                );
                Assert.Equal(progressBeforeReversal, track.TransitionProgress, precision: 6);
                Assert.Equal(progressBeforeReversal, checkedVisual.Opacity, precision: 6);

                toggleSwitch.IsChecked = false;
                var progressAfterReversal = toggleSwitch.TransitionProgress;
                Assert.True(
                    Math.Abs(progressAfterReversal - progressBeforeReversal) <= 0.05d,
                    $"Progress jumped from {progressBeforeReversal} to {progressAfterReversal}."
                );
                Assert.Equal(progressAfterReversal, checkedVisual.Opacity, precision: 6);

                await Task.Delay(450, TestContext.Current.CancellationToken);
                Assert.Equal(0.0d, toggleSwitch.TransitionProgress, precision: 6);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ReapplyingTemplate_RestoresTemplateSuppliedThumbTransform()
    {
        WpfTestThread.Run(() =>
        {
            var toggleSwitch = new ToggleSwitch
            {
                IsAnimationEnabled = false,
                Template = ParseTemplate(horizontal: true, includeScaleTransform: true),
            };

            Assert.True(toggleSwitch.ApplyTemplate());
            var thumb = GetPart(toggleSwitch, ToggleSwitch.ThumbPartName);
            var group = Assert.IsType<TransformGroup>(thumb.RenderTransform);
            var originalScale = Assert.IsType<ScaleTransform>(group.Children[0]);

            toggleSwitch.Template = ParseEmptyTemplate();
            Assert.True(toggleSwitch.ApplyTemplate());

            Assert.Same(originalScale, thumb.RenderTransform);
        });
    }

    [Fact]
    public void TransitionDuration_RejectsNegativeValues()
    {
        WpfTestThread.Run(() =>
        {
            var toggleSwitch = new ToggleSwitch();

            Assert.Throws<ArgumentException>(
                () => toggleSwitch.TransitionDuration = TimeSpan.FromTicks(-1)
            );
            toggleSwitch.TransitionDuration = TimeSpan.Zero;
            Assert.Equal(TimeSpan.Zero, toggleSwitch.TransitionDuration);
        });
    }

    private static ToggleSwitch CreateArrangedSwitch(bool horizontal)
    {
        var toggleSwitch = new ToggleSwitch
        {
            IsAnimationEnabled = false,
            Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
            Template = ParseTemplate(horizontal, includeScaleTransform: false),
        };

        Assert.True(toggleSwitch.ApplyTemplate());
        toggleSwitch.Measure(new Size(100.0d, 100.0d));
        toggleSwitch.Arrange(new Rect(toggleSwitch.DesiredSize));
        toggleSwitch.UpdateLayout();
        return toggleSwitch;
    }

    private static FrameworkElement GetPart(ToggleSwitch toggleSwitch, string name) =>
        Assert.IsAssignableFrom<FrameworkElement>(
            toggleSwitch.Template.FindName(name, toggleSwitch)
        );

    private static TranslateTransform GetOwnedTranslation(FrameworkElement thumb) =>
        thumb.RenderTransform switch
        {
            TranslateTransform translation => translation,
            TransformGroup group => Assert.IsType<TranslateTransform>(group.Children[^1]),
            _ => throw new Xunit.Sdk.XunitException("ToggleSwitch did not install a thumb translation."),
        };

    private static ControlTemplate ParseTemplate(bool horizontal, bool includeScaleTransform)
    {
        var trackWidth = horizontal ? 40 : 20;
        var trackHeight = horizontal ? 20 : 40;
        var transform = includeScaleTransform
            ? "<ui:ToggleSwitchPart.RenderTransform><ScaleTransform ScaleX=\"0.9\" ScaleY=\"0.9\" /></ui:ToggleSwitchPart.RenderTransform>"
            : string.Empty;

        return (ControlTemplate)XamlReader.Parse(
            $$"""
              <ControlTemplate
                  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"
                  TargetType="{x:Type ui:ToggleSwitch}">
                  <Grid>
                      <ui:ToggleSwitchPart
                          x:Name="PART_Track"
                          Width="{{trackWidth}}"
                          Height="{{trackHeight}}"
                          HorizontalAlignment="Left"
                          VerticalAlignment="Top" />
                      <ui:ToggleSwitchPart
                          x:Name="PART_Thumb"
                          Width="12"
                          Height="12"
                          Margin="4"
                          HorizontalAlignment="Left"
                          VerticalAlignment="Top">
                          {{transform}}
                      </ui:ToggleSwitchPart>
                  </Grid>
              </ControlTemplate>
              """
        );
    }

    private static ControlTemplate ParseEmptyTemplate() =>
        (ControlTemplate)XamlReader.Parse(
            """
            <ControlTemplate
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"
                TargetType="{x:Type ui:ToggleSwitch}">
                <Border />
            </ControlTemplate>
            """
        );

    private static IEnumerable<DependencyObject> EnumerateVisualDescendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;

            foreach (var descendant in EnumerateVisualDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}