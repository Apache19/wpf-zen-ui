// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using NativeGridViewColumn = System.Windows.Controls.GridViewColumn;
using NativeGridViewColumnHeader = System.Windows.Controls.GridViewColumnHeader;
using NativeGridViewColumnHeaderRole = System.Windows.Controls.GridViewColumnHeaderRole;
using UiDataGrid = Wpf.Ui.Controls.DataGrid;
using UiGridView = Wpf.Ui.Controls.GridView;
using UiGridViewColumn = Wpf.Ui.Controls.GridViewColumn;
using UiGridViewHeaderRowPresenter = Wpf.Ui.Controls.GridViewHeaderRowPresenter;
using UiListView = Wpf.Ui.Controls.ListView;
using UiListViewItem = Wpf.Ui.Controls.ListViewItem;

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
}
