// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.RatingControlControl;

public class RatingControlTests
{
    [Fact]
    public void Defaults_RepresentAnUnratedFiveStarControl()
    {
        RunOnStaThread(
            () =>
            {
                var control = new RatingControl();
                var ratingMetadata = Assert.IsType<FrameworkPropertyMetadata>(
                    RatingControl.RatingProperty.GetMetadata(typeof(RatingControl))
                );

                Assert.Null(control.Rating);
                Assert.Equal(5, control.MaximumRating);
                Assert.Equal(0, control.PlaceholderRating);
                Assert.Equal(RatingPrecision.FullStar, control.Precision);
                Assert.False(control.IsClearEnabled);
                Assert.False(control.IsReadOnly);
                Assert.Null(control.ItemTemplate);
                Assert.True(ratingMetadata.BindsTwoWayByDefault);
            }
        );
    }

    [Theory]
    [InlineData(RatingPrecision.FullStar, 1.365, 1.0)]
    [InlineData(RatingPrecision.HalfStar, 1.365, 1.5)]
    [InlineData(RatingPrecision.QuarterStar, 1.365, 1.25)]
    [InlineData(RatingPrecision.QuarterStar, -2.0, 0.0)]
    [InlineData(RatingPrecision.QuarterStar, 8.0, 5.0)]
    public void SourceRating_IsRetainedWhilePresentationIsRoundedAndConstrained(
        RatingPrecision precision,
        double rating,
        double expectedDisplay
    )
    {
        RunOnStaThread(
            () =>
            {
                var control = new RatingControl
                {
                    Precision = precision,
                    Rating = rating,
                };

                Assert.Equal(rating, control.Rating);
                Assert.Equal(expectedDisplay, control.GetDisplayedRating(rating));
            }
        );
    }

    [Theory]
    [InlineData(RatingPrecision.FullStar, 1.365, 1.0)]
    [InlineData(RatingPrecision.HalfStar, 1.365, 1.5)]
    [InlineData(RatingPrecision.QuarterStar, 1.365, 1.25)]
    [InlineData(RatingPrecision.QuarterStar, -10.0, 0.0)]
    [InlineData(RatingPrecision.QuarterStar, 10.0, 5.0)]
    public void InteractionRating_IsRoundedAndConstrained(
        RatingPrecision precision,
        double interactionRating,
        double expectedRating
    )
    {
        RunOnStaThread(
            () =>
            {
                var control = new RatingControl { Precision = precision };

                control.SetRatingFromInteraction(interactionRating);

                Assert.Equal(expectedRating, control.Rating);
            }
        );
    }

    [Fact]
    public void ZeroRatingTarget_PreviewsAndCommitsZero()
    {
        RunOnStaThread(
            () =>
            {
                RatingControl control = CreateTemplatedRatingControl(
                    new TestRatingControl
                    {
                        Precision = RatingPrecision.QuarterStar,
                        Rating = 3,
                    }
                );
                var testControl = Assert.IsType<TestRatingControl>(control);
                var zeroTarget = Assert.IsAssignableFrom<FrameworkElement>(
                    control.Template.FindName("PART_ZeroRatingTarget", control)
                );
                ItemsControl itemsHost = Assert.Single(
                    FindVisualDescendants<ItemsControl>(control)
                );

                Assert.True(zeroTarget.ActualWidth > 0);

                testControl.ProcessMouseMove(zeroTarget);

                Assert.All(
                    itemsHost.Items.Cast<RatingItem>(),
                    item =>
                    {
                        Assert.Equal(RatingItemState.Empty, item.State);
                        Assert.Equal(RatingItemDisplayMode.Preview, item.DisplayMode);
                    }
                );

                MouseButtonEventArgs click = testControl.ProcessMouseLeftButtonDown(zeroTarget);

                Assert.True(click.Handled);
                Assert.Equal(0, control.Rating);
            }
        );
    }

