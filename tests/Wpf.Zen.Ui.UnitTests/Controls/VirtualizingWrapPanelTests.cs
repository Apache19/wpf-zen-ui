// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using UiItemsAlignment = Wpf.Zen.Ui.Controls.VirtualizingWrapPanelItemsAlignment;
using UiItemSizingMode = Wpf.Zen.Ui.Controls.VirtualizingWrapPanelItemSizingMode;
using UiVirtualizingWrapPanel = Wpf.Zen.Ui.Controls.VirtualizingWrapPanel;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class VirtualizingWrapPanelTests
{
    [Fact]
    public void Defaults_DoNotImposeMediaGeometry()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel();

            Assert.True(double.IsNaN(panel.ItemAspectRatio));
            Assert.True(double.IsNaN(panel.ItemWidth));
            Assert.True(double.IsNaN(panel.ItemHeight));
            Assert.Equal(0D, panel.MinimumItemWidth);
            Assert.Equal(double.PositiveInfinity, panel.MaximumItemWidth);
            Assert.Equal(UiItemsAlignment.Start, panel.ItemsAlignment);
            Assert.Null(typeof(UiVirtualizingWrapPanel).GetProperty("PreferredItemWidth"));
            Assert.False(panel.IsVirtualizationActive);
            Assert.Equal(UiItemSizingMode.Natural, panel.ActualItemSizingMode);
            Assert.Equal(ScrollUnit.Pixel, panel.ActualScrollUnit);
        });
    }

    [Fact]
    public void FixedLayout_AlignsSharedColumnGridWithoutResizingItems()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                HorizontalSpacing = 10D,
                ItemWidth = 100D,
                ItemHeight = 60D,
                ItemsAlignment = UiItemsAlignment.Center,
            };
            for (int index = 0; index < 5; index++)
            {
                panel.Children.Add(new Border());
            }

            Arrange(panel, new Size(500D, 150D));

            Assert.Equal(4, panel.Columns);
            Assert.Equal(35D, panel.Children[0].TranslatePoint(default, panel).X, 3);
            Assert.Equal(365D, panel.Children[3].TranslatePoint(default, panel).X, 3);
            Assert.Equal(35D, panel.Children[4].TranslatePoint(default, panel).X, 3);
            Assert.All(panel.Children.Cast<UIElement>(), child =>
                Assert.Equal(100D, child.RenderSize.Width, 3)
            );

            panel.ItemsAlignment = UiItemsAlignment.End;
            panel.UpdateLayout();

            Assert.Equal(70D, panel.Children[0].TranslatePoint(default, panel).X, 3);
            Assert.Equal(70D, panel.Children[4].TranslatePoint(default, panel).X, 3);
        });
    }

    [Fact]
    public void FixedLayout_AlignsPopulatedColumnsWhenCollectionIsShorterThanOneRow()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                HorizontalSpacing = 10D,
                ItemWidth = 100D,
                ItemHeight = 60D,
                ItemsAlignment = UiItemsAlignment.Center,
            };
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());

            Arrange(panel, new Size(500D, 100D));

            Assert.Equal(4, panel.Columns);
            Assert.Equal(145D, panel.Children[0].TranslatePoint(default, panel).X, 3);
            Assert.Equal(255D, panel.Children[1].TranslatePoint(default, panel).X, 3);
        });
    }

    [Fact]
    public void NaturalLayout_AlignsVariableWidthRows()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                HorizontalSpacing = 10D,
                ItemsAlignment = UiItemsAlignment.Center,
            };
            panel.Children.Add(new Border { Width = 100D, Height = 30D });
            panel.Children.Add(new Border { Width = 120D, Height = 30D });
            panel.Children.Add(new Border { Width = 200D, Height = 30D });

            Arrange(panel, new Size(250D, 100D));

            Assert.Equal(10D, panel.Children[0].TranslatePoint(default, panel).X, 3);
            Assert.Equal(120D, panel.Children[1].TranslatePoint(default, panel).X, 3);
            Assert.Equal(25D, panel.Children[2].TranslatePoint(default, panel).X, 3);
        });
    }

    [Fact]
    public void InvalidItemsAlignment_IsRejectedByDependencyProperty()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel();

            Assert.Throws<ArgumentException>(() =>
                panel.ItemsAlignment = (UiItemsAlignment)int.MaxValue
            );
        });
    }

    [Fact]
    public void ResponsiveLayout_DividesTheViewportUsingMaximumWidth()
    {
        var layout = UiVirtualizingWrapPanel.CalculateLayout(
            1000D,
            20D,
            double.NaN,
            0D,
            400D,
            16D / 9D,
            double.NaN
        );

        Assert.Equal(3, layout.Columns);
        Assert.Equal(320D, layout.ItemWidth, 3);
        Assert.Equal(180D, layout.ItemHeight, 3);
    }

    [Fact]
    public void ResponsiveLayout_AddsColumnOnlyAfterMaximumWouldBeExceeded()
    {
        var exactLayout = UiVirtualizingWrapPanel.CalculateLayout(
            820D,
            20D,
            double.NaN,
            0D,
            400D,
            2D,
            double.NaN
        );
        var overflowLayout = UiVirtualizingWrapPanel.CalculateLayout(
            821D,
            20D,
            double.NaN,
            0D,
            400D,
            2D,
            double.NaN
        );

        Assert.Equal(2, exactLayout.Columns);
        Assert.Equal(400D, exactLayout.ItemWidth, 3);
        Assert.Equal(3, overflowLayout.Columns);
        Assert.Equal(781D / 3D, overflowLayout.ItemWidth, 3);
    }

    [Fact]
    public void ResponsiveLayout_DoesNotAddEmptyColumnsAfterAllItemsFitOneRow()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                HorizontalSpacing = 10D,
                ItemAspectRatio = 1D,
                MaximumItemWidth = 300D,
                ItemsAlignment = UiItemsAlignment.Center,
            };
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());

            Arrange(panel, new Size(500D, 320D));

            Assert.Equal(2, panel.Columns);
            Assert.Equal(245D, panel.ActualItemWidth, 3);

            panel.Width = 950D;
            panel.Height = 320D;
            Relayout(panel, new Size(950D, 320D));

            Assert.Equal(2, panel.Columns);
            Assert.Equal(300D, panel.ActualItemWidth, 3);
            Assert.Equal(170D, panel.Children[0].TranslatePoint(default, panel).X, 3);
            Assert.Equal(480D, panel.Children[1].TranslatePoint(default, panel).X, 3);
        });
    }

    [Fact]
    public void ResponsiveRange_DelaysNewColumnUntilItsMinimumWidthFits()
    {
        var beforeBreakpoint = UiVirtualizingWrapPanel.CalculateLayout(
            939D,
            20D,
            double.NaN,
            300D,
            400D,
            2D,
            double.NaN
        );
        var atBreakpoint = UiVirtualizingWrapPanel.CalculateLayout(
            940D,
            20D,
            double.NaN,
            300D,
            400D,
            2D,
            double.NaN
        );

        Assert.Equal(2, beforeBreakpoint.Columns);
        Assert.Equal(400D, beforeBreakpoint.ItemWidth, 3);
        Assert.Equal(3, atBreakpoint.Columns);
        Assert.Equal(300D, atBreakpoint.ItemWidth, 3);
    }

    [Fact]
    public void MinimumWidthAlone_ExplicitlyEnablesResponsiveSizing()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                HorizontalSpacing = 20D,
                MinimumItemWidth = 300D,
                ItemAspectRatio = 2D,
            };
            for (int index = 0; index < 10; index++)
            {
                panel.Children.Add(new Border());
            }

            Arrange(panel, new Size(1000D, 400D));

            Assert.Equal(UiItemSizingMode.Responsive, panel.ActualItemSizingMode);
            Assert.Equal(3, panel.Columns);
            Assert.Equal(320D, panel.ActualItemWidth, 3);
            Assert.Equal(160D, panel.ActualItemHeight, 3);
        });
    }

    [Fact]
    public void MinimumWidthAlone_ProvidesPredictableGeometryForVirtualization()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 1000,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.HorizontalSpacingProperty, 8D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemAspectRatioProperty, 1.5D);
                    factory.SetValue(UiVirtualizingWrapPanel.MinimumItemWidthProperty, 180D);
                }
            );

            Arrange(listBox, new Size(640D, 320D));
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );

            Assert.Equal(UiItemSizingMode.Responsive, panel.ActualItemSizingMode);
            Assert.True(panel.IsVirtualizationActive);
            Assert.Equal(3, panel.Columns);
            Assert.Equal((panel.ViewportWidth - 16D) / 3D, panel.ActualItemWidth, 3);
            Assert.True(panel.ActualItemWidth >= panel.MinimumItemWidth);
            Assert.InRange(panel.Children.Count, 1, 30);
        });
    }

    [Fact]
    public void ResponsiveMinimum_TakesPrecedenceWhenBoundsCross()
    {
        var layout = UiVirtualizingWrapPanel.CalculateLayout(
            650D,
            0D,
            double.NaN,
            300D,
            200D,
            1D,
            double.NaN
        );

        Assert.Equal(2, layout.Columns);
        Assert.Equal(300D, layout.ItemWidth, 3);
    }

    [Fact]
    public void ResponsiveMinimum_UsesHorizontalOverflowWhenOneCellCannotFit()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                MinimumItemWidth = 300D,
                MaximumItemWidth = 400D,
                ItemHeight = 100D,
            };
            panel.Children.Add(new Border());

            Arrange(panel, new Size(200D, 120D));

            Assert.Equal(1, panel.Columns);
            Assert.Equal(300D, panel.ActualItemWidth, 3);
            Assert.Equal(300D, panel.ExtentWidth, 3);
        });
    }

    [Fact]
    public void AspectRatioWithoutMaximum_DoesNotEnableAutomaticSizing()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                HorizontalSpacing = 10D,
                ItemAspectRatio = 2D,
            };
            panel.Children.Add(new Border { Width = 120D });
            panel.Children.Add(new Border { Width = 120D });

            Arrange(panel, new Size(650D, 200D));

            Assert.Equal(UiItemSizingMode.Natural, panel.ActualItemSizingMode);
            Assert.Equal(0, panel.Columns);
            Assert.Equal(120D, panel.Children[0].RenderSize.Width, 3);
            Assert.Equal(60D, panel.Children[0].RenderSize.Height, 3);
            Assert.Equal(130D, panel.Children[1].TranslatePoint(default, panel).X, 3);
        });
    }

    [Fact]
    public void FixedItemDimensions_AreNeverResizedToFillTheViewport()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                HorizontalSpacing = 10D,
                ItemWidth = 160D,
                ItemHeight = 90D,
                MaximumItemWidth = 100D,
            };
            for (int index = 0; index < 4; index++)
            {
                panel.Children.Add(new Border());
            }

            Arrange(panel, new Size(650D, 220D));

            Assert.Equal(UiItemSizingMode.Fixed, panel.ActualItemSizingMode);
            Assert.Equal(3, panel.Columns);
            Assert.Equal(160D, panel.ActualItemWidth, 3);
            Assert.Equal(90D, panel.ActualItemHeight, 3);
            Assert.Equal(170D, panel.Children[1].TranslatePoint(default, panel).X, 3);
        });
    }

    [Fact]
    public void HeightAndAspectRatio_DeriveFixedWidthWithoutResponsiveStretching()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                HorizontalSpacing = 8D,
                ItemHeight = 90D,
                ItemAspectRatio = 16D / 9D,
            };
            for (int index = 0; index < 4; index++)
            {
                panel.Children.Add(new Border());
            }

            Arrange(panel, new Size(500D, 220D));

            Assert.Equal(UiItemSizingMode.Fixed, panel.ActualItemSizingMode);
            Assert.Equal(3, panel.Columns);
            Assert.Equal(160D, panel.ActualItemWidth, 3);
            Assert.Equal(90D, panel.ActualItemHeight, 3);
            Assert.Equal(160D, panel.Children[0].RenderSize.Width, 3);
            Assert.Equal(168D, panel.Children[1].TranslatePoint(default, panel).X, 3);
            Assert.Equal(0D, panel.Children[3].TranslatePoint(default, panel).X, 3);
        });
    }

    [Fact]
    public void FiniteMaximum_KeepsHeightAndAspectRatioInResponsiveMode()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                ItemHeight = 90D,
                ItemAspectRatio = 2D,
                MaximumItemWidth = 200D,
            };
            panel.Children.Add(new Border());

            Arrange(panel, new Size(500D, 120D));

            Assert.Equal(
                UiItemSizingMode.Responsive,
                panel.ActualItemSizingMode
            );
            Assert.Equal(1, panel.Columns);
            Assert.Equal(200D, panel.ActualItemWidth, 3);
            Assert.Equal(90D, panel.ActualItemHeight, 3);
        });
    }

    [Fact]
    public void UniformViewport_ExactlyFittingRowsDoesNotPublishFalseOverflow()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                ItemAspectRatio = 1D,
                MaximumItemWidth = 100D,
                VerticalSpacing = 10D,
            };
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());

            Arrange(panel, new Size(100D, 210D));

            Assert.Equal(panel.ExtentHeight, panel.ViewportHeight, 3);
        });
    }

    [Fact]
    public void InvalidGeometryValues_AreRejectedByDependencyProperties()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel();

            Assert.Throws<ArgumentException>(() => panel.HorizontalSpacing = -1D);
            Assert.Throws<ArgumentException>(() => panel.VerticalSpacing = double.PositiveInfinity);
            Assert.Throws<ArgumentException>(() => panel.ItemAspectRatio = 0D);
            Assert.Throws<ArgumentException>(() => panel.ItemWidth = 0D);
            Assert.Throws<ArgumentException>(() => panel.MinimumItemWidth = -1D);
            Assert.Throws<ArgumentException>(() => panel.MinimumItemWidth = double.PositiveInfinity);
            Assert.Throws<ArgumentException>(() => panel.MaximumItemWidth = double.NaN);
            Assert.Throws<ArgumentException>(() => panel.ItemHeight = double.PositiveInfinity);
        });
    }

    [Fact]
    public void UniformLayout_ResizesItemsAndNeverCreatesAPartialColumn()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                HorizontalSpacing = 10D,
                ItemAspectRatio = 2D,
                MaximumItemWidth = 300D,
            };
            for (int index = 0; index < 5; index++)
            {
                panel.Children.Add(new Border());
            }

            Arrange(panel, new Size(650D, 400D));

            Assert.Equal(3, panel.Columns);
            Assert.Equal(210D, panel.ActualItemWidth, 3);
            Assert.Equal(105D, panel.ActualItemHeight, 3);
            Assert.Equal(
                UiItemSizingMode.Responsive,
                panel.ActualItemSizingMode
            );
            Assert.Equal(0D, panel.Children[0].TranslatePoint(default, panel).X, 3);
            Assert.Equal(220D, panel.Children[1].TranslatePoint(default, panel).X, 3);
            Assert.Equal(440D, panel.Children[2].TranslatePoint(default, panel).X, 3);
            Assert.Equal(0D, panel.Children[3].TranslatePoint(default, panel).X, 3);
        });
    }

    [Fact]
    public void MissingPredictableHeight_FallsBackToNaturalWrapping()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                HorizontalSpacing = 10D,
                VerticalSpacing = 5D,
            };
            panel.Children.Add(new Border { Width = 100D, Height = 20D });
            panel.Children.Add(new Border { Width = 100D, Height = 30D });
            panel.Children.Add(new Border { Width = 100D, Height = 40D });

            Arrange(panel, new Size(250D, 200D));

            Assert.False(panel.IsVirtualizationActive);
            Assert.Equal(0, panel.Columns);
            Assert.Equal(0D, panel.Children[0].TranslatePoint(default, panel).Y, 3);
            Assert.Equal(0D, panel.Children[1].TranslatePoint(default, panel).Y, 3);
            Assert.Equal(35D, panel.Children[2].TranslatePoint(default, panel).Y, 3);
        });
    }

    [Fact]
    public void ItemsHost_RealizesVisibleRowsAndScrollsByLogicalRow()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 1000,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.HorizontalSpacingProperty, 8D);
                    factory.SetValue(UiVirtualizingWrapPanel.VerticalSpacingProperty, 8D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemAspectRatioProperty, 1.5D);
                    factory.SetValue(UiVirtualizingWrapPanel.MaximumItemWidthProperty, 300D);
                }
            );

            Arrange(listBox, new Size(640D, 320D));
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );
            int initiallyRealized = panel.Children.Count;

            Assert.True(panel.IsVirtualizationActive);
            Assert.Equal(ScrollUnit.Item, panel.ActualScrollUnit);
            Assert.InRange(initiallyRealized, 1, 30);
            Assert.True(panel.Columns >= 2);
            Assert.True(panel.ExtentHeight > panel.ViewportHeight);

            panel.LineDown();
            listBox.UpdateLayout();

            Assert.Equal(1D, panel.VerticalOffset);

            panel.SetVerticalOffset(1.75D);
            listBox.UpdateLayout();

            Assert.Equal(1D, panel.VerticalOffset);
            Assert.InRange(panel.Children.Count, 1, 30);
            Assert.Null(listBox.ItemContainerGenerator.ContainerFromIndex(0));
            Assert.NotNull(
                listBox.ItemContainerGenerator.ContainerFromIndex(panel.Columns)
            );

            panel.SetVerticalOffset(20D);
            listBox.UpdateLayout();
            Assert.NotNull(
                listBox.ItemContainerGenerator.ContainerFromIndex(20 * panel.Columns)
            );

            panel.SetVerticalOffset(0D);
            listBox.UpdateLayout();
            Assert.NotNull(listBox.ItemContainerGenerator.ContainerFromIndex(0));
        });
    }

    [Fact]
    public void ItemsAlignment_DoesNotDisableRowVirtualization()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 1000,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.HorizontalSpacingProperty, 4D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemWidthProperty, 80D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemHeightProperty, 80D);
                    factory.SetValue(
                        UiVirtualizingWrapPanel.ItemsAlignmentProperty,
                        UiItemsAlignment.Center
                    );
                }
            );

            Arrange(listBox, new Size(520D, 260D));
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );

            Assert.True(panel.IsVirtualizationActive);
            var occupiedWidth = (panel.Columns * 80D) + ((panel.Columns - 1) * 4D);
            var expectedRowOrigin = (panel.ViewportWidth - occupiedWidth) / 2D;
            Assert.True(expectedRowOrigin > 0D);
            Assert.Equal(
                expectedRowOrigin,
                panel.Children[0].TranslatePoint(default, panel).X,
                3
            );

            panel.SetVerticalOffset(20D);
            listBox.UpdateLayout();

            UIElement firstItemInRow = Assert.IsAssignableFrom<UIElement>(
                listBox.ItemContainerGenerator.ContainerFromIndex(20 * panel.Columns)
            );
            Assert.Equal(
                expectedRowOrigin,
                firstItemInRow.TranslatePoint(default, panel).X,
                3
            );
            Assert.InRange(panel.Children.Count, 1, 30);
        });
    }

    [Fact]
    public void RecyclingMode_ReusesRealizedContainersAcrossDistantScrollJump()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 1000,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.ItemWidthProperty, 80D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemHeightProperty, 80D);
                    factory.SetValue(UiVirtualizingWrapPanel.VerticalSpacingProperty, 4D);
                }
            );

            Arrange(listBox, new Size(500D, 260D));
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );
            var initialContainers = new HashSet<UIElement>(panel.Children.Cast<UIElement>());

            panel.SetVerticalOffset(100D);
            listBox.UpdateLayout();

            Assert.True(panel.IsVirtualizationActive);
            Assert.NotEmpty(initialContainers);
            int reusedContainers = panel.Children
                .Cast<UIElement>()
                .Count(initialContainers.Contains);
            Assert.InRange(
                reusedContainers,
                Math.Max(1, panel.Children.Count - panel.Columns),
                panel.Children.Count
            );
            Assert.NotNull(
                listBox.ItemContainerGenerator.ContainerFromIndex(100 * panel.Columns)
            );
        });
    }

    [Fact]
    public void StandardMode_GeneratesDistantRangeAfterBatchedCleanup()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 1000,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.ItemWidthProperty, 80D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemHeightProperty, 80D);
                }
            );
            VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Standard);

            Arrange(listBox, new Size(500D, 260D));
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );

            panel.SetVerticalOffset(100D);
            listBox.UpdateLayout();

            Assert.Null(listBox.ItemContainerGenerator.ContainerFromIndex(0));
            Assert.NotNull(
                listBox.ItemContainerGenerator.ContainerFromIndex(100 * panel.Columns)
            );
            Assert.InRange(panel.Children.Count, 1, 30);
        });
    }

    [Fact]
    public void ScrollViewer_DelegatesLineScrollingUsingConfiguredScrollUnit()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 1000,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.ItemWidthProperty, 160D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemHeightProperty, 90D);
                    factory.SetValue(UiVirtualizingWrapPanel.VerticalSpacingProperty, 8D);
                }
            );
            VirtualizingPanel.SetScrollUnit(listBox, ScrollUnit.Item);

            Arrange(listBox, new Size(500D, 260D));
            var scrollViewer = Assert.IsAssignableFrom<System.Windows.Controls.ScrollViewer>(
                FindVisualChild<System.Windows.Controls.ScrollViewer>(listBox)
            );
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );

            Assert.True(scrollViewer.CanContentScroll);
            Assert.Equal(ScrollUnit.Item, VirtualizingPanel.GetScrollUnit(listBox));
            Assert.True(panel.IsVirtualizationActive);
            Assert.Equal(ScrollUnit.Item, panel.ActualScrollUnit);

            scrollViewer.LineDown();
            listBox.UpdateLayout();

            Assert.Equal(1D, panel.VerticalOffset);

            VirtualizingPanel.SetScrollUnit(listBox, ScrollUnit.Pixel);
            listBox.UpdateLayout();
            Assert.Equal(ScrollUnit.Pixel, panel.ActualScrollUnit);
            scrollViewer.LineDown();
            listBox.UpdateLayout();

            Assert.Equal(114D, panel.VerticalOffset);
        });
    }

    [Fact]
    public void IsVirtualizationActive_TracksEffectiveRuntimeBehavior()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 1000,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.ItemWidthProperty, 160D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemHeightProperty, 90D);
                }
            );

            Arrange(listBox, new Size(500D, 260D));
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );
            Assert.True(panel.IsVirtualizationActive);
            Assert.True(panel.Children.Count < listBox.Items.Count);

            VirtualizingPanel.SetIsVirtualizing(listBox, false);
            listBox.UpdateLayout();

            Assert.False(panel.IsVirtualizationActive);
            Assert.Equal(listBox.Items.Count, panel.Children.Count);

            VirtualizingPanel.SetIsVirtualizing(listBox, true);
            listBox.UpdateLayout();

            Assert.True(panel.IsVirtualizationActive);
            Assert.True(panel.Children.Count < listBox.Items.Count);
        });
    }

    [Fact]
    public void FixedItemGeometry_VirtualizesWithoutEnablingAutomaticSizing()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 1000,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.ItemWidthProperty, 160D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemHeightProperty, 90D);
                    factory.SetValue(UiVirtualizingWrapPanel.HorizontalSpacingProperty, 8D);
                    factory.SetValue(UiVirtualizingWrapPanel.VerticalSpacingProperty, 8D);
                }
            );

            Arrange(listBox, new Size(500D, 260D));
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );

            Assert.True(panel.IsVirtualizationActive);
            Assert.Equal(UiItemSizingMode.Fixed, panel.ActualItemSizingMode);
            Assert.Equal(
                (int)Math.Floor((panel.ViewportWidth + 8D) / (160D + 8D)),
                panel.Columns
            );
            Assert.Equal(160D, panel.ActualItemWidth);
            Assert.InRange(panel.Children.Count, 1, 12);
        });
    }

    [Fact]
    public void HeightAndAspectRatio_DerivedGeometrySupportsRowVirtualization()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 1000,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.ItemHeightProperty, 90D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemAspectRatioProperty, 16D / 9D);
                    factory.SetValue(UiVirtualizingWrapPanel.HorizontalSpacingProperty, 8D);
                    factory.SetValue(UiVirtualizingWrapPanel.VerticalSpacingProperty, 8D);
                }
            );

            Arrange(listBox, new Size(500D, 260D));
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );

            Assert.True(panel.IsVirtualizationActive);
            Assert.Equal(UiItemSizingMode.Fixed, panel.ActualItemSizingMode);
            Assert.Equal(160D, panel.ActualItemWidth, 3);
            Assert.Equal(90D, panel.ActualItemHeight, 3);
            Assert.Equal(
                (int)Math.Floor((panel.ViewportWidth + 8D) / (160D + 8D)),
                panel.Columns
            );
            Assert.InRange(panel.Children.Count, 1, 12);
        });
    }

    [Fact]
    public void ItemsHost_WithUnknownRowHeight_UsesResponsiveWidthButGeneratesAllItems()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 40,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.MaximumItemWidthProperty, 180D);
                }
            );

            Arrange(listBox, new Size(500D, 260D));
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );

            Assert.False(panel.IsVirtualizationActive);
            Assert.Equal(ScrollUnit.Pixel, panel.ActualScrollUnit);
            Assert.Equal(
                UiItemSizingMode.Responsive,
                panel.ActualItemSizingMode
            );
            Assert.Equal(3, panel.Columns);
            Assert.Equal(panel.ViewportWidth / panel.Columns, panel.ActualItemWidth, 3);
            Assert.Equal(0D, panel.ActualItemHeight);
            Assert.Equal(40, panel.Children.Count);
        });
    }

    [Fact]
    public void NaturalItemsHost_RebuildsGeneratorPositionsAfterCollectionChanges()
    {
        RunOnStaThread(static () =>
        {
            var items = new System.Collections.ObjectModel.ObservableCollection<int>(
                System.Linq.Enumerable.Range(0, 40)
            );
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 0,
                configurePanel: static _ => { }
            );
            listBox.ItemsSource = items;

            Arrange(listBox, new Size(500D, 260D));
            items.RemoveAt(0);
            items.Add(40);
            listBox.UpdateLayout();

            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );
            Assert.Equal(40, panel.Children.Count);
            Assert.Equal(
                1,
                listBox.ItemContainerGenerator.ItemFromContainer(panel.Children[0])
            );
            Assert.NotNull(listBox.ItemContainerGenerator.ContainerFromIndex(39));
        });
    }

    [Fact]
    public void MakeVisible_ScrollsBothAxesAndReturnsViewportRelativeBounds()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel
            {
                CanHorizontallyScroll = true,
                CanVerticallyScroll = true,
                ItemWidth = 200D,
                ItemHeight = 100D,
            };
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());
            panel.Children.Add(new Border());

            Arrange(panel, new Size(100D, 150D));
            Rect visible = panel.MakeVisible(
                panel.Children[2],
                new Rect(150D, 0D, 20D, 100D)
            );

            Assert.Equal(70D, panel.HorizontalOffset, 3);
            Assert.Equal(1.5D, panel.VerticalOffset, 3);
            Assert.Equal(new Rect(80D, 50D, 20D, 100D), visible);
        });
    }

    [Fact]
    public void RuntimeSizingModeChanges_PreservePhysicalScrollPosition()
    {
        RunOnStaThread(static () =>
        {
            var panel = new UiVirtualizingWrapPanel { CanVerticallyScroll = true };
            for (int index = 0; index < 6; index++)
            {
                panel.Children.Add(new Border { Width = 100D, Height = 100D });
            }

            Arrange(panel, new Size(100D, 150D));
            panel.SetVerticalOffset(150D);
            panel.ItemWidth = 100D;
            panel.ItemHeight = 100D;
            Relayout(panel, new Size(100D, 150D));

            Assert.Equal(UiItemSizingMode.Fixed, panel.ActualItemSizingMode);
            Assert.Equal(1D, panel.VerticalOffset, 3);

            panel.ItemWidth = double.NaN;
            panel.ItemHeight = double.NaN;
            Relayout(panel, new Size(100D, 150D));

            Assert.Equal(UiItemSizingMode.Natural, panel.ActualItemSizingMode);
            Assert.Equal(100D, panel.VerticalOffset, 3);
        });
    }

    [Fact]
    public void PixelScrollUnit_UsesPixelExtentAndKeepsVirtualization()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 1000,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.ItemWidthProperty, 160D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemHeightProperty, 90D);
                    factory.SetValue(UiVirtualizingWrapPanel.VerticalSpacingProperty, 8D);
                }
            );
            VirtualizingPanel.SetScrollUnit(listBox, ScrollUnit.Pixel);

            Arrange(listBox, new Size(500D, 260D));
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );

            Assert.True(panel.IsVirtualizationActive);
            Assert.Equal(ScrollUnit.Pixel, panel.ActualScrollUnit);
            Assert.True(panel.ExtentHeight > 30000D);
            Assert.True(panel.ViewportHeight > 200D);

            panel.LineDown();
            listBox.UpdateLayout();

            Assert.Equal(16D, panel.VerticalOffset);
            Assert.InRange(panel.Children.Count, 1, 15);
        });
    }

    [Fact]
    public void VirtualizationCacheLength_RealizesCompleteAdjacentRows()
    {
        RunOnStaThread(static () =>
        {
            System.Windows.Controls.ListBox listBox = CreateListBox(
                itemCount: 1000,
                configurePanel: factory =>
                {
                    factory.SetValue(UiVirtualizingWrapPanel.ItemWidthProperty, 160D);
                    factory.SetValue(UiVirtualizingWrapPanel.ItemHeightProperty, 90D);
                }
            );
            VirtualizingPanel.SetCacheLengthUnit(
                listBox,
                VirtualizationCacheLengthUnit.Item
            );

            Arrange(listBox, new Size(500D, 260D));
            UiVirtualizingWrapPanel panel = Assert.IsType<UiVirtualizingWrapPanel>(
                FindVisualChild<UiVirtualizingWrapPanel>(listBox)
            );
            VirtualizingPanel.SetCacheLength(
                listBox,
                new VirtualizationCacheLength(panel.Columns, panel.Columns)
            );
            panel.SetVerticalOffset(20D);
            listBox.UpdateLayout();

            int precedingCachedRowIndex = 19 * panel.Columns;
            int precedingUncachedRowIndex = 18 * panel.Columns;
            Assert.NotNull(
                listBox.ItemContainerGenerator.ContainerFromIndex(precedingCachedRowIndex)
            );
            Assert.Null(
                listBox.ItemContainerGenerator.ContainerFromIndex(precedingUncachedRowIndex)
            );
        });
    }

    [Fact]
    public void Panel_IsAvailableFromThePublicXamlNamespace()
    {
        RunOnStaThread(static () =>
        {
            const string xaml =
                "<ui:VirtualizingWrapPanel xmlns:ui=\"http://schemas.thezendev.com/wpf/2027\" ItemAspectRatio=\"1.5\" MinimumItemWidth=\"180\" ItemsAlignment=\"Center\" />";

            var panel = Assert.IsType<UiVirtualizingWrapPanel>(XamlReader.Parse(xaml));

            Assert.Equal(1.5D, panel.ItemAspectRatio);
            Assert.Equal(180D, panel.MinimumItemWidth);
            Assert.Equal(UiItemsAlignment.Center, panel.ItemsAlignment);
        });
    }

    private static System.Windows.Controls.ListBox CreateListBox(
        int itemCount,
        Action<FrameworkElementFactory> configurePanel
    )
    {
#pragma warning disable CS0618 // FrameworkElementFactory remains the programmatic API for ItemsPanelTemplate.
        var panelFactory = new FrameworkElementFactory(typeof(UiVirtualizingWrapPanel));
#pragma warning restore CS0618
        configurePanel(panelFactory);
        var itemsPanel = new ItemsPanelTemplate(panelFactory);
        var listBox = new System.Windows.Controls.ListBox
        {
            ItemsPanel = itemsPanel,
            ItemsSource = System.Linq.Enumerable.Range(0, itemCount),
        };
        System.Windows.Controls.ScrollViewer.SetCanContentScroll(listBox, true);
        VirtualizingPanel.SetIsVirtualizing(listBox, true);
        VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Recycling);
        VirtualizingPanel.SetCacheLength(listBox, new VirtualizationCacheLength(0D));
        VirtualizingPanel.SetCacheLengthUnit(
            listBox,
            VirtualizationCacheLengthUnit.Item
        );
        return listBox;
    }

    private static void Arrange(FrameworkElement element, Size size)
    {
        var host = new Grid();
        host.Children.Add(element);
        element.Width = size.Width;
        element.Height = size.Height;
        host.Measure(size);
        host.Arrange(new Rect(default, size));
        host.UpdateLayout();
    }

    private static void Relayout(FrameworkElement element, Size size)
    {
        element.Measure(size);
        element.Arrange(new Rect(default, size));
        element.UpdateLayout();
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            T? descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }
}
