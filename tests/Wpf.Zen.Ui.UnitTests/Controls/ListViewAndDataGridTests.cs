// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using NativeListViewItem = System.Windows.Controls.ListViewItem;
using UiDataGrid = Wpf.Zen.Ui.Controls.DataGrid;
using UiListItemContainerStyleSelector = Wpf.Zen.Ui.Controls.ListItemContainerStyleSelector;
using UiListView = Wpf.Zen.Ui.Controls.ListView;
using UiListViewItem = Wpf.Zen.Ui.Controls.ListViewItem;
using UiPassiveScrollViewer = Wpf.Zen.Ui.Controls.PassiveScrollViewer;
using UiVirtualizingGridView = Wpf.Zen.Ui.Controls.VirtualizingGridView;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ListViewAndDataGridTests
{
    [Fact]
    public void ListItemContainerStyleSelector_AppliesSpecificPositionalRulesToListView()
    {
        RunOnStaThread(static () =>
        {
            var selector = new UiListItemContainerStyleSelector
            {
                DefaultStyle = CreateTaggedListViewItemStyle("default"),
                EvenItemStyle = CreateTaggedListViewItemStyle("even"),
                FirstItemStyle = CreateTaggedListViewItemStyle("first"),
                LastItemStyle = CreateTaggedListViewItemStyle("last"),
                OddItemStyle = CreateTaggedListViewItemStyle("odd"),
                SingleItemStyle = CreateTaggedListViewItemStyle("single"),
            };
            var listView = new UiListView
            {
                Height = 200,
                ItemContainerStyleSelector = selector,
                ItemsSource = new[] { "A", "B", "C", "D" },
                Width = 300,
            };

            Arrange(listView, 300, 200);

            Assert.Equal("first", GetListViewItem(listView, 0).Tag);
            Assert.Equal("odd", GetListViewItem(listView, 1).Tag);
            Assert.Equal("even", GetListViewItem(listView, 2).Tag);
            Assert.Equal("last", GetListViewItem(listView, 3).Tag);

            listView.ItemsSource = new[] { "only" };
            listView.UpdateLayout();

            Assert.Equal("single", GetListViewItem(listView, 0).Tag);
        });
    }

    [Fact]
    public void ListItemContainerStyleSelector_ReevaluatesSurvivingListViewContainersAfterCollectionChanges()
    {
        RunOnStaThread(static () =>
        {
            var items = new ObservableCollection<string> { "A", "B" };
            var listView = new UiListView
            {
                Height = 200,
                ItemContainerStyleSelector = new UiListItemContainerStyleSelector
                {
                    DefaultStyle = CreateTaggedListViewItemStyle("default"),
                    FirstItemStyle = CreateTaggedListViewItemStyle("first"),
                    LastItemStyle = CreateTaggedListViewItemStyle("last"),
                    OddItemStyle = CreateTaggedListViewItemStyle("odd"),
                    SingleItemStyle = CreateTaggedListViewItemStyle("single"),
                },
                ItemsSource = items,
                Width = 300,
            };

            Arrange(listView, 300, 200);
            UiListViewItem secondContainer = GetListViewItem(listView, 1);
            Assert.Equal("last", secondContainer.Tag);

            items.Add("C");
            listView.UpdateLayout();

            Assert.Same(secondContainer, GetListViewItem(listView, 1));
            Assert.Equal("odd", secondContainer.Tag);
            Assert.Equal("last", GetListViewItem(listView, 2).Tag);

            items.RemoveAt(0);
            listView.UpdateLayout();

            Assert.Same(secondContainer, GetListViewItem(listView, 0));
            Assert.Equal("first", secondContainer.Tag);

            items.RemoveAt(1);
            listView.UpdateLayout();

            Assert.Equal("single", GetListViewItem(listView, 0).Tag);
        });
    }

    [Fact]
    public void ListItemContainerStyleSelector_PreservesExplicitListViewContainerStyle()
    {
        RunOnStaThread(static () =>
        {
            Style explicitStyle = CreateTaggedListViewItemStyle("explicit");
            var explicitContainer = new UiListViewItem { Content = "A", Style = explicitStyle };
            var listView = new UiListView
            {
                Height = 200,
                ItemContainerStyleSelector = new UiListItemContainerStyleSelector
                {
                    FirstItemStyle = CreateTaggedListViewItemStyle("first"),
                    LastItemStyle = CreateTaggedListViewItemStyle("last"),
                },
                Width = 300,
            };
            listView.Items.Add(explicitContainer);
            listView.Items.Add("B");

            Arrange(listView, 300, 200);
            Style runtimeStyle = CreateTaggedListViewItemStyle("runtime");
            UiListViewItem generatedContainer = GetListViewItem(listView, 1);
            generatedContainer.Style = runtimeStyle;
            listView.Items.Insert(0, "new");
            listView.UpdateLayout();

            Assert.Same(explicitStyle, explicitContainer.Style);
            Assert.Equal("explicit", explicitContainer.Tag);
            Assert.Same(runtimeStyle, generatedContainer.Style);
            Assert.Equal("runtime", generatedContainer.Tag);
        });
    }

    [Fact]
    public void ListView_AcceptsNativeAndFluentItemsAsContainers()
    {
        RunOnStaThread(static () =>
        {
            var listView = new TestListView();

            Assert.True(listView.IsOwnContainer(new NativeListViewItem()));
            Assert.True(listView.IsOwnContainer(new UiListViewItem()));
            Assert.False(listView.IsOwnContainer("data item"));
        });
    }

    [Fact]
    public void ListViewItemTemplate_ForwardsContentTemplateSelector()
    {
        RunOnStaThread(static () =>
        {
            var selector = new TestTemplateSelector();
            var listView = new UiListView
            {
                Height = 100,
                ItemTemplateSelector = selector,
                ItemsSource = new[] { "First" },
                Width = 300,
            };

            Arrange(listView, 300, 100);

            var item = Assert.IsType<UiListViewItem>(
                listView.ItemContainerGenerator.ContainerFromIndex(0)
            );
            var presenter = Assert.IsType<ContentPresenter>(FindVisualChild<ContentPresenter>(item));

            Assert.Same(selector, presenter.ContentTemplateSelector);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupedListView_UsesLogicalScrollingOnlyWhenGroupedVirtualizationIsEnabled(
        bool isVirtualizingWhenGrouping
    )
    {
        RunOnStaThread(() =>
        {
            var listView = new UiListView
            {
                Height = 160,
                ItemsSource = new[] { "A", "A", "B" },
                Width = 400,
            };
            listView.GroupStyle.Add(new GroupStyle());
            listView.Items.GroupDescriptions.Add(new PropertyGroupDescription());
            listView.Items.Refresh();
            VirtualizingPanel.SetIsVirtualizingWhenGrouping(listView, isVirtualizingWhenGrouping);

            Arrange(listView, 400, 160);

            Assert.True(listView.IsGrouping);
            var scrollViewer = Assert.IsType<UiPassiveScrollViewer>(
                FindVisualChild<UiPassiveScrollViewer>(listView)
            );
            Assert.Equal(isVirtualizingWhenGrouping, scrollViewer.CanContentScroll);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupedDataGrid_UsesLogicalScrollingOnlyWhenGroupedVirtualizationIsEnabled(
        bool isVirtualizingWhenGrouping
    )
    {
        RunOnStaThread(() =>
        {
            var dataGrid = new UiDataGrid
            {
                Height = 160,
                ItemsSource = new[] { "A", "A", "B" },
                Width = 400,
            };
            dataGrid.GroupStyle.Add(new GroupStyle());
            dataGrid.Items.GroupDescriptions.Add(new PropertyGroupDescription());
            dataGrid.Items.Refresh();
            VirtualizingPanel.SetIsVirtualizingWhenGrouping(dataGrid, isVirtualizingWhenGrouping);

            Arrange(dataGrid, 400, 160);

            Assert.True(dataGrid.IsGrouping);
            var scrollViewer = Assert.IsType<UiPassiveScrollViewer>(
                FindVisualChild<UiPassiveScrollViewer>(dataGrid)
            );
            Assert.Equal(isVirtualizingWhenGrouping, scrollViewer.CanContentScroll);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupedVirtualizingGridView_PreservesGroupedVirtualizationPolicy(
        bool isVirtualizingWhenGrouping
    )
    {
        RunOnStaThread(() =>
        {
            var gridView = new UiVirtualizingGridView
            {
                Height = 160,
                ItemsSource = new[] { "A", "A", "B" },
                Width = 400,
            };
            gridView.GroupStyle.Add(new GroupStyle());
            gridView.Items.GroupDescriptions.Add(new PropertyGroupDescription());
            gridView.Items.Refresh();
            VirtualizingPanel.SetIsVirtualizingWhenGrouping(gridView, isVirtualizingWhenGrouping);

            Arrange(gridView, 400, 160);

            Assert.True(gridView.IsGrouping);
            var scrollViewer = Assert.IsType<UiPassiveScrollViewer>(
                FindVisualChild<UiPassiveScrollViewer>(gridView)
            );
            Assert.Equal(isVirtualizingWhenGrouping, scrollViewer.CanContentScroll);
        });
    }

    [Fact]
    public void DataGridTemplates_KeepCellsHitTestableAndIndicatorsInputTransparent()
    {
        RunOnStaThread(static () =>
        {
            var dataGrid = new UiDataGrid
            {
                Height = 160,
                HeadersVisibility = DataGridHeadersVisibility.All,
                ItemsSource = new[] { new GroupedRow("A", "First") },
                RowHeaderWidth = 72,
                SelectedIndex = 0,
                Width = 400,
            };

            Arrange(dataGrid, 400, 160);

            var row = Assert.IsType<DataGridRow>(dataGrid.ItemContainerGenerator.ContainerFromIndex(0));
            var cell = Assert.IsType<DataGridCell>(FindVisualChild<DataGridCell>(row));
            var cellBorder = Assert.IsType<Border>(cell.Template.FindName("Border", cell));
            var indicator = Assert.IsType<System.Windows.Shapes.Rectangle>(
                row.Template.FindName("ActiveRectangle", row)
            );
            var rowHeader = Assert.IsType<DataGridRowHeader>(FindVisualChild<DataGridRowHeader>(row));
            var rowHeaderBorder = Assert.IsType<Border>(
                rowHeader.Template.FindName("rowHeaderBorder", rowHeader)
            );

            Assert.NotNull(cell.Background);
            Assert.Same(cell.Background, cellBorder.Background);
            Assert.False(indicator.IsHitTestVisible);
            Assert.True(double.IsNaN(rowHeaderBorder.Width));
            Assert.Equal(72d, rowHeader.ActualWidth, 3);
        });
    }

    [Fact]
    public void DataGridColumnStyleBindings_FollowColumnToItsCurrentOwner()
    {
        RunOnStaThread(static () =>
        {
            var firstStyle = new Style(typeof(ComboBox));
            var secondStyle = new Style(typeof(ComboBox));
            var firstGrid = new UiDataGrid { ComboBoxColumnEditingElementStyle = firstStyle };
            var secondGrid = new UiDataGrid { ComboBoxColumnEditingElementStyle = secondStyle };
            Initialize(firstGrid);
            Initialize(secondGrid);

            var column = new DataGridComboBoxColumn();
            firstGrid.Columns.Add(column);

            Assert.Same(firstStyle, column.EditingElementStyle);
            Assert.Same(
                firstGrid,
                BindingOperations
                    .GetBinding(column, DataGridBoundColumn.EditingElementStyleProperty)
                    ?.Source
            );

            firstGrid.Columns.Remove(column);
            secondGrid.Columns.Add(column);

            Assert.Same(secondStyle, column.EditingElementStyle);
            Assert.Same(
                secondGrid,
                BindingOperations
                    .GetBinding(column, DataGridBoundColumn.EditingElementStyleProperty)
                    ?.Source
            );
        });
    }

    [Fact]
    public void DataGridColumnStyleBindings_ReturnAfterExplicitStyleIsCleared()
    {
        RunOnStaThread(static () =>
        {
            var gridStyle = new Style(typeof(TextBlock));
            var explicitStyle = new Style(typeof(TextBlock));
            var dataGrid = new UiDataGrid { TextColumnElementStyle = gridStyle };
            Initialize(dataGrid);
            var column = new DataGridTextColumn();
            dataGrid.Columns.Add(column);

            Assert.Same(gridStyle, column.ElementStyle);

            column.ElementStyle = explicitStyle;
            Assert.Same(explicitStyle, column.ElementStyle);

            column.ClearValue(DataGridBoundColumn.ElementStyleProperty);

            Assert.Same(gridStyle, column.ElementStyle);
            Assert.Same(
                dataGrid,
                BindingOperations
                    .GetBinding(column, DataGridBoundColumn.ElementStyleProperty)
                    ?.Source
            );
        });
    }

    [Fact]
    public void DataGridCellTemplate_ShowsTextColorRectangleOnlyForCellSelection()
    {
        RunOnStaThread(static () =>
        {
            var item = new GroupedRow("A", "Value");
            var dataGrid = new UiDataGrid
            {
                AutoGenerateColumns = false,
                Height = 120,
                ItemsSource = new[] { item },
                SelectionUnit = DataGridSelectionUnit.Cell,
                Width = 300,
            };
            var column = new DataGridTextColumn { Binding = new Binding(nameof(GroupedRow.Name)) };
            dataGrid.Columns.Add(column);
            Arrange(dataGrid, 300, 120);

            dataGrid.SelectedCells.Add(new DataGridCellInfo(item, column));
            dataGrid.UpdateLayout();

            var cell = Assert.IsType<DataGridCell>(FindVisualChild<DataGridCell>(dataGrid));
            var selectionBorder = Assert.IsType<Border>(
                cell.Template.FindName("CellSelectionBorder", cell)
            );
            Assert.Equal(Visibility.Visible, selectionBorder.Visibility);
            Assert.Equal(new Thickness(1d), selectionBorder.BorderThickness);
            Assert.NotNull(selectionBorder.BorderBrush);
            Assert.False(selectionBorder.IsHitTestVisible);

            dataGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
            dataGrid.SelectedItem = item;
            dataGrid.UpdateLayout();

            Assert.Equal(Visibility.Collapsed, selectionBorder.Visibility);
        });
    }

    [Fact]
    public void DataGridDragIndicatorTemplate_ContainsVisualBrushCanvasPart()
    {
        RunOnStaThread(static () =>
        {
            var dataGrid = new UiDataGrid();
            Arrange(dataGrid, 300, 120);
            var floatingHeader = new Control
            {
                Height = 40,
                Style = dataGrid.DragIndicatorStyle,
                Width = 120,
            };
            Arrange(floatingHeader, 120, 40);

            var canvas = Assert.IsType<Canvas>(
                floatingHeader.Template.FindName("PART_VisualBrushCanvas", floatingHeader)
            );
            Assert.False(canvas.IsHitTestVisible);
        });
    }

    [Fact]
    public void DataGridColumnStyleBindings_DoNotReplaceExplicitColumnStyles()
    {
        RunOnStaThread(static () =>
        {
            var explicitStyle = new Style(typeof(TextBlock));
            var column = new DataGridTextColumn { ElementStyle = explicitStyle };
            var dataGrid = new UiDataGrid();
            Initialize(dataGrid);

            dataGrid.Columns.Add(column);

            Assert.Same(explicitStyle, column.ElementStyle);
            Assert.Null(BindingOperations.GetBinding(column, DataGridBoundColumn.ElementStyleProperty));
        });
    }

    [Fact]
    public void DataGridColumnStyleBindings_ApplyEveryGridLevelColumnStyle()
    {
        RunOnStaThread(static () =>
        {
            var checkBoxElementStyle = new Style(typeof(CheckBox));
            var checkBoxEditingStyle = new Style(typeof(CheckBox));
            var comboBoxElementStyle = new Style(typeof(ComboBox));
            var comboBoxEditingStyle = new Style(typeof(ComboBox));
            var textElementStyle = new Style(typeof(TextBlock));
            var textEditingStyle = new Style(typeof(TextBox));
            var dataGrid = new UiDataGrid
            {
                CheckBoxColumnElementStyle = checkBoxElementStyle,
                CheckBoxColumnEditingElementStyle = checkBoxEditingStyle,
                ComboBoxColumnElementStyle = comboBoxElementStyle,
                ComboBoxColumnEditingElementStyle = comboBoxEditingStyle,
                TextColumnElementStyle = textElementStyle,
                TextColumnEditingElementStyle = textEditingStyle,
            };
            Initialize(dataGrid);

            var checkBoxColumn = new DataGridCheckBoxColumn();
            var comboBoxColumn = new DataGridComboBoxColumn();
            var textColumn = new DataGridTextColumn();
            dataGrid.Columns.Add(checkBoxColumn);
            dataGrid.Columns.Add(comboBoxColumn);
            dataGrid.Columns.Add(textColumn);

            Assert.Same(checkBoxElementStyle, checkBoxColumn.ElementStyle);
            Assert.Same(checkBoxEditingStyle, checkBoxColumn.EditingElementStyle);
            Assert.Same(comboBoxElementStyle, comboBoxColumn.ElementStyle);
            Assert.Same(comboBoxEditingStyle, comboBoxColumn.EditingElementStyle);
            Assert.Same(textElementStyle, textColumn.ElementStyle);
            Assert.Same(textEditingStyle, textColumn.EditingElementStyle);
        });
    }

    [Fact]
    public void DataGridTemplate_ScrollsLogicallyAndKeepsScrollbarSynchronized()
    {
        RunOnStaThread(static () =>
        {
            var dataGrid = new UiDataGrid
            {
                Height = 200,
                ItemsSource = Enumerable
                    .Range(0, 100)
                    .Select(static index => new GroupedRow(index % 2 == 0 ? "A" : "B", $"Item {index}"))
                    .ToArray(),
                Width = 500,
            };
            var window = new Window
            {
                Content = dataGrid,
                Height = 240,
                ShowInTaskbar = false,
                Width = 540,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                dataGrid.UpdateLayout();

                var scrollViewer = Assert.IsType<UiPassiveScrollViewer>(
                    FindVisualChild<UiPassiveScrollViewer>(dataGrid)
                );
                var scrollBar = Assert.IsType<ScrollBar>(
                    scrollViewer.Template.FindName("PART_VerticalScrollBar", scrollViewer)
                );

                Assert.True(scrollViewer.CanContentScroll);
                Assert.True(scrollViewer.ScrollableHeight > 0d);
                Assert.True(CountVisualChildren<DataGridRow>(dataGrid) < dataGrid.Items.Count);

                scrollViewer.ScrollToVerticalOffset(10d);
                dataGrid.UpdateLayout();

                Assert.Equal(10d, scrollViewer.VerticalOffset, 3);
                Assert.Equal(scrollViewer.VerticalOffset, scrollBar.Value, 3);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DataGridTemplate_StarColumnResizeDoesNotExposeHorizontalScrollRange()
    {
        RunOnStaThread(static () =>
        {
            var dataGrid = new UiDataGrid
            {
                Height = 200,
                ItemsSource = Enumerable.Range(0, 100).Select(static index => new WideRow(
                    index,
                    $"Code {index}",
                    $"Name {index}",
                    $"Quantity {index}",
                    $"Unit {index}",
                    $"Price {index}",
                    $"Stock {index}",
                    $"Virtual {index}",
                    $"Other {index}"
                )).ToArray(),
                Width = 500,
            };

            var window = new Window
            {
                Content = dataGrid,
                Height = 240,
                ShowInTaskbar = false,
                Width = 540,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                window.Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                var headers = FindVisualChildren<DataGridColumnHeader>(dataGrid)
                    .Where(static header => header.Column is not null)
                    .OrderBy(static header => header.Column.DisplayIndex)
                    .ToArray();
                var gripper = Assert.IsType<Thumb>(headers[0].Template.FindName("PART_RightHeaderGripper", headers[0]));
                gripper.RaiseEvent(new DragStartedEventArgs(0d, 0d) { RoutedEvent = Thumb.DragStartedEvent });
                gripper.RaiseEvent(new DragDeltaEventArgs(80d, 0d) { RoutedEvent = Thumb.DragDeltaEvent });
                gripper.RaiseEvent(new DragCompletedEventArgs(80d, 0d, false) { RoutedEvent = Thumb.DragCompletedEvent });
                window.Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                var scrollViewer = Assert.IsType<UiPassiveScrollViewer>(
                    FindVisualChild<UiPassiveScrollViewer>(dataGrid)
                );
                Assert.Equal(0d, scrollViewer.ScrollableWidth, 6);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FluentColumnHeaders_DisableTheLastTrailingResizeHandle()
    {
        RunOnStaThread(static () =>
        {
            var view = new Wpf.Zen.Ui.Controls.GridView();
            view.Columns.Add(new Wpf.Zen.Ui.Controls.GridViewColumn { Header = "First", Width = new GridLength(1, GridUnitType.Star) });
            view.Columns.Add(new Wpf.Zen.Ui.Controls.GridViewColumn { Header = "Last", Width = new GridLength(1, GridUnitType.Star) });
            var listView = new UiListView
            {
                Height = 160,
                ItemsSource = new[] { "Value" },
                View = view,
                Width = 500,
            };
            var dataGrid = new UiDataGrid
            {
                AutoGenerateColumns = false,
                Height = 160,
                ItemsSource = new[] { new GroupedRow("A", "Value") },
                Width = 500,
            };
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "First", Binding = new Binding(nameof(GroupedRow.Group)) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Last", Binding = new Binding(nameof(GroupedRow.Name)) });

            var root = new StackPanel();
            root.Children.Add(listView);
            root.Children.Add(dataGrid);
            var window = new Window
            {
                Content = root,
                Height = 360,
                ShowInTaskbar = false,
                Width = 540,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                window.Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                GridViewColumnHeader[] listHeaders = FindVisualChildren<GridViewColumnHeader>(listView)
                    .Where(static header => header.Role == GridViewColumnHeaderRole.Normal)
                    .ToArray();
                GridViewColumnHeader firstListHeader = listHeaders.Single(header =>
                    ReferenceEquals(header.Column, view.Columns[0])
                );
                GridViewColumnHeader lastListHeader = listHeaders.Single(header =>
                    ReferenceEquals(header.Column, view.Columns[1])
                );
                Thumb firstListGripper = Assert.IsType<Thumb>(
                    firstListHeader.Template.FindName("PART_HeaderGripper", firstListHeader)
                );
                Thumb lastListGripper = Assert.IsType<Thumb>(
                    lastListHeader.Template.FindName("PART_HeaderGripper", lastListHeader)
                );

                DataGridColumnHeader[] dataGridHeaders = FindVisualChildren<DataGridColumnHeader>(dataGrid)
                    .Where(static header => header.Column is not null)
                    .OrderBy(static header => header.Column.DisplayIndex)
                    .ToArray();
                Thumb firstDataGridGripper = Assert.IsType<Thumb>(
                    dataGridHeaders[0].Template.FindName("PART_RightHeaderGripper", dataGridHeaders[0])
                );
                Thumb lastDataGridGripper = Assert.IsType<Thumb>(
                    dataGridHeaders[1].Template.FindName("PART_RightHeaderGripper", dataGridHeaders[1])
                );

                Assert.True(firstListGripper.IsHitTestVisible);
                Assert.False(lastListGripper.IsHitTestVisible);
                Assert.True(firstDataGridGripper.IsHitTestVisible);
                Assert.False(lastDataGridGripper.IsHitTestVisible);

                view.Columns.Move(1, 0);
                dataGrid.Columns[0].DisplayIndex = 1;
                window.Dispatcher.Invoke(
                    static () => { },
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle
                );

                Assert.False(firstListGripper.IsHitTestVisible);
                Assert.True(lastListGripper.IsHitTestVisible);
                Assert.False(firstDataGridGripper.IsHitTestVisible);
                Assert.True(lastDataGridGripper.IsHitTestVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DataGridTemplate_IgnoresSubPixelHorizontalOverflowButKeepsRealOverflow()
    {
        RunOnStaThread(static () =>
        {
            var dataGrid = new UiDataGrid
            {
                AutoGenerateColumns = false,
                Height = 160,
                ItemsSource = new[] { new GroupedRow("A", "Value") },
                Width = 500,
            };
            var firstColumn = new DataGridTextColumn
            {
                Header = "First",
                Binding = new Binding(nameof(GroupedRow.Group)),
            };
            var lastColumn = new DataGridTextColumn
            {
                Header = "Last",
                Binding = new Binding(nameof(GroupedRow.Name)),
            };
            dataGrid.Columns.Add(firstColumn);
            dataGrid.Columns.Add(lastColumn);

            var window = new Window
            {
                Content = dataGrid,
                Height = 200,
                ShowInTaskbar = false,
                Width = 540,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                window.Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                var scrollViewer = Assert.IsType<UiPassiveScrollViewer>(
                    FindVisualChild<UiPassiveScrollViewer>(dataGrid)
                );
                var horizontalScrollBar = Assert.IsType<ScrollBar>(
                    scrollViewer.Template.FindName("PART_HorizontalScrollBar", scrollViewer)
                );
                double columnsViewport = scrollViewer.ViewportWidth - dataGrid.CellsPanelHorizontalOffset;

                firstColumn.Width = new DataGridLength(columnsViewport / 2.0);
                lastColumn.Width = new DataGridLength((columnsViewport / 2.0) + 0.5);
                window.Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                Assert.InRange(scrollViewer.ScrollableWidth, 0.01, 1.0);
                Assert.Equal(Visibility.Collapsed, scrollViewer.EffectiveHorizontalScrollBarVisibility);
                Assert.Equal(Visibility.Collapsed, horizontalScrollBar.Visibility);

                lastColumn.Width = new DataGridLength(lastColumn.ActualWidth + 2.0);
                window.Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                Assert.True(scrollViewer.ScrollableWidth > 1.0);
                Assert.Equal(Visibility.Visible, scrollViewer.EffectiveHorizontalScrollBarVisibility);
                Assert.Equal(Visibility.Visible, horizontalScrollBar.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void Initialize(FrameworkElement element)
    {
        element.BeginInit();
        element.EndInit();
    }

    private static UiListViewItem GetListViewItem(UiListView listView, int index)
    {
        return Assert.IsType<UiListViewItem>(listView.ItemContainerGenerator.ContainerFromIndex(index));
    }

    private static Style CreateTaggedListViewItemStyle(string tag)
    {
        var style = new Style(typeof(NativeListViewItem));
        style.Setters.Add(new Setter(FrameworkElement.TagProperty, tag));
        return style;
    }

    private static void Arrange(FrameworkElement element, double width, double height)
    {
        // A control receives its implicit theme style only after it participates in a logical tree.
        // Host it exactly as an application would instead of testing an uninitialized bare element.
        var host = new Grid();
        host.Children.Add(element);
        host.Measure(new Size(width, height));
        host.Arrange(new Rect(0, 0, width, height));
        host.UpdateLayout();
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T result)
            {
                return result;
            }

            if (FindVisualChild<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T result)
            {
                yield return result;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static int CountVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        int count = 0;
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T)
            {
                count++;
            }

            count += CountVisualChildren<T>(child);
        }

        return count;
    }

    private sealed class TestListView : UiListView
    {
        public bool IsOwnContainer(object item) => IsItemItsOwnContainerOverride(item);
    }

    private sealed class TestTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return new DataTemplate();
        }
    }

    private sealed record GroupedRow(string Group, string Name);

    private sealed record WideRow(
        int Id,
        string Code,
        string Name,
        string Quantity,
        string Unit,
        string Price,
        string Stock,
        string Virtual,
        string Other
    );
}