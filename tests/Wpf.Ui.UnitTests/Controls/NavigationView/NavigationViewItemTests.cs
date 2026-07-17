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

    private static readonly MethodInfo RemoveAccessKeyMarkersMethod = typeof(NavigationViewItem).GetMethod(
        "RemoveAccessKeyMarkers",
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    private static readonly MethodInfo HandleChildMenuKeyMethod = typeof(NavigationViewItem).GetMethod(
        "HandleChildMenuKey",
        BindingFlags.NonPublic | BindingFlags.Instance
    )!;

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

            parent.MenuItems.Add(child);
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

            parent.MenuItems.Remove(child);

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
            outerItem.MenuItems.Add(new NavigationViewItem());
            var innerItem = new NavigationViewItem();
            innerItem.MenuItems.Add(new NavigationViewItem());

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

            parent.MenuItems.Add(child);
            AttachToNavigationView(parent, navigationView);

            Assert.Same(resources["NavigationViewItemLeftTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemLeftTemplate"], child.Template);
            Assert.True(parent.ApplyTemplate());
            object leftTemplateRoot = Assert.IsAssignableFrom<FrameworkElement>(
                parent.Template.FindName("MainBorder", parent)
            );

            navigationView.IsPaneOpen = false;

            Assert.Same(resources["NavigationViewItemLeftTemplate"], parent.Template);
            Assert.Same(leftTemplateRoot, parent.Template.FindName("MainBorder", parent));
            Assert.Same(resources["NavigationViewItemSubmenuTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;

            Assert.Same(resources["NavigationViewItemLeftMinimalTemplate"], parent.Template);
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
    public void InlineExpansionAndPopupState_AreIndependentAcrossModeChanges()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView();
            var parent = new NavigationViewItem();
            parent.MenuItems.Add(new NavigationViewItem());
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
            parent.MenuItems.Add(new NavigationViewItem());
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
            item.MenuItems.Add(new NavigationViewItem());
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
            firstChild.MenuItems.Add(firstGrandchild);
            first.MenuItems.Add(firstChild);
            second.MenuItems.Add(new NavigationViewItem());
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
            child.MenuItems.Add(new NavigationViewItem());
            parent.MenuItems.Add(child);
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
    public void PrimarySurface_CombinesNavigationAndChildMenuActions()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            };
            var navigableParent = new NavigationViewItem(typeof(System.Windows.Controls.Border));
            navigableParent.MenuItems.Add(new NavigationViewItem());
            var groupingParent = new NavigationViewItem();
            groupingParent.MenuItems.Add(new NavigationViewItem());
            navigationView.MenuItems.Add(navigableParent);
            navigationView.MenuItems.Add(groupingParent);
            AttachToNavigationView(navigableParent, navigationView);
            AttachToNavigationView(groupingParent, navigationView);

            InvokeClick(navigableParent);

            Assert.Same(navigableParent, navigationView.SelectedItem);
            Assert.True(navigableParent.IsSubmenuOpen);

            InvokeClick(groupingParent);

            Assert.True(groupingParent.IsSubmenuOpen);

            navigableParent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            groupingParent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void CancelledNavigation_DoesNotSuppressTheChildMenuAction()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            };
            var parent = new NavigationViewItem(typeof(System.Windows.Controls.Border));
            parent.MenuItems.Add(new NavigationViewItem());
            navigationView.MenuItems.Add(parent);
            AttachToNavigationView(parent, navigationView);
            navigationView.Navigating += (_, args) => args.Cancel = true;

            InvokeClick(parent);

            Assert.True(parent.IsSubmenuOpen);
            Assert.Null(navigationView.SelectedItem);
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
            parent.MenuItems.Add(new NavigationViewItem());
            navigationView.MenuItems.Add(parent);
            AttachToNavigationView(parent, navigationView);

            InvokeClick(parent);

            Assert.Same(parent, navigationView.SelectedItem);
            Assert.True(parent.IsExpanded);
            Assert.False(parent.IsSubmenuOpen);

            navigationView.IsPaneOpen = false;
            InvokeClick(parent);

            Assert.True(parent.IsExpanded);
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
            child.MenuItems.Add(new NavigationViewItem());
            parent.MenuItems.Add(child);
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
            parent.MenuItems.Add(new NavigationViewItem());
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

            parent.MenuItems.Clear();

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
            disabledParent.MenuItems.Add(new NavigationViewItem());
            var disabledProvider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
                CreateAutomationPeer(disabledParent)
            );
            var disabledSelectionProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(
                CreateAutomationPeer(disabledParent)
            );

            Assert.Throws<ElementNotEnabledException>(disabledProvider.Expand);
            Assert.Throws<ElementNotEnabledException>(disabledProvider.Collapse);
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
            parent.MenuItems.Add(new NavigationViewItem());
            AttachToNavigationView(parent, navigationView);

            Assert.True(parent.ApplyTemplate());
            var popup = FindSubmenuPopup(parent);

            Assert.Equal(expectedPlacement, popup.Placement);
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
            child.MenuItems.Add(new NavigationViewItem());
            parent.MenuItems.Add(child);
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
    public void LeftTemplate_RemainsConnectedWhilePaneClosesAndThenOpensItsPopup()
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
            parent.MenuItems.Add(child);
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

                ControlTemplate template = parent.Template;
                object mainBorder = Assert.IsAssignableFrom<FrameworkElement>(
                    template.FindName("MainBorder", parent)
                );

                navigationView.IsPaneOpen = false;
                DrainDispatcher();

                Assert.Same(template, parent.Template);
                Assert.Same(mainBorder, parent.Template.FindName("MainBorder", parent));

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

            parent.MenuItems.Add(child);
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
        typeof(ButtonBase)
            .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(item, null);
    }

    private static Popup FindSubmenuPopup(NavigationViewItem item)
    {
        _ = item.ApplyTemplate();
        if (item.Template.FindName("PART_SubMenuPopup", item) is Popup popup)
        {
            return popup;
        }

        var childMenuPresenter = Assert.IsType<ContentPresenter>(
            item.Template.FindName("ChildMenuPresenter", item)
        );
        _ = childMenuPresenter.ApplyTemplate();
        return Assert.IsType<Popup>(
            childMenuPresenter.ContentTemplate.FindName("PART_SubMenuPopup", childMenuPresenter)
        );
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