    [Theory]
    [InlineData(FlowDirection.LeftToRight, true)]
    [InlineData(FlowDirection.RightToLeft, false)]
    public void ZeroRatingTarget_RemainsOnTheLowRatingEdge(
        FlowDirection flowDirection,
        bool targetPrecedesItems
    )
    {
        RunOnStaThread(
            () =>
            {
                RatingControl control = CreateTemplatedRatingControl(
                    new RatingControl { FlowDirection = flowDirection }
                );
                var zeroTarget = Assert.IsAssignableFrom<FrameworkElement>(
                    control.Template.FindName("PART_ZeroRatingTarget", control)
                );
                ItemsControl itemsHost = Assert.Single(
                    FindVisualDescendants<ItemsControl>(control)
                );
                double targetCenter = zeroTarget
                    .TranslatePoint(new Point(zeroTarget.ActualWidth / 2, 0), control)
                    .X;
                double itemsCenter = itemsHost
                    .TranslatePoint(new Point(itemsHost.ActualWidth / 2, 0), control)
                    .X;
                Point lowEdge = flowDirection == FlowDirection.LeftToRight
                    ? new Point(0, control.ActualHeight / 2)
                    : new Point(control.ActualWidth, control.ActualHeight / 2);
                Point highEdge = flowDirection == FlowDirection.LeftToRight
                    ? new Point(control.ActualWidth, control.ActualHeight / 2)
                    : new Point(0, control.ActualHeight / 2);

                Assert.Equal(targetPrecedesItems, targetCenter < itemsCenter);
                Assert.True(control.IsPositionInZeroRatingRegion(lowEdge));
                Assert.False(control.IsPositionInZeroRatingRegion(highEdge));
            }
        );
    }

    [Theory]
    [InlineData(10, 0.25)]
    [InlineData(25, 0.25)]
    [InlineData(50, 0.5)]
    [InlineData(75, 0.75)]
    [InlineData(90, 1.0)]
    public void RtlPointerCoordinate_IsConsumedWithoutASecondMirror(
        double localX,
        double expectedRating
    )
    {
        RunOnStaThread(
            () =>
            {
                var control = new RatingControl
                {
                    FlowDirection = FlowDirection.RightToLeft,
                    Precision = RatingPrecision.QuarterStar,
                };

                // WPF reports the pointer in the mirrored container's local coordinate system.
                // Consequently the physical left side of an RTL star arrives as a high local X,
                // and must retain that high fractional score here.
                double rating = control.GetRatingFromItemPosition(0, localX, 100);

                Assert.Equal(expectedRating, rating);
            }
        );
    }

