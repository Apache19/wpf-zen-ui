// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls.NavigationView;

public class NavigationViewTests
{
    [Fact]
    public void NavigationViewList_DefaultTemplate_HostsItemsPresenterInPassiveScrollViewer()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationViewList = new NavigationViewList
            {
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewList)]),
                ScrollSpillMode = ScrollSpillMode.Disabled,
            };
            System.Windows.Controls.ScrollViewer.SetCanContentScroll(navigationViewList, true);
            System.Windows.Controls.ScrollViewer.SetHorizontalScrollBarVisibility(
                navigationViewList,
                ScrollBarVisibility.Disabled
            );
            System.Windows.Controls.ScrollViewer.SetVerticalScrollBarVisibility(
                navigationViewList,
                ScrollBarVisibility.Auto
            );

            Assert.True(navigationViewList.ApplyTemplate());

            var scrollViewer = Assert.IsType<PassiveScrollViewer>(
                navigationViewList.Template.FindName("ScrollViewer", navigationViewList)
            );
            Assert.IsType<ItemsPresenter>(scrollViewer.Content);
            Assert.True(scrollViewer.CanContentScroll);
            Assert.Equal(ScrollBarVisibility.Disabled, scrollViewer.HorizontalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);
            Assert.Equal(ScrollSpillMode.Disabled, scrollViewer.ScrollSpillMode);
        });
    }

    [Fact]
    public void NavigationViewList_DynamicTemplate_HostsItemsPresenterInDynamicScrollViewer()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationViewList = new NavigationViewList
            {
                IsDynamicScrollViewerEnabled = true,
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewList)]),
            };
            System.Windows.Controls.ScrollViewer.SetCanContentScroll(navigationViewList, true);
            System.Windows.Controls.ScrollViewer.SetVerticalScrollBarVisibility(
                navigationViewList,
                ScrollBarVisibility.Auto
            );

            Assert.True(navigationViewList.ApplyTemplate());

            var scrollViewer = Assert.IsType<DynamicScrollViewer>(
                navigationViewList.Template.FindName("ScrollViewer", navigationViewList)
            );
            Assert.IsType<ItemsPresenter>(scrollViewer.Content);
            Assert.True(scrollViewer.CanContentScroll);
            Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NavigationViewList_CanContentScroll_UsesItemBasedExtent(
        bool isDynamicScrollViewerEnabled)
    {
        RunOnStaThread(() =>
        {
            const int itemCount = 10;
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationViewList = new NavigationViewList
            {
                Width = 100D,
                Height = 60D,
                IsDynamicScrollViewerEnabled = isDynamicScrollViewerEnabled,
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewList)]),
            };
            System.Windows.Controls.ScrollViewer.SetCanContentScroll(navigationViewList, true);
            System.Windows.Controls.ScrollViewer.SetVerticalScrollBarVisibility(
                navigationViewList,
                ScrollBarVisibility.Auto
            );

            for (var index = 0; index < itemCount; index++)
            {
                navigationViewList.Items.Add(new Border { Height = 20D });
            }

            var window = new Window
            {
                Content = navigationViewList,
                Width = 100D,
                Height = 60D,
                Left = -10_000D,
                Top = -10_000D,
                ShowActivated = false,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.Manual,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                DrainDispatcher();

                var scrollViewer = Assert.IsAssignableFrom<PassiveScrollViewer>(
                    navigationViewList.Template.FindName("ScrollViewer", navigationViewList)
                );

                // StackPanel reports one logical unit per item through IScrollInfo. A pixel-scrolling
                // outer viewer would report the combined 200-DIP item height instead.
                Assert.Equal(itemCount, scrollViewer.ExtentHeight);
                Assert.True(scrollViewer.ViewportHeight < scrollViewer.ExtentHeight);

                scrollViewer.LineDown();
                navigationViewList.UpdateLayout();

                Assert.Equal(1D, scrollViewer.VerticalOffset);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void NavigationViewList_ItemContainerStyle_IsAppliedOnlyToNavigationViewItem()
    {
        RunOnStaThread(() =>
        {
            var itemContainerStyle = new Style(typeof(NavigationViewItem));
            var navigationItem = new NavigationViewItem();
            var separator = new Wpf.Ui.Controls.Separator();
            var header = new NavigationViewItemHeader();
            var navigationViewList = new NavigationViewList
            {
                ItemContainerStyle = itemContainerStyle,
            };
            navigationViewList.Items.Add(navigationItem);
            navigationViewList.Items.Add(separator);
            navigationViewList.Items.Add(header);

            var window = new Window
            {
                Content = navigationViewList,
                Width = 1,
                Height = 1,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                DrainDispatcher();

                Assert.Same(itemContainerStyle, navigationItem.ReadLocalValue(FrameworkElement.StyleProperty));
                Assert.Equal(
                    DependencyProperty.UnsetValue,
                    separator.ReadLocalValue(FrameworkElement.StyleProperty)
                );
                Assert.Equal(
                    DependencyProperty.UnsetValue,
                    header.ReadLocalValue(FrameworkElement.StyleProperty)
                );
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void BuiltInTemplates_UseNavigationViewListForMenuHosts()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
            };

            NavigationViewPaneDisplayMode[] displayModes =
            [
                NavigationViewPaneDisplayMode.Left,
                NavigationViewPaneDisplayMode.LeftFluent,
                NavigationViewPaneDisplayMode.Top,
                NavigationViewPaneDisplayMode.Bottom,
            ];

            foreach (NavigationViewPaneDisplayMode displayMode in displayModes)
            {
                navigationView.PaneDisplayMode = displayMode;

                Assert.True(navigationView.ApplyTemplate());
                Assert.IsType<NavigationViewList>(
                    navigationView.Template.FindName("PART_MenuItemsList", navigationView)
                );
                Assert.IsType<NavigationViewList>(
                    navigationView.Template.FindName("PART_FooterMenuItemsList", navigationView)
                );
            }
        });
    }

    [Theory]
    [InlineData(NavigationViewPaneDisplayMode.Left)]
    [InlineData(NavigationViewPaneDisplayMode.LeftFluent)]
    [InlineData(NavigationViewPaneDisplayMode.LeftMinimal)]
    public void BuiltInVerticalTemplates_UseDynamicViewerInsideNavigationViewList(
        NavigationViewPaneDisplayMode displayMode)
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = displayMode,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
            };

            Assert.True(navigationView.ApplyTemplate());
            Assert.Null(navigationView.Template.FindName("PART_ScrollViewer", navigationView));

            var navigationViewList = Assert.IsType<NavigationViewList>(
                navigationView.Template.FindName("PART_MenuItemsList", navigationView)
            );
            Assert.True(navigationViewList.IsDynamicScrollViewerEnabled);
            Assert.False(
                System.Windows.Media.VisualTreeHelper.GetParent(navigationViewList)
                    is System.Windows.Controls.ScrollViewer
            );
            Assert.True(navigationViewList.ApplyTemplate());

            var scrollViewer = Assert.IsType<DynamicScrollViewer>(
                navigationViewList.Template.FindName("ScrollViewer", navigationViewList)
            );
            Assert.IsType<ItemsPresenter>(scrollViewer.Content);
            Assert.True(scrollViewer.CanContentScroll);
            Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);
        });
    }

    [Fact]
    public void HasItemsProperties_AreReadOnlyAndTrackCollectionsAndSources()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView();

            Assert.True(Wpf.Ui.Controls.NavigationView.HasMenuItemsProperty.ReadOnly);
            Assert.True(Wpf.Ui.Controls.NavigationView.HasFooterMenuItemsProperty.ReadOnly);
            Assert.False(navigationView.HasMenuItems);
            Assert.False(navigationView.HasFooterMenuItems);

            navigationView.MenuItems.Add(new object());
            Assert.True(navigationView.HasMenuItems);
            Assert.False(navigationView.HasFooterMenuItems);

            navigationView.FooterMenuItems.Add(new object());
            Assert.True(navigationView.HasMenuItems);
            Assert.True(navigationView.HasFooterMenuItems);

            navigationView.MenuItems.Clear();
            navigationView.FooterMenuItems.Clear();
            Assert.False(navigationView.HasMenuItems);
            Assert.False(navigationView.HasFooterMenuItems);

            var menuItemsSource = new ObservableCollection<object>();
            var footerMenuItemsSource = new ObservableCollection<object>();
            navigationView.MenuItemsSource = menuItemsSource;
            navigationView.FooterMenuItemsSource = footerMenuItemsSource;

            menuItemsSource.Add(new object());
            footerMenuItemsSource.Add(new object());
            Assert.True(navigationView.HasMenuItems);
            Assert.True(navigationView.HasFooterMenuItems);

            menuItemsSource.Clear();
            footerMenuItemsSource.Clear();
            Assert.False(navigationView.HasMenuItems);
            Assert.False(navigationView.HasFooterMenuItems);
        });
    }

    [Fact]
    public void PaneDisplayMode_ReplacesVisualTemplateAndPreservesNavigationContent()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]);
            var leftTemplate = Assert.IsType<ControlTemplate>(resources["LeftNavigationViewTemplate"]);
            var fluentTemplate = Assert.IsType<ControlTemplate>(resources["LeftFluentNavigationViewTemplate"]);
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = style,
                AutoSuggestBox = new AutoSuggestBox(),
                PaneHeader = new System.Windows.Controls.TextBlock { Text = "Header" },
                IsBackButtonVisible = NavigationViewBackButtonVisible.Visible,
                IsPaneToggleVisible = true,
            };

            Assert.NotSame(leftTemplate, fluentTemplate);
            Assert.Same(leftTemplate, navigationView.Template);
            Assert.True(navigationView.ApplyTemplate());

            var navigationPresenter = GetNavigationPresenter(navigationView);
            var page = new Border();
            navigationPresenter.JournalOwnership = System.Windows.Navigation.JournalOwnership.OwnsJournal;
            Assert.True(navigationView.ReplaceContent(page));
            DrainDispatcher();
            Assert.Same(page, navigationPresenter.Content);
            Assert.NotNull(leftTemplate.FindName("SearchGrid", navigationView));

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;

            Assert.Same(fluentTemplate, navigationView.Template);
            Assert.True(navigationView.ApplyTemplate());
            Assert.Same(navigationPresenter, GetNavigationPresenter(navigationView));
            Assert.Same(page, navigationPresenter.Content);
            Assert.Null(fluentTemplate.FindName("SearchGrid", navigationView));
            Assert.NotNull(fluentTemplate.FindName("PART_TopSeparator", navigationView));
            Assert.True(double.IsNaN(GetPaneGrid(navigationView).Width));
            Assert.Equal(NavigationViewBackButtonVisible.Visible, navigationView.IsBackButtonVisible);
            Assert.True(navigationView.IsPaneToggleVisible);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;

            Assert.Same(leftTemplate, navigationView.Template);
            Assert.True(navigationView.ApplyTemplate());
            Assert.Same(navigationPresenter, GetNavigationPresenter(navigationView));
            Assert.Same(page, navigationPresenter.Content);
            Assert.NotNull(leftTemplate.FindName("SearchGrid", navigationView));
            Assert.NotNull(leftTemplate.FindName("PART_TopSeparator", navigationView));
            Assert.Equal(navigationView.LeftPaneOpenWidth, GetPaneGrid(navigationView).Width);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;

            Assert.True(navigationView.ApplyTemplate());
            Assert.Same(navigationPresenter, GetNavigationPresenter(navigationView));
            Assert.Same(page, navigationPresenter.Content);
            Assert.True(double.IsNaN(GetPaneGrid(navigationView).Width));
        });
    }

    [Theory]
    [InlineData(true, 300D)]
    [InlineData(false, 48D)]
    public void LeftPane_ColdStartAndModeRoundTrip_RetainTheResolvedWidth(
        bool isPaneOpen,
        double expectedWidth)
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
                IsPaneOpen = isPaneOpen,
                LeftPaneOpenWidth = 300D,
                LeftPaneCompactWidth = 48D,
            };

            Assert.True(navigationView.ApplyTemplate());
            Grid paneGrid = GetPaneGrid(navigationView);

            Assert.Equal(expectedWidth, navigationView.LeftPaneWidth);
            Assert.Equal(expectedWidth, paneGrid.Width);
            Assert.False(
                DependencyPropertyHelper
                    .GetValueSource(paneGrid, FrameworkElement.WidthProperty)
                    .IsAnimated
            );

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;
            Assert.True(navigationView.ApplyTemplate());
            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
            Assert.True(navigationView.ApplyTemplate());

            paneGrid = GetPaneGrid(navigationView);
            Assert.Equal(expectedWidth, navigationView.LeftPaneWidth);
            Assert.Equal(expectedWidth, paneGrid.Width);
            Assert.False(
                DependencyPropertyHelper
                    .GetValueSource(paneGrid, FrameworkElement.WidthProperty)
                    .IsAnimated
            );
        });
    }

    [Fact]
    public void LeftPane_ModeRoundTrip_CapturesRenderedWidthWithoutReplayingAnimation()
    {
        RunOnStaThread(() =>
        {
            const double renderedWidth = 137D;
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
                LeftPaneOpenWidth = 300D,
                LeftPaneCompactWidth = 40D,
            };

            Assert.True(navigationView.ApplyTemplate());
            Grid paneGrid = GetPaneGrid(navigationView);
            paneGrid.Width = renderedWidth;
            navigationView.Measure(new Size(800D, 600D));
            navigationView.Arrange(new Rect(0D, 0D, 800D, 600D));
            navigationView.UpdateLayout();
            Assert.Equal(renderedWidth, paneGrid.Width);
            Assert.Equal(renderedWidth, paneGrid.ActualWidth);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;
            Assert.True(navigationView.ApplyTemplate());
            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
            Assert.True(navigationView.ApplyTemplate());

            paneGrid = GetPaneGrid(navigationView);
            Assert.Equal(renderedWidth, navigationView.LeftPaneWidth);
            Assert.Equal(renderedWidth, paneGrid.Width);
            Assert.False(
                DependencyPropertyHelper
                    .GetValueSource(paneGrid, FrameworkElement.WidthProperty)
                    .IsAnimated
            );
        });
    }

    [Fact]
    public void LeftPane_ToggleAnimatesToConfiguredWidth_AndModeRoundTripDoesNotReplayIt()
    {
        RunOnStaThread(() =>
        {
            const double compactWidth = 72D;
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
                LeftPaneOpenWidth = 300D,
                LeftPaneCompactWidth = compactWidth,
            };
            var window = new Window
            {
                Content = navigationView,
                Width = 800D,
                Height = 600D,
                Left = -10_000D,
                Top = -10_000D,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                DrainDispatcher();

                Grid paneGrid = GetPaneGrid(navigationView);
                Assert.Equal(300D, paneGrid.Width);
                Assert.False(
                    DependencyPropertyHelper
                        .GetValueSource(paneGrid, FrameworkElement.WidthProperty)
                        .IsAnimated
                );

                navigationView.IsPaneOpen = false;
                DrainDispatcher(TimeSpan.FromMilliseconds(50D));
                Assert.True(paneGrid.Width > compactWidth);
                Assert.True(paneGrid.Width < navigationView.LeftPaneOpenWidth);
                DrainDispatcher(TimeSpan.FromMilliseconds(200D));
                Assert.Equal(compactWidth, paneGrid.Width);

                navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;
                Assert.True(navigationView.ApplyTemplate());
                navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
                Assert.True(navigationView.ApplyTemplate());

                paneGrid = GetPaneGrid(navigationView);
                Assert.Equal(compactWidth, navigationView.LeftPaneWidth);
                Assert.Equal(compactWidth, paneGrid.Width);
                Assert.False(
                    DependencyPropertyHelper
                        .GetValueSource(paneGrid, FrameworkElement.WidthProperty)
                        .IsAnimated
                );

                navigationView.IsPaneOpen = true;
                DrainDispatcher(TimeSpan.FromMilliseconds(50D));
                Assert.True(paneGrid.Width < navigationView.LeftPaneOpenWidth);
                Assert.True(paneGrid.Width > compactWidth);
                DrainDispatcher(TimeSpan.FromMilliseconds(200D));
                Assert.Equal(navigationView.LeftPaneOpenWidth, paneGrid.Width);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void PaneDisplayMode_WhenLoaded_DoesNotNavigateAwayFromPreservedPage()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
            };
            var window = new Window
            {
                Content = navigationView,
                Width = 1,
                Height = 1,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };

            Assert.True(navigationView.ApplyTemplate());
            var navigationPresenter = GetNavigationPresenter(navigationView);
            navigationPresenter.JournalOwnership = System.Windows.Navigation.JournalOwnership.OwnsJournal;
            var page = new NavigationAwarePage();

            try
            {
                window.Show();
                DrainDispatcher();

                Assert.True(navigationView.ReplaceContent(page));
                DrainDispatcher();
                Assert.Equal(1, page.NavigatedToCount);
                Assert.Equal(0, page.NavigatedFromCount);

                navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;
                Assert.True(navigationView.ApplyTemplate());
                DrainDispatcher();

                Assert.Same(navigationPresenter, GetNavigationPresenter(navigationView));
                Assert.Same(page, navigationPresenter.Content);
                Assert.Equal(1, page.NavigatedToCount);
                Assert.Equal(0, page.NavigatedFromCount);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }

            Assert.Equal(1, page.NavigatedFromCount);
        });
    }

    private static NavigationViewContentPresenter GetNavigationPresenter(
        Wpf.Ui.Controls.NavigationView navigationView
    )
    {
        var host = Assert.IsType<ContentPresenter>(
            navigationView.Template.FindName("PART_NavigationViewContentPresenterHost", navigationView)
        );

        return Assert.IsType<NavigationViewContentPresenter>(host.Content);
    }

    private static Grid GetPaneGrid(Wpf.Ui.Controls.NavigationView navigationView) =>
        Assert.IsType<Grid>(navigationView.Template.FindName("PaneGrid", navigationView));

    private static Uri CreateResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
    }

    private static void DrainDispatcher()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false)
        );
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static void DrainDispatcher(TimeSpan delay)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle
        )
        {
            Interval = delay,
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
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

    private sealed class NavigationAwarePage : Border, INavigationAware
    {
        public int NavigatedToCount { get; private set; }

        public int NavigatedFromCount { get; private set; }

        public Task OnNavigatedToAsync()
        {
            NavigatedToCount++;
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            NavigatedFromCount++;
            return Task.CompletedTask;
        }
    }
}
