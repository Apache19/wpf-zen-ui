// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls.NavigationView;

public class NavigationViewItemTests
{
    private static readonly DependencyProperty NavigationParentProperty =
        typeof(Wpf.Ui.Controls.NavigationView)
            .GetField("NavigationParentProperty", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null) as DependencyProperty
        ?? throw new InvalidOperationException("NavigationParentProperty was not found.");

    private static readonly DependencyProperty PaneDisplayModeContextProperty =
        typeof(NavigationViewItem)
            .GetField("PaneDisplayModeContextProperty", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null) as DependencyProperty
        ?? throw new InvalidOperationException("PaneDisplayModeContextProperty was not found.");

    private static readonly DependencyProperty IsPaneOpenContextProperty =
        typeof(NavigationViewItem)
            .GetField("IsPaneOpenContextProperty", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null) as DependencyProperty
        ?? throw new InvalidOperationException("IsPaneOpenContextProperty was not found.");

    private static readonly MethodInfo RemoveAccessKeyMarkersMethod = typeof(NavigationViewItem).GetMethod(
        "RemoveAccessKeyMarkers",
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    private static readonly MethodInfo HandleChildMenuKeyMethod = typeof(NavigationViewItem).GetMethod(
        "HandleChildMenuKey",
        BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    private static readonly MethodInfo OnExpandButtonClickMethod = typeof(NavigationViewItem).GetMethod(
        "OnExpandButtonClick",
        BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    private static readonly MethodInfo OnNavigationViewItemSubmenuOpenedMethod =
        typeof(Wpf.Ui.Controls.NavigationView).GetMethod(
            "OnNavigationViewItemSubmenuOpened",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;

    private static readonly MethodInfo IsInputWithinOpenSubmenuMethod =
        typeof(NavigationViewItem).GetMethod(
            "IsInputWithinOpenSubmenu",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;

    private static readonly PropertyInfo IsPointerSubmenuDismissalActiveProperty =
        typeof(Wpf.Ui.Controls.NavigationView).GetProperty(
            "IsPointerSubmenuDismissalActive",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;

    [Fact]
    public void ContainerUsesBoundedHeaderAndExpansionInputSurfaces()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var itemStyle = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]);
            var item = new NavigationViewItem { Style = itemStyle };
            item.Items.Add(new NavigationViewItem());

            Assert.True(item.ApplyTemplate());
            var headerButton = Assert.IsAssignableFrom<ButtonBase>(
                item.Template.FindName("PART_HeaderButton", item)
            );
            var expandButton = Assert.IsAssignableFrom<System.Windows.Controls.Primitives.ToggleButton>(
                item.Template.FindName("PART_ExpandButton", item)
            );

            Assert.True(typeof(HeaderedItemsControl).IsAssignableFrom(typeof(NavigationViewItem)));
            Assert.False(typeof(ButtonBase).IsAssignableFrom(typeof(NavigationViewItem)));
            Assert.False(item.Focusable);
            Assert.False(KeyboardNavigation.GetIsTabStop(item));
            Assert.True(headerButton.Focusable);
            Assert.False(expandButton.Focusable);
            Assert.False(KeyboardNavigation.GetIsTabStop(expandButton));
        });
    }

    [Fact]
    public void ButtonSpecificAndMirroredChildApisAreNotPartOfTheNewContract()
    {
        Type itemType = typeof(NavigationViewItem);

        Assert.Null(itemType.GetProperty("MenuItems"));
        Assert.Null(itemType.GetProperty("MenuItemsSource"));
        Assert.Null(itemType.GetProperty("ClickMode"));
        Assert.Null(itemType.GetProperty("Command"));
        Assert.Null(itemType.GetProperty("IsMenuElement"));
        Assert.Null(itemType.GetEvent("Click"));
        Assert.NotNull(itemType.GetProperty(nameof(HeaderedItemsControl.Header)));
        Assert.NotNull(itemType.GetProperty(nameof(ItemsControl.ItemsSource)));
    }

    [Fact]
    public void FocusingVisibleHeaderDoesNotRevealTheExpandedContainerBounds()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var parent = new NavigationViewItem
            {
                Header = "Parent",
                IsExpanded = true,
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]),
            };
            for (int index = 0; index < 8; index++)
            {
                parent.Items.Add(new NavigationViewItem { Header = $"Child {index}" });
            }

            var content = new StackPanel();
            content.Children.Add(new Border { Height = 100 });
            content.Children.Add(parent);
            var scrollViewer = new PassiveScrollViewer
            {
                Width = 240,
                Height = 120,
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            var window = new Window
            {
                Content = scrollViewer,
                Width = 260,
                Height = 140,
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
                scrollViewer.ScrollToVerticalOffset(80);
                scrollViewer.UpdateLayout();
                double offsetBeforeFocus = scrollViewer.VerticalOffset;
                var headerButton = Assert.IsAssignableFrom<ButtonBase>(
                    parent.Template.FindName("PART_HeaderButton", parent)
                );

                headerButton.BringIntoView();
                DrainDispatcher();

                Assert.Equal(offsetBeforeFocus, scrollViewer.VerticalOffset);
                Assert.True(parent.ActualHeight > scrollViewer.ViewportHeight);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void DataItemsReceiveNavigationContainersAtRootAndChildLevels()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var itemTemplate = new DataTemplate();
            var itemContainerStyle = new Style(
                typeof(NavigationViewItem),
                Assert.IsType<Style>(resources[typeof(NavigationViewItem)])
            );
            itemContainerStyle.Setters.Add(new Setter(FrameworkElement.TagProperty, "styled"));
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
                ItemContainerStyle = itemContainerStyle,
                ItemTemplate = itemTemplate,
            };
            navigationView.MenuItems.Add("Root data");
            var window = new Window
            {
                Content = navigationView,
                Width = 400,
                Height = 300,
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

                var rootList = Assert.IsType<NavigationViewList>(
                    navigationView.Template.FindName("PART_MenuItemsList", navigationView)
                );
                var root = Assert.IsType<NavigationViewItem>(
                    rootList.ItemContainerGenerator.ContainerFromIndex(0)
                );
                Assert.Equal("Root data", root.Header);
                Assert.Equal("styled", root.Tag);
                Assert.Same(itemTemplate, root.HeaderTemplate);
                Assert.Equal(navigationView.PaneDisplayMode, root.PaneDisplayMode);

                root.ItemsSource = new[] { "Child data" };
                root.IsExpanded = true;
                DrainDispatcher();

                var child = Assert.IsType<NavigationViewItem>(
                    root.ItemContainerGenerator.ContainerFromIndex(0)
                );
                Assert.Equal("Child data", child.Header);
                Assert.Equal("styled", child.Tag);
                Assert.Same(itemTemplate, child.HeaderTemplate);
                Assert.Same(root, child.NavigationViewItemParent);
                Assert.True(child.IsSubItem);
                Assert.Equal(root.PaneDisplayMode, child.PaneDisplayMode);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void PaneState_IsReadOnlyAndSynchronizesFromHostThroughTheItemTree()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                IsPaneOpen = true,
            };
            var parent = new NavigationViewItem();
            var child = new NavigationViewItem();

            parent.Items.Add(child);
            AttachToNavigationView(parent, navigationView);

            Assert.True(NavigationViewItem.PaneDisplayModeProperty.ReadOnly);
            Assert.True(NavigationViewItem.IsSubItemProperty.ReadOnly);
            Assert.Equal(NavigationViewPaneDisplayMode.Top, parent.PaneDisplayMode);
            Assert.Equal(NavigationViewPaneDisplayMode.Top, child.PaneDisplayMode);
            Assert.True(child.IsSubItem);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Bottom;
            navigationView.IsPaneOpen = false;

            Assert.Equal(NavigationViewPaneDisplayMode.Bottom, parent.PaneDisplayMode);
            Assert.Equal(NavigationViewPaneDisplayMode.Bottom, child.PaneDisplayMode);
            Assert.False(parent.IsPaneOpen);
            Assert.False(child.IsPaneOpen);

            parent.Items.Clear();

            Assert.False(child.IsSubItem);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void NestedNavigationView_PaneEventsDoNotChangeOuterItemPresentation()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationViewStyle = Assert.IsType<Style>(
                resources[typeof(Wpf.Ui.Controls.NavigationView)]
            );
            var outerItem = new NavigationViewItem();
            outerItem.Items.Add(new NavigationViewItem());
            var innerItem = new NavigationViewItem();
            innerItem.Items.Add(new NavigationViewItem());

            var innerNavigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = navigationViewStyle,
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
            };
            innerNavigationView.MenuItems.Add(innerItem);

            var outerNavigationView = new Wpf.Ui.Controls.NavigationView
            {
                Style = navigationViewStyle,
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
                PaneHeader = innerNavigationView,
            };
            outerNavigationView.MenuItems.Add(outerItem);

            var window = new Window
            {
                Content = outerNavigationView,
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

                Assert.True(outerItem.IsPaneOpen);
                Assert.True(innerItem.IsPaneOpen);

                innerNavigationView.IsPaneOpen = false;

                Assert.True(outerNavigationView.IsPaneOpen);
                Assert.True(outerItem.IsPaneOpen);
                Assert.False(innerItem.IsPaneOpen);

                // The outer item must still use inline expansion. A stale pane snapshot would open
                // IsSubmenuOpen here and display the compact-pane Popup instead.
                InvokeClick(outerItem);
                Assert.True(outerItem.IsExpanded);
                Assert.False(outerItem.IsSubmenuOpen);

                outerNavigationView.IsPaneOpen = false;
                Assert.False(outerItem.IsPaneOpen);

                innerNavigationView.IsPaneOpen = true;

                Assert.False(outerNavigationView.IsPaneOpen);
                Assert.False(outerItem.IsPaneOpen);
                Assert.True(innerItem.IsPaneOpen);

                innerNavigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;

                Assert.Equal(NavigationViewPaneDisplayMode.Left, outerItem.PaneDisplayMode);
                Assert.Equal(NavigationViewPaneDisplayMode.Top, innerItem.PaneDisplayMode);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void ItemStyle_SelectsTopLevelAndSubItemTemplatesFromPaneDisplayMode()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var itemStyle = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]);
            var navigationView = new Wpf.Ui.Controls.NavigationView();
            var parent = new NavigationViewItem { Style = itemStyle };
            var child = new NavigationViewItem { Style = itemStyle };

            parent.Items.Add(child);
            AttachToNavigationView(parent, navigationView);

            Assert.Same(resources["NavigationViewItemLeftTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemLeftTemplate"], child.Template);

            navigationView.IsPaneOpen = false;

            Assert.Same(resources["NavigationViewItemLeftCompactTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemSubmenuTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;

            Assert.Same(resources["NavigationViewItemLeftCompactTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemSubmenuTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;

            Assert.Same(resources["NavigationViewItemLeftFluentTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemSubmenuTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;

            Assert.Same(resources["NavigationViewItemTopTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemSubmenuTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Bottom;

            Assert.Same(resources["NavigationViewItemBottomTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemSubmenuTemplate"], child.Template);

            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void TemplatesExposeOneHeaderSurfaceAndASeparateExpansionSurface()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            (string Key, bool HasPopup)[] templates =
            [
                ("NavigationViewItemLeftTemplate", false),
                ("NavigationViewItemLeftCompactTemplate", true),
                ("NavigationViewItemLeftFluentTemplate", true),
                ("NavigationViewItemSubmenuTemplate", true),
                ("NavigationViewItemTopTemplate", true),
                ("NavigationViewItemBottomTemplate", true),
            ];

            foreach ((string templateKey, bool hasPopup) in templates)
            {
                var template = Assert.IsType<ControlTemplate>(resources[templateKey]);
                var item = new NavigationViewItem { Template = template };
                item.Items.Add(new NavigationViewItem());
                Assert.True(item.ApplyTemplate());
                Assert.IsAssignableFrom<ButtonBase>(
                    template.FindName("PART_HeaderButton", item)
                );
                Assert.IsAssignableFrom<System.Windows.Controls.Primitives.ToggleButton>(
                    template.FindName("PART_ExpandButton", item)
                );
                Assert.Equal(hasPopup, template.FindName("PART_SubMenuPopup", item) is Popup);
                Assert.Null(template.FindName("ChildMenuPresenter", item));
            }
        });
    }

    [Fact]
    public void InlineExpansionAndPopupState_AreIndependentAcrossModeChanges()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView();
            var parent = new NavigationViewItem();
            parent.Items.Add(new NavigationViewItem());
            navigationView.MenuItems.Add(parent);
            AttachToNavigationView(parent, navigationView);

            parent.IsExpanded = true;
            Assert.True(parent.IsExpanded);
            Assert.False(parent.IsSubmenuOpen);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;

            Assert.True(parent.IsExpanded);
            Assert.False(parent.IsSubmenuOpen);

            parent.IsSubmenuOpen = true;
            Assert.True(parent.IsSubmenuOpen);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Bottom;

            Assert.True(parent.IsExpanded);
            Assert.False(parent.IsSubmenuOpen);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;

            Assert.True(parent.IsExpanded);
            Assert.False(parent.IsSubmenuOpen);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void ReplacingTheControlTemplate_ClosesTransientPopupState()
    {
        RunOnStaThread(() =>
        {
            var parent = new NavigationViewItem();
            parent.Items.Add(new NavigationViewItem());
            parent.IsSubmenuOpen = true;

            parent.Template = new ControlTemplate(typeof(NavigationViewItem));

            Assert.False(parent.IsSubmenuOpen);
        });
    }

    [Fact]
    public void PaneModeRoundTrip_PreservesNavigationObjectIdentityAndJournal()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView();
            var item = new NavigationViewItem(typeof(Border));
            item.Items.Add(new NavigationViewItem());
            navigationView.MenuItems.Add(item);
            AttachToNavigationView(item, navigationView);

            InvokeClick(item);
            string itemId = item.Id;

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;
            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Bottom;
            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;
            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;

            Assert.Same(item, navigationView.MenuItems[0]);
            Assert.Same(item, navigationView.SelectedItem);
            var navigationStack = Assert.IsAssignableFrom<IEnumerable<INavigationViewItem>>(
                typeof(Wpf.Ui.Controls.NavigationView)
                    .GetProperty("NavigationStack", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(navigationView)
            );
            var journal = Assert.IsAssignableFrom<IEnumerable<string>>(
                typeof(Wpf.Ui.Controls.NavigationView)
                    .GetProperty("Journal", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(navigationView)
            );
            Assert.Same(item, Assert.Single(navigationStack));
            Assert.Equal(itemId, Assert.Single(journal));
            item.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void PopupSubmenus_KeepOneAncestorBranchOpen()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            };
            var first = new NavigationViewItem();
            var firstChild = new NavigationViewItem();
            var firstGrandchild = new NavigationViewItem();
            var second = new NavigationViewItem();
            firstChild.Items.Add(firstGrandchild);
            first.Items.Add(firstChild);
            second.Items.Add(new NavigationViewItem());
            navigationView.MenuItems.Add(first);
            navigationView.MenuItems.Add(second);
            AttachToNavigationView(first, navigationView);
            AttachToNavigationView(second, navigationView);

            first.IsSubmenuOpen = true;
            firstChild.IsSubmenuOpen = true;

            Assert.True(first.IsSubmenuOpen);
            Assert.True(firstChild.IsSubmenuOpen);

            second.IsSubmenuOpen = true;

            Assert.False(first.IsSubmenuOpen);
            Assert.False(firstChild.IsSubmenuOpen);
            Assert.True(second.IsSubmenuOpen);

            first.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            second.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void KeyboardCommands_OpenACascadeAndEscapeClosesTheBranch()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            };
            var parent = new NavigationViewItem();
            var child = new NavigationViewItem();
            child.Items.Add(new NavigationViewItem());
            parent.Items.Add(child);
            navigationView.MenuItems.Add(parent);
            AttachToNavigationView(parent, navigationView);

            Assert.True(HandleChildMenuKey(parent, Key.Down));
            Assert.True(parent.IsSubmenuOpen);

            Assert.True(HandleChildMenuKey(child, Key.Right));
            Assert.True(parent.IsSubmenuOpen);
            Assert.True(child.IsSubmenuOpen);

            Assert.True(HandleChildMenuKey(child, Key.Escape));
            Assert.False(parent.IsSubmenuOpen);
            Assert.False(child.IsSubmenuOpen);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void HeaderNavigationAndExpansionAreSeparateActions()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            };
            var navigableParent = new NavigationViewItem(typeof(System.Windows.Controls.Border));
            navigableParent.Items.Add(new NavigationViewItem());
            var groupingParent = new NavigationViewItem();
            groupingParent.Items.Add(new NavigationViewItem());
            navigationView.MenuItems.Add(navigableParent);
            navigationView.MenuItems.Add(groupingParent);
            AttachToNavigationView(navigableParent, navigationView);
            AttachToNavigationView(groupingParent, navigationView);

            InvokeClick(navigableParent);

            Assert.Same(navigableParent, navigationView.SelectedItem);
            Assert.False(navigableParent.IsSubmenuOpen);

            InvokeExpand(navigableParent);
            Assert.True(navigableParent.IsSubmenuOpen);

            InvokeClick(groupingParent);

            Assert.True(groupingParent.IsSubmenuOpen);

            navigableParent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            groupingParent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void CancelledNavigationDoesNotImplicitlyOpenChildren()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            };
            var parent = new NavigationViewItem(typeof(System.Windows.Controls.Border));
            parent.Items.Add(new NavigationViewItem());
            navigationView.MenuItems.Add(parent);
            AttachToNavigationView(parent, navigationView);
            navigationView.Navigating += (_, args) => args.Cancel = true;

            InvokeClick(parent);

            Assert.False(parent.IsSubmenuOpen);
            Assert.Null(navigationView.SelectedItem);

            InvokeExpand(parent);
            Assert.True(parent.IsSubmenuOpen);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void LeftPrimarySurface_UsesInlineChildrenWhenOpenAndPopupChildrenWhenClosed()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
            };
            var parent = new NavigationViewItem(typeof(System.Windows.Controls.Border));
            parent.Items.Add(new NavigationViewItem());
            navigationView.MenuItems.Add(parent);
            AttachToNavigationView(parent, navigationView);

            InvokeClick(parent);

            Assert.Same(parent, navigationView.SelectedItem);
            Assert.False(parent.IsExpanded);
            Assert.False(parent.IsSubmenuOpen);

            InvokeExpand(parent);
            Assert.True(parent.IsExpanded);
            Assert.False(parent.IsSubmenuOpen);

            navigationView.IsPaneOpen = false;
            InvokeClick(parent);

            Assert.True(parent.IsExpanded);
            Assert.False(parent.IsSubmenuOpen);

            InvokeExpand(parent);
            Assert.True(parent.IsSubmenuOpen);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void ClearingRootItems_ClosesTheEntireDetachedPopupBranch()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            };
            var parent = new NavigationViewItem();
            var child = new NavigationViewItem();
            child.Items.Add(new NavigationViewItem());
            parent.Items.Add(child);
            navigationView.MenuItems.Add(parent);
            AttachToNavigationView(parent, navigationView);

            parent.IsSubmenuOpen = true;
            child.IsSubmenuOpen = true;
            navigationView.MenuItems.Clear();

            Assert.False(parent.IsSubmenuOpen);
            Assert.False(child.IsSubmenuOpen);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void ExpandCollapseAutomation_UsesTheActivePresentationState()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            };
            var parent = new NavigationViewItem();
            parent.Items.Add(new NavigationViewItem());
            navigationView.MenuItems.Add(parent);
            AttachToNavigationView(parent, navigationView);

            var provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(CreateAutomationPeer(parent));
            Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);

            provider.Expand();

            Assert.True(parent.IsSubmenuOpen);
            Assert.False(parent.IsExpanded);
            Assert.Equal(ExpandCollapseState.Expanded, provider.ExpandCollapseState);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;

            Assert.False(parent.IsSubmenuOpen);
            Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);

            provider.Expand();

            Assert.True(parent.IsExpanded);
            Assert.Equal(ExpandCollapseState.Expanded, provider.ExpandCollapseState);

            parent.Items.Clear();

            Assert.False(parent.IsExpanded);
            Assert.Equal(ExpandCollapseState.LeafNode, provider.ExpandCollapseState);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void ExpandCollapseAutomation_UsesWpfLeafAndDisabledErrorSemantics()
    {
        RunOnStaThread(() =>
        {
            var leaf = new NavigationViewItem();
            var leafProvider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
                CreateAutomationPeer(leaf)
            );

            Assert.Equal(ExpandCollapseState.LeafNode, leafProvider.ExpandCollapseState);
            Assert.Throws<InvalidOperationException>(leafProvider.Expand);
            Assert.Null(CreateAutomationPeer(leaf).GetPattern(
                System.Windows.Automation.Peers.PatternInterface.ExpandCollapse
            ));

            var disabledParent = new NavigationViewItem { IsEnabled = false };
            disabledParent.Items.Add(new NavigationViewItem());
            var disabledProvider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
                CreateAutomationPeer(disabledParent)
            );
            var disabledSelectionProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(
                CreateAutomationPeer(disabledParent)
            );
            var disabledPeer = CreateAutomationPeer(disabledParent);

            Assert.Throws<ElementNotEnabledException>(disabledProvider.Expand);
            Assert.Throws<ElementNotEnabledException>(disabledProvider.Collapse);
            Assert.Throws<ElementNotEnabledException>(disabledPeer.SetFocus);
            Assert.Throws<ElementNotEnabledException>(disabledSelectionProvider.Select);
            Assert.Throws<ElementNotEnabledException>(disabledSelectionProvider.AddToSelection);
            Assert.Throws<ElementNotEnabledException>(disabledSelectionProvider.RemoveFromSelection);
        });
    }

    [Theory]
    [InlineData(NavigationViewPaneDisplayMode.Left, false, PlacementMode.Right)]
    [InlineData(NavigationViewPaneDisplayMode.LeftMinimal, true, PlacementMode.Right)]
    [InlineData(NavigationViewPaneDisplayMode.LeftFluent, true, PlacementMode.Right)]
    [InlineData(NavigationViewPaneDisplayMode.Top, true, PlacementMode.Bottom)]
    [InlineData(NavigationViewPaneDisplayMode.Bottom, true, PlacementMode.Top)]
    public void PopupTemplates_UseModeAppropriatePlacement(
        NavigationViewPaneDisplayMode paneDisplayMode,
        bool isPaneOpen,
        PlacementMode expectedPlacement)
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = paneDisplayMode,
                IsPaneOpen = isPaneOpen,
            };
            var parent = new NavigationViewItem
            {
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]),
            };
            parent.Items.Add(new NavigationViewItem());
            AttachToNavigationView(parent, navigationView);

            Assert.True(parent.ApplyTemplate());
            var popup = FindSubmenuPopup(parent);

            Assert.Equal(expectedPlacement, popup.Placement);
            Assert.True(popup.StaysOpen);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void PopupTemplate_CanOpenARecursiveUiElementHierarchy()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
            };
            var parent = new NavigationViewItem();
            var child = new NavigationViewItem();
            child.Items.Add(new NavigationViewItem());
            parent.Items.Add(child);
            navigationView.MenuItems.Add(parent);
            var window = new Window
            {
                Content = navigationView,
                Width = 320,
                Height = 200,
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

                parent.IsSubmenuOpen = true;
                DrainDispatcher();

                var parentPopup = Assert.IsType<Popup>(
                    parent.Template.FindName("PART_SubMenuPopup", parent)
                );
                Assert.True(parentPopup.IsOpen);
                Assert.True(child.IsLoaded);
                Assert.NotNull(child.Template.FindName("PART_SubMenuPopup", child));

                child.IsSubmenuOpen = true;
                DrainDispatcher();

                var childPopup = Assert.IsType<Popup>(
                    child.Template.FindName("PART_SubMenuPopup", child)
                );
                Assert.True(parentPopup.IsOpen);
                Assert.True(childPopup.IsOpen);

                childPopup.IsOpen = false;
                DrainDispatcher();

                Assert.True(parent.IsSubmenuOpen);
                Assert.False(child.IsSubmenuOpen);

                child.IsSubmenuOpen = true;
                DrainDispatcher();
                parentPopup.IsOpen = false;
                DrainDispatcher();

                Assert.False(parent.IsSubmenuOpen);
                Assert.False(child.IsSubmenuOpen);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void OpeningDescendantPopup_OpensItsRequiredAncestorBranch()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = false,
            };
            var parent = new NavigationViewItem();
            var child = new NavigationViewItem();
            child.Items.Add(new NavigationViewItem());
            parent.Items.Add(child);
            navigationView.MenuItems.Add(parent);
            AttachToNavigationView(parent, navigationView);

            child.IsSubmenuOpen = true;

            Assert.True(parent.IsSubmenuOpen);
            Assert.True(child.IsSubmenuOpen);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void LeftCollapsed_NavigableDescendantOpensSecondPopupWithoutOrphaningIt()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = false,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
            };
            var parent = new NavigationViewItem();
            var child = new NavigationViewItem(typeof(Border));
            child.Items.Add(new NavigationViewItem());
            parent.Items.Add(child);
            navigationView.MenuItems.Add(parent);
            var window = new Window
            {
                Content = navigationView,
                Width = 320,
                Height = 200,
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

                InvokeClick(parent);
                DrainDispatcher();
                Popup parentPopup = FindSubmenuPopup(parent);
                Assert.True(parentPopup.IsOpen);

                InvokeClick(child);
                DrainDispatcher();
                Assert.Same(child, navigationView.SelectedItem);
                Assert.False(child.IsSubmenuOpen);

                InvokeExpand(child);
                DrainDispatcher();
                Popup childPopup = FindSubmenuPopup(child);

                Assert.True(parent.IsSubmenuOpen);
                Assert.True(parentPopup.IsOpen);
                Assert.True(child.IsSubmenuOpen);
                Assert.True(childPopup.IsOpen);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void PopupCascade_UsesOneNavigationViewCaptureInsteadOfNestedPopupCaptures()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = false,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
            };
            var parent = new NavigationViewItem();
            var child = new NavigationViewItem();
            child.Items.Add(new NavigationViewItem());
            parent.Items.Add(child);
            navigationView.MenuItems.Add(parent);
            var window = new Window
            {
                Content = navigationView,
                Width = 320,
                Height = 200,
                Left = -10_000,
                Top = -10_000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            bool windowClosed = false;

            try
            {
                window.Show();
                DrainDispatcher();
                parent.IsSubmenuOpen = true;
                DrainDispatcher();
                child.IsSubmenuOpen = true;
                DrainDispatcher();

                Assert.True(parent.IsSubmenuOpen);
                Assert.True(child.IsSubmenuOpen);
                Popup parentPopup = FindSubmenuPopup(parent);
                Popup childPopup = FindSubmenuPopup(child);
                Assert.True(parentPopup.IsOpen);
                Assert.True(childPopup.IsOpen);
                Assert.True(parentPopup.StaysOpen);
                Assert.True(childPopup.StaysOpen);
                Assert.Same(navigationView, Mouse.Captured);

                // Input ownership is resolved from the hit-tested Popup visual, not from pointer
                // coordinates sampled after one of the independent HWNDs has closed.
                Assert.True(IsInputWithinOpenSubmenu(parent, parentPopup.Child));
                Assert.False(IsInputWithinOpenSubmenu(parent, childPopup.Child));
                Assert.True(IsInputWithinOpenSubmenu(child, childPopup.Child));

                // ButtonBase temporarily replaces root capture while an item is pressed. Releasing
                // that child capture must restore the single cascade owner immediately.
                Assert.True(child.CaptureMouse());
                Assert.Same(child, Mouse.Captured);
                child.ReleaseMouseCapture();
                Assert.Same(navigationView, Mouse.Captured);

                parent.IsSubmenuOpen = false;
                DrainDispatcher();

                Assert.False(parent.IsSubmenuOpen);
                Assert.False(child.IsSubmenuOpen);
                Assert.NotSame(navigationView, Mouse.Captured);

                parent.IsSubmenuOpen = true;
                DrainDispatcher();
                Assert.Same(navigationView, Mouse.Captured);

                // An unrelated control taking capture ends menu mode instead of NavigationView
                // stealing capture back and interfering with the new interaction.
                Assert.True(window.CaptureMouse());
                Assert.False(parent.IsSubmenuOpen);
                Assert.Same(window, Mouse.Captured);
                window.ReleaseMouseCapture();

                parent.IsSubmenuOpen = true;
                DrainDispatcher();
                Assert.Same(navigationView, Mouse.Captured);

                window.Close();
                windowClosed = true;
                DrainDispatcher();
                Assert.NotSame(navigationView, Mouse.Captured);
                Assert.False(IsPointerSubmenuDismissalActive(navigationView));
            }
            finally
            {
                if (!windowClosed)
                {
                    window.Close();
                }

                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void PopupBranchArbitration_RetainsClickedOwnerAndClosesEveryDeeperLevel()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            };
            var firstPopupOwner = new NavigationViewItem();
            var secondPopupOwner = new NavigationViewItem();
            var thirdPopupOwner = new NavigationViewItem();
            var fourthPopupOwner = new NavigationViewItem();
            fourthPopupOwner.Items.Add(new NavigationViewItem());
            thirdPopupOwner.Items.Add(fourthPopupOwner);
            secondPopupOwner.Items.Add(thirdPopupOwner);
            firstPopupOwner.Items.Add(secondPopupOwner);
            navigationView.MenuItems.Add(firstPopupOwner);
            AttachToNavigationView(firstPopupOwner, navigationView);

            firstPopupOwner.IsSubmenuOpen = true;
            secondPopupOwner.IsSubmenuOpen = true;
            thirdPopupOwner.IsSubmenuOpen = true;
            fourthPopupOwner.IsSubmenuOpen = true;

            OnNavigationViewItemSubmenuOpenedMethod.Invoke(
                navigationView,
                [firstPopupOwner]
            );

            Assert.True(firstPopupOwner.IsSubmenuOpen);
            Assert.False(secondPopupOwner.IsSubmenuOpen);
            Assert.False(thirdPopupOwner.IsSubmenuOpen);
            Assert.False(fourthPopupOwner.IsSubmenuOpen);
            Assert.False(IsPointerSubmenuDismissalActive(navigationView));
            firstPopupOwner.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void ClosingLeftPaneReplacesInlinePresenterAndRetainsTreeState()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Ui.Controls.NavigationView)]),
            };
            var parent = new NavigationViewItem();
            var child = new NavigationViewItem();
            parent.Items.Add(child);
            navigationView.MenuItems.Add(parent);
            var window = new Window
            {
                Content = navigationView,
                Width = 320,
                Height = 200,
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

                ControlTemplate inlineTemplate = parent.Template;
                object inlineHeader = Assert.IsAssignableFrom<ButtonBase>(
                    inlineTemplate.FindName("PART_HeaderButton", parent)
                );
                parent.IsExpanded = true;

                navigationView.IsPaneOpen = false;
                DrainDispatcher();

                Assert.NotSame(inlineTemplate, parent.Template);
                Assert.NotSame(
                    inlineHeader,
                    parent.Template.FindName("PART_HeaderButton", parent)
                );
                Assert.True(parent.IsExpanded);

                InvokeClick(parent);
                DrainDispatcher();

                Assert.True(parent.IsSubmenuOpen);
                Assert.True(FindSubmenuPopup(parent).IsOpen);
                Assert.True(child.IsLoaded);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void Templates_UseOneInputSurfaceAndPreserveFluentIconState()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var itemStyle = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]);
            var navigationView = new Wpf.Ui.Controls.NavigationView();
            var icon = new SymbolIcon(SymbolRegular.Home24);
            var parent = new NavigationViewItem { Icon = icon, Style = itemStyle };
            var child = new NavigationViewItem { Style = itemStyle };

            parent.Items.Add(child);
            AttachToNavigationView(parent, navigationView);
            child.Activate(navigationView);
            Assert.True(parent.ApplyTemplate());

            Assert.Null(parent.Template.FindName("PART_ChevronButton", parent));
            Assert.True(child.IsActive);
            Assert.Same(
                navigationView,
                typeof(NavigationViewItem)
                    .GetProperty("NavigationViewHost", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(parent)
            );

            parent.Activate(navigationView);
            foreach (
                NavigationViewPaneDisplayMode paneDisplayMode in new[]
                {
                    NavigationViewPaneDisplayMode.LeftMinimal,
                    NavigationViewPaneDisplayMode.Top,
                    NavigationViewPaneDisplayMode.Bottom,
                }
            )
            {
                navigationView.PaneDisplayMode = paneDisplayMode;
                _ = parent.ApplyTemplate();
                Assert.Null(parent.Template.FindName("PART_ChevronButton", parent));
            }

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;
            Assert.True(parent.ApplyTemplate());
            Assert.Null(parent.Template.FindName("PART_ChevronButton", parent));
            Assert.True(icon.Filled);

            parent.Deactivate(navigationView);
            Assert.False(icon.Filled);

            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Theory]
    [InlineData("Date & Time")]
    [InlineData("Dialogs & flyouts")]
    [InlineData("File _ name")]
    [InlineData("Trailing &")]
    [InlineData("Punctuation &!")]
    public void RemoveAccessKeyMarkers_PreservesCharactersThatDoNotPrefixAccessKeys(string value)
    {
        Assert.Equal(value, RemoveAccessKeyMarkers(value));
    }

    [Theory]
    [InlineData("_File", "File")]
    [InlineData("E_xit", "Exit")]
    [InlineData("&Settings", "Settings")]
    [InlineData("文件(_F)", "文件F")]
    [InlineData("Save && Close", "Save & Close")]
    [InlineData("Save __ Close", "Save _ Close")]
    public void RemoveAccessKeyMarkers_RemovesValidMarkersAndUnescapesLiteralMarkers(
        string value,
        string expected
    )
    {
        Assert.Equal(expected, RemoveAccessKeyMarkers(value));
    }

    private static string RemoveAccessKeyMarkers(string value) =>
        (string)RemoveAccessKeyMarkersMethod.Invoke(null, [value])!;

    private static bool HandleChildMenuKey(NavigationViewItem item, Key key) =>
        (bool)HandleChildMenuKeyMethod.Invoke(item, [key])!;

    private static bool IsInputWithinOpenSubmenu(NavigationViewItem item, object source) =>
        (bool)IsInputWithinOpenSubmenuMethod.Invoke(item, [source])!;

    private static bool IsPointerSubmenuDismissalActive(Wpf.Ui.Controls.NavigationView navigationView) =>
        (bool)IsPointerSubmenuDismissalActiveProperty.GetValue(navigationView)!;

    private static Uri CreateResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
    }

    private static void InvokeClick(NavigationViewItem item)
    {
        _ = item.ApplyTemplate();
        var headerButton = Assert.IsAssignableFrom<ButtonBase>(
            item.Template.FindName("PART_HeaderButton", item)
        );
        typeof(ButtonBase)
            .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(headerButton, null);
    }

    private static void InvokeExpand(NavigationViewItem item)
    {
        _ = item.ApplyTemplate();
        var expandButton = Assert.IsAssignableFrom<System.Windows.Controls.Primitives.ToggleButton>(
            item.Template.FindName("PART_ExpandButton", item)
        );
        bool isOpen = item.PaneDisplayMode == NavigationViewPaneDisplayMode.Left && item.IsPaneOpen
            ? item.IsExpanded
            : item.IsSubmenuOpen;
        expandButton.SetCurrentValue(
            System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            !isOpen
        );
        Assert.Equal(!isOpen, expandButton.IsChecked == true);
        OnExpandButtonClickMethod.Invoke(
            item,
            [expandButton, new RoutedEventArgs(ButtonBase.ClickEvent, expandButton)]
        );
    }

    private static Popup FindSubmenuPopup(NavigationViewItem item)
    {
        _ = item.ApplyTemplate();
        if (item.Template.FindName("PART_SubMenuPopup", item) is Popup popup)
        {
            return popup;
        }

        throw new Xunit.Sdk.XunitException("The active item template does not define PART_SubMenuPopup.");
    }

    private static System.Windows.Automation.Peers.AutomationPeer CreateAutomationPeer(
        NavigationViewItem item) =>
        (System.Windows.Automation.Peers.AutomationPeer)typeof(NavigationViewItem)
            .GetMethod("OnCreateAutomationPeer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(item, null)!;

    private static void DrainDispatcher()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false)
        );
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static void AttachToNavigationView(
        NavigationViewItem item,
        Wpf.Ui.Controls.NavigationView navigationView
    )
    {
        item.SetValue(NavigationParentProperty, navigationView);
        BindingOperations.SetBinding(
            item,
            PaneDisplayModeContextProperty,
            new Binding(nameof(Wpf.Ui.Controls.NavigationView.PaneDisplayMode))
            {
                Source = navigationView,
                Mode = BindingMode.OneWay,
            }
        );
        BindingOperations.SetBinding(
            item,
            IsPaneOpenContextProperty,
            new Binding(nameof(Wpf.Ui.Controls.NavigationView.IsPaneOpen))
            {
                Source = navigationView,
                Mode = BindingMode.OneWay,
            }
        );
        item.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
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
}
