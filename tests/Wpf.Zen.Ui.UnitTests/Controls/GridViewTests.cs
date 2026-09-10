// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wpf.Zen.Ui.Theming;
using NativeGridViewColumn = System.Windows.Controls.GridViewColumn;
using NativeGridViewColumnHeader = System.Windows.Controls.GridViewColumnHeader;
using NativeGridViewColumnHeaderRole = System.Windows.Controls.GridViewColumnHeaderRole;
using NativeGridViewHeaderRowPresenter = System.Windows.Controls.GridViewHeaderRowPresenter;
using NativeGridViewRowPresenter = System.Windows.Controls.GridViewRowPresenter;
using UiDataGrid = Wpf.Zen.Ui.Controls.DataGrid;
using UiGridView = Wpf.Zen.Ui.Controls.GridView;
using UiGridViewColumn = Wpf.Zen.Ui.Controls.GridViewColumn;
using UiListView = Wpf.Zen.Ui.Controls.ListView;
using UiListViewItem = Wpf.Zen.Ui.Controls.ListViewItem;
using UiScrollBar = Wpf.Zen.Ui.Controls.UiScrollBar;
using UiScrollViewer = Wpf.Zen.Ui.Controls.UiScrollViewer;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class GridViewTests
{
    [Fact]
    public void Width_ParsesGridLengthFromXaml()
    {
        RunOnStaThread(static () =>
        {
            const string xaml =
                "<ui:GridViewColumn xmlns:ui=\"http://schemas.thezendev.com/wpf/2027\" Width=\"2*\" />";

            var column = Assert.IsType<UiGridViewColumn>(XamlReader.Parse(xaml));

            Assert.Equal(GridUnitType.Star, column.Width.GridUnitType);
            Assert.Equal(2.0, column.Width.Value);
        });
    }

    [Fact]
    public void StarColumns_DivideRemainingWidthByWeight()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            var fixedColumn = new UiGridViewColumn { Header = "Fixed", Width = new GridLength(100) };
            var firstStarColumn = new UiGridViewColumn
            {
                Header = "First",
                Width = new GridLength(1, GridUnitType.Star),
            };
            var secondStarColumn = new UiGridViewColumn
            {
                Header = "Second",
                Width = new GridLength(2, GridUnitType.Star),
            };
            view.Columns.Add(fixedColumn);
            view.Columns.Add(firstStarColumn);
            view.Columns.Add(secondStarColumn);

            Assert.True(Wpf.Zen.Ui.Controls.GridViewColumnLayout.ResolveStarColumns(view.Columns, 600));

            Assert.Equal(100.0, fixedColumn.ActualWidth, 3);
            Assert.Equal(498.0 / 3.0, firstStarColumn.ActualWidth, 3);
            Assert.Equal(996.0 / 3.0, secondStarColumn.ActualWidth, 3);
            Assert.Equal(
                598.0,
                fixedColumn.ActualWidth + firstStarColumn.ActualWidth + secondStarColumn.ActualWidth,
                3
            );
        });
    }

    [Fact]
    public void StarColumns_RedistributeSpaceAfterMinAndMaxConstraints()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            var limitedColumn = new UiGridViewColumn
            {
                Header = "Limited",
                MaxWidth = 120,
                Width = new GridLength(1, GridUnitType.Star),
            };
            var remainingColumn = new UiGridViewColumn
            {
                Header = "Remaining",
                MinWidth = 200,
                Width = new GridLength(1, GridUnitType.Star),
            };
            view.Columns.Add(limitedColumn);
            view.Columns.Add(remainingColumn);

            Assert.True(Wpf.Zen.Ui.Controls.GridViewColumnLayout.ResolveStarColumns(view.Columns, 500));

            Assert.Equal(120.0, limitedColumn.ActualWidth, 3);
            Assert.Equal(378.0, remainingColumn.ActualWidth, 3);
        });
    }

    [Fact]
    public void StarColumn_NeverPublishesAutoWidthToNativePresenters()
    {
        RunOnStaThread(static () =>
        {
            var column = new UiGridViewColumn
            {
                Header = "Description",
                MinWidth = 100,
                Width = new GridLength(1, GridUnitType.Star),
            };

            Assert.True(column.Width.IsStar);
            Assert.Equal(100.0, (double)column.GetValue(NativeGridViewColumn.WidthProperty), 3);
            Assert.Equal(100.0, column.ActualWidth, 3);
        });
    }

    [Fact]
    public void NativeHeaderResize_ConvertsWidthToPixelOrAuto()
    {
        RunOnStaThread(static () =>
        {
            var column = new UiGridViewColumn { Width = new GridLength(1, GridUnitType.Star) };

            column.SetValue(NativeGridViewColumn.WidthProperty, 175.0);

            Assert.True(column.Width.IsAbsolute);
            Assert.Equal(175.0, column.Width.Value);

            column.SetValue(NativeGridViewColumn.WidthProperty, double.NaN);

            Assert.True(column.Width.IsAuto);
        });
    }

    [Fact]
    public void AutoColumn_ReMeasuresContentWhenMaximumWidthIsRelaxed()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            var autoColumn = new UiGridViewColumn
            {
                DisplayMemberBinding = new System.Windows.Data.Binding(nameof(TestRow.Name)),
                Header = "Value",
                MaxWidth = 80,
                Width = GridLength.Auto,
            };
            view.Columns.Add(autoColumn);

            var listView = new UiListView
            {
                Height = 120,
                ItemsSource = new[] { new TestRow(new string('W', 100)) },
                View = view,
                Width = 600,
            };
            var window = new Window
            {
                Content = listView,
                Height = 160,
                ShowInTaskbar = false,
                Width = 640,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                listView.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                listView.UpdateLayout();
                Assert.Equal(80.0, autoColumn.ActualWidth, 3);

                autoColumn.MaxWidth = 400;
                listView.UpdateLayout();
                listView.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                listView.UpdateLayout();

                Assert.Equal(400.0, autoColumn.ActualWidth, 3);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GridView_UsesViewBaseStyleKeys()
    {
        RunOnStaThread(static () =>
        {
            var listView = new TestListView { View = new UiGridView() };

            Assert.Same(StyleKeys.GridViewListViewStyle, listView.CurrentDefaultStyleKey);
        });
    }

    [Fact]
    public void GridView_DefaultContainers_KeepRowDividers()
    {
        RunOnStaThread(static () =>
        {
            var listView = new UiListView
            {
                Height = 200,
                View = new UiGridView(),
                Width = 300,
            };
            listView.Items.Add("First");
            listView.Items.Add("Second");
            var root = new Grid();
            root.Children.Add(listView);

            root.Measure(new Size(300, 200));
            root.Arrange(new Rect(0, 0, 300, 200));
            root.UpdateLayout();

            Assert.Null(listView.ItemContainerStyleSelector);
            var firstItem = Assert.IsType<UiListViewItem>(
                listView.ItemContainerGenerator.ContainerFromIndex(0)
            );
            var lastItem = Assert.IsType<UiListViewItem>(
                listView.ItemContainerGenerator.ContainerFromIndex(1)
            );

            Assert.Equal(new Thickness(0, 0, 0, 1), firstItem.BorderThickness);
            Assert.Equal(new Thickness(0, 0, 0, 1), lastItem.BorderThickness);
            Assert.Same(firstItem.Template, lastItem.Template);
            Assert.NotNull(FindVisualChild<NativeGridViewRowPresenter>(lastItem));

            listView.Items.Add("Third");
            root.UpdateLayout();

            var newLastItem = Assert.IsType<UiListViewItem>(
                listView.ItemContainerGenerator.ContainerFromIndex(2)
            );
            Assert.Equal(new Thickness(0, 0, 0, 1), lastItem.BorderThickness);
            Assert.Equal(new Thickness(0, 0, 0, 1), newLastItem.BorderThickness);

            // ViewBase restores the normal ListView style when the view is removed. This verifies
            // that the GridView-only selector does not leak into the default non-column layout.
            listView.View = null;
            root.UpdateLayout();

            Assert.Null(listView.ItemContainerStyleSelector);
            Assert.Equal(new Thickness(0, 0, 0, 1), firstItem.BorderThickness);
            Assert.Equal(new Thickness(0, 0, 0, 1), lastItem.BorderThickness);
            Assert.Equal(new Thickness(0, 0, 0, 1), newLastItem.BorderThickness);
        });
    }

    [Fact]
    public void GridView_ColumnHeaderStyleToken_AllowsPartialExternalStyleCustomization()
    {
        RunOnStaThread(static () =>
        {
            var resources = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Wpf.Zen.Ui;component/Controls/GridView/GridViewColumnHeader.xaml",
                    UriKind.Absolute
                ),
            };
            var headerStyle = new Style(typeof(NativeGridViewColumnHeader))
            {
                BasedOn = Assert.IsType<Style>(
                    resources[StyleKeys.GridViewColumnHeaderStyle]
                ),
            };
            headerStyle.Setters.Add(
                new Setter(FrameworkElement.MinHeightProperty, 52D)
            );

            var header = new NativeGridViewColumnHeader
            {
                Content = "Value",
                Style = headerStyle,
            };

            Assert.True(header.ApplyTemplate());
            Assert.Equal(52D, header.MinHeight);
            Assert.NotNull(header.Template.FindName("PART_HeaderGripper", header));
        });
    }

    [Fact]
    public void DefaultTemplates_LoadAndSelectionIndicatorSettingIsInherited()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            view.Columns.Add(
                new UiGridViewColumn { Header = "Value", Width = new GridLength(1, GridUnitType.Star) }
            );
            var listView = new UiListView
            {
                Height = 120,
                ItemsSource = new[] { "First", "Second" },
                SelectedIndex = 0,
                View = view,
                Width = 600,
            };
            var dataGrid = new UiDataGrid
            {
                Height = 120,
                ItemsSource = new[] { new TestRow("First"), new TestRow("Second") },
                SelectedIndex = 0,
                Width = 600,
            };
            var root = new StackPanel();
            root.Children.Add(listView);
            root.Children.Add(dataGrid);

            root.Measure(new Size(600, 240));
            root.Arrange(new Rect(0, 0, 600, 240));
            root.UpdateLayout();

            Assert.NotNull(FindVisualChild<NativeGridViewHeaderRowPresenter>(listView));
            var columnHeader = Assert.IsType<NativeGridViewColumnHeader>(
                FindVisualChild<NativeGridViewColumnHeader>(
                    listView,
                    static header => header.Role == NativeGridViewColumnHeaderRole.Normal
                )
            );
            Assert.Equal("Value", columnHeader.Content);
            Assert.Equal(40.0, columnHeader.MinHeight);

            var listViewItem = Assert.IsType<UiListViewItem>(
                listView.ItemContainerGenerator.ContainerFromIndex(0)
            );
            var listIndicator = Assert.IsType<Rectangle>(
                listViewItem.Template.FindName("SelectionIndicator", listViewItem)
            );
            var dataGridRow = Assert.IsType<DataGridRow>(
                dataGrid.ItemContainerGenerator.ContainerFromIndex(0)
            );
            var dataGridIndicator = Assert.IsType<Rectangle>(
                dataGridRow.Template.FindName("ActiveRectangle", dataGridRow)
            );

            Assert.Equal(Visibility.Visible, listIndicator.Visibility);
            Assert.Equal(Visibility.Visible, dataGridIndicator.Visibility);

            listView.IsSelectionIndicatorVisible = false;
            dataGrid.IsSelectionIndicatorVisible = false;
            root.UpdateLayout();

            Assert.Equal(Visibility.Collapsed, listIndicator.Visibility);
            Assert.Equal(Visibility.Collapsed, dataGridIndicator.Visibility);
        });
    }

    [Fact]
    public void GridViewTemplate_StarColumnsSettleAndScrollOffsetsStaySynchronized()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            view.Columns.Add(
                new UiGridViewColumn
                {
                    Header = "Value",
                    MinWidth = 100,
                    Width = new GridLength(1, GridUnitType.Star),
                }
            );
            var listView = new UiListView
            {
                Height = 160,
                ItemsSource = Enumerable.Range(0, 20).Select(static index => $"Item {index}").ToArray(),
                View = view,
                Width = 600,
            };
            var window = new Window
            {
                Content = listView,
                Height = 200,
                ShowInTaskbar = false,
                Width = 640,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                listView.UpdateLayout();
                window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                listView.UpdateLayout();

                var scrollViewer = Assert.IsType<UiScrollViewer>(
                    FindVisualChild<UiScrollViewer>(listView)
                );
                var scrollBar = Assert.IsType<UiScrollBar>(
                    scrollViewer.Template.FindName("PART_VerticalScrollBar", scrollViewer)
                );
                Assert.True(scrollViewer.ScrollableHeight > 0d);
                Assert.True(listView.IsMeasureValid);
                Assert.True(listView.IsArrangeValid);

                scrollViewer.ScrollToVerticalOffset(2d);
                listView.UpdateLayout();

                Assert.Equal(2d, scrollViewer.VerticalOffset);
                Assert.Equal(scrollViewer.VerticalOffset, scrollBar.Value);
                Assert.True(listView.IsMeasureValid);
                Assert.True(listView.IsArrangeValid);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GridViewTemplate_HorizontalScrollingKeepsHeadersAndRowsAligned()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            for (int index = 0; index < 4; index++)
            {
                view.Columns.Add(
                    new UiGridViewColumn
                    {
                        Header = $"Column {index}",
                        Width = new GridLength(240),
                    }
                );
            }

            var listView = new UiListView
            {
                Height = 160,
                ItemsSource = new[] { "First", "Second" },
                View = view,
                Width = 500,
            };
            var window = new Window
            {
                Content = listView,
                Height = 200,
                ShowInTaskbar = false,
                Width = 540,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                listView.UpdateLayout();

                var scrollViewer = Assert.IsType<UiScrollViewer>(
                    FindVisualChild<UiScrollViewer>(listView)
                );
                var scrollBar = Assert.IsType<UiScrollBar>(
                    scrollViewer.Template.FindName("PART_HorizontalScrollBar", scrollViewer)
                );
                var headerPresenter = Assert.IsType<NativeGridViewHeaderRowPresenter>(
                    FindVisualChild<NativeGridViewHeaderRowPresenter>(listView)
                );
                var rowPresenter = Assert.IsType<NativeGridViewRowPresenter>(
                    FindVisualChild<NativeGridViewRowPresenter>(listView)
                );

                Assert.True(scrollViewer.ScrollableWidth > 0d);
                scrollViewer.ScrollToHorizontalOffset(200d);
                listView.UpdateLayout();

                double headerX = headerPresenter
                    .TransformToAncestor(listView)
                    .Transform(new Point(0, 0))
                    .X;
                double rowX = rowPresenter
                    .TransformToAncestor(listView)
                    .Transform(new Point(0, 0))
                    .X;

                Assert.Equal(200d, scrollViewer.HorizontalOffset, 3);
                Assert.Equal(scrollViewer.HorizontalOffset, scrollBar.Value, 3);
                Assert.Equal(rowX, headerX, 3);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GridViewTemplate_StarColumnsTrackViewportResizeWithoutAccumulatingWidth()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            view.Columns.Add(new UiGridViewColumn { Header = "Fixed", Width = new GridLength(100) });
            view.Columns.Add(
                new UiGridViewColumn
                {
                    Header = "Star",
                    MinWidth = 100,
                    Width = new GridLength(1, GridUnitType.Star),
                }
            );
            var listView = new UiListView
            {
                Height = 160,
                ItemsSource = new[] { "First", "Second" },
                View = view,
            };
            var window = new Window
            {
                Content = listView,
                Height = 200,
                ShowInTaskbar = false,
                Width = 840,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                AssertColumnsFillViewport(listView, view);

                window.Width = 640;
                window.UpdateLayout();
                AssertColumnsFillViewport(listView, view);

                window.Width = 1040;
                window.UpdateLayout();
                AssertColumnsFillViewport(listView, view);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GridViewTemplate_HeaderResizeDoesNotPublishTransientHorizontalExtent()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            var fixedColumn = new UiGridViewColumn
            {
                Header = "Fixed",
                Width = new GridLength(120),
            };
            view.Columns.Add(fixedColumn);
            view.Columns.Add(
                new UiGridViewColumn
                {
                    Header = "Star",
                    MinWidth = 100,
                    Width = new GridLength(1, GridUnitType.Star),
                }
            );

            var listView = new UiListView
            {
                Height = 160,
                ItemsSource = new[] { "First", "Second" },
                View = view,
                Width = 600,
            };
            var window = new Window
            {
                Content = listView,
                Height = 200,
                ShowInTaskbar = false,
                Width = 640,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                AssertColumnsFillViewport(listView, view);

                var scrollViewer = Assert.IsType<UiScrollViewer>(
                    FindVisualChild<UiScrollViewer>(listView)
                );
                var publishedScrollableWidths = new List<double>();
                scrollViewer.ScrollChanged += (_, _) =>
                    publishedScrollableWidths.Add(scrollViewer.ScrollableWidth);

                // GridViewColumnHeader writes the native double Width property while dragging.
                // Exercise that exact path rather than only changing the GridLength definition.
                fixedColumn.SetValue(NativeGridViewColumn.WidthProperty, 260.0);
                listView.UpdateLayout();
                listView.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                listView.UpdateLayout();

                Assert.True(fixedColumn.Width.IsAbsolute);
                Assert.Equal(260.0, fixedColumn.Width.Value, 3);
                Assert.Equal(0.0, scrollViewer.ScrollableWidth, 3);
                Assert.All(publishedScrollableWidths, width => Assert.InRange(width, 0.0, 0.001));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GridViewTemplate_StarHeaderResizeRetainsStarSizingAndFillsViewport()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            var firstColumn = new UiGridViewColumn
            {
                Header = "First",
                MinWidth = 80,
                Width = new GridLength(1, GridUnitType.Star),
            };
            var secondColumn = new UiGridViewColumn
            {
                Header = "Second",
                MinWidth = 80,
                Width = new GridLength(1, GridUnitType.Star),
            };
            var thirdColumn = new UiGridViewColumn
            {
                Header = "Third",
                MinWidth = 80,
                Width = new GridLength(1, GridUnitType.Star),
            };
            view.Columns.Add(firstColumn);
            view.Columns.Add(secondColumn);
            view.Columns.Add(thirdColumn);

            var listView = new UiListView
            {
                Height = 160,
                ItemsSource = new[] { "First", "Second" },
                View = view,
                Width = 600,
            };
            var window = new Window
            {
                Content = listView,
                Height = 200,
                ShowInTaskbar = false,
                Width = 640,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                AssertColumnsFillViewport(listView, view);
                double firstWidthBeforeResize = firstColumn.ActualWidth;
                double secondWidthBeforeResize = secondColumn.ActualWidth;

                // This is the finite native Width write performed by GridViewColumnHeader.DragDelta.
                firstColumn.SetValue(
                    NativeGridViewColumn.WidthProperty,
                    firstWidthBeforeResize + 80.0
                );
                listView.UpdateLayout();
                window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                listView.UpdateLayout();

                Assert.True(firstColumn.Width.IsStar);
                Assert.True(secondColumn.Width.IsStar);
                Assert.True(thirdColumn.Width.IsStar);
                Assert.True(firstColumn.ActualWidth > firstWidthBeforeResize + 79.0);
                Assert.True(secondColumn.ActualWidth < secondWidthBeforeResize);
                AssertColumnsFillViewport(listView, view);

                double trailingWidthBeforeResize = thirdColumn.ActualWidth;
                thirdColumn.SetValue(
                    NativeGridViewColumn.WidthProperty,
                    trailingWidthBeforeResize - 60.0
                );
                listView.UpdateLayout();

                Assert.True(thirdColumn.Width.IsStar);
                Assert.Equal(trailingWidthBeforeResize, thirdColumn.ActualWidth, 3);
                AssertColumnsFillViewport(listView, view);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GridViewTemplate_StarResizeFallsBackToRightAutoAndFixedColumns()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            var starColumn = new UiGridViewColumn
            {
                Header = "Star",
                MinWidth = 80,
                Width = new GridLength(1, GridUnitType.Star),
            };
            var autoColumn = new UiGridViewColumn
            {
                DisplayMemberBinding = new System.Windows.Data.Binding(),
                Header = "Automatic content",
                MinWidth = 60,
                Width = GridLength.Auto,
            };
            var fixedColumn = new UiGridViewColumn
            {
                Header = "Fixed",
                MinWidth = 60,
                Width = new GridLength(140),
            };
            view.Columns.Add(starColumn);
            view.Columns.Add(autoColumn);
            view.Columns.Add(fixedColumn);
            var listView = new UiListView
            {
                Height = 160,
                ItemsSource = new[] { "Automatic content value" },
                View = view,
                Width = 600,
            };
            var window = new Window
            {
                Content = listView,
                Height = 200,
                ShowInTaskbar = false,
                Width = 640,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                AssertColumnsFillViewport(listView, view);
                double starWidth = starColumn.ActualWidth;
                double autoWidth = autoColumn.ActualWidth;
                double fixedWidth = fixedColumn.ActualWidth;

                starColumn.SetValue(NativeGridViewColumn.WidthProperty, starWidth + 40d);
                listView.UpdateLayout();

                Assert.True(starColumn.Width.IsStar);
                Assert.True(autoColumn.Width.IsAbsolute);
                Assert.True(fixedColumn.Width.IsAbsolute);
                Assert.True(autoColumn.ActualWidth < autoWidth);
                Assert.True(fixedColumn.ActualWidth < fixedWidth);
                AssertColumnsFillViewport(listView, view);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GridViewTemplates_AlignHeaderAndRowTextAfterSelectionIndicatorGutter()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            view.Columns.Add(
                new UiGridViewColumn
                {
                    DisplayMemberBinding = new System.Windows.Data.Binding(),
                    Header = "Header",
                    Width = new GridLength(1, GridUnitType.Star),
                }
            );
            var listView = new UiListView
            {
                Height = 160,
                ItemsSource = new[] { "Row" },
                View = view,
                Width = 400,
            };
            var window = new Window
            {
                Content = listView,
                Height = 200,
                ShowInTaskbar = false,
                Width = 440,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                listView.UpdateLayout();
                var item = Assert.IsAssignableFrom<System.Windows.Controls.ListViewItem>(
                    listView.ItemContainerGenerator.ContainerFromIndex(0)
                );
                var header = Assert.IsType<NativeGridViewColumnHeader>(
                    FindVisualChild<NativeGridViewColumnHeader>(
                        listView,
                        static candidate => candidate.Role == NativeGridViewColumnHeaderRole.Normal
                    )
                );
                var headerContent = Assert.IsType<ContentPresenter>(
                    header.Template.FindName("contentPresenter", header)
                    ?? FindVisualChild<ContentPresenter>(header)
                );
                var rowPresenter = Assert.IsType<NativeGridViewRowPresenter>(
                    FindVisualChild<NativeGridViewRowPresenter>(item)
                );
                var rowText = Assert.IsType<TextBlock>(FindVisualChild<TextBlock>(rowPresenter));
                var indicator = Assert.IsType<Rectangle>(
                    item.Template.FindName("SelectionIndicator", item)
                );

                double headerTextX = headerContent.TranslatePoint(default, listView).X;
                double rowTextX = rowText.TranslatePoint(default, listView).X;
                double presenterX = rowPresenter.TranslatePoint(default, listView).X;
                double indicatorRight =
                    indicator.TranslatePoint(default, listView).X + indicator.ActualWidth;

                Assert.Equal(headerTextX, rowTextX, 3);
                Assert.True(indicatorRight <= presenterX);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GalleryNestedScrollLayout_UsesViewportWithoutContentWidthFeedback()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            view.Columns.Add(
                new UiGridViewColumn
                {
                    DisplayMemberBinding = new System.Windows.Data.Binding(nameof(TestGridRow.FirstName)),
                    Header = "First Name",
                    MinWidth = 100,
                    Width = GridLength.Auto,
                }
            );
            view.Columns.Add(
                new UiGridViewColumn
                {
                    DisplayMemberBinding = new System.Windows.Data.Binding(nameof(TestGridRow.LastName)),
                    Header = "Last Name",
                    MinWidth = 100,
                    Width = new GridLength(1, GridUnitType.Star),
                }
            );
            view.Columns.Add(
                new UiGridViewColumn
                {
                    DisplayMemberBinding = new System.Windows.Data.Binding(nameof(TestGridRow.Company)),
                    Header = "Company",
                    MinWidth = 160,
                    Width = new GridLength(2, GridUnitType.Star),
                }
            );

            TestGridRow[] rows = Enumerable
                .Range(0, 50)
                .Select(static index =>
                    new TestGridRow(
                        $"First {index}",
                        $"Last {index}",
                        index % 2 == 0 ? "Megabyte Computers Inc" : "XD Projekt Red S.A."
                    )
                )
                .ToArray();
            var listView = new UiListView
            {
                Height = 200,
                ItemsSource = rows,
                View = view,
                Width = 800,
            };
            var pageContent = new StackPanel();
            pageContent.Children.Add(
                new UiListView { Height = 200, ItemsSource = rows, Width = 800 }
            );
            var selectionExample = new Grid { Margin = new Thickness(0, 36, 0, 0) };
            selectionExample.ColumnDefinitions.Add(new ColumnDefinition());
            selectionExample.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            selectionExample.Children.Add(
                new UiListView { Height = 200, ItemsSource = rows, Width = 650 }
            );
            var comboBox = new ComboBox
            {
                Margin = new Thickness(12, 0, 0, 0),
                SelectedIndex = 0,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 138,
            };
            comboBox.Items.Add("Single");
            comboBox.Items.Add("Multiple");
            Grid.SetColumn(comboBox, 1);
            selectionExample.Children.Add(comboBox);
            pageContent.Children.Add(selectionExample);
            listView.Margin = new Thickness(0, 36, 0, 24);
            pageContent.Children.Add(listView);

            var pageScrollViewer = new Wpf.Zen.Ui.Controls.DynamicScrollViewer
            {
                CanContentScroll = false,
                Content = pageContent,
                Height = 600,
                Width = 840,
            };
            var window = new Window
            {
                Content = pageScrollViewer,
                Height = 640,
                ShowInTaskbar = false,
                Width = 880,
                WindowStyle = WindowStyle.None,
            };
            try
            {
                window.Show();
                window.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(listView.IsMeasureValid);
                Assert.True(listView.IsArrangeValid);

                double totalColumnWidth = view.Columns.Cast<NativeGridViewColumn>().Sum(static column => column.ActualWidth);
                Assert.InRange(totalColumnWidth, 783d, 786d);
                Assert.InRange(
                    Assert.IsType<NativeGridViewHeaderRowPresenter>(
                        FindVisualChild<NativeGridViewHeaderRowPresenter>(listView)
                    ).DesiredSize.Width,
                    795d,
                    800d
                );
                double rowPresenterWidth = Assert.IsType<NativeGridViewRowPresenter>(
                    FindVisualChild<NativeGridViewRowPresenter>(listView)
                ).DesiredSize.Width;

                // The header presenter owns the template's 12-pixel leading margin. Rows instead live in the
                // second column beside a separate 12-pixel selection-indicator gutter, so their desired width is
                // the resolved column total plus WPF's two-pixel GridView presenter allowance, not the header's
                // full width. Keeping the two values distinct prevents the test from encoding contradictory
                // expectations for the same 800-pixel viewport.
                Assert.InRange(rowPresenterWidth, 785d, 788d);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }

    private static void AssertColumnsFillViewport(UiListView listView, UiGridView view)
    {
        listView.UpdateLayout();
        listView.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
        listView.UpdateLayout();
        var scrollViewer = Assert.IsType<UiScrollViewer>(
            FindVisualChild<UiScrollViewer>(listView)
        );
        double totalColumnWidth = view.Columns
            .Cast<NativeGridViewColumn>()
            .Sum(static column => column.ActualWidth);

        Assert.Equal(scrollViewer.ViewportWidth - 14d, totalColumnWidth, 3);
        Assert.InRange(scrollViewer.ScrollableWidth, 0d, 0.001d);
        Assert.True(listView.IsMeasureValid);
        Assert.True(listView.IsArrangeValid);
    }

    private static T? FindVisualChild<T>(DependencyObject parent, Func<T, bool>? predicate = null)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T result && (predicate is null || predicate(result)))
            {
                return result;
            }

            if (FindVisualChild(child, predicate) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private sealed class TestListView : UiListView
    {
        public object CurrentDefaultStyleKey => GetValue(DefaultStyleKeyProperty);
    }

    private sealed record TestRow(string Name);

    private sealed record TestGridRow(string FirstName, string LastName, string Company);
}
