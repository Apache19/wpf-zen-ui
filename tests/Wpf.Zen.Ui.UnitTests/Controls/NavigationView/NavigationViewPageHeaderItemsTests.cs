// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Wpf.Zen.Ui.Controls;
using NavigationControl = Wpf.Zen.Ui.Controls.NavigationView;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

/// <summary>
/// Exercises page-owned header collections through real WPF navigation, binding, and layout.
/// </summary>
public class NavigationViewPageHeaderItemsTests
{
    /// <summary>
    /// Verifies direct XAML collection syntax, shared Grid layout, and independent single content.
    /// </summary>
    [Fact]
    public void XamlItemsShareHostGridAndCoexistWithSingleContent()
    {
        WpfTestThread.Run(() =>
        {
            const string xaml = """
                <Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                      xmlns:ui="http://schemas.thezendev.com/wpf/2027">
                    <ui:NavigationView.PageHeaderContent>
                        <TextBlock Text="{Binding Title}" />
                    </ui:NavigationView.PageHeaderContent>
                    <ui:NavigationView.PageHeaderItems>
                        <Button x:Name="Action" Content="Run" Command="{Binding Command}"
                                Grid.Column="2" Grid.RowSpan="2" VerticalAlignment="Stretch" />
                        <TextBlock x:Name="Subtitle" Text="{Binding Title}" Grid.Row="1" />
                    </ui:NavigationView.PageHeaderItems>
                    <Grid />
                </Page>
                """;
            var page = Assert.IsType<Page>(XamlReader.Parse(xaml));
            var initialModel = new HeaderModel("Page subtitle");
            page.DataContext = initialModel;
            var items = NavigationControl.GetPageHeaderItems(page);
            var button = Assert.IsType<System.Windows.Controls.Button>(items[0]);
            var subtitle = Assert.IsType<TextBlock>(items[1]);
            var single = Assert.IsType<TextBlock>(NavigationControl.GetPageHeaderContent(page));
            var navigation = CreateNavigationView(out var grid, out var singleSlot);
            navigation.MenuItems.Add(new NavigationItem("Plain", typeof(NavigationViewPageHeaderTests.PlainPage)));
            var window = CreateWindow(navigation);

            try
            {
                window.Show();
                Assert.True(navigation.ReplaceContent(page));
                DrainDispatcher();
                Assert.Same(items, navigation.CurrentPageHeaderItems);
                Assert.Same(single, singleSlot.Content);
                Assert.Same(initialModel, single.DataContext);
                Assert.Same(initialModel, button.DataContext);
                Assert.Equal(initialModel.Title, subtitle.Text);
                Assert.Same(button, page.FindName("Action"));
                Assert.Same(grid, VisualTreeHelper.GetParent(button));
                Assert.Same(grid, LogicalTreeHelper.GetParent(subtitle));
                Assert.Equal(grid.ColumnDefinitions[2].Offset, button.TranslatePoint(new Point(), grid).X, 3);
                Assert.Equal(grid.RowDefinitions[1].Offset, subtitle.TranslatePoint(new Point(), grid).Y, 3);
                Assert.Equal(grid.ActualHeight, button.ActualHeight, 3);
                ((IInvokeProvider)new ButtonAutomationPeer(button)).Invoke();
                DrainDispatcher();
                Assert.Equal(1, initialModel.ExecutionCount);

                var replacementModel = new HeaderModel("Changed subtitle");
                page.DataContext = replacementModel;
                DrainDispatcher();
                Assert.Equal(replacementModel.Title, subtitle.Text);
                Assert.Equal(replacementModel.Title, single.Text);
                Assert.Same(replacementModel.Command, button.Command);

                navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;
                DrainDispatcher();
                Assert.True(button.IsLoaded);
                Assert.True(subtitle.IsLoaded);
                Assert.Same(grid, VisualTreeHelper.GetParent(button));
                Assert.Equal(4, grid.Children.Count);

                navigation.Navigating += (_, args) => args.Cancel = true;
                Assert.False(navigation.Navigate(typeof(NavigationViewPageHeaderTests.PlainPage)));
                DrainDispatcher();
                Assert.Same(items, navigation.CurrentPageHeaderItems);
                Assert.Same(single, singleSlot.Content);
                Assert.True(button.IsLoaded);

                Assert.True(navigation.ReplaceContent(new Page { Content = new Grid() }));
                DrainDispatcher();
                Assert.Null(navigation.CurrentPageHeaderItems);
                Assert.Null(singleSlot.Content);
                Assert.Null(VisualTreeHelper.GetParent(button));
                Assert.Equal(2, grid.Children.Count);
                Assert.Same(DependencyProperty.UnsetValue, subtitle.ReadLocalValue(FrameworkElement.DataContextProperty));

                var inactiveSingle = new TextBlock { Text = "Inactive replacement" };
                NavigationControl.SetPageHeaderContent(page, inactiveSingle);
                var inactiveItem = CreateBoundText();
                items.Add(inactiveItem);
                Assert.Null(singleSlot.Content);
                Assert.Null(navigation.CurrentPageHeaderItems);
                Assert.Same(DependencyProperty.UnsetValue, inactiveItem.ReadLocalValue(FrameworkElement.DataContextProperty));

                // Cached pages can contribute the same element instances on a later visit.
                Assert.True(navigation.ReplaceContent(page));
                DrainDispatcher();
                Assert.Same(items, navigation.CurrentPageHeaderItems);
                Assert.Equal(replacementModel.Title, subtitle.Text);
                Assert.True(button.IsLoaded);
                Assert.Same(inactiveSingle, singleSlot.Content);
                Assert.Equal(replacementModel.Title, inactiveItem.Text);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    /// <summary>
    /// Verifies live mutations, binding ownership, and collection replacement without single churn.
    /// </summary>
    [Fact]
    public void ActiveCollectionMutationsPreserveExplicitContextsAndReleaseRemovedBridges()
    {
        WpfTestThread.Run(() =>
        {
            var model = new HeaderModel("Initial");
            var page = new Page { Content = new Grid(), DataContext = model };
            var single = new TextBlock();
            NavigationControl.SetPageHeaderContent(page, single);
            var items = NavigationControl.GetPageHeaderItems(page);
            var inherited = CreateBoundText();
            var explicitModel = new HeaderModel("Explicit");
            var explicitItem = CreateBoundText();
            explicitItem.DataContext = explicitModel;
            var explicitNull = new TextBlock { DataContext = null };
            items.Add(inherited);
            items.Add(explicitItem);
            items.Add(explicitNull);
            var navigation = CreateNavigationView(out var grid, out _);
            var window = CreateWindow(navigation);

            try
            {
                window.Show();
                Assert.True(navigation.ReplaceContent(page));
                DrainDispatcher();
                var singleBinding = BindingOperations.GetBindingExpression(single, FrameworkElement.DataContextProperty);
                Assert.Equal("Initial", inherited.Text);
                Assert.Equal("Explicit", explicitItem.Text);
                Assert.Null(explicitNull.DataContext);

                var added = CreateBoundText();
                items.Add(added);
                DrainDispatcher();
                Assert.Same(grid, VisualTreeHelper.GetParent(added));
                Assert.Equal("Initial", added.Text);

                var addedBinding = BindingOperations.GetBindingExpression(added, FrameworkElement.DataContextProperty);
                items.Move(3, 0);
                DrainDispatcher();
                Assert.Same(added, grid.Children[2]);
                Assert.Same(addedBinding, BindingOperations.GetBindingExpression(added, FrameworkElement.DataContextProperty));

                // A binding installed by the page after activation remains owned by the page.
                var pageBinding = BindingOperations.SetBinding(
                    inherited, FrameworkElement.DataContextProperty, new Binding { Source = explicitModel }
                );
                var replacementItem = CreateBoundText();
                items[1] = replacementItem;
                DrainDispatcher();
                Assert.Null(VisualTreeHelper.GetParent(inherited));
                Assert.Same(pageBinding, BindingOperations.GetBindingExpression(inherited, FrameworkElement.DataContextProperty));
                Assert.Equal("Initial", replacementItem.Text);

                items.Remove(added);
                DrainDispatcher();
                Assert.Null(VisualTreeHelper.GetParent(added));
                Assert.Same(DependencyProperty.UnsetValue, added.ReadLocalValue(FrameworkElement.DataContextProperty));
                items.Clear();
                DrainDispatcher();
                Assert.Equal(2, grid.Children.Count);
                Assert.Same(explicitModel, explicitItem.DataContext);
                Assert.Same(DependencyProperty.UnsetValue, replacementItem.ReadLocalValue(FrameworkElement.DataContextProperty));

                var nextItem = CreateBoundText();
                var nextCollection = new PageHeaderItemCollection { nextItem };
                NavigationControl.SetPageHeaderItems(page, nextCollection);
                DrainDispatcher();
                Assert.Same(nextCollection, navigation.CurrentPageHeaderItems);
                Assert.Equal("Initial", nextItem.Text);
                Assert.Same(singleBinding, BindingOperations.GetBindingExpression(single, FrameworkElement.DataContextProperty));

                // Replacing a collection releases its subscription, even if callers retain it.
                var obsoleteItem = new TextBlock();
                items.Add(obsoleteItem);
                Assert.Null(VisualTreeHelper.GetParent(obsoleteItem));
                Assert.Same(DependencyProperty.UnsetValue, obsoleteItem.ReadLocalValue(FrameworkElement.DataContextProperty));

                page.DataContext = new HeaderModel("Updated");
                DrainDispatcher();
                Assert.Equal("Updated", nextItem.Text);
                NavigationControl.SetPageHeaderItems(page, null);
                DrainDispatcher();
                Assert.Null(navigation.CurrentPageHeaderItems);
                Assert.Equal(2, grid.Children.Count);
                Assert.Same(single, navigation.CurrentPageHeaderContent);
                Assert.Same(DependencyProperty.UnsetValue, nextItem.ReadLocalValue(FrameworkElement.DataContextProperty));

                var replacementSingle = new TextBlock();
                NavigationControl.SetPageHeaderContent(page, replacementSingle);
                DrainDispatcher();
                Assert.Same(replacementSingle, navigation.CurrentPageHeaderContent);
                Assert.Null(navigation.CurrentPageHeaderItems);
                Assert.Same(DependencyProperty.UnsetValue, single.ReadLocalValue(FrameworkElement.DataContextProperty));
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    /// <summary>
    /// Verifies that default collections belong to individual pages and invalid changes are atomic.
    /// </summary>
    [Fact]
    public void EachPageHasItsOwnCollectionAndRejectsNullOrDuplicateItems()
    {
        WpfTestThread.Run(() =>
        {
            var first = new Page();
            var second = new Page();
            var firstItems = NavigationControl.GetPageHeaderItems(first);
            Assert.Same(firstItems, NavigationControl.GetPageHeaderItems(first));
            Assert.NotSame(firstItems, NavigationControl.GetPageHeaderItems(second));
            var item = new TextBlock();
            var other = new TextBlock();
            firstItems.Add(item);
            firstItems.Add(other);
            Assert.Throws<ArgumentException>(() => firstItems.Add(item));
            Assert.Throws<ArgumentNullException>(() => firstItems.Add(null!));
            Assert.Throws<ArgumentException>(() => firstItems[1] = item);
            Assert.Equal(new FrameworkElement[] { item, other }, firstItems);
        });
    }

    /// <summary>
    /// Builds a real NavigationView whose single slot and collection host use independent bindings.
    /// </summary>
    private static NavigationControl CreateNavigationView(out Grid grid, out ContentPresenter singleSlot)
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        var uri = System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
        var resources = new ResourceDictionary { Source = uri };
        var navigation = new NavigationControl
        {
            Style = Assert.IsType<Style>(resources[typeof(NavigationControl)]),
        };
        grid = new Grid { DataContext = new object() };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = "Shell breadcrumb", FontSize = 24 });
        singleSlot = new ContentPresenter();
        Grid.SetColumn(singleSlot, 1);
        grid.Children.Add(singleSlot);
        grid.SetBinding(PanelItems.SourceProperty, new Binding(nameof(NavigationControl.CurrentPageHeaderItems))
        {
            Source = navigation,
        });
        singleSlot.SetBinding(ContentPresenter.ContentProperty, new Binding(nameof(NavigationControl.CurrentPageHeaderContent))
        {
            Source = navigation,
        });
        navigation.Header = grid;
        return navigation;
    }

    private static TextBlock CreateBoundText()
    {
        var text = new TextBlock();
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(HeaderModel.Title)));
        return text;
    }

    private static Window CreateWindow(object content)
    {
        return new Window
        {
            Content = content,
            Width = 800,
            Height = 600,
            Left = -10_000,
            Top = -10_000,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
    }

    private static void DrainDispatcher()
    {
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
    }

    private sealed class HeaderModel
    {
        /// <summary>
        /// Gets the label consumed by contributed header bindings.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Gets the action consumed by contributed header buttons.
        /// </summary>
        public System.Windows.Input.ICommand Command { get; }

        /// <summary>
        /// Gets the number of actions invoked through the presented button.
        /// </summary>
        public int ExecutionCount { get; private set; }

        /// <summary>
        /// Creates the label and command for one page context.
        /// </summary>
        /// <param name="title">The label displayed by the header.</param>
        public HeaderModel(string title)
        {
            Title = title;
            Command = new RelayCommand(() => ExecutionCount++);
        }
    }
}
