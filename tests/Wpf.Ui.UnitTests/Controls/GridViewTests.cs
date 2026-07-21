// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NativeGridViewColumn = System.Windows.Controls.GridViewColumn;
using NativeGridViewColumnHeader = System.Windows.Controls.GridViewColumnHeader;
using NativeGridViewColumnHeaderRole = System.Windows.Controls.GridViewColumnHeaderRole;
using UiDataGrid = Wpf.Ui.Controls.DataGrid;
using UiGridView = Wpf.Ui.Controls.GridView;
using UiGridViewColumn = Wpf.Ui.Controls.GridViewColumn;
using UiGridViewHeaderRowPresenter = Wpf.Ui.Controls.GridViewHeaderRowPresenter;
using UiListView = Wpf.Ui.Controls.ListView;
using UiListViewItem = Wpf.Ui.Controls.ListViewItem;
using UiPassiveScrollViewer = Wpf.Ui.Controls.PassiveScrollViewer;

namespace Wpf.Ui.UnitTests.Controls;

public class GridViewTests
{
    [Fact]
    public void Width_ParsesGridLengthFromXaml()
    {
        RunOnStaThread(static () =>
        {
            const string xaml =
                "<ui:GridViewColumn xmlns:ui=\"http://schemas.lepo.co/wpfui/2022/xaml\" Width=\"2*\" />";

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

            var presenter = new UiGridViewHeaderRowPresenter { Columns = view.Columns };
            presenter.Measure(new Size(600, 100));
            presenter.Arrange(new Rect(0, 0, 600, 40));

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

            var presenter = new UiGridViewHeaderRowPresenter { Columns = view.Columns };
            presenter.Measure(new Size(500, 100));
            presenter.Arrange(new Rect(0, 0, 500, 40));

            Assert.Equal(120.0, limitedColumn.ActualWidth, 3);
            Assert.Equal(378.0, remainingColumn.ActualWidth, 3);
        });
    }

    [Fact]
    public void InfiniteHeaderMeasure_DoesNotDiscardWidthResolvedByFiniteRows()
    {
        RunOnStaThread(static () =>
        {
            var view = new UiGridView();
            var column = new UiGridViewColumn
            {
                Header = "Description",
                MinWidth = 100,
                Width = new GridLength(1, GridUnitType.Star),
            };
            view.Columns.Add(column);

            // WPF's GridView template measures the header inside a horizontally enabled
            // ScrollViewer, so its horizontal constraint is infinite. Rows eventually resolve
            // against the finite viewport. Both presenters share the same GridViewColumn instance.
            var finiteWidthPresenter = new UiGridViewHeaderRowPresenter { Columns = view.Columns };
            finiteWidthPresenter.Measure(new Size(500, 100));
            double rowResolvedWidth = column.ActualWidth;

            var headerPresenter = new UiGridViewHeaderRowPresenter { Columns = view.Columns };
            headerPresenter.Measure(new Size(double.PositiveInfinity, 100));

            Assert.Equal(498.0, rowResolvedWidth, 3);
            Assert.Equal(rowResolvedWidth, column.ActualWidth, 3);
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
    public void GridView_UsesViewBaseStyleKeys()
    {
        RunOnStaThread(static () =>
        {
            var listView = new TestListView { View = new UiGridView() };

            Assert.Same(UiGridView.GridViewStyleKey, listView.CurrentDefaultStyleKey);
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

            Assert.NotNull(FindVisualChild<UiGridViewHeaderRowPresenter>(listView));
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

                var scrollViewer = Assert.IsType<UiPassiveScrollViewer>(
                    FindVisualChild<UiPassiveScrollViewer>(listView)
                );
                var scrollBar = Assert.IsType<ScrollBar>(
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

                var scrollViewer = Assert.IsType<UiPassiveScrollViewer>(
                    FindVisualChild<UiPassiveScrollViewer>(listView)
                );
                var scrollBar = Assert.IsType<ScrollBar>(
                    scrollViewer.Template.FindName("PART_HorizontalScrollBar", scrollViewer)
                );
                var headerPresenter = Assert.IsType<UiGridViewHeaderRowPresenter>(
                    FindVisualChild<UiGridViewHeaderRowPresenter>(listView)
                );
                var rowPresenter = Assert.IsType<Wpf.Ui.Controls.GridViewRowPresenter>(
                    FindVisualChild<Wpf.Ui.Controls.GridViewRowPresenter>(listView)
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

            var pageScrollViewer = new Wpf.Ui.Controls.DynamicScrollViewer
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
                Assert.InRange(totalColumnWidth, 795d, 798d);
                Assert.InRange(
                    Assert.IsType<UiGridViewHeaderRowPresenter>(
                        FindVisualChild<UiGridViewHeaderRowPresenter>(listView)
                    ).DesiredSize.Width,
                    795d,
                    800d
                );
                Assert.InRange(
                    Assert.IsType<Wpf.Ui.Controls.GridViewRowPresenter>(
                        FindVisualChild<Wpf.Ui.Controls.GridViewRowPresenter>(listView)
                    ).DesiredSize.Width,
                    795d,
                    800d
                );
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private static void AssertColumnsFillViewport(UiListView listView, UiGridView view)
    {
        listView.UpdateLayout();
        listView.Dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
        listView.UpdateLayout();
        var scrollViewer = Assert.IsType<UiPassiveScrollViewer>(
            FindVisualChild<UiPassiveScrollViewer>(listView)
        );
        double totalColumnWidth = view.Columns
            .Cast<NativeGridViewColumn>()
            .Sum(static column => column.ActualWidth);

        Assert.Equal(scrollViewer.ViewportWidth - 2d, totalColumnWidth, 3);
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