    [Theory]
    [InlineData(FlowDirection.LeftToRight, Key.Right, 2.25)]
    [InlineData(FlowDirection.LeftToRight, Key.Left, 1.75)]
    [InlineData(FlowDirection.RightToLeft, Key.Right, 1.75)]
    [InlineData(FlowDirection.RightToLeft, Key.Left, 2.25)]
    [InlineData(FlowDirection.RightToLeft, Key.Up, 2.25)]
    [InlineData(FlowDirection.RightToLeft, Key.Down, 1.75)]
    public void ArrowKeys_FollowVisualHorizontalDirectionAndStableVerticalSemantics(
        FlowDirection flowDirection,
        Key key,
        double expectedRating
    )
    {
        RunOnStaThread(
            () =>
            {
                var control = new TestRatingControl
                {
                    FlowDirection = flowDirection,
                    Precision = RatingPrecision.QuarterStar,
                    Rating = 2,
                };
                var window = new Window
                {
                    Content = control,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                };

                try
                {
                    window.Show();
                    PresentationSource source = Assert.IsAssignableFrom<PresentationSource>(
                        PresentationSource.FromVisual(control)
                    );
                    var keyEvent = new KeyEventArgs(
                        Keyboard.PrimaryDevice,
                        source,
                        Environment.TickCount,
                        key
                    )
                    {
                        RoutedEvent = Keyboard.KeyDownEvent,
                    };

                    control.ProcessKeyDown(keyEvent);

                    Assert.True(keyEvent.Handled);
                    Assert.Equal(expectedRating, control.Rating);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void ArrowKeys_StopAtMaximumAndNullInsteadOfWrapping()
    {
        RunOnStaThread(
            () =>
            {
                var control = new TestRatingControl
                {
                    IsClearEnabled = true,
                    MaximumRating = 2,
                    Rating = 2,
                };

                control.ProcessKey(Key.Up);
                Assert.Equal(2, control.Rating);

                control.ProcessKey(Key.End);
                Assert.Equal(2, control.Rating);

                control.Rating = 0;
                control.ProcessKey(Key.Home);
                Assert.Equal(0, control.Rating);

                control.ProcessKey(Key.Down);
                Assert.Null(control.Rating);

                control.ProcessKey(Key.Down);
                Assert.Null(control.Rating);

                control.ProcessKey(Key.Up);
                Assert.Equal(0, control.Rating);
            }
        );
    }

    [Fact]
    public void ReducingMaximumRating_ClampsRatingAndPreservesBinding()
    {
        RunOnStaThread(
            () =>
            {
                var source = new BoundRatingSource { Rating = 7 };
                var control = new RatingControl { MaximumRating = 10 };
                control.SetBinding(
                    RatingControl.RatingProperty,
                    new Binding(nameof(BoundRatingSource.Rating))
                    {
                        Mode = BindingMode.TwoWay,
                        Source = source,
                    }
                );

                control.MaximumRating = 5;

                Assert.Equal(5, control.Rating);
                Assert.Equal(5, source.Rating);
                Assert.True(BindingOperations.IsDataBound(control, RatingControl.RatingProperty));
            }
        );
    }

    [Fact]
    public void ClearEnabled_RepeatedSelectionClearsRatherThanAssigningZero()
    {
        RunOnStaThread(
            () =>
            {
                var control = new RatingControl
                {
                    IsClearEnabled = true,
                    Precision = RatingPrecision.QuarterStar,
                    Rating = 1.365,
                };

                // The first selection is a real edit because the source still contains its original
                // precision even though the displayed score already occupies the same star segment.
                control.SetRatingFromInteraction(1.25);
                Assert.Equal(1.25, control.Rating);

                control.SetRatingFromInteraction(1.25);
                Assert.Null(control.Rating);
            }
        );
    }

    [Fact]
    public void Interaction_UpdatesTwoWaySourceWithoutReplacingItsBinding()
    {
        RunOnStaThread(
            () =>
            {
                var source = new BoundRatingSource { Rating = 1.365 };
                var control = new RatingControl { Precision = RatingPrecision.QuarterStar };
                control.SetBinding(
                    RatingControl.RatingProperty,
                    new Binding(nameof(BoundRatingSource.Rating))
                    {
                        Mode = BindingMode.TwoWay,
                        Source = source,
                    }
                );

                Assert.Equal(1.365, control.Rating);

                control.SetRatingFromInteraction(1.5);

                Assert.Equal(1.5, control.Rating);
                Assert.Equal(1.5, source.Rating);
                Assert.True(BindingOperations.IsDataBound(control, RatingControl.RatingProperty));
            }
        );
    }

    [Fact]
    public void ReadOnly_PreventsRatingChangesAndClearing()
    {
        RunOnStaThread(
            () =>
            {
                var control = new RatingControl
                {
                    IsClearEnabled = true,
                    IsReadOnly = true,
                    Rating = 2,
                };

                control.SetRatingFromInteraction(2);
                control.SetRatingFromInteraction(4);

                Assert.Equal(2, control.Rating);
            }
        );
    }

    [Fact]
    public void Placeholder_IsRenderedWithoutBecomingAnActualRating()
    {
        RunOnStaThread(
            () =>
            {
                RatingControl control = CreateTemplatedRatingControl(
                    new RatingControl
                    {
                        MaximumRating = 3,
                        PlaceholderRating = 2.25,
                        Precision = RatingPrecision.QuarterStar,
                    }
                );
                FluentIcon[] icons = FindVisualDescendants<FluentIcon>(control).ToArray();
                RatingItem[] items = icons.Select(icon => Assert.IsType<RatingItem>(icon.DataContext)).ToArray();

                Assert.Null(control.Rating);
                Assert.All(
                    items,
                    item => Assert.Equal(RatingItemDisplayMode.Placeholder, item.DisplayMode)
                );
                Assert.Collection(
                    icons,
                    icon => AssertFullStar(icon),
                    icon => AssertFullStar(icon),
                    icon =>
                    {
                        Assert.Equal(SymbolRegular.StarOneQuarter24, icon.Symbol);
                        Assert.False(icon.Filled);
                    }
                );
            }
        );
    }

    [Fact]
    public void ItemTemplate_ReceivesPublicStateForEveryRatingItem()
    {
        RunOnStaThread(
            () =>
            {
                var itemTemplate = new DataTemplate(typeof(RatingItem));
                RatingControl control = CreateTemplatedRatingControl(
                    new RatingControl
                    {
                        ItemTemplate = itemTemplate,
                        MaximumRating = 3,
                        Precision = RatingPrecision.QuarterStar,
                        Rating = 1.75,
                    }
                );
                ItemsControl itemsHost = Assert.Single(
                    FindVisualDescendants<ItemsControl>(control)
                );
                RatingItem[] items = itemsHost.Items.Cast<RatingItem>().ToArray();

                Assert.Same(itemTemplate, itemsHost.ItemTemplate);
                Assert.Collection(
                    items,
                    item =>
                    {
                        Assert.Equal(0, item.Index);
                        Assert.Equal(1, item.Position);
                        Assert.Equal(RatingItemState.FullStar, item.State);
                        Assert.Equal(RatingItemDisplayMode.Rating, item.DisplayMode);
                    },
                    item =>
                    {
                        Assert.Equal(0.75, item.Fill);
                        Assert.Equal(RatingItemState.ThreeQuarterStar, item.State);
                        Assert.Equal(RatingItemDisplayMode.Rating, item.DisplayMode);
                    },
                    item => Assert.Equal(RatingItemState.Empty, item.State)
                );
            }
        );
    }

    [Fact]
    public void DefaultItemTemplate_MapsSemanticStatesToGlyphsAndTracksMaximumRating()
    {
        RunOnStaThread(
            () =>
            {
                RatingControl control = CreateTemplatedRatingControl(
                    new RatingControl
                    {
                        MaximumRating = 3,
                        Precision = RatingPrecision.QuarterStar,
                        Rating = 1.75,
                    }
                );

                Assert.Collection(
                    FindVisualDescendants<FluentIcon>(control),
                    icon => AssertFullStar(icon),
                    icon =>
                    {
                        Assert.Equal(SymbolRegular.StarThreeQuarter24, icon.Symbol);
                        Assert.False(icon.Filled);
                    },
                    icon =>
                    {
                        Assert.Equal(SymbolRegular.Star24, icon.Symbol);
                        Assert.False(icon.Filled);
                    }
                );

                control.MaximumRating = 4;
                control.UpdateLayout();

                Assert.Equal(4, FindVisualDescendants<FluentIcon>(control).Count());
                Assert.Equal(1.75, control.Rating);
            }
        );
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    public void DefaultItemTemplate_FlipsOnlyPartialGlyphsForRightToLeft(double rating)
    {
        RunOnStaThread(
            () =>
            {
                RatingControl control = CreateTemplatedRatingControl(
                    new RatingControl
                    {
                        FlowDirection = FlowDirection.RightToLeft,
                        MaximumRating = 2,
                        Precision = RatingPrecision.QuarterStar,
                        Rating = rating,
                    }
                );
                FluentIcon partialIcon = FindVisualDescendants<FluentIcon>(control)
                    .Single(icon => ((RatingItem)icon.DataContext).State != RatingItemState.Empty);

                var rtlTransform = Assert.IsType<ScaleTransform>(partialIcon.RenderTransform);
                Assert.Equal(-1, rtlTransform.ScaleX);

                control.FlowDirection = FlowDirection.LeftToRight;
                control.UpdateLayout();

                Assert.False(
                    partialIcon.RenderTransform is ScaleTransform { ScaleX: -1 },
                    "A partial glyph must return to its native orientation in left-to-right layout."
                );
            }
        );
    }

    [Fact]
    public void DefaultItemTemplate_DoesNotFlipSymmetricGlyphsForRightToLeft()
    {
        RunOnStaThread(
            () =>
            {
                RatingControl control = CreateTemplatedRatingControl(
                    new RatingControl
                    {
                        FlowDirection = FlowDirection.RightToLeft,
                        MaximumRating = 2,
                        Rating = 1,
                    }
                );

                Assert.All(
                    FindVisualDescendants<FluentIcon>(control),
                    icon => Assert.False(icon.RenderTransform is ScaleTransform { ScaleX: -1 })
                );
            }
        );
    }

    [Theory]
    [InlineData(0.0, RatingItemState.Empty)]
    [InlineData(0.25, RatingItemState.QuarterStar)]
    [InlineData(0.5, RatingItemState.HalfStar)]
    [InlineData(0.75, RatingItemState.ThreeQuarterStar)]
    [InlineData(1.0, RatingItemState.FullStar)]
    public void RatingItem_ExposesEveryDiscreteTemplateState(
        double fill,
        RatingItemState expectedState
    )
    {
        var item = new RatingItem(2);

        item.Update(fill, RatingItemDisplayMode.Preview);

        Assert.Equal(2, item.Index);
        Assert.Equal(3, item.Position);
        Assert.Equal(fill, item.Fill);
        Assert.Equal(expectedState, item.State);
        Assert.Equal(RatingItemDisplayMode.Preview, item.DisplayMode);
    }

    [Fact]
    public void RatingChanged_ReportsNullableTransitions()
    {
        RunOnStaThread(
            () =>
            {
                var control = new RatingControl();
                var transitions = new List<(double? OldRating, double? NewRating)>();
                control.RatingChanged += (_, e) => transitions.Add((e.OldValue, e.NewValue));

                control.Rating = 1.365;
                control.Rating = null;

                Assert.Equal([(null, 1.365), (1.365, null)], transitions);
            }
        );
    }

    [Fact]
    public void InvalidSemanticValuesAreRejected()
    {
        RunOnStaThread(
            () =>
            {
                var control = new RatingControl();

                Assert.Throws<ArgumentException>(() => control.MaximumRating = 0);
                Assert.Throws<ArgumentException>(() => control.Rating = double.NaN);
                Assert.Throws<ArgumentException>(() => control.Rating = double.PositiveInfinity);
                Assert.Throws<ArgumentException>(() => control.PlaceholderRating = double.NegativeInfinity);
                Assert.Throws<ArgumentException>(
                    () => control.Precision = (RatingPrecision)99
                );
            }
        );
    }

    private static RatingControl CreateTemplatedRatingControl(RatingControl control)
    {
        var root = new Grid();
        root.Resources.MergedDictionaries.Add(
            new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml",
                    UriKind.Absolute
                ),
            }
        );
        root.Children.Add(control);

        Assert.True(control.ApplyTemplate());
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));
        root.UpdateLayout();
        return control;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void AssertFullStar(FluentIcon icon)
    {
        Assert.Equal(SymbolRegular.Star24, icon.Symbol);
        Assert.True(icon.Filled);
    }

    private static void RunOnStaThread(Action action) => WpfTestThread.Run(action);

    private sealed class TestRatingControl : RatingControl
    {
        public void ProcessMouseMove(DependencyObject source)
        {
            var args = new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
            {
                RoutedEvent = Mouse.MouseMoveEvent,
                Source = source,
            };

            OnMouseMove(args);
        }

        public MouseButtonEventArgs ProcessMouseLeftButtonDown(DependencyObject source)
        {
            var args = new MouseButtonEventArgs(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                MouseButton.Left
            )
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = source,
            };

            OnMouseLeftButtonDown(args);
            return args;
        }

        public void ProcessKeyDown(KeyEventArgs args) => OnKeyDown(args);

        public void ProcessKey(Key key)
        {
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new TestPresentationSource(),
                Environment.TickCount,
                key
            )
            {
                RoutedEvent = Keyboard.KeyDownEvent,
            };

            OnKeyDown(args);
        }
    }

    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;

        public override bool IsDisposed => false;

        protected override CompositionTarget? GetCompositionTargetCore() => null;
    }

    private sealed class BoundRatingSource : DependencyObject
    {
        public static readonly DependencyProperty RatingProperty = DependencyProperty.Register(
            nameof(Rating),
            typeof(double?),
            typeof(BoundRatingSource)
        );

        public double? Rating
        {
            get => (double?)GetValue(RatingProperty);
            set => SetValue(RatingProperty, value);
        }
    }
}
