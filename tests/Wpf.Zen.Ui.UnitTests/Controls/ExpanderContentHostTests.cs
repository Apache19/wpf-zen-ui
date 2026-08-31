// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ExpanderContentHostTests
{
    [Fact]
    public void ExpanderTemplate_ArrangesAndShapesAllDirections()
    {
        WpfTestThread.Run(
            () =>
            {
                var resources = new ResourceDictionary
                {
                    Source = new Uri(
                        "/Wpf.Zen.Ui;component/Controls/Expander/Expander.xaml",
                        UriKind.Relative
                    ),
                };
                var expander = new Expander
                {
                    Style = Assert.IsType<Style>(resources[typeof(Expander)]),
                    Header = "Header",
                    Content = new Border { Width = 140D, Height = 90D },
                    BorderThickness = new Thickness(1D, 2D, 3D, 4D),
                    IsExpanded = true,
                };
                expander.SetValue(
                    Border.CornerRadiusProperty,
                    new CornerRadius(5D, 6D, 7D, 8D)
                );

                var window = new Window
                {
                    Width = 600D,
                    Height = 400D,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Content = expander,
                };

                try
                {
                    window.Show();
                    expander.ApplyTemplate();

                    var header = Assert.IsType<Wpf.Zen.Ui.Controls.ToggleButton>(
                        expander.Template.FindName("headerToggleButton", expander)
                    );
                    header.ApplyTemplate();
                    var chevron = Assert.IsType<FluentIcon>(
                        header.Template.FindName("chevronIcon", header)
                    );
                    var contentControl = Assert.IsType<ContentControl>(
                        expander.Template.FindName("contentControl", expander)
                    );
                    contentControl.ApplyTemplate();

                    var host = Assert.IsType<ExpanderContentHost>(
                        expander.Template.FindName("contentHost", expander)
                    );
                    var contentBorder = Assert.IsType<Border>(
                        contentControl.Template.FindName("rootBorder", contentControl)
                    );

                    foreach (var direction in Enum.GetValues<ExpandDirection>())
                    {
                        expander.ExpandDirection = direction;
                        PumpDispatcher(TimeSpan.FromMilliseconds(20D));
                        window.UpdateLayout();

                        Assert.Equal(direction, host.ExpandDirection);
                        Assert.Equal(GetExpectedHeaderDock(direction), DockPanel.GetDock(header));
                        Assert.Equal(GetExpectedContentDock(direction), DockPanel.GetDock(host));
                        Assert.Equal(GetExpectedChevron(direction), chevron.Symbol);
                        Assert.Equal(
                            GetExpectedHeaderCornerRadius(direction),
                            header.CornerRadius
                        );
                        Assert.Equal(
                            GetExpectedContentCornerRadius(direction),
                            contentBorder.CornerRadius
                        );
                        Assert.Equal(
                            GetExpectedContentBorderThickness(direction),
                            contentBorder.BorderThickness
                        );
                    }
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Theory]
    [InlineData(ExpandDirection.Down)]
    [InlineData(ExpandDirection.Up)]
    [InlineData(ExpandDirection.Left)]
    [InlineData(ExpandDirection.Right)]
    public void ExpanderTemplate_KeepsJoinedHeaderCornersUntilContentIsCollapsed(
        ExpandDirection direction)
    {
        WpfTestThread.Run(
            () =>
            {
                var resources = new ResourceDictionary
                {
                    Source = new Uri(
                        "/Wpf.Zen.Ui;component/Controls/Expander/Expander.xaml",
                        UriKind.Relative
                    ),
                };
                var cornerRadius = new CornerRadius(5D, 6D, 7D, 8D);
                var expander = new Expander
                {
                    Style = Assert.IsType<Style>(resources[typeof(Expander)]),
                    Header = "Header",
                    Content = new Border { Width = 140D, Height = 90D },
                    ExpandDirection = direction,
                };
                expander.SetValue(Border.CornerRadiusProperty, cornerRadius);

                var window = new Window
                {
                    Width = 600D,
                    Height = 400D,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Content = expander,
                };

                try
                {
                    window.Show();
                    expander.ApplyTemplate();

                    var header = Assert.IsType<Wpf.Zen.Ui.Controls.ToggleButton>(
                        expander.Template.FindName("headerToggleButton", expander)
                    );
                    var host = Assert.IsType<ExpanderContentHost>(
                        expander.Template.FindName("contentHost", expander)
                    );
                    host.ExpandDuration = TimeSpan.FromMilliseconds(100D);
                    host.CollapseDuration = TimeSpan.FromMilliseconds(250D);

                    Assert.Equal(Visibility.Collapsed, host.Visibility);
                    Assert.Equal(cornerRadius, header.CornerRadius);

                    expander.IsExpanded = true;
                    PumpDispatcher(TimeSpan.FromMilliseconds(150D));
                    Assert.Equal(GetExpectedHeaderCornerRadius(direction), header.CornerRadius);

                    expander.IsExpanded = false;
                    Assert.Equal(Visibility.Visible, host.Visibility);
                    Assert.Equal(GetExpectedHeaderCornerRadius(direction), header.CornerRadius);

                    PumpDispatcher(TimeSpan.FromMilliseconds(100D));
                    Assert.Equal(Visibility.Visible, host.Visibility);
                    Assert.Equal(GetExpectedHeaderCornerRadius(direction), header.CornerRadius);

                    PumpDispatcher(TimeSpan.FromMilliseconds(200D));
                    Assert.Equal(Visibility.Collapsed, host.Visibility);
                    Assert.Equal(cornerRadius, header.CornerRadius);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Theory]
    [InlineData(ExpandDirection.Down)]
    [InlineData(ExpandDirection.Up)]
    [InlineData(ExpandDirection.Left)]
    [InlineData(ExpandDirection.Right)]
    public void DirectionalTransition_ReversesFromCurrentProgress_ThenCollapses(
        ExpandDirection direction)
    {
        WpfTestThread.Run(
            () =>
            {
                var child = new Border
                {
                    Width = 140D,
                    Height = 90D,
                };
                var host = new ExpanderContentHost
                {
                    Child = child,
                    ExpandDirection = direction,
                    ExpandDuration = TimeSpan.FromMilliseconds(400D),
                    CollapseDuration = TimeSpan.FromMilliseconds(400D),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };
                var window = new Window
                {
                    Width = 500D,
                    Height = 400D,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Content = host,
                };

                try
                {
                    window.Show();
                    window.UpdateLayout();

                    host.IsExpanded = true;
                    PumpDispatcher(TimeSpan.FromMilliseconds(100D));

                    var translation = GetTranslation(child);
                    var openingOffset = GetDirectionalOffset(translation, direction);
                    var fullDistance = direction is ExpandDirection.Left or ExpandDirection.Right
                        ? child.RenderSize.Width
                        : child.RenderSize.Height;

                    Assert.Equal(Visibility.Visible, host.Visibility);
                    Assert.InRange(openingOffset, 0.1D, fullDistance - 0.1D);
                    AssertDirectionSign(translation, direction);

                    // Changing direction at an arbitrary progress must commit the displayed value
                    // as the next animation's origin instead of resetting to an endpoint.
                    host.IsExpanded = false;
                    var offsetImmediatelyAfterCollapseRequest =
                        GetDirectionalOffset(translation, direction);
                    Assert.True(
                        Math.Abs(offsetImmediatelyAfterCollapseRequest - openingOffset) <= 1D,
                        $"opening={openingOffset}, after={offsetImmediatelyAfterCollapseRequest}"
                    );

                    PumpDispatcher(TimeSpan.FromMilliseconds(70D));
                    var closingOffset = GetDirectionalOffset(translation, direction);
                    Assert.True(closingOffset > openingOffset);

                    host.IsExpanded = true;
                    var offsetImmediatelyAfterExpansionRequest =
                        GetDirectionalOffset(translation, direction);
                    Assert.InRange(
                        Math.Abs(offsetImmediatelyAfterExpansionRequest - closingOffset),
                        0D,
                        1D
                    );

                    PumpDispatcher(TimeSpan.FromMilliseconds(70D));
                    Assert.True(GetDirectionalOffset(translation, direction) < closingOffset);

                    host.IsExpanded = false;
                    PumpDispatcher(TimeSpan.FromMilliseconds(500D));

                    Assert.Equal(Visibility.Collapsed, host.Visibility);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void ReversedCollapse_ScalesDurationByRemainingTimelineProgress()
    {
        WpfTestThread.Run(
            () =>
            {
                var child = new Border
                {
                    Width = 140D,
                    Height = 100D,
                };
                var host = new ExpanderContentHost
                {
                    Child = child,
                    ExpandDirection = ExpandDirection.Down,
                    ExpandDuration = TimeSpan.FromMilliseconds(1000D),
                    CollapseDuration = TimeSpan.FromMilliseconds(1000D),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };
                var window = new Window
                {
                    Width = 500D,
                    Height = 400D,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Content = host,
                };

                try
                {
                    window.Show();
                    host.IsExpanded = true;
                    PumpDispatcher(TimeSpan.FromMilliseconds(500D));

                    var translation = GetTranslation(child);
                    var progressAtReversal = -translation.Y / child.RenderSize.Height;

                    // The global cubic mapping places the content near one eighth of its hidden
                    // offset halfway through the linear entrance timeline.
                    Assert.InRange(progressAtReversal, 0.02D, 0.35D);

                    host.IsExpanded = false;
                    PumpDispatcher(TimeSpan.FromMilliseconds(300D));
                    Assert.Equal(Visibility.Visible, host.Visibility);

                    PumpDispatcher(TimeSpan.FromMilliseconds(400D));

                    // A timeline-aware reversal finishes in roughly 500 ms. Scaling from the
                    // eased pixel offset instead would incorrectly require roughly 875 ms.
                    Assert.Equal(Visibility.Collapsed, host.Visibility);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void ChangingChildSizeDuringTransition_ReappliesNormalizedProgress()
    {
        WpfTestThread.Run(
            () =>
            {
                var child = new Border
                {
                    Width = 140D,
                    Height = 80D,
                };
                var host = new ExpanderContentHost
                {
                    Child = child,
                    ExpandDirection = ExpandDirection.Down,
                    ExpandDuration = TimeSpan.FromMilliseconds(500D),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };
                var window = new Window
                {
                    Width = 500D,
                    Height = 400D,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Content = host,
                };

                try
                {
                    window.Show();
                    host.IsExpanded = true;
                    PumpDispatcher(TimeSpan.FromMilliseconds(100D));

                    var translation = GetTranslation(child);
                    var progressBeforeResize = -translation.Y / child.RenderSize.Height;

                    child.Height = 160D;
                    window.UpdateLayout();

                    var progressAfterResize = -translation.Y / child.RenderSize.Height;
                    Assert.InRange(
                        Math.Abs(progressAfterResize - progressBeforeResize),
                        0D,
                        0.03D
                    );
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    private static TranslateTransform GetTranslation(UIElement child)
    {
        if (child.RenderTransform is TranslateTransform translation)
        {
            return translation;
        }

        var group = Assert.IsType<TransformGroup>(child.RenderTransform);
        return Assert.IsType<TranslateTransform>(group.Children[^1]);
    }

    private static double GetDirectionalOffset(
        TranslateTransform translation,
        ExpandDirection direction)
    {
        return direction switch
        {
            ExpandDirection.Down => -translation.Y,
            ExpandDirection.Up => translation.Y,
            ExpandDirection.Left => translation.X,
            ExpandDirection.Right => -translation.X,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    private static void AssertDirectionSign(
        TranslateTransform translation,
        ExpandDirection direction)
    {
        switch (direction)
        {
            case ExpandDirection.Down:
                Assert.True(translation.Y < 0D);
                Assert.Equal(0D, translation.X);
                break;

            case ExpandDirection.Up:
                Assert.True(translation.Y > 0D);
                Assert.Equal(0D, translation.X);
                break;

            case ExpandDirection.Left:
                Assert.True(translation.X > 0D);
                Assert.Equal(0D, translation.Y);
                break;

            case ExpandDirection.Right:
                Assert.True(translation.X < 0D);
                Assert.Equal(0D, translation.Y);
                break;
        }
    }

    private static Dock GetExpectedHeaderDock(ExpandDirection direction)
    {
        return direction switch
        {
            ExpandDirection.Down => Dock.Top,
            ExpandDirection.Up => Dock.Bottom,
            ExpandDirection.Left => Dock.Right,
            ExpandDirection.Right => Dock.Left,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    private static Dock GetExpectedContentDock(ExpandDirection direction)
    {
        return direction switch
        {
            ExpandDirection.Down => Dock.Bottom,
            ExpandDirection.Up => Dock.Top,
            ExpandDirection.Left => Dock.Left,
            ExpandDirection.Right => Dock.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    private static SymbolRegular GetExpectedChevron(ExpandDirection direction)
    {
        return direction switch
        {
            ExpandDirection.Down => SymbolRegular.ChevronDown24,
            ExpandDirection.Up => SymbolRegular.ChevronUp24,
            ExpandDirection.Left => SymbolRegular.ChevronLeft24,
            ExpandDirection.Right => SymbolRegular.ChevronRight24,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    private static CornerRadius GetExpectedHeaderCornerRadius(ExpandDirection direction)
    {
        return direction switch
        {
            ExpandDirection.Down => new CornerRadius(5D, 6D, 0D, 0D),
            ExpandDirection.Up => new CornerRadius(0D, 0D, 7D, 8D),
            ExpandDirection.Left => new CornerRadius(0D, 6D, 7D, 0D),
            ExpandDirection.Right => new CornerRadius(5D, 0D, 0D, 8D),
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    private static CornerRadius GetExpectedContentCornerRadius(ExpandDirection direction)
    {
        return direction switch
        {
            ExpandDirection.Down => new CornerRadius(0D, 0D, 7D, 8D),
            ExpandDirection.Up => new CornerRadius(5D, 6D, 0D, 0D),
            ExpandDirection.Left => new CornerRadius(5D, 0D, 0D, 8D),
            ExpandDirection.Right => new CornerRadius(0D, 6D, 7D, 0D),
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    private static Thickness GetExpectedContentBorderThickness(ExpandDirection direction)
    {
        return direction switch
        {
            ExpandDirection.Down => new Thickness(1D, 0D, 3D, 4D),
            ExpandDirection.Up => new Thickness(1D, 2D, 3D, 0D),
            ExpandDirection.Left => new Thickness(1D, 2D, 0D, 4D),
            ExpandDirection.Right => new Thickness(0D, 2D, 3D, 4D),
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration,
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
