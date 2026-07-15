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
            var fluentTemplate = Assert.IsType<ControlTemplate>(resources["FluentNavigationViewTemplate"]);
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
            Assert.Equal(navigationView.OpenPaneLength, GetPaneGrid(navigationView).Width);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;

            Assert.True(navigationView.ApplyTemplate());
            Assert.Same(navigationPresenter, GetNavigationPresenter(navigationView));
            Assert.Same(page, navigationPresenter.Content);
            Assert.True(double.IsNaN(GetPaneGrid(navigationView).Width));
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PaneActualWidth_IsReadOnlyAndTracksRenderedPane(bool isPaneOpen)
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
                PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent,
                IsPaneOpen = isPaneOpen,
            };
            navigationView.MenuItems.Add(new NavigationViewItem { Content = "Home" });

            var window = new Window
            {
                Content = navigationView,
                Width = 800,
                Height = 600,
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

                var fluentPane = GetPaneGrid(navigationView);
                Assert.True(Wpf.Ui.Controls.NavigationView.PaneActualWidthProperty.ReadOnly);
                Assert.True(fluentPane.ActualWidth > 0);
                Assert.Equal(fluentPane.ActualWidth, navigationView.PaneActualWidth);

                var animationOrigin = navigationView.PaneActualWidth;
                navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
                Assert.True(navigationView.ApplyTemplate());

                Assert.Equal(animationOrigin, navigationView.PaneActualWidth);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PaneActualWidth_UsesCurrentPaneStateBeforeFirstLayout(bool isPaneOpen)
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
                IsPaneOpen = isPaneOpen,
            };
            var window = new Window
            {
                Content = navigationView,
                Width = 800,
                Height = 600,
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

                var expectedWidth = isPaneOpen
                    ? navigationView.OpenPaneLength
                    : navigationView.CompactPaneLength;
                Assert.Equal(expectedWidth, navigationView.PaneActualWidth);
                Assert.Equal(expectedWidth, GetPaneGrid(navigationView).ActualWidth);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PaneDisplayMode_AnimatesFromFluentWidthAndPaneToggleRemainsResponsive(
        bool initialIsPaneOpen
    )
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
                PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent,
                IsPaneOpen = initialIsPaneOpen,
            };
            navigationView.MenuItems.Add(new NavigationViewItem { Content = "Home" });

            var window = new Window
            {
                Content = navigationView,
                Width = 800,
                Height = 600,
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

                var fluentWidth = GetPaneGrid(navigationView).ActualWidth;
                navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
                Assert.True(navigationView.ApplyTemplate());
                Assert.Equal(fluentWidth, navigationView.PaneActualWidth);

                PumpDispatcher(TimeSpan.FromMilliseconds(250));
                Assert.Equal(
                    initialIsPaneOpen ? "PaneOpen" : "PaneCompact",
                    GetPaneStateGroup(navigationView).CurrentState.Name
                );
                Assert.Equal(
                    initialIsPaneOpen
                        ? navigationView.OpenPaneLength
                        : navigationView.CompactPaneLength,
                    GetPaneGrid(navigationView).ActualWidth
                );
                Assert.Equal(GetPaneGrid(navigationView).ActualWidth, navigationView.PaneActualWidth);

                navigationView.IsPaneOpen = !initialIsPaneOpen;
                PumpDispatcher(TimeSpan.FromMilliseconds(250));
                Assert.Equal(
                    initialIsPaneOpen ? "PaneCompact" : "PaneOpen",
                    GetPaneStateGroup(navigationView).CurrentState.Name
                );
                Assert.Equal(
                    initialIsPaneOpen
                        ? navigationView.CompactPaneLength
                        : navigationView.OpenPaneLength,
                    GetPaneGrid(navigationView).ActualWidth
                );
                Assert.Equal(GetPaneGrid(navigationView).ActualWidth, navigationView.PaneActualWidth);

                navigationView.IsPaneOpen = initialIsPaneOpen;
                PumpDispatcher(TimeSpan.FromMilliseconds(250));
                Assert.Equal(
                    initialIsPaneOpen
                        ? navigationView.OpenPaneLength
                        : navigationView.CompactPaneLength,
                    GetPaneGrid(navigationView).ActualWidth
                );
                Assert.Equal(GetPaneGrid(navigationView).ActualWidth, navigationView.PaneActualWidth);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void PaneAnimationSettings_AreConfigurableAndCanDisableAnimations()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var configuredDuration = new System.Windows.Duration(TimeSpan.FromSeconds(1D));
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
                PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent,
                PaneAnimationDuration = configuredDuration,
                PaneAnimationAccelerationRatio = 0.75D,
                IsPaneAnimationEnabled = false,
            };
            navigationView.MenuItems.Add(new NavigationViewItem { Content = "Home" });

            Assert.Equal(TimeSpan.Zero, navigationView.PaneAnimationDuration.TimeSpan);
            Assert.Equal(0.75D, navigationView.PaneAnimationAccelerationRatio);

            navigationView.IsPaneAnimationEnabled = true;
            Assert.Equal(configuredDuration, navigationView.PaneAnimationDuration);
            navigationView.IsPaneAnimationEnabled = false;

            var window = CreateTestWindow(navigationView);

            try
            {
                window.Show();
                DrainDispatcher();

                navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
                Assert.True(navigationView.ApplyTemplate());
                DrainDispatcher();
                Assert.Equal(navigationView.OpenPaneLength, GetPaneGrid(navigationView).ActualWidth);

                navigationView.IsPaneOpen = false;
                DrainDispatcher();
                Assert.Equal(navigationView.CompactPaneLength, GetPaneGrid(navigationView).ActualWidth);
                Assert.Equal(
                    Visibility.Hidden,
                    GetAutoSuggestBoxPresenter(navigationView).Visibility
                );
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Theory]
    [InlineData(NavigationViewPaneDisplayMode.Top, true)]
    [InlineData(NavigationViewPaneDisplayMode.Top, false)]
    [InlineData(NavigationViewPaneDisplayMode.Bottom, true)]
    [InlineData(NavigationViewPaneDisplayMode.Bottom, false)]
    public void PaneDisplayMode_FromHorizontalMode_AppliesCompactWidthWithoutAnimation(
        NavigationViewPaneDisplayMode initialMode,
        bool isPaneOpen
    )
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
                PaneDisplayMode = initialMode,
                IsPaneOpen = isPaneOpen,
                PaneAnimationDuration = new System.Windows.Duration(TimeSpan.FromSeconds(5D)),
            };
            navigationView.MenuItems.Add(new NavigationViewItem { Content = "Home" });
            var window = CreateTestWindow(navigationView);

            try
            {
                window.Show();
                DrainDispatcher();

                navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
                Assert.True(navigationView.ApplyTemplate());
                DrainDispatcher();

                Assert.Equal(
                    isPaneOpen
                        ? navigationView.OpenPaneLength
                        : navigationView.CompactPaneLength,
                    GetPaneGrid(navigationView).ActualWidth
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
    public void PaneAnimationDuration_SynchronizesWidthAndPaneContentAnimations()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
                PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent,
                PaneAnimationDuration = new System.Windows.Duration(
                    TimeSpan.FromMilliseconds(600D)
                ),
                PaneAnimationAccelerationRatio = 0D,
            };
            navigationView.MenuItems.Add(new NavigationViewItem { Content = "Home" });
            var window = CreateTestWindow(navigationView);

            try
            {
                window.Show();
                DrainDispatcher();

                var fluentWidth = GetPaneGrid(navigationView).ActualWidth;
                navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
                Assert.True(navigationView.ApplyTemplate());

                PumpDispatcher(TimeSpan.FromMilliseconds(200D));
                Assert.InRange(
                    GetPaneGrid(navigationView).ActualWidth,
                    fluentWidth + 1D,
                    navigationView.OpenPaneLength - 1D
                );
                Assert.InRange(GetAutoSuggestBoxPresenter(navigationView).Opacity, 0.01D, 0.99D);

                PumpDispatcher(TimeSpan.FromMilliseconds(800D));
                Assert.Equal(navigationView.OpenPaneLength, GetPaneGrid(navigationView).ActualWidth);
                Assert.Equal(1D, GetAutoSuggestBoxPresenter(navigationView).Opacity);

                navigationView.IsPaneOpen = false;
                PumpDispatcher(TimeSpan.FromMilliseconds(200D));
                Assert.InRange(
                    GetPaneGrid(navigationView).ActualWidth,
                    navigationView.CompactPaneLength + 1D,
                    navigationView.OpenPaneLength - 1D
                );
                Assert.InRange(GetAutoSuggestBoxPresenter(navigationView).Opacity, 0.01D, 0.99D);
                Assert.InRange(GetAutoSuggestBoxButton(navigationView).Opacity, 0.01D, 0.99D);

                PumpDispatcher(TimeSpan.FromMilliseconds(800D));
                Assert.Equal(navigationView.CompactPaneLength, GetPaneGrid(navigationView).ActualWidth);
                Assert.Equal(
                    Visibility.Hidden,
                    GetAutoSuggestBoxPresenter(navigationView).Visibility
                );
                Assert.Equal(1D, GetAutoSuggestBoxButton(navigationView).Opacity);
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
        Assert.IsAssignableFrom<Grid>(
            navigationView.Template.FindName("PaneGrid", navigationView)
        );

    private static ContentPresenter GetAutoSuggestBoxPresenter(
        Wpf.Ui.Controls.NavigationView navigationView
    ) =>
        Assert.IsType<ContentPresenter>(
            navigationView.Template.FindName("AutoSuggestBoxContentPresenter", navigationView)
        );

    private static UIElement GetAutoSuggestBoxButton(
        Wpf.Ui.Controls.NavigationView navigationView
    ) =>
        Assert.IsAssignableFrom<UIElement>(
            navigationView.Template.FindName("PART_AutoSuggestBoxSymbolButton", navigationView)
        );

    private static Window CreateTestWindow(object content) =>
        new()
        {
            Content = content,
            Width = 800,
            Height = 600,
            Left = -10_000,
            Top = -10_000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };

    private static VisualStateGroup GetPaneStateGroup(
        Wpf.Ui.Controls.NavigationView navigationView
    )
    {
        var root = Assert.IsAssignableFrom<FrameworkElement>(
            System.Windows.Media.VisualTreeHelper.GetParent(GetPaneGrid(navigationView))
        );

        foreach (var group in VisualStateManager.GetVisualStateGroups(root))
        {
            if (group is VisualStateGroup { Name: "PaneStates" } paneStates)
            {
                return paneStates;
            }
        }

        throw new Xunit.Sdk.XunitException("PaneStates visual state group was not found.");
    }

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

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background
        )
        {
            Interval = duration,
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        DrainDispatcher();
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
