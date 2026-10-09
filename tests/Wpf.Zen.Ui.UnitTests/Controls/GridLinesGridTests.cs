// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies the custom line drawing against WPF's actual visual tree and rasterizer.
/// </summary>
public class GridLinesGridTests
{
    /// <summary>
    /// Lines stay above children and follow definition and visibility changes.
    /// </summary>
    [Fact]
    public void Pens_RenderAboveSpanningChildren_AndFollowLayoutAndVisibility()
    {
        WpfTestThread.Run(() =>
        {
            var grid = CreateGrid();
            var child = new Border { Background = Brushes.White };
            Grid.SetColumnSpan(child, 2);
            Grid.SetRowSpan(child, 2);
            Panel.SetZIndex(child, 100);
            grid.Children.Add(child);
            Layout(grid);

            Assert.Equal(Colors.Red, ReadColor(grid, 39, 10));
            Assert.Equal(Colors.Blue, ReadColor(grid, 10, 29));
            Assert.Equal(Colors.White, ReadColor(grid, 10, 10));
            Assert.Single(grid.Children);
            Assert.Equal(new Size(120, 80), child.RenderSize);

            grid.ColumnDefinitions[0].Width = new GridLength(60);
            grid.UpdateLayout();
            Assert.Equal(Colors.White, ReadColor(grid, 39, 10));
            Assert.Equal(Colors.Red, ReadColor(grid, 59, 10));

            grid.ShowGridLines = false;
            grid.UpdateLayout();
            Assert.Equal(Colors.White, ReadColor(grid, 59, 10));
            Assert.Equal(1, VisualTreeHelper.GetChildrenCount(grid));
            grid.ShowGridLines = true;
            grid.UpdateLayout();
            Assert.Equal(Colors.Red, ReadColor(grid, 59, 10));

            grid.ColumnDefinitions.RemoveAt(1);
            grid.RowDefinitions.Clear();
            grid.UpdateLayout();
            Assert.Equal(Colors.White, ReadColor(grid, 59, 10));
            Assert.Equal(Colors.White, ReadColor(grid, 10, 29));
        });
    }

    /// <summary>
    /// Freezable subproperty changes update both line appearance and suppression.
    /// </summary>
    [Fact]
    public void MutablePensAndBrushes_RefreshWithoutExplicitInvalidation()
    {
        WpfTestThread.Run(() =>
        {
            var grid = CreateGrid();
            var brush = new SolidColorBrush(Colors.Red);
            var pen = new Pen(brush, 2);
            grid.VerticalGridLinePen = pen;
            Layout(grid);
            Assert.Equal(Colors.Red, ReadColor(grid, 39, 10));

            brush.Color = Colors.Green;
            pen.Thickness = 6;
            grid.UpdateLayout();
            Assert.Equal(Colors.Green, ReadColor(grid, 37, 10));

            pen.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
            grid.UpdateLayout();
            Assert.Equal(Colors.Green, ReadColor(grid, 39, 5));
            Assert.Equal(0, ReadColor(grid, 39, 17).A);

            pen.Thickness = 0;
            grid.UpdateLayout();
            Assert.Equal(0, ReadColor(grid, 39, 5).A);
            grid.HorizontalGridLinePen = null;
            grid.UpdateLayout();
            Assert.Equal(0, ReadColor(grid, 10, 29).A);
        });
    }

    /// <summary>
    /// XAML-defined pens follow the rounded boundaries and inherited RTL transform.
    /// </summary>
    [Fact]
    public void XamlPens_UseRoundedActualOffsets_InRightToLeftGrid()
    {
        WpfTestThread.Run(() =>
        {
            const string xaml = """
                <ui:GridLinesGrid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"
                    ShowGridLines="True" FlowDirection="RightToLeft" UseLayoutRounding="True">
                    <ui:GridLinesGrid.VerticalGridLinePen>
                        <Pen Brush="Red" Thickness="2" />
                    </ui:GridLinesGrid.VerticalGridLinePen>
                    <ui:GridLinesGrid.ColumnDefinitions>
                        <ColumnDefinition Width="40.3" />
                        <ColumnDefinition Width="*" />
                    </ui:GridLinesGrid.ColumnDefinitions>
                </ui:GridLinesGrid>
                """;
            var grid = (GridLinesGrid)XamlReader.Parse(xaml);
            var host = new Grid();
            host.Children.Add(grid);
            Layout(host);
            Assert.Equal(40, grid.ColumnDefinitions[1].Offset);
            Assert.Equal(Colors.Red, ReadColor(host, 79, 10));
            Assert.Equal(0, ReadColor(host, 39, 10).A);
        });
    }

    /// <summary>
    /// A grid without definitions remains empty and can acquire lines after initial layout.
    /// </summary>
    [Fact]
    public void TrivialGrid_AndLateDefinitions_DoNotRequireExtraChildren()
    {
        WpfTestThread.Run(() =>
        {
            var grid = new GridLinesGrid();
            Assert.False(grid.ShowGridLines);
            Assert.True(grid.VerticalGridLinePen!.IsFrozen);
            grid.ShowGridLines = true;
            Layout(grid);
            Assert.Equal(0, VisualTreeHelper.GetChildrenCount(grid));
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.VerticalGridLinePen = new Pen(Brushes.Red, 2);
            grid.UpdateLayout();
            Assert.Equal(Colors.Red, ReadColor(grid, 59, 10));
            Assert.Empty(grid.Children);
        });
    }

    /// <summary>
    /// Creates fixed and star-sized cells with different pens for raster assertions.
    /// </summary>
    private static GridLinesGrid CreateGrid()
    {
        var grid = new GridLinesGrid
        {
            ShowGridLines = true,
            VerticalGridLinePen = new Pen(Brushes.Red, 2),
            HorizontalGridLinePen = new Pen(Brushes.Blue, 2),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
        grid.RowDefinitions.Add(new RowDefinition());
        return grid;
    }

    /// <summary>
    /// Completes initial layout; subsequent mutations must refresh through WPF's layout queue.
    /// </summary>
    private static void Layout(FrameworkElement element)
    {
        var size = new Size(120, 80);
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();
    }

    /// <summary>
    /// Samples actual composited pixels so line order and stale native drawings are observable.
    /// </summary>
    private static Color ReadColor(Visual visual, int x, int y)
    {
        var bitmap = new RenderTargetBitmap(120, 80, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }
}
