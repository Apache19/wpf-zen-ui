// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies the layout and visual-state contracts of <see cref="ProgressRing"/>.
/// </summary>
public class ProgressRingTests
{
    [Fact]
    public void Arrange_WithRectangularBounds_KeepsEveryArcCircularAfterResize()
    {
        RunOnStaThread(
            () =>
            {
                var ring = CreateRing(120, 60);
                using var host = new TestWindowHost(ring);

                ring.UpdateLayout();
                Arc[] arcs = FindVisualDescendants<Arc>(ring).ToArray();

                Assert.Equal(3, arcs.Length);
                Assert.All(
                    arcs.Where(arc => arc.Visibility == Visibility.Visible),
                    arc => Assert.Equal(new Size(60, 60), arc.RenderSize)
                );
                Assert.All(arcs, arc => Assert.Equal(7.5d, arc.StrokeThickness));

                // Resize while the animated arc owns the foreground layer. This reproduces the
                // original failure without restarting the indeterminate state or its storyboard.
                ring.IsIndeterminate = true;
                ring.Width = 40;
                ring.Height = 100;
                ring.UpdateLayout();

                Assert.Equal(new Size(40, 100), ring.RenderSize);
                Assert.Equal(arcs, FindVisualDescendants<Arc>(ring).ToArray());
                Assert.All(
                    arcs.Where(arc => arc.Visibility == Visibility.Visible),
                    arc => Assert.Equal(new Size(40, 40), arc.RenderSize)
                );
                Assert.All(arcs, arc => Assert.Equal(5.0d, arc.StrokeThickness));

                ring.StrokeThickness = 3;
                ring.UpdateLayout();

                Assert.All(arcs, arc => Assert.Equal(3.0d, arc.StrokeThickness));
            }
        );
    }

    [Fact]
    public void IsIndeterminate_ChangesArcStateWithoutReplacingTemplateVisuals()
    {
        RunOnStaThread(
            () =>
            {
                var ring = CreateRing(80, 50);

                Arrange(ring);
                var determinateArc = Assert.IsType<Arc>(
                    ring.Template.FindName("PART_DeterminateArc", ring)
                );
                var indeterminateArc = Assert.IsType<Arc>(
                    ring.Template.FindName("IndeterminateArc", ring)
                );

                ring.IsIndeterminate = true;
                ring.UpdateLayout();

                Assert.Same(determinateArc, ring.Template.FindName("PART_DeterminateArc", ring));
                Assert.Same(indeterminateArc, ring.Template.FindName("IndeterminateArc", ring));
                Assert.Equal(Visibility.Collapsed, determinateArc.Visibility);
                Assert.Equal(Visibility.Visible, indeterminateArc.Visibility);
                Assert.Equal(new Size(50, 50), indeterminateArc.RenderSize);

                ring.IsIndeterminate = false;
                ring.UpdateLayout();

                Assert.Same(determinateArc, ring.Template.FindName("PART_DeterminateArc", ring));
                Assert.Equal(Visibility.Visible, determinateArc.Visibility);
                Assert.Equal(Visibility.Collapsed, indeterminateArc.Visibility);
            }
        );
    }

    [Fact]
    public void Arc_EndAngleChange_RebuildsRenderedGeometryWithoutAnotherLayoutPass()
    {
        RunOnStaThread(
            () =>
            {
                var arc = new Arc
                {
                    EndAngle = 90,
                    Stroke = Brushes.Black,
                    StrokeThickness = 4,
                };
                arc.Measure(new Size(60, 60));
                arc.Arrange(new Rect(0, 0, 60, 60));

                Rect firstBounds = arc.RenderedGeometry.Bounds;
                arc.EndAngle = 270;
                Rect secondBounds = arc.RenderedGeometry.Bounds;

                Assert.NotEqual(firstBounds, secondBounds);
            }
        );
    }

