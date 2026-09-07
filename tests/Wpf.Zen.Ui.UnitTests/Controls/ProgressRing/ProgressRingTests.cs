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
using System.Windows.Media.Imaging;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies the range, drawing and animation contracts of <see cref="ProgressRing"/>.
/// </summary>
public class ProgressRingTests
{
    [Fact]
    public void Render_WithRectangularBounds_DrawsCenteredCircleAndUpdatesOnResize()
    {
        RunOnStaThread(
            () =>
            {
                var ring = CreateRing(120, 60);
                ring.Background = Brushes.Red;
                ring.Foreground = Brushes.Blue;
                ring.Value = 50;

                Render(ring);
                GeometryDrawing[] drawings = GetGeometryDrawings(ring);

                Assert.Equal(2, drawings.Length);
                AssertEllipse(drawings[0], Brushes.Red, new Point(60, 30), 26.25d, 7.5d);
                Assert.Equal(7.5d, drawings[1].Pen!.Thickness);
                Assert.Equal(Colors.Blue, GetPenColor(drawings[1]));

                ring.Width = 40;
                ring.Height = 100;
                Render(ring);
                drawings = GetGeometryDrawings(ring);

                Assert.Equal(new Size(40, 100), ring.RenderSize);
                AssertEllipse(drawings[0], Brushes.Red, new Point(20, 50), 17.5d, 5.0d);
                Assert.Equal(5.0d, drawings[1].Pen!.Thickness);

                ring.StrokeThickness = 3;
                Render(ring);
                drawings = GetGeometryDrawings(ring);

                AssertEllipse(drawings[0], Brushes.Red, new Point(20, 50), 18.5d, 3.0d);
                Assert.Equal(3.0d, drawings[1].Pen!.Thickness);
            }
        );
    }

    [Fact]
    public void DeterminateRange_RendersEmptyPartialAndCompleteStates()
    {
        RunOnStaThread(
            () =>
            {
                var ring = CreateRing(60, 60);
                ring.Foreground = Brushes.Blue;
                ring.Minimum = 20;
                ring.Maximum = 60;

                ring.Value = 20;
                Render(ring);
                Assert.Empty(GetGeometryDrawings(ring));

                ring.Value = 40;
                Render(ring);
                GeometryDrawing partial = Assert.Single(GetGeometryDrawings(ring));
                Assert.IsType<StreamGeometry>(partial.Geometry);
                Assert.Equal(Colors.Blue, GetPenColor(partial));

                ring.Value = 100;
                Assert.Equal(60.0d, ring.Value);
                Render(ring);
                Assert.IsType<EllipseGeometry>(Assert.Single(GetGeometryDrawings(ring)).Geometry);

                ring.Minimum = 80;
                Assert.Equal(80.0d, ring.Maximum);
                Assert.Equal(80.0d, ring.Value);
                Render(ring);
                Assert.Empty(GetGeometryDrawings(ring));
            }
        );
    }

    [Fact]
    public void PartialIndicator_UsesConfiguredLineCap()
    {
        RunOnStaThread(
            () =>
            {
                var ring = CreateRing(60, 60);
                ring.Foreground = Brushes.Blue;
                ring.Value = 25;
                ring.StrokeLineCap = PenLineCap.Square;

                Render(ring);
                Pen pen = Assert.Single(GetGeometryDrawings(ring)).Pen!;

                Assert.Equal(PenLineCap.Square, pen.StartLineCap);
                Assert.Equal(PenLineCap.Square, pen.EndLineCap);
            }
        );
    }

