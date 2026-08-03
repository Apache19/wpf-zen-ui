// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Wpf.Zen.Ui.Abstractions.Controls;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Markup;
using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

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
            };
            PassiveScrollViewer.SetScrollSpillMode(
                navigationViewList,
                ScrollSpillMode.Disabled
            );
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
    public void NavigationViewList_CanContentScroll_UsesItemBasedExtent(bool isDynamicScrollViewerEnabled)
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
            var separator = new Wpf.Zen.Ui.Controls.Separator();
            var header = new NavigationViewItemHeader();
            var navigationViewList = new NavigationViewList { ItemContainerStyle = itemContainerStyle };
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

                Assert.Same(
                    itemContainerStyle,
                    navigationItem.ReadLocalValue(FrameworkElement.StyleProperty)
                );
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
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
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
    [InlineData(NavigationViewPaneDisplayMode.LeftMinimal)]
    [InlineData(NavigationViewPaneDisplayMode.LeftFluent)]
    [InlineData(NavigationViewPaneDisplayMode.Top)]
    [InlineData(NavigationViewPaneDisplayMode.Bottom)]
    public void BuiltInTemplates_ForwardIndependentContainerPresentationToEachMenuHost(
        NavigationViewPaneDisplayMode displayMode
    )
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var menuStyle = new Style(typeof(NavigationViewItem));
            var footerStyle = new Style(typeof(NavigationViewItem));
            var menuSelector = new FixedStyleSelector(menuStyle);
            var footerSelector = new FixedStyleSelector(footerStyle);
            var menuTemplate = new DataTemplate(typeof(string));
            var footerTemplate = new DataTemplate(typeof(int));
            var menuTemplateSelector = new FixedDataTemplateSelector(menuTemplate);
            var footerTemplateSelector = new FixedDataTemplateSelector(footerTemplate);
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
                PaneDisplayMode = displayMode,
                MenuItemContainerStyle = menuStyle,
                MenuItemContainerStyleSelector = menuSelector,
                FooterMenuItemContainerStyle = footerStyle,
                FooterMenuItemContainerStyleSelector = footerSelector,
                MenuItemTemplate = menuTemplate,
                MenuItemTemplateSelector = menuTemplateSelector,
                FooterMenuItemTemplate = footerTemplate,
                FooterMenuItemTemplateSelector = footerTemplateSelector,
            };

            Assert.True(navigationView.ApplyTemplate());

            var menuItemsList = Assert.IsType<NavigationViewList>(
                navigationView.Template.FindName("PART_MenuItemsList", navigationView)
            );
            var footerMenuItemsList = Assert.IsType<NavigationViewList>(
                navigationView.Template.FindName("PART_FooterMenuItemsList", navigationView)
            );

            Assert.Same(menuStyle, menuItemsList.ItemContainerStyle);
            Assert.Same(menuSelector, menuItemsList.ItemContainerStyleSelector);
            Assert.Same(footerStyle, footerMenuItemsList.ItemContainerStyle);
            Assert.Same(footerSelector, footerMenuItemsList.ItemContainerStyleSelector);
            Assert.Same(menuTemplate, menuItemsList.ItemTemplate);
            Assert.Same(menuTemplateSelector, menuItemsList.ItemTemplateSelector);
            Assert.Same(footerTemplate, footerMenuItemsList.ItemTemplate);
            Assert.Same(footerTemplateSelector, footerMenuItemsList.ItemTemplateSelector);
        });
    }

    [Theory]
    [InlineData(NavigationViewPaneDisplayMode.Left)]
    [InlineData(NavigationViewPaneDisplayMode.LeftFluent)]
    [InlineData(NavigationViewPaneDisplayMode.LeftMinimal)]
    public void BuiltInVerticalTemplates_UseDynamicViewerInsideNavigationViewList(
        NavigationViewPaneDisplayMode displayMode
    )
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = displayMode,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
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
            // Built-in navigation menus use pixel scrolling because an expanded hierarchical item
            // can change height without changing the number of logical items. NavigationViewList
            // still supports logical scrolling when a caller explicitly opts in.
            Assert.False(scrollViewer.CanContentScroll);
            Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);
        });
    }

    [Fact]
    public void NavigationFrame_DoesNotChangePageScrollingOrAddAnOuterScrollViewer()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var page = new Page();
            var window = new Window
            {
                Content = navigationView,
                Width = 100D,
                Height = 100D,
                Left = -10_000D,
                Top = -10_000D,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };

            // NavigationView must not override Page metadata globally. CanContentScroll belongs to
            // the page's own ScrollViewer and is unrelated to whether navigation adds one.
            Assert.False(System.Windows.Controls.ScrollViewer.GetCanContentScroll(page));
            System.Windows.Controls.ScrollViewer.SetCanContentScroll(page, true);

            try
            {
                window.Show();
                DrainDispatcher();
                _ = navigationView.ApplyTemplate();
                var navigationFrame = GetNavigationFrame(navigationView);

                Assert.True(navigationView.ReplaceContent(page));
                DrainDispatcher();
                _ = navigationFrame.ApplyTemplate();

                Assert.Same(page, navigationFrame.Content);
                Assert.True(System.Windows.Controls.ScrollViewer.GetCanContentScroll(page));
                Assert.IsType<ContentPresenter>(
                    navigationFrame.Template.FindName("PART_FrameCP", navigationFrame)
                );
                Assert.False(HasVisualDescendant<System.Windows.Controls.ScrollViewer>(navigationFrame));
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void NavigationFrame_AppliesPaddingOutsideContentThatDoesNotHandleIt()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var contentPadding = new Thickness(7D, 8D, 9D, 10D);
            var content = new Border();
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Padding = contentPadding,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var window = new Window
            {
                Content = navigationView,
                Width = 240D,
                Height = 180D,
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
                _ = navigationView.ApplyTemplate();
                Frame navigationFrame = GetNavigationFrame(navigationView);

                Assert.True(navigationView.ReplaceContent(content));
                DrainDispatcher();
                _ = navigationFrame.ApplyTemplate();
                DrainDispatcher();

                Assert.False(Wpf.Zen.Ui.Controls.NavigationView.GetIsContentPaddingHandled(content));
                Assert.Equal(contentPadding, navigationFrame.Padding);
                Assert.Equal(
                    new Point(contentPadding.Left, contentPadding.Top),
                    content.TranslatePoint(default, navigationFrame)
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
    public void DynamicScrollPageStyle_KeepsViewerAtNavigationEdgeAndInsetsOnlyItsContent()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var contentPadding = new Thickness(11D, 12D, 13D, 14D);
            var page = new Page { Content = new Border { Height = 500D } };
            page.SetResourceReference(FrameworkElement.StyleProperty, StyleKeys.DynamicScrollPageStyle);
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Padding = contentPadding,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var window = new Window
            {
                Content = navigationView,
                Width = 240D,
                Height = 180D,
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
                _ = navigationView.ApplyTemplate();
                Frame navigationFrame = GetNavigationFrame(navigationView);

                Assert.True(navigationView.ReplaceContent(page));
                DrainDispatcher();
                _ = navigationFrame.ApplyTemplate();
                _ = page.ApplyTemplate();
                DrainDispatcher();

                DynamicScrollViewer viewer = Assert.IsType<DynamicScrollViewer>(
                    FindVisualDescendant<DynamicScrollViewer>(page)
                );
                _ = viewer.ApplyTemplate();
                DrainDispatcher();

                // The page declares that it handles ContentPadding, so the native Frame receives
                // no outer inset. The page-owned viewer fills the navigation viewport and applies
                // that inset to its own content, leaving its scroll bars at the outer edge.
                Assert.Equal(default(Thickness), navigationFrame.Padding);
                Assert.True(Wpf.Zen.Ui.Controls.NavigationView.GetIsContentPaddingHandled(page));
                Assert.Equal(contentPadding, Wpf.Zen.Ui.Controls.NavigationView.GetContentPadding(viewer));
                Assert.Equal(contentPadding, viewer.Padding);
                Assert.Equal(new Point(0D, 0D), viewer.TranslatePoint(default, navigationFrame));

                var updatedPadding = new Thickness(21D, 22D, 23D, 24D);
                navigationView.Padding = updatedPadding;
                DrainDispatcher();

                Assert.Equal(default(Thickness), navigationFrame.Padding);
                Assert.Equal(updatedPadding, Wpf.Zen.Ui.Controls.NavigationView.GetContentPadding(viewer));
                Assert.Equal(updatedPadding, viewer.Padding);

                // The attached inset is host-owned state. Navigating away clears it from the old
                // page while ordinary replacement content resumes native Frame padding.
                var replacement = new Border();
                Assert.True(navigationView.ReplaceContent(replacement));
                DrainDispatcher();

                Assert.Equal(default(Thickness), Wpf.Zen.Ui.Controls.NavigationView.GetContentPadding(page));
                Assert.Equal(updatedPadding, navigationFrame.Padding);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void DynamicScrollPageStyle_IsAvailableThroughDynamicStyleExtension()
    {
        var extension = new DynamicStyleExtension(StyleToken.DynamicScrollPage);

        Assert.Same(StyleKeys.DynamicScrollPageStyle, extension.ResourceKey);
    }

    [Fact]
    public void NavigationFrame_IsolatesJournalWithoutCapturingNavigationCommands()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };

            Assert.True(navigationView.ApplyTemplate());
            var navigationFrame = GetNavigationFrame(navigationView);

            Assert.Equal(
                System.Windows.Navigation.JournalOwnership.OwnsJournal,
                navigationFrame.JournalOwnership
            );
            Assert.Equal(
                System.Windows.Navigation.NavigationUIVisibility.Hidden,
                navigationFrame.NavigationUIVisibility
            );
            Assert.False(navigationFrame.SandboxExternalContent);
            Assert.DoesNotContain(
                navigationFrame.CommandBindings.Cast<System.Windows.Input.CommandBinding>(),
                binding =>
                    binding.Command == System.Windows.Input.NavigationCommands.BrowseBack
                    || binding.Command == System.Windows.Input.NavigationCommands.BrowseForward
                    || binding.Command == System.Windows.Input.NavigationCommands.NavigateJournal
                    || binding.Command == System.Windows.Input.NavigationCommands.Refresh
                    || binding.Command == System.Windows.Input.NavigationCommands.BrowseStop
            );
        });
    }

    [Fact]
    public void NavigationInputSuppression_IsExplicitAndGranular()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new InputTestNavigationView();
            var window = new Window
            {
                Content = navigationView,
                Width = 100D,
                Height = 100D,
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

                Assert.Equal(NavigationViewInput.None, navigationView.SuppressedNavigationInput);
                Assert.False(navigationView.InvokePreviewKey(Key.F5, isDown: true));
                Assert.False(navigationView.InvokePreviewMouse(MouseButton.XButton1, isDown: true));
                Assert.False(navigationView.InvokeMouseDown(MouseButton.XButton1));
                Assert.Equal(0, navigationView.GoBackCallCount);

                navigationView.SuppressedNavigationInput = NavigationViewInput.Refresh;

                Assert.True(navigationView.InvokePreviewKey(Key.F5, isDown: true));
                Assert.True(navigationView.InvokePreviewKey(Key.F5, isDown: false));
                Assert.False(navigationView.InvokePreviewKey(Key.F6, isDown: true));
                Assert.False(navigationView.InvokePreviewMouse(MouseButton.XButton1, isDown: true));

                navigationView.SuppressedNavigationInput = NavigationViewInput.BrowserNavigation;

                Assert.False(navigationView.InvokePreviewKey(Key.F5, isDown: true));
                Assert.True(navigationView.InvokePreviewMouse(MouseButton.XButton1, isDown: true));
                Assert.True(navigationView.InvokePreviewMouse(MouseButton.XButton1, isDown: false));
                Assert.True(navigationView.InvokePreviewMouse(MouseButton.XButton2, isDown: true));
                Assert.True(navigationView.InvokePreviewMouse(MouseButton.XButton2, isDown: false));
                Assert.False(navigationView.InvokePreviewMouse(MouseButton.Middle, isDown: true));
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void NavigationInputSuppression_StopsRoutedNavigationCommandsAtControlBoundary()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            var window = new Window { Content = navigationView };
            var executionCounts = new Dictionary<ICommand, int>();
            RoutedCommand[] commands =
            [
                NavigationCommands.Refresh,
                NavigationCommands.BrowseBack,
                NavigationCommands.BrowseForward,
            ];

            foreach (RoutedCommand command in commands)
            {
                executionCounts.Add(command, 0);
                window.CommandBindings.Add(
                    new CommandBinding(
                        command,
                        (_, _) => executionCounts[command]++,
                        (_, e) => e.CanExecute = true
                    )
                );
                command.Execute(null, navigationView);
            }

            Assert.All(commands, command => Assert.Equal(1, executionCounts[command]));

            navigationView.SuppressedNavigationInput = NavigationViewInput.All;
            foreach (RoutedCommand command in commands)
            {
                command.Execute(null, navigationView);
            }

            Assert.All(commands, command => Assert.Equal(1, executionCounts[command]));
        });
    }

    [Fact]
    public void Navigate_WithNavigationAwareDataContext_EntersAndExitsPages()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var window = new Window { Content = navigationView };
            var firstViewModel = new NavigationAwareViewModel();
            var secondViewModel = new NavigationAwareViewModel();

            try
            {
                window.Show();
                DrainDispatcher();

                Assert.True(
                    navigationView.Navigate(typeof(NavigationAwarePageOne), firstViewModel)
                );
                DrainDispatcher();
                Assert.Equal(1, firstViewModel.NavigationEnteredCount);
                Assert.Equal(0, firstViewModel.NavigationExitedCount);

                Assert.True(
                    navigationView.Navigate(typeof(NavigationAwarePageTwo), secondViewModel)
                );
                DrainDispatcher();
                Assert.Equal(1, firstViewModel.NavigationExitedCount);
                Assert.Equal(1, secondViewModel.NavigationEnteredCount);
                Assert.Equal(0, secondViewModel.NavigationExitedCount);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }

            Assert.Equal(1, secondViewModel.NavigationExitedCount);
        });
    }

    [Fact]
    public void NavigationCommitted_WithLateDataContext_EntersAndExitsViewModel()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var window = new Window { Content = navigationView };
            var firstViewModel = new NavigationAwareViewModel();
            var firstPage = new LateDataContextPage(firstViewModel);
            var secondPage = new Border();

            try
            {
                window.Show();
                DrainDispatcher();

                Assert.True(navigationView.ReplaceContent(firstPage));
                DrainDispatcher();
                Assert.Equal(1, firstViewModel.NavigationEnteredCount);

                Assert.True(navigationView.ReplaceContent(secondPage));
                DrainDispatcher();
                Assert.Equal(1, firstViewModel.NavigationExitedCount);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void RapidNavigation_SerializesNavigationAwareNotifications()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var firstPage = new DeferredNavigationExitPage();
            var secondPage = new NavigationAwarePage();
            var thirdPage = new NavigationAwarePage();

            Assert.True(navigationView.ApplyTemplate());
            Assert.True(navigationView.ReplaceContent(firstPage));
            DrainDispatcher();
            Assert.Equal(1, firstPage.NavigationEnteredCount);

            Assert.True(navigationView.ReplaceContent(secondPage));
            Assert.True(navigationView.ReplaceContent(thirdPage));
            DrainDispatcher();

            Assert.Equal(1, firstPage.NavigationExitedCount);
            Assert.Equal(0, secondPage.NavigationEnteredCount);
            Assert.Equal(0, secondPage.NavigationExitedCount);
            Assert.Equal(0, thirdPage.NavigationEnteredCount);

            firstPage.CompleteNavigationExit();
            DrainDispatcher(TimeSpan.FromMilliseconds(50D));

            // WPF stops the second request before committing it when a third request arrives in
            // the same dispatcher turn. Navigation-aware entry is tied to successful navigation,
            // so superseded content must not receive either lifecycle callback.
            Assert.Equal(0, secondPage.NavigationEnteredCount);
            Assert.Equal(0, secondPage.NavigationExitedCount);
            Assert.Equal(1, thirdPage.NavigationEnteredCount);
        });
    }

    [Fact]
    public void HasItemsProperties_AreReadOnlyAndTrackCollectionsAndSources()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();

            Assert.True(Wpf.Zen.Ui.Controls.NavigationView.HasMenuItemsProperty.ReadOnly);
            Assert.True(Wpf.Zen.Ui.Controls.NavigationView.HasFooterMenuItemsProperty.ReadOnly);
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
            var style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]);
            var leftTemplate = Assert.IsType<ControlTemplate>(resources["LeftNavigationViewTemplate"]);
            var fluentTemplate = Assert.IsType<ControlTemplate>(
                resources["LeftFluentNavigationViewTemplate"]
            );
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
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

            var navigationFrame = GetNavigationFrame(navigationView);
            var page = new Border();
            Assert.True(navigationView.ReplaceContent(page));
            DrainDispatcher();
            Assert.Same(page, navigationFrame.Content);
            Assert.NotNull(leftTemplate.FindName("SearchGrid", navigationView));

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;

            Assert.Same(fluentTemplate, navigationView.Template);
            Assert.True(navigationView.ApplyTemplate());
            Assert.Same(navigationFrame, GetNavigationFrame(navigationView));
            Assert.Same(page, navigationFrame.Content);
            Assert.Null(fluentTemplate.FindName("SearchGrid", navigationView));
            Assert.NotNull(fluentTemplate.FindName("PART_TopSeparator", navigationView));
            Assert.True(double.IsNaN(GetPaneGrid(navigationView).Width));
            Assert.Equal(NavigationViewBackButtonVisible.Visible, navigationView.IsBackButtonVisible);
            Assert.True(navigationView.IsPaneToggleVisible);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;

            Assert.Same(leftTemplate, navigationView.Template);
            Assert.True(navigationView.ApplyTemplate());
            Assert.Same(navigationFrame, GetNavigationFrame(navigationView));
            Assert.Same(page, navigationFrame.Content);
            Assert.NotNull(leftTemplate.FindName("SearchGrid", navigationView));
            Assert.NotNull(leftTemplate.FindName("PART_TopSeparator", navigationView));
            Assert.Equal(navigationView.LeftPaneOpenWidth, GetPaneGrid(navigationView).Width);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;

            Assert.True(navigationView.ApplyTemplate());
            Assert.Same(navigationFrame, GetNavigationFrame(navigationView));
            Assert.Same(page, navigationFrame.Content);
            Assert.True(double.IsNaN(GetPaneGrid(navigationView).Width));
        });
    }

    [Theory]
    [InlineData(true, 300D)]
    [InlineData(false, 48D)]
    public void LeftPane_ColdStartAndModeRoundTrip_RetainTheResolvedWidth(
        bool isPaneOpen,
        double expectedWidth
    )
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
                IsPaneOpen = isPaneOpen,
                LeftPaneOpenWidth = 300D,
                LeftPaneCompactWidth = 48D,
            };

            Assert.True(navigationView.ApplyTemplate());
            Grid paneGrid = GetPaneGrid(navigationView);

            Assert.Equal(expectedWidth, navigationView.LeftPaneWidth);
            Assert.Equal(expectedWidth, paneGrid.Width);
            Assert.False(
                DependencyPropertyHelper.GetValueSource(paneGrid, FrameworkElement.WidthProperty).IsAnimated
            );

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;
            Assert.True(navigationView.ApplyTemplate());
            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
            Assert.True(navigationView.ApplyTemplate());

            paneGrid = GetPaneGrid(navigationView);
            Assert.Equal(expectedWidth, navigationView.LeftPaneWidth);
            Assert.Equal(expectedWidth, paneGrid.Width);
            Assert.False(
                DependencyPropertyHelper.GetValueSource(paneGrid, FrameworkElement.WidthProperty).IsAnimated
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
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
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
                DependencyPropertyHelper.GetValueSource(paneGrid, FrameworkElement.WidthProperty).IsAnimated
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
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
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
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
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
            var navigationFrame = GetNavigationFrame(navigationView);
            var page = new NavigationAwarePage();

            try
            {
                window.Show();
                DrainDispatcher();

                Assert.True(navigationView.ReplaceContent(page));
                DrainDispatcher();
                Assert.Equal(1, page.NavigationEnteredCount);
                Assert.Equal(0, page.NavigationExitedCount);

                navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;
                Assert.True(navigationView.ApplyTemplate());
                DrainDispatcher();

                Assert.Same(navigationFrame, GetNavigationFrame(navigationView));
                Assert.Same(page, navigationFrame.Content);
                Assert.Equal(1, page.NavigationEnteredCount);
                Assert.Equal(0, page.NavigationExitedCount);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }

            Assert.Equal(1, page.NavigationExitedCount);
        });
    }

    private static Frame GetNavigationFrame(Wpf.Zen.Ui.Controls.NavigationView navigationView)
    {
        var host = Assert.IsType<ContentPresenter>(
            navigationView.Template.FindName("PART_NavigationFrameHost", navigationView)
        );

        return Assert.IsType<Frame>(host.Content);
    }

    private static bool HasVisualDescendant<T>(DependencyObject root)
        where T : DependencyObject => FindVisualDescendant<T>(root) is not null;

    private static T? FindVisualDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (
            int childIndex = 0;
            childIndex < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            ++childIndex
        )
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, childIndex);
            if (child is T match)
            {
                return match;
            }

            if (FindVisualDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static Grid GetPaneGrid(Wpf.Zen.Ui.Controls.NavigationView navigationView) =>
        Assert.IsType<Grid>(navigationView.Template.FindName("PaneGrid", navigationView));

    private static Uri CreateResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative)
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

    [Fact]
    public void NavigationItemSource_CanNavigateCollapsedDataBeforeContainerGeneration()
    {
        RunOnStaThread(() =>
        {
            var child = new NavigationItem("Child", typeof(NavigationAwarePageOne));
            var root = new NavigationItem("Root") { Children = new[] { child } };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                MenuItemsSource = new[] { root },
            };

            Assert.True(navigationView.Navigate(typeof(NavigationAwarePageOne)));
            Assert.Same(child, navigationView.SelectedItem);
            Assert.Same(child, Assert.Single(navigationView.BreadcrumbItems));
        });
    }

    [Fact]
    public void NavigationView_BreadcrumbCollaboration_PreservesSourceAndPresentation()
    {
        RunOnStaThread(() =>
        {
            var itemTemplate = new DataTemplate(typeof(NavigationItem));
            var breadcrumbBar = new BreadcrumbBar { ItemTemplate = itemTemplate };
            var item = new NavigationItem("Page", typeof(NavigationAwarePageOne));
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                MenuItemsSource = new[] { item },
                BreadcrumbBar = breadcrumbBar,
            };

            Assert.Same(itemTemplate, breadcrumbBar.ItemTemplate);
            Assert.Same(navigationView.BreadcrumbItems, breadcrumbBar.ItemsSource);

            Assert.True(navigationView.Navigate(typeof(NavigationAwarePageOne)));
            Assert.Same(item, Assert.Single(breadcrumbBar.Items));
        });
    }

    [Fact]
    public void BreadcrumbBar_GeneratedContainer_UsesSourceItemAndPreparedTemplate()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var itemTemplate = new DataTemplate(typeof(NavigationItem));
            var first = new NavigationItem("First");
            var second = new NavigationItem("Second");
            var breadcrumbBar = new BreadcrumbBar
            {
                Style = Assert.IsType<Style>(resources[typeof(BreadcrumbBar)]),
                ItemTemplate = itemTemplate,
                ItemsSource = new[] { first, second },
            };
            var window = new Window
            {
                Content = breadcrumbBar,
                Width = 320,
                Height = 80,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };

            BreadcrumbBarItemClickedEventArgs? clicked = null;
            breadcrumbBar.ItemClicked += (_, args) => clicked = args;

            try
            {
                window.Show();
                breadcrumbBar.UpdateLayout();

                var firstContainer = Assert.IsType<BreadcrumbBarItem>(
                    breadcrumbBar.ItemContainerGenerator.ContainerFromIndex(0)
                );
                var lastContainer = Assert.IsType<BreadcrumbBarItem>(
                    breadcrumbBar.ItemContainerGenerator.ContainerFromIndex(1)
                );

                Assert.Same(first, firstContainer.Content);
                Assert.Same(itemTemplate, firstContainer.ContentTemplate);
                Assert.False(firstContainer.IsLast);
                Assert.True(lastContainer.IsLast);

                firstContainer.RaiseEvent(
                    new RoutedEventArgs(ButtonBase.ClickEvent, firstContainer)
                );

                Assert.NotNull(clicked);
                Assert.Same(first, clicked.Item);
                Assert.Same(firstContainer, clicked.Container);
                Assert.Equal(0, clicked.Index);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ObservableSource_RemovingJournalAndSelectedItems_ReconcilesNavigationState()
    {
        RunOnStaThread(() =>
        {
            var first = new NavigationItem("First", typeof(NavigationAwarePageOne));
            var second = new NavigationItem("Second", typeof(NavigationAwarePageTwo));
            var source = new ObservableCollection<object> { first, second };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView { MenuItemsSource = source };

            Assert.True(navigationView.Navigate(typeof(NavigationAwarePageOne)));
            Assert.True(navigationView.Navigate(typeof(NavigationAwarePageTwo)));
            Assert.True(navigationView.CanGoBack);

            source.Remove(first);

            Assert.False(navigationView.CanGoBack);
            Assert.False(navigationView.GoBack());
            Assert.Same(second, navigationView.SelectedItem);

            source.Remove(second);

            Assert.Null(navigationView.SelectedItem);
            Assert.Empty(navigationView.BreadcrumbItems);
        });
    }

    [Fact]
    public void NavigationItem_AllPropertiesNotification_RebuildsChildrenHierarchy()
    {
        RunOnStaThread(() =>
        {
            var initialChild = new MutableNavigationItem("initial", "Initial")
            {
                TargetPageType = typeof(NavigationAwarePageOne),
            };
            var replacementChild = new MutableNavigationItem("replacement", "Replacement")
            {
                TargetPageType = typeof(NavigationAwarePageTwo),
            };
            var root = new MutableNavigationItem("root", "Root")
            {
                Children = new[] { initialChild },
            };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                MenuItemsSource = new[] { root },
            };

            root.Children = new[] { replacementChild };
            root.NotifyAllPropertiesChanged();

            Assert.False(navigationView.Navigate(initialChild.Id));
            Assert.True(navigationView.Navigate(replacementChild.Id));
            Assert.Same(replacementChild, navigationView.SelectedItem);
        });
    }

    [Fact]
    public void NavigationItemNotifications_UseSourceReferenceIdentity()
    {
        RunOnStaThread(() =>
        {
            var first = new MutableNavigationItem("first", "First", "equal")
            {
                TargetPageType = typeof(NavigationAwarePageOne),
            };
            var second = new MutableNavigationItem("second", "Second", "equal")
            {
                TargetPageType = typeof(NavigationAwarePageTwo),
            };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                MenuItemsSource = new[] { first, second },
            };

            second.TargetPageTag = "updated-second";
            second.NotifyPropertyChanged(nameof(INavigationItem.TargetPageTag));

            Assert.True(navigationView.Navigate("updated-second"));
            Assert.Same(second, navigationView.SelectedItem);
        });
    }

    [Fact]
    public void NavigationItemCacheModeChange_MigratesExistingCachedPage()
    {
        RunOnStaThread(() =>
        {
            CacheProbePage.InstanceCount = 0;
            var cachedItem = new NavigationItem("Cached", typeof(CacheProbePage))
            {
                NavigationCacheMode = NavigationCacheMode.Required,
            };
            var otherItem = new NavigationItem("Other", typeof(NavigationAwarePageTwo));
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                MenuItemsSource = new[] { cachedItem, otherItem },
            };

            Assert.True(navigationView.Navigate(typeof(CacheProbePage)));
            Assert.Equal(1, CacheProbePage.InstanceCount);

            cachedItem.NavigationCacheMode = NavigationCacheMode.Enabled;
            Assert.True(navigationView.Navigate(typeof(NavigationAwarePageTwo)));
            Assert.True(navigationView.Navigate(typeof(CacheProbePage)));

            Assert.Equal(1, CacheProbePage.InstanceCount);
        });
    }

    [Fact]
    public void BreadcrumbCollaboration_ReattachesAfterNavigationViewReload()
    {
        RunOnStaThread(() =>
        {
            var breadcrumbBar = new BreadcrumbBar();
            var navigationView = new BreadcrumbLifecycleNavigationView
            {
                BreadcrumbBar = breadcrumbBar,
                Header = breadcrumbBar,
            };
            var window = new Window
            {
                Content = navigationView,
                Width = 320,
                Height = 120,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                DrainDispatcher();

                window.Content = null;
                DrainDispatcher();
                window.Content = navigationView;
                DrainDispatcher();

                navigationView.ResetBreadcrumbClickCount();
                breadcrumbBar.RaiseEvent(
                    new BreadcrumbBarItemClickedEventArgs(
                        BreadcrumbBar.ItemClickedEvent,
                        breadcrumbBar,
                        new object(),
                        new BreadcrumbBarItem(),
                        0
                    )
                );

                Assert.Equal(1, navigationView.BreadcrumbClickCount);
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

    private sealed class NavigationAwarePage : Border, INavigationAware
    {
        public int NavigationEnteredCount { get; private set; }

        public int NavigationExitedCount { get; private set; }

        public Task OnNavigationEnteredAsync()
        {
            NavigationEnteredCount++;
            return Task.CompletedTask;
        }

        public Task OnNavigationExitedAsync()
        {
            NavigationExitedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NavigationAwareViewModel : INavigationAware
    {
        public int NavigationEnteredCount { get; private set; }

        public int NavigationExitedCount { get; private set; }

        public Task OnNavigationEnteredAsync()
        {
            NavigationEnteredCount++;
            return Task.CompletedTask;
        }

        public Task OnNavigationExitedAsync()
        {
            NavigationExitedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class LateDataContextPage(NavigationAwareViewModel viewModel) : Border
    {
        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);

            if (VisualParent is not null)
            {
                DataContext = viewModel;
            }
        }
    }

    public sealed class NavigationAwarePageOne : Border { }

    public sealed class NavigationAwarePageTwo : Border { }

    public sealed class CacheProbePage : Border
    {
        public CacheProbePage()
        {
            InstanceCount++;
        }

        public static int InstanceCount { get; set; }
    }

    private sealed class MutableNavigationItem(
        string id,
        object? content,
        string? equalityKey = null) : INavigationItem, INotifyPropertyChanged
    {
        private readonly string _equalityKey = equalityKey ?? id;

        public string Id { get; } = id;

        public object? Content { get; set; } = content;

        public object? Icon { get; set; }

        public string? TargetPageTag { get; set; }

        public Type? TargetPageType { get; set; }

        public NavigationCacheMode NavigationCacheMode { get; set; }

        public System.Collections.IEnumerable? Children { get; set; }

        public string? SearchText { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyAllPropertiesChanged() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

        public void NotifyPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public override bool Equals(object? obj) =>
            obj is MutableNavigationItem other && other._equalityKey == _equalityKey;

        public override int GetHashCode() => _equalityKey.GetHashCode();
    }

    private sealed class BreadcrumbLifecycleNavigationView : Wpf.Zen.Ui.Controls.NavigationView
    {
        public int BreadcrumbClickCount { get; private set; }

        public void ResetBreadcrumbClickCount() => BreadcrumbClickCount = 0;

        protected override void BreadcrumbBarOnItemClicked(
            BreadcrumbBar sender,
            BreadcrumbBarItemClickedEventArgs e)
        {
            BreadcrumbClickCount++;
        }
    }

    private sealed class FixedStyleSelector(Style style) : StyleSelector
    {
        public override Style SelectStyle(object item, DependencyObject container) => style;
    }

    private sealed class FixedDataTemplateSelector(DataTemplate template) : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container) => template;
    }

    private sealed class DeferredNavigationExitPage : Border, INavigationAware
    {
        private readonly TaskCompletionSource<object?> _navigationExitedCompletion = new();

        public int NavigationEnteredCount { get; private set; }

        public int NavigationExitedCount { get; private set; }

        public Task OnNavigationEnteredAsync()
        {
            NavigationEnteredCount++;
            return Task.CompletedTask;
        }

        public Task OnNavigationExitedAsync()
        {
            NavigationExitedCount++;
            return _navigationExitedCompletion.Task;
        }

        public void CompleteNavigationExit() => _navigationExitedCompletion.SetResult(null);
    }

    private sealed class InputTestNavigationView : Wpf.Zen.Ui.Controls.NavigationView
    {
        public int GoBackCallCount { get; private set; }

        public override bool GoBack()
        {
            GoBackCallCount++;
            return false;
        }

        public bool InvokePreviewKey(Key key, bool isDown)
        {
            PresentationSource source = Assert.IsAssignableFrom<PresentationSource>(
                PresentationSource.FromVisual(this)
            );
            var eventArgs = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
            {
                RoutedEvent = isDown ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent,
            };

            if (isDown)
            {
                OnPreviewKeyDown(eventArgs);
            }
            else
            {
                OnPreviewKeyUp(eventArgs);
            }

            return eventArgs.Handled;
        }

        public bool InvokePreviewMouse(MouseButton button, bool isDown)
        {
            var eventArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button)
            {
                RoutedEvent = isDown ? Mouse.PreviewMouseDownEvent : Mouse.PreviewMouseUpEvent,
            };

            if (isDown)
            {
                OnPreviewMouseDown(eventArgs);
            }
            else
            {
                OnPreviewMouseUp(eventArgs);
            }

            return eventArgs.Handled;
        }

        public bool InvokeMouseDown(MouseButton button)
        {
            var eventArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button)
            {
                RoutedEvent = Mouse.MouseDownEvent,
            };

            OnMouseDown(eventArgs);
            return eventArgs.Handled;
        }
    }
}