    [Fact]
    public void Arc_SweepDirection_SelectsComplementaryPathWithoutChangingEndpoints()
    {
        RunOnStaThread(
            () =>
            {
                var arc = new Arc
                {
                    StartAngle = 0,
                    EndAngle = 90,
                    Stroke = Brushes.Black,
                    StrokeThickness = 2,
                };
                arc.Measure(new Size(100, 100));
                arc.Arrange(new Rect(0, 0, 100, 100));

                Rect clockwiseBounds = arc.RenderedGeometry.Bounds;
                arc.SweepDirection = SweepDirection.Counterclockwise;
                Rect counterclockwiseBounds = arc.RenderedGeometry.Bounds;

                Assert.True(clockwiseBounds.Width < 55);
                Assert.True(clockwiseBounds.Height < 55);
                Assert.True(counterclockwiseBounds.Width > 95);
                Assert.True(counterclockwiseBounds.Height > 95);
            }
        );
    }

    [Fact]
    public void DeterminateRange_CoercesValueAndOwnsTheDerivedArcAngle()
    {
        RunOnStaThread(
            () =>
            {
                var ring = CreateRing(60, 60);
                var arc = Assert.IsType<Arc>(ring.Template.FindName("PART_DeterminateArc", ring));

                ring.Minimum = 20;
                ring.Maximum = 60;
                ring.Value = 40;
                Assert.Equal(180.0d, arc.EndAngle);

                ring.Value = 100;
                Assert.Equal(60.0d, ring.Value);
                Assert.Equal(360.0d, arc.EndAngle);

                ring.Minimum = 80;
                Assert.Equal(80.0d, ring.Maximum);
                Assert.Equal(80.0d, ring.Value);
                Assert.Equal(0.0d, arc.EndAngle);
            }
        );
    }

    [Fact]
    public void PublicApi_DoesNotExposeDerivedOrDuplicateVisualState()
    {
        const BindingFlags declaredPublic =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        Assert.Null(typeof(Arc).GetProperty("IsLargeArc", declaredPublic));
        Assert.Null(typeof(ProgressRing).GetProperty("Progress", declaredPublic));
        Assert.Null(typeof(ProgressRing).GetProperty("EndAngle", declaredPublic));
        Assert.Null(typeof(ProgressRing).GetProperty("IndeterminateAngle", declaredPublic));
        Assert.Null(typeof(ProgressRing).GetProperty("CoverRingStroke", declaredPublic));
        Assert.Null(typeof(ProgressRing).GetProperty("CoverRingVisibility", declaredPublic));

        // The range API is inherited from RangeBase rather than declared locally, so it is
        // asserted without DeclaredOnly while the duplicate/derived properties above still are.
        Assert.NotNull(typeof(ProgressRing).GetProperty(nameof(ProgressRing.Minimum)));
        Assert.NotNull(typeof(ProgressRing).GetProperty(nameof(ProgressRing.Maximum)));
        Assert.NotNull(typeof(ProgressRing).GetProperty(nameof(ProgressRing.Value)));
        Assert.NotNull(typeof(Control).GetProperty(nameof(Control.Background)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Value_RejectsValuesThatCannotProduceValidGeometry(double value)
    {
        RunOnStaThread(
            () =>
            {
                var ring = new ProgressRing();

                Assert.Throws<ArgumentException>(() => ring.Value = value);
            }
        );
    }

    private static ProgressRing CreateRing(double width, double height)
    {
        var resources = new ResourceDictionary { Source = CreateThemeUri() };
        var ring = new ProgressRing
        {
            Width = width,
            Height = height,
            Style = Assert.IsType<Style>(resources[typeof(ProgressRing)]),
        };

        Assert.True(ring.ApplyTemplate());

        return ring;
    }

    private static void Arrange(FrameworkElement element)
    {
        element.Measure(new Size(element.Width, element.Height));
        element.Arrange(new Rect(0, 0, element.Width, element.Height));
        element.UpdateLayout();
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

    private static Uri CreateThemeUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
    }

    private static void RunOnStaThread(Action action) => WpfTestThread.Run(action);

    /// <summary>
    /// Connects layout tests to a presentation source so <see cref="FrameworkElement.UpdateLayout"/>
    /// processes invalidations exactly as it does in an application window.
    /// </summary>
    private sealed class TestWindowHost : IDisposable
    {
        private readonly Window window;

        public TestWindowHost(UIElement content)
        {
            window = new Window
            {
                Content = content,
                Left = -10000,
                ShowActivated = false,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStyle = WindowStyle.None,
            };
            window.Show();
        }

        public void Dispose() => window.Close();
    }
}