    [Fact]
    public void IsIndeterminate_UsesInternalAnimationWithoutTemplateParts()
    {
        RunOnStaThread(
            () =>
            {
                var ring = CreateRing(80, 50);
                ring.Foreground = Brushes.Blue;
                ring.Value = 100;

                using var host = new TestWindowHost(ring);
                Render(ring);
                Assert.IsType<EllipseGeometry>(Assert.Single(GetGeometryDrawings(ring)).Geometry);

                ring.IsIndeterminate = true;
                Render(ring);

                Assert.True(ring.HasAnimatedProperties);
                Assert.IsType<StreamGeometry>(Assert.Single(GetGeometryDrawings(ring)).Geometry);
                Assert.Null(ring.Template.FindName("PART_DeterminateArc", ring));
                Assert.Null(ring.Template.FindName("IndeterminateArc", ring));

                ring.IsIndeterminate = false;
                Render(ring);

                Assert.False(ring.HasAnimatedProperties);
                Assert.IsType<EllipseGeometry>(Assert.Single(GetGeometryDrawings(ring)).Geometry);
            }
        );
    }

    [Fact]
    public void BrushMetadata_InvalidatesDirectRendering()
    {
        RuntimeHelpers.RunClassConstructor(typeof(ProgressRing).TypeHandle);

        var backgroundMetadata = Assert.IsType<FrameworkPropertyMetadata>(
            ProgressRing.BackgroundProperty.GetMetadata(typeof(ProgressRing))
        );
        var foregroundMetadata = Assert.IsType<FrameworkPropertyMetadata>(
            ProgressRing.ForegroundProperty.GetMetadata(typeof(ProgressRing))
        );

        Assert.True(backgroundMetadata.AffectsRender);
        Assert.True(foregroundMetadata.AffectsRender);
        Assert.True(foregroundMetadata.Inherits);
    }

    [Fact]
    public void PublicApi_DoesNotExposeArcOrDerivedVisualState()
    {
        const BindingFlags declaredPublic =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        Assert.Null(typeof(ProgressRing).Assembly.GetType("Wpf.Zen.Ui.Controls.Arc"));
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

    private static void Render(ProgressRing ring)
    {
        var width = (int)ring.Width;
        var height = (int)ring.Height;

        ring.Measure(new Size(width, height));
        ring.Arrange(new Rect(0, 0, width, height));
        ring.UpdateLayout();

        // Rendering into a bitmap commits the retained OnRender drawing even when a test does not
        // attach the control to a presentation source.
        var bitmap = new RenderTargetBitmap(width, height, 96.0d, 96.0d, PixelFormats.Pbgra32);
        bitmap.Render(ring);
    }

    private static GeometryDrawing[] GetGeometryDrawings(Visual visual)
    {
        DrawingGroup? drawing = VisualTreeHelper.GetDrawing(visual);
        return drawing is null ? [] : EnumerateGeometryDrawings(drawing).ToArray();
    }

    private static IEnumerable<GeometryDrawing> EnumerateGeometryDrawings(Drawing drawing)
    {
        if (drawing is GeometryDrawing geometryDrawing)
        {
            yield return geometryDrawing;
        }

        if (drawing is not DrawingGroup group)
        {
            yield break;
        }

        foreach (Drawing child in group.Children)
        {
            foreach (GeometryDrawing descendant in EnumerateGeometryDrawings(child))
            {
                yield return descendant;
            }
        }
    }

    private static void AssertEllipse(
        GeometryDrawing drawing,
        SolidColorBrush brush,
        Point           center,
        double          radius,
        double          strokeThickness)
    {
        var ellipse = Assert.IsType<EllipseGeometry>(drawing.Geometry);

        Assert.Equal(center, ellipse.Center);
        Assert.Equal(radius, ellipse.RadiusX, 10);
        Assert.Equal(radius, ellipse.RadiusY, 10);
        Assert.Equal(brush.Color, GetPenColor(drawing));
        Assert.Equal(strokeThickness, drawing.Pen!.Thickness, 10);
    }

    private static Color GetPenColor(GeometryDrawing drawing)
    {
        return Assert.IsType<SolidColorBrush>(drawing.Pen!.Brush).Color;
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
    /// Connects animation tests to a presentation source so WPF creates and advances clocks under
    /// the same loaded-state contract used by an application window.
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
