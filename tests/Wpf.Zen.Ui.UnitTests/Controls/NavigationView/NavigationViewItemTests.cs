// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

public class NavigationViewItemTests
{
    private static readonly DependencyProperty NavigationParentProperty =
        typeof(Wpf.Zen.Ui.Controls.NavigationView)
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

    private static readonly MethodInfo InvokePrimaryActionMethod = typeof(NavigationViewItem).GetMethod(
        "InvokePrimaryAction",
        BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    private static readonly PropertyInfo SearchableContentProperty = typeof(NavigationViewItem).GetProperty(
        "SearchableContent",
        BindingFlags.NonPublic | BindingFlags.Instance
    )!;

    private static readonly MethodInfo OnHeaderContextMenuOpeningMethod =
        typeof(NavigationViewItem).GetMethod(
            "OnHeaderContextMenuOpening",
            BindingFlags.NonPublic | BindingFlags.Instance
        )!;

    private static readonly ConstructorInfo ContextMenuEventArgsConstructor =
        typeof(ContextMenuEventArgs).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            [typeof(object), typeof(bool), typeof(double), typeof(double)],
            modifiers: null
        )!;

    [Fact]
    public void ContainerUsesOneBoundedHeaderInputSurface()
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

            Assert.True(typeof(HeaderedItemsControl).IsAssignableFrom(typeof(NavigationViewItem)));
            Assert.False(typeof(ButtonBase).IsAssignableFrom(typeof(NavigationViewItem)));
            Assert.False(item.Focusable);
            Assert.False(KeyboardNavigation.GetIsTabStop(item));
            Assert.True(headerButton.Focusable);
            Assert.Null(item.Template.FindName("PART_ExpandButton", item));
        });
    }

    [Fact]
    public void AutomationPeer_RoutesHiddenHeaderButtonFocusToNavigationItem()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var item = new NavigationViewItem
            {
                Header = "Settings",
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]),
            };

            Assert.True(item.ApplyTemplate());
            var headerButton = Assert.IsAssignableFrom<ButtonBase>(
                item.Template.FindName("PART_HeaderButton", item)
            );
            var itemPeer = CreateAutomationPeer(item);

            List<System.Windows.Automation.Peers.AutomationPeer>? children = itemPeer.GetChildren();
            var headerButtonPeer = Assert.IsAssignableFrom<ButtonBaseAutomationPeer>(
                UIElementAutomationPeer.CreatePeerForElement(headerButton)
            );

            Assert.DoesNotContain(children ?? [], peer => ReferenceEquals(peer, headerButtonPeer));
            Assert.Same(itemPeer, headerButtonPeer.EventsSource);
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
    public void ActivationContract_OwnsStateAndExposesPostTransitionHooks()
    {
        RunOnStaThread(() =>
        {
            MethodInfo activateMethod = typeof(NavigationViewItem).GetMethod(nameof(NavigationViewItem.Activate))!;
            MethodInfo deactivateMethod = typeof(NavigationViewItem).GetMethod(nameof(NavigationViewItem.Deactivate))!;

            // Activation remains a container concern, but NavigationView no longer exposes the
            // container through a data interface or uses it as public selection identity.
            Assert.False(activateMethod.IsVirtual);
            Assert.False(deactivateMethod.IsVirtual);
            Assert.True(
                typeof(NavigationViewItem)
                   .GetMethod("OnActivated", BindingFlags.NonPublic | BindingFlags.Instance)!
                   .IsVirtual
            );
            Assert.True(
                typeof(NavigationViewItem)
                   .GetMethod("OnDeactivated", BindingFlags.NonPublic | BindingFlags.Instance)!
                   .IsVirtual
            );

            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            var parent = new NavigationViewItem();
            var item = new ExtensibleNavigationViewItem();
            parent.Items.Add(item);
            AttachToNavigationView(parent, navigationView);

            item.Activate(navigationView);

            Assert.True(item.IsActive);
            Assert.True(parent.IsDescendantActive);
            Assert.Equal((true, true), item.ActivatedState);

            item.Deactivate(navigationView);

            Assert.False(item.IsActive);
            Assert.False(parent.IsDescendantActive);
            Assert.Equal((false, false), item.DeactivatedState);
        });
    }

    [Fact]
    public void InvocationAndExpansionHooks_RunAfterOwnedStateChanges()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
            };
            var item = new ExtensibleNavigationViewItem();
            item.Items.Add(new NavigationViewItem());
            AttachToNavigationView(item, navigationView);

            Assert.True((bool)InvokePrimaryActionMethod.Invoke(item, null)!);
            Assert.Equal((false, true, true), item.ExpandedState);
            Assert.True(item.WasExpandedWhenInvoked);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
            item.IsSubmenuOpen = true;

            Assert.Equal((false, true, true), item.SubmenuState);
        });
    }

    [Fact]
    public void SearchText_OverridesHeaderDerivedSearchContent()
    {
        RunOnStaThread(() =>
        {
            var item = new NavigationViewItem { Header = "_Settings" };

            Assert.Equal("Settings", SearchableContentProperty.GetValue(item));

            item.SearchText = "Preferences";
            item.Header = "Account";
            Assert.Equal("Preferences", SearchableContentProperty.GetValue(item));

            item.SearchText = string.Empty;
            Assert.Equal(string.Empty, SearchableContentProperty.GetValue(item));

            item.ClearValue(NavigationViewItem.SearchTextProperty);
            Assert.Equal("Account", SearchableContentProperty.GetValue(item));
        });
    }

    [Fact]
    public void SemanticHost_ReceivesLookupAndSearchChangesWithoutInheritedHostContext()
    {
        RunOnStaThread(() =>
        {
            var autoSuggestBox = new AutoSuggestBox();
            var navigationView = new InspectableNavigationView
            {
                AutoSuggestBox = autoSuggestBox,
            };
            var parent = new NavigationViewItem();
            var child = new NavigationViewItem
            {
                Header = "Original",
                TargetPageTag = "original-tag",
                TargetPageType = typeof(Border),
            };

            parent.Items.Add(child);
            navigationView.MenuItems.Add(parent);

            Assert.True(navigationView.ContainsPageKey("original-tag"));
            Assert.Contains("Original", autoSuggestBox.OriginalItemsSource.Cast<string>());

            // Simulate a generated or detached descendant whose inherited NavigationParent value
            // is unavailable while its semantic parent still belongs to the navigation view.
            child.SetValue(NavigationParentProperty, null);
            Assert.Null(child.GetValue(NavigationParentProperty));

            child.SearchText = "Updated";
            child.TargetPageTag = "updated-tag";

            Assert.DoesNotContain("Original", autoSuggestBox.OriginalItemsSource.Cast<string>());
            Assert.Contains("Updated", autoSuggestBox.OriginalItemsSource.Cast<string>());
            Assert.False(navigationView.ContainsPageKey("original-tag"));
            Assert.True(navigationView.ContainsPageKey("updated-tag"));
        });
    }

    [Fact]
    public void Constructors_CoverContentNavigationAndHierarchyCombinations()
    {
        RunOnStaThread(() =>
        {
            object[] children = ["First", "Second"];
            var headerOnly = new NavigationViewItem("Header only");

            Assert.Equal("Header only", headerOnly.Header);
            Assert.Equal(
                DependencyProperty.UnsetValue,
                headerOnly.ReadLocalValue(NavigationViewItem.IconProperty)
            );
            Assert.Equal(
                DependencyProperty.UnsetValue,
                headerOnly.ReadLocalValue(NavigationViewItem.TargetPageTypeProperty)
            );
            Assert.Equal(
                DependencyProperty.UnsetValue,
                headerOnly.ReadLocalValue(ItemsControl.ItemsSourceProperty)
            );

            var symbolGroup = new NavigationViewItem(
                "Symbol group",
                SymbolRegular.Folder24,
                itemsSource: children
            );

            Assert.Equal("Symbol group", symbolGroup.Header);
            Assert.Equal(
                SymbolRegular.Folder24,
                Assert.IsType<FluentIconSource>(symbolGroup.Icon).Symbol
            );
            Assert.Null(symbolGroup.TargetPageType);
            Assert.Same(children, symbolGroup.ItemsSource);

            var iconOnly = new NavigationViewItem(SymbolRegular.Heart24, typeof(Border));

            Assert.Null(iconOnly.Header);
            Assert.Equal(
                SymbolRegular.Heart24,
                Assert.IsType<FluentIconSource>(iconOnly.Icon).Symbol
            );
            Assert.Equal(typeof(Border), iconOnly.TargetPageType);

            var header = new System.Windows.Controls.TextBlock { Text = "Custom header" };
            var icon = new System.Windows.Controls.TextBlock { Text = "A" };
            var infoBadge = new InfoBadge();
            var complete = new NavigationViewItem(
                header: header,
                icon: icon,
                targetPageType: typeof(Border),
                itemsSource: children,
                targetPageTag: "custom-tag",
                infoBadge: infoBadge,
                navigationCacheMode: NavigationCacheMode.Permanent
            );

            Assert.Same(header, complete.Header);
            Assert.Same(icon, complete.Icon);
            Assert.Equal(typeof(Border), complete.TargetPageType);
            Assert.Same(children, complete.ItemsSource);
            Assert.Equal("custom-tag", complete.TargetPageTag);
            Assert.Same(infoBadge, complete.InfoBadge);
            Assert.Equal(NavigationCacheMode.Permanent, complete.NavigationCacheMode);
        });
    }

    [Fact]
    public void FocusAndAutomationBoundsRemainOnHeaderWhenBranchChangesExpansion()
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

                var itemPeer = CreateAutomationPeer(parent);
                var headerButtonPeer = Assert.IsAssignableFrom<ButtonBaseAutomationPeer>(
                    UIElementAutomationPeer.CreatePeerForElement(headerButton)
                );

                Assert.Equal(
                    headerButtonPeer.GetBoundingRectangle(),
                    itemPeer.GetBoundingRectangle()
                );

                parent.IsExpanded = false;
                window.UpdateLayout();
                Assert.Equal(
                    headerButtonPeer.GetBoundingRectangle(),
                    itemPeer.GetBoundingRectangle()
                );

                parent.IsExpanded = true;
                window.UpdateLayout();
                Assert.Equal(
                    headerButtonPeer.GetBoundingRectangle(),
                    itemPeer.GetBoundingRectangle()
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
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
                MenuItemContainerStyle = itemContainerStyle,
                MenuItemTemplate = itemTemplate,
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectItemsInheritRegionContainerStyleThroughTheEntireTree(bool useFooter)
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var itemContainerStyle = new Style(
                typeof(NavigationViewItem),
                Assert.IsType<Style>(resources[typeof(NavigationViewItem)])
            );
            itemContainerStyle.Setters.Add(new Setter(FrameworkElement.TagProperty, "root"));
            var subItemTrigger = new Trigger
            {
                Property = NavigationViewItem.IsSubItemProperty,
                Value = true,
            };
            subItemTrigger.Setters.Add(new Setter(FrameworkElement.TagProperty, "subitem"));
            itemContainerStyle.Triggers.Add(subItemTrigger);
            var replacementStyle = new Style(
                typeof(NavigationViewItem),
                Assert.IsType<Style>(resources[typeof(NavigationViewItem)])
            );
            replacementStyle.Setters.Add(
                new Setter(FrameworkElement.TagProperty, "replacement")
            );

            var grandchild = new NavigationViewItem { Header = "Grandchild" };
            var child = new NavigationViewItem { Header = "Child", IsExpanded = true };
            child.Items.Add(grandchild);
            var root = new NavigationViewItem { Header = "Root", IsExpanded = true };
            root.Items.Add(child);

            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };

            if (useFooter)
            {
                navigationView.FooterMenuItemContainerStyle = itemContainerStyle;
                navigationView.FooterMenuItems.Add(root);
            }
            else
            {
                navigationView.MenuItemContainerStyle = itemContainerStyle;
                navigationView.MenuItems.Add(root);
            }

            var window = new Window
            {
                Content = navigationView,
                Width = 400,
                Height = 400,
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

                Assert.Same(itemContainerStyle, root.Style);
                Assert.Same(itemContainerStyle, root.ItemContainerStyle);
                Assert.Equal("root", root.Tag);

                Assert.Same(root, child.NavigationViewItemParent);
                Assert.Same(itemContainerStyle, child.Style);
                Assert.Same(itemContainerStyle, child.ItemContainerStyle);
                Assert.Equal("subitem", child.Tag);

                Assert.Same(child, grandchild.NavigationViewItemParent);
                Assert.Same(itemContainerStyle, grandchild.Style);
                Assert.Same(itemContainerStyle, grandchild.ItemContainerStyle);
                Assert.Equal("subitem", grandchild.Tag);

                if (useFooter)
                {
                    navigationView.FooterMenuItemContainerStyle = replacementStyle;
                }
                else
                {
                    navigationView.MenuItemContainerStyle = replacementStyle;
                }

                DrainDispatcher();

                Assert.Same(replacementStyle, root.Style);
                Assert.Same(replacementStyle, root.ItemContainerStyle);
                Assert.Same(replacementStyle, child.Style);
                Assert.Same(replacementStyle, child.ItemContainerStyle);
                Assert.Same(replacementStyle, grandchild.Style);
                Assert.Equal("replacement", grandchild.Tag);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void DirectItemContainerStyleOverridesTheInheritedStyleForItsBranch()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            Style defaultItemStyle = Assert.IsType<Style>(
                resources[typeof(NavigationViewItem)]
            );
            var menuStyle = new Style(typeof(NavigationViewItem), defaultItemStyle);
            menuStyle.Setters.Add(new Setter(FrameworkElement.TagProperty, "menu"));
            var branchStyle = new Style(typeof(NavigationViewItem), defaultItemStyle);
            branchStyle.Setters.Add(new Setter(FrameworkElement.TagProperty, "branch"));

            var grandchild = new NavigationViewItem { Header = "Grandchild" };
            var child = new NavigationViewItem { Header = "Child", IsExpanded = true };
            child.Items.Add(grandchild);
            var root = new NavigationViewItem
            {
                Header = "Root",
                IsExpanded = true,
                ItemContainerStyle = branchStyle,
            };
            root.Items.Add(child);

            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
                MenuItemContainerStyle = menuStyle,
            };
            navigationView.MenuItems.Add(root);
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

                Assert.Same(menuStyle, root.Style);
                Assert.Equal("menu", root.Tag);
                Assert.Same(branchStyle, root.ItemContainerStyle);

                Assert.Same(branchStyle, child.Style);
                Assert.Equal("branch", child.Tag);
                Assert.Same(branchStyle, child.ItemContainerStyle);

                Assert.Same(branchStyle, grandchild.Style);
                Assert.Equal("branch", grandchild.Tag);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void DirectItemsInheritContainerStyleSelectorInDetachedPresentation()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var selectedStyle = new Style(
                typeof(NavigationViewItem),
                Assert.IsType<Style>(resources[typeof(NavigationViewItem)])
            );
            selectedStyle.Setters.Add(new Setter(FrameworkElement.TagProperty, "selected"));
            var selector = new FixedStyleSelector(selectedStyle);

            var grandchild = new NavigationViewItem { Header = "Grandchild" };
            var child = new NavigationViewItem { Header = "Child" };
            child.Items.Add(grandchild);
            var root = new NavigationViewItem { Header = "Root" };
            root.Items.Add(child);

            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
                MenuItemContainerStyleSelector = selector,
            };
            navigationView.MenuItems.Add(root);
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
                root.IsSubmenuOpen = true;
                DrainDispatcher();

                Assert.Same(selectedStyle, root.Style);
                Assert.Same(selector, root.ItemContainerStyleSelector);
                Assert.Same(selectedStyle, child.Style);
                Assert.Same(selector, child.ItemContainerStyleSelector);
                Assert.Same(selectedStyle, grandchild.Style);
                Assert.Same(selector, grandchild.ItemContainerStyleSelector);
                Assert.Equal("selected", grandchild.Tag);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void NullRegionContainerPresentationDoesNotMaskAnItemStyleSetter()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            Style defaultItemStyle = Assert.IsType<Style>(
                resources[typeof(NavigationViewItem)]
            );
            var childStyle = new Style(typeof(NavigationViewItem), defaultItemStyle);
            childStyle.Setters.Add(new Setter(FrameworkElement.TagProperty, "child"));
            var branchOwnerStyle = new Style(typeof(NavigationViewItem), defaultItemStyle);
            branchOwnerStyle.Setters.Add(
                new Setter(ItemsControl.ItemContainerStyleProperty, childStyle)
            );

            var child = new NavigationViewItem { Header = "Child" };
            var root = new NavigationViewItem { Header = "Root", IsExpanded = true };
            root.Items.Add(child);
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            navigationView.MenuItems.Add(root);
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

                Assert.Null(
                    BindingOperations.GetBindingBase(
                        root,
                        ItemsControl.ItemContainerStyleProperty
                    )
                );

                root.Style = branchOwnerStyle;
                DrainDispatcher();

                Assert.Same(childStyle, root.ItemContainerStyle);
                Assert.Same(childStyle, child.Style);
                Assert.Equal("child", child.Tag);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void ReparentedDirectItemUsesItsNewRegionContainerStyle()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            Style defaultItemStyle = Assert.IsType<Style>(
                resources[typeof(NavigationViewItem)]
            );
            var menuStyle = new Style(typeof(NavigationViewItem), defaultItemStyle);
            menuStyle.Setters.Add(new Setter(FrameworkElement.TagProperty, "menu"));
            var footerStyle = new Style(typeof(NavigationViewItem), defaultItemStyle);
            footerStyle.Setters.Add(new Setter(FrameworkElement.TagProperty, "footer"));

            var root = new NavigationViewItem { Header = "Root" };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
                MenuItemContainerStyle = menuStyle,
                FooterMenuItemContainerStyle = footerStyle,
            };
            navigationView.MenuItems.Add(root);
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

                Assert.Same(menuStyle, root.Style);
                Assert.Same(menuStyle, root.ItemContainerStyle);
                Assert.Equal("menu", root.Tag);

                navigationView.MenuItems.Remove(root);
                navigationView.FooterMenuItems.Add(root);
                DrainDispatcher();

                Assert.Same(footerStyle, root.Style);
                Assert.Same(footerStyle, root.ItemContainerStyle);
                Assert.Equal("footer", root.Tag);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void NavigationItemDefaultProjection_TracksMutableChildrenAndIconData()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var initialChildren = new ObservableCollection<object> { new NavigationItem("Initial") };
            var replacementChildren = new ObservableCollection<object> { new NavigationItem("Replacement") };
            object initialIcon = SymbolRegular.Home24;
            object replacementIcon = SymbolRegular.Settings24;
            var item = new NavigationItem("Root")
            {
                Children = initialChildren,
                Icon = initialIcon,
            };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
                MenuItemsSource = new[] { item },
            };
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
                var container = Assert.IsType<NavigationViewItem>(
                    rootList.ItemContainerGenerator.ContainerFromIndex(0)
                );

                Assert.Same(initialChildren, container.ItemsSource);
                Assert.Same(initialIcon, container.Icon);

                item.Children = replacementChildren;
                item.Icon = replacementIcon;

                Assert.Same(replacementChildren, container.ItemsSource);
                Assert.Same(replacementIcon, container.Icon);

                item.Children = null;
                item.Icon = null;

                Assert.Null(container.ItemsSource);
                Assert.Null(container.Icon);
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
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
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
            Assert.True(NavigationViewItem.IsDescendantActiveProperty.ReadOnly);
            Assert.True(NavigationViewItem.IsBranchActiveProperty.ReadOnly);
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
    public void ActiveDescendantState_PropagatesThroughAncestorsAndTracksDetachment()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
            };
            var root = new NavigationViewItem();
            var child = new NavigationViewItem();
            var grandchild = new NavigationViewItem();

            child.Items.Add(grandchild);
            root.Items.Add(child);
            AttachToNavigationView(root, navigationView);

            grandchild.Activate(navigationView);

            Assert.True(grandchild.IsActive);
            Assert.False(grandchild.IsDescendantActive);
            Assert.True(grandchild.IsBranchActive);
            Assert.True(child.IsDescendantActive);
            Assert.True(child.IsBranchActive);
            Assert.True(root.IsDescendantActive);
            Assert.True(root.IsBranchActive);

            // Branch state stays active when both source states are true and one is cleared.
            child.Activate(navigationView);
            Assert.True(child.IsActive);
            Assert.True(child.IsDescendantActive);
            Assert.True(child.IsBranchActive);
            child.Deactivate(navigationView);
            Assert.False(child.IsActive);
            Assert.True(child.IsDescendantActive);
            Assert.True(child.IsBranchActive);

            child.Items.Remove(grandchild);

            Assert.False(child.IsDescendantActive);
            Assert.False(child.IsBranchActive);
            Assert.False(root.IsDescendantActive);
            Assert.False(root.IsBranchActive);

            child.Items.Add(grandchild);

            Assert.True(child.IsDescendantActive);
            Assert.True(child.IsBranchActive);
            Assert.True(root.IsDescendantActive);
            Assert.True(root.IsBranchActive);

            grandchild.Deactivate(navigationView);

            Assert.False(child.IsDescendantActive);
            Assert.False(child.IsBranchActive);
            Assert.False(root.IsDescendantActive);
            Assert.False(root.IsBranchActive);
            root.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Theory]
    [InlineData(NavigationViewPaneDisplayMode.Left, false)]
    [InlineData(NavigationViewPaneDisplayMode.LeftMinimal, true)]
    [InlineData(NavigationViewPaneDisplayMode.LeftFluent, true)]
    [InlineData(NavigationViewPaneDisplayMode.Top, true)]
    [InlineData(NavigationViewPaneDisplayMode.Bottom, true)]
    public void DetachedPresentations_DoNotActivateAncestors(
        NavigationViewPaneDisplayMode paneDisplayMode,
        bool isPaneOpen
    )
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = paneDisplayMode,
                IsPaneOpen = isPaneOpen,
            };
            var root = new NavigationViewItem();
            var child = new NavigationViewItem();
            var grandchild = new NavigationViewItem();
            child.Items.Add(grandchild);
            root.Items.Add(child);
            AttachToNavigationView(root, navigationView);

            grandchild.Activate(navigationView);

            Assert.True(grandchild.IsActive);
            Assert.False(child.IsActive);
            Assert.False(root.IsActive);
            Assert.True(child.IsDescendantActive);
            Assert.True(root.IsDescendantActive);

            grandchild.Deactivate(navigationView);

            Assert.False(grandchild.IsActive);
            Assert.False(child.IsDescendantActive);
            Assert.False(root.IsDescendantActive);
            root.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void LeftTemplate_ShowsActiveIndicatorOnlyWhileActiveDescendantIsCollapsed()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
            };
            var parent = new NavigationViewItem
            {
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]),
            };
            var child = new NavigationViewItem();
            parent.Items.Add(child);
            AttachToNavigationView(parent, navigationView);
            var window = new Window
            {
                Content = parent,
                Width = 240,
                Height = 160,
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
                child.Activate(navigationView);
                parent.IsExpanded = false;
                DrainDispatcher();
                var headerButton = Assert.IsType<System.Windows.Controls.Button>(
                    parent.Template.FindName("PART_HeaderButton", parent)
                );
                var activeIndicator = Assert.IsType<System.Windows.Shapes.Rectangle>(
                    headerButton.Template.FindName("ActiveIndicator", headerButton)
                );

                Assert.Equal(1d, activeIndicator.Opacity);

                parent.IsExpanded = true;

                Assert.Equal(0d, activeIndicator.Opacity);

                // An expanded branch still surfaces its own active state. IsBranchActive remains
                // true throughout, while IsActive determines whether the parent row owns the
                // indicator or merely contains the visible active descendant.
                parent.Activate(navigationView);

                Assert.True(parent.IsActive);
                Assert.True(parent.IsBranchActive);
                Assert.Equal(1d, activeIndicator.Opacity);

                parent.Deactivate(navigationView);

                Assert.False(parent.IsActive);
                Assert.True(parent.IsBranchActive);
                Assert.Equal(0d, activeIndicator.Opacity);

                // Compact Left always hides descendants. Its indicator must not depend on the
                // persistent inline expansion value retained for when the pane reopens.
                navigationView.IsPaneOpen = false;
                DrainDispatcher();
                headerButton = Assert.IsType<System.Windows.Controls.Button>(
                    parent.Template.FindName("PART_HeaderButton", parent)
                );
                activeIndicator = Assert.IsType<System.Windows.Shapes.Rectangle>(
                    headerButton.Template.FindName("ActiveIndicator", headerButton)
                );
                Assert.False(parent.IsActive);
                Assert.Equal(1d, activeIndicator.Opacity);

                navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;
                DrainDispatcher();
                headerButton = Assert.IsType<System.Windows.Controls.Button>(
                    parent.Template.FindName("PART_HeaderButton", parent)
                );
                activeIndicator = Assert.IsType<System.Windows.Shapes.Rectangle>(
                    headerButton.Template.FindName("ActiveIndicator", headerButton)
                );
                Assert.False(parent.IsActive);
                Assert.Equal(1d, activeIndicator.Opacity);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void NestedNavigationView_PaneEventsDoNotChangeOuterItemPresentation()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationViewStyle = Assert.IsType<Style>(
                resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]
            );
            var outerItem = new NavigationViewItem();
            outerItem.Items.Add(new NavigationViewItem());
            var innerItem = new NavigationViewItem();
            innerItem.Items.Add(new NavigationViewItem());

            var innerNavigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Style = navigationViewStyle,
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
            };
            innerNavigationView.MenuItems.Add(innerItem);

            var outerNavigationView = new Wpf.Zen.Ui.Controls.NavigationView
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
                // IsSubmenuOpen here and display the compact-pane ContextMenu instead.
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
    public void ItemStyle_SelectsOneSourceTemplatePerPaneDisplayMode()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var itemStyle = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]);
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            var parent = new NavigationViewItem { Style = itemStyle };
            var child = new NavigationViewItem { Style = itemStyle };

            parent.Items.Add(child);
            AttachToNavigationView(parent, navigationView);

            Assert.Same(resources["NavigationViewItemLeftTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemLeftTemplate"], child.Template);

            navigationView.IsPaneOpen = false;

            Assert.Same(resources["NavigationViewItemLeftCompactTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemLeftCompactTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;

            Assert.Same(resources["NavigationViewItemLeftCompactTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemLeftCompactTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;

            Assert.Same(resources["NavigationViewItemLeftFluentTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemLeftFluentTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;

            Assert.Same(resources["NavigationViewItemTopTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemTopTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Bottom;

            Assert.Same(resources["NavigationViewItemBottomTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemBottomTemplate"], child.Template);

            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void TemplatesExposeOnlyOneHeaderInputSurface()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            (string Key, bool HasContextMenu)[] templates =
            [
                ("NavigationViewItemLeftTemplate", false),
                ("NavigationViewItemLeftCompactTemplate", true),
                ("NavigationViewItemLeftFluentTemplate", true),
                ("NavigationViewItemTopTemplate", true),
                ("NavigationViewItemBottomTemplate", true),
            ];

            foreach ((string templateKey, bool hasContextMenu) in templates)
            {
                var template = Assert.IsType<ControlTemplate>(resources[templateKey]);
                var item = new NavigationViewItem { Template = template };
                item.Items.Add(new NavigationViewItem());
                Assert.True(item.ApplyTemplate());
                Assert.IsAssignableFrom<ButtonBase>(
                    template.FindName("PART_HeaderButton", item)
                );
                Assert.Null(template.FindName("PART_ExpandButton", item));
                Assert.Equal(
                    hasContextMenu,
                    template.FindName("PART_SubMenuContextMenu", item) is ContextMenu
                );
                Assert.Null(template.FindName("PART_SubMenuPopup", item));
                Assert.Null(template.FindName("ChildMenuPresenter", item));
            }
        });
    }

    [Fact]
    public void LeftInlineExpansion_AnimatesChildrenAndChevronFromTheSingleHeaderSurface()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
            };
            var parent = new NavigationViewItem
            {
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]),
            };
            parent.Items.Add(new NavigationViewItem());
            AttachToNavigationView(parent, navigationView);

            var window = new Window
            {
                Width = 320,
                Height = 240,
                Content = parent,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            try
            {
                window.Show();
                DrainDispatcher();

                _ = parent.ApplyTemplate();
                var headerButton = Assert.IsAssignableFrom<ButtonBase>(
                    parent.Template.FindName("PART_HeaderButton", parent)
                );
                _ = headerButton.ApplyTemplate();
                var chevron = Assert.IsAssignableFrom<FluentIcon>(
                    headerButton.Template.FindName("ChevronIcon", headerButton)
                );
                var itemsPresenter = Assert.IsAssignableFrom<ItemsPresenter>(
                    parent.Template.FindName("PART_ItemsPresenter", parent)
                );

                Assert.Equal(Visibility.Collapsed, itemsPresenter.Visibility);
                Assert.Equal(Visibility.Visible, chevron.Visibility);

                InvokeClick(parent);

                Assert.True(parent.IsExpanded);
                Assert.Equal(Visibility.Visible, itemsPresenter.Visibility);
                Assert.True(itemsPresenter.HasAnimatedProperties);
                Assert.True(Assert.IsType<RotateTransform>(chevron.RenderTransform).HasAnimatedProperties);
                parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void InlineExpansionAndContextMenuState_AreIndependentAcrossModeChanges()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
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
    public void ReplacingTheControlTemplate_ClosesTransientContextMenuState()
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
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            var item = new NavigationViewItem(typeof(Border)) { Header = "Root" };
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
            // Semantic state exposes only data segments and cannot acquire the declarative menu
            // control's Visual parent. The journal independently retains the current occurrence.
            BreadcrumbSegment breadcrumb = Assert.Single(navigationView.Breadcrumbs);
            Assert.Equal("Root", breadcrumb.Content);
            Assert.Equal(itemId, breadcrumb.Target?.RouteKey);
            Assert.False(navigationView.CanGoBack);
            item.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void NativeContextMenus_KeepOneRootBranchOpen()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
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
    public void SourceKeyboardModel_OpensOnlyTheNativeRootMenu()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
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

            // Projected MenuItems, not the source child controls, own all cascading key handling.
            Assert.False(HandleChildMenuKey(child, Key.Right));
            Assert.True(parent.IsSubmenuOpen);
            Assert.False(child.IsSubmenuOpen);

            Assert.True(HandleChildMenuKey(parent, Key.Escape));
            Assert.False(parent.IsSubmenuOpen);
            Assert.False(child.IsSubmenuOpen);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void NavigationItemSurfaceCombinesNavigationAndChildMenuActions()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var navigableParent = new NavigationViewItem(typeof(System.Windows.Controls.Border));
            navigableParent.Items.Add(new NavigationViewItem());
            var groupingParent = new NavigationViewItem();
            groupingParent.Items.Add(new NavigationViewItem());
            navigationView.MenuItems.Add(navigableParent);
            navigationView.MenuItems.Add(groupingParent);
            int invokedCount = 0;
            navigationView.ItemInvoked += (_, _) => invokedCount++;
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
                // A ContextMenu with a real PlacementTarget requires both controls to share a
                // PresentationSource. A manually raised Loaded event cannot model native Popup
                // placement and causes WPF to close the otherwise correctly anchored menu.
                window.Show();
                DrainDispatcher();

                InvokeClick(navigableParent);

                Assert.Same(navigableParent, navigationView.SelectedItem);
                Assert.True(navigableParent.IsSubmenuOpen);
                Assert.Equal(1, invokedCount);

                InvokeClick(groupingParent);

                Assert.True(groupingParent.IsSubmenuOpen);
                Assert.Equal(1, invokedCount);

                // Re-entering through the same header surface closes the competing branch,
                // navigates, and opens this item's child presentation as one deterministic action.
                InvokeClick(navigableParent);

                Assert.Same(navigableParent, navigationView.SelectedItem);
                Assert.True(navigableParent.IsSubmenuOpen);
                Assert.False(groupingParent.IsSubmenuOpen);
                Assert.Equal(2, invokedCount);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void CancelledNavigationDoesNotSuppressTheChildMenuAction()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            };
            var parent = new NavigationViewItem(typeof(System.Windows.Controls.Border));
            parent.Items.Add(new NavigationViewItem());
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
    public void LeftPrimarySurface_UsesInlineChildrenWhenOpenAndContextMenuWhenClosed()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
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
    public void ClearingRootItems_ClosesTheEntireDetachedSubmenuBranch()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
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
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
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
    public void ContextMenuTemplates_UseModeAppropriatePlacementAndSharedFlyoutBehavior(
        NavigationViewPaneDisplayMode paneDisplayMode,
        bool isPaneOpen,
        PlacementMode expectedPlacement)
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
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
            var contextMenu = FindSubmenuContextMenu(parent);
            var headerButton = Assert.IsAssignableFrom<ButtonBase>(
                parent.Template.FindName("PART_HeaderButton", parent)
            );

            Assert.Equal(expectedPlacement, contextMenu.Placement);
            Assert.Same(headerButton, contextMenu.PlacementTarget);
            Assert.True(
                BindingOperations.IsDataBound(contextMenu, ContextMenu.IsOpenProperty)
            );
            Assert.True(contextMenu.Focusable);
            Assert.Same(parent.SubmenuContextMenuStyle, contextMenu.Style);
            Assert.NotNull(parent.SubmenuMenuItemStyle);
            Assert.False(PopupSlideAnimator.GetIsEnabled(contextMenu));
            Assert.Equal(PopupAnimation.Fade, contextMenu.GetValue(Popup.PopupAnimationProperty));
            Assert.True(PopupShadow.GetIsEnabled(contextMenu));
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void ContextMenuGesture_RestoresTemplatePlacementBeforeTheNextHeaderClick()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal,
                IsPaneOpen = true,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var parent = new NavigationViewItem { Header = "Parent" };
            parent.Items.Add(new NavigationViewItem { Header = "Child" });
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

                ContextMenu contextMenu = FindSubmenuContextMenu(parent);
                var headerButton = Assert.IsAssignableFrom<ButtonBase>(
                    parent.Template.FindName("PART_HeaderButton", parent)
                );
                parent.Tag = PlacementMode.Right;
                BindingOperations.SetBinding(
                    contextMenu,
                    ContextMenu.PlacementProperty,
                    new Binding(nameof(FrameworkElement.Tag)) { Source = parent }
                );
                var openingArgs = (ContextMenuEventArgs)ContextMenuEventArgsConstructor.Invoke(
                    [headerButton, true, 10d, 10d]
                );

                OnHeaderContextMenuOpeningMethod.Invoke(parent, [headerButton, openingArgs]);
                DrainDispatcher();

                Assert.True(openingArgs.Handled);
                Assert.True(contextMenu.IsOpen);
                Assert.Equal(PlacementMode.MousePoint, contextMenu.Placement);
                Assert.True(
                    BindingOperations.IsDataBound(contextMenu, ContextMenu.PlacementProperty)
                );

                contextMenu.IsOpen = false;
                DrainDispatcher(TimeSpan.FromMilliseconds(200));

                Assert.Equal(PlacementMode.Right, contextMenu.Placement);
                Assert.True(
                    BindingOperations.IsDataBound(contextMenu, ContextMenu.PlacementProperty)
                );

                InvokeClick(parent);
                DrainDispatcher();

                Assert.True(contextMenu.IsOpen);
                Assert.Equal(PlacementMode.Right, contextMenu.Placement);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void LeafItem_RejectsAnEmptyContextMenuOpenRequest()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var leaf = new NavigationViewItem();
            navigationView.MenuItems.Add(leaf);
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

                ContextMenu contextMenu = FindSubmenuContextMenu(leaf);
                contextMenu.IsOpen = true;
                DrainDispatcher();

                Assert.False(leaf.IsSubmenuOpen);
                Assert.False(contextMenu.IsOpen);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void ContextMenuRoot_ProjectsACompleteNativeMenuItemHierarchy()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var parent = new NavigationViewItem { Header = "Parent" };
            var child = new NavigationViewItem { Header = "Child" };
            child.Icon = SymbolRegular.Home24;
            var grandchild = new NavigationViewItem { Header = "Grandchild" };
            child.Items.Add(grandchild);
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

                // A descendant can already have a live control template after it was previously
                // presented inline. Its template-owned ContextMenu must not compete with the
                // projected MenuItem submenu when native state is mirrored back to the source.
                _ = child.ApplyTemplate();
                ContextMenu childSourceContextMenu = FindSubmenuContextMenu(child);
                Assert.False(
                    BindingOperations.IsDataBound(
                        childSourceContextMenu,
                        ContextMenu.IsOpenProperty
                    )
                );

                parent.IsSubmenuOpen = true;
                DrainDispatcher();

                ContextMenu parentContextMenu = FindSubmenuContextMenu(parent);
                Assert.True(parentContextMenu.IsOpen);
                var childMenuItem = Assert.IsType<MenuItem>(Assert.Single(parentContextMenu.Items));
                Assert.Same(child, childMenuItem.DataContext);
                Assert.Equal("Child", childMenuItem.Header);
                Assert.Same(child.SubmenuMenuItemStyle, childMenuItem.Style);
                Assert.False(
                    BindingOperations.IsDataBound(
                        childMenuItem,
                        MenuItem.IsSubmenuOpenProperty
                    )
                );
                Assert.Equal(SymbolRegular.Home24, Assert.IsType<SymbolRegular>(childMenuItem.Icon));

                var grandchildMenuItem = Assert.IsType<MenuItem>(Assert.Single(childMenuItem.Items));
                Assert.Same(grandchild, grandchildMenuItem.DataContext);
                Assert.Equal("Grandchild", grandchildMenuItem.Header);
                Assert.Null(grandchildMenuItem.ItemsSource);
                Assert.NotNull(grandchildMenuItem.Command);
                Assert.False(ContainsNavigationViewItem(parentContextMenu));

                var childMenuItemPeer = new System.Windows.Automation.Peers.MenuItemAutomationPeer(
                    childMenuItem
                );
                Assert.IsAssignableFrom<IExpandCollapseProvider>(
                    childMenuItemPeer.GetPattern(
                        System.Windows.Automation.Peers.PatternInterface.ExpandCollapse
                    )
                ).Expand();
                DrainDispatcher();

                Assert.True(parentContextMenu.IsOpen);
                Assert.True(childMenuItem.IsSubmenuOpen);
                Assert.True(child.IsSubmenuOpen);
                _ = childMenuItem.ApplyTemplate();
                var childPopup = Assert.IsType<Popup>(
                    childMenuItem.Template.FindName("Popup", childMenuItem)
                );
                Assert.True(childPopup.IsOpen);
                Assert.False(PopupSlideAnimator.GetIsEnabled(childPopup));
                Assert.Equal(PopupAnimation.Fade, childPopup.PopupAnimation);
                Assert.NotNull(childPopup.PlacementTarget);
                var childPopupSource = Assert.IsType<System.Windows.Interop.HwndSource>(
                    PresentationSource.FromVisual(childPopup.Child)
                );
                var childPopupRoot = Assert.IsAssignableFrom<UIElement>(childPopupSource.RootVisual);
                Assert.True(childPopupRoot.IsVisible);
                Assert.True(childPopupRoot.Opacity > 0D);
                Assert.True(childPopup.Child.RenderSize.Width > 0D);
                Assert.True(childPopup.Child.RenderSize.Height > 0D);
                Assert.False(childSourceContextMenu.IsOpen);

                childMenuItem.IsSubmenuOpen = false;
                DrainDispatcher();

                Assert.True(parent.IsSubmenuOpen);
                Assert.False(child.IsSubmenuOpen);

                childMenuItem.IsSubmenuOpen = true;
                DrainDispatcher();
                parentContextMenu.IsOpen = false;
                DrainDispatcher(TimeSpan.FromMilliseconds(200));

                Assert.False(parent.IsSubmenuOpen);
                Assert.False(child.IsSubmenuOpen);
                Assert.Empty(parentContextMenu.Items);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void ContextMenuProjection_UsesPreparedNavigationContainersForDataItems()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var generatedItemStyle = new Style(
                typeof(NavigationViewItem),
                Assert.IsType<Style>(resources[typeof(NavigationViewItem)])
            );
            generatedItemStyle.Setters.Add(
                new Setter(NavigationViewItem.TargetPageTypeProperty, typeof(Border))
            );
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var parent = new NavigationViewItem
            {
                Header = "Parent",
                ItemContainerStyle = generatedItemStyle,
                ItemsSource = new[] { "Data leaf" },
            };
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

                ContextMenu contextMenu = FindSubmenuContextMenu(parent);
                var projectedItem = Assert.IsType<MenuItem>(Assert.Single(contextMenu.Items));
                var generatedSource = Assert.IsType<NavigationViewItem>(projectedItem.DataContext);

                Assert.Same(
                    generatedSource,
                    parent.ItemContainerGenerator.ContainerFromIndex(0)
                );
                Assert.Equal("Data leaf", generatedSource.Header);
                Assert.Equal("Data leaf", projectedItem.Header);
                Assert.Equal(typeof(Border), generatedSource.TargetPageType);

                Assert.NotNull(projectedItem.Command);
                projectedItem.Command.Execute(projectedItem.CommandParameter);
                Assert.Equal("Data leaf", navigationView.SelectedItem);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void ContextMenuProjection_DoesNotReparentAUiElementHeader()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
            };
            var visualHeader = new System.Windows.Controls.TextBlock { Text = "Visual header" };
            var child = new NavigationViewItem { Header = visualHeader };
            var parent = new NavigationViewItem
            {
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]),
            };
            parent.Items.Add(child);
            navigationView.MenuItems.Add(parent);
            AttachToNavigationView(parent, navigationView);

            parent.IsSubmenuOpen = true;

            var projectedItem = Assert.IsType<MenuItem>(
                Assert.Single(FindSubmenuContextMenu(parent).Items)
            );
            Assert.Equal("Visual header", projectedItem.Header);
            Assert.NotSame(visualHeader, projectedItem.Header);
            Assert.Same(visualHeader, child.Header);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void OpenContextMenu_ReprojectsCollectionChangesAndKeepsPropertyBindingsLive()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var first = new NavigationViewItem { Header = "First" };
            var second = new NavigationViewItem { Header = "Second" };
            var parent = new NavigationViewItem { Header = "Parent" };
            var customContextMenuStyle = new Style(
                typeof(ContextMenu),
                Assert.IsType<Style>(resources["NavigationViewItemSubmenuContextMenuStyle"])
            );
            var customMenuItemStyle = new Style(
                typeof(MenuItem),
                Assert.IsType<Style>(resources["NavigationViewItemSubmenuMenuItemStyle"])
            );
            parent.SubmenuContextMenuStyle = customContextMenuStyle;
            parent.SubmenuMenuItemStyle = customMenuItemStyle;
            parent.Items.Add(first);
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

                ContextMenu contextMenu = FindSubmenuContextMenu(parent);
                Assert.Same(customContextMenuStyle, contextMenu.Style);
                var firstMenuItem = Assert.IsType<MenuItem>(Assert.Single(contextMenu.Items));
                Assert.Same(customMenuItemStyle, firstMenuItem.Style);

                first.Header = "Renamed";
                Assert.Equal("Renamed", firstMenuItem.Header);

                parent.Items.Add(second);
                DrainDispatcher();
                Assert.Equal(2, contextMenu.Items.Count);
                Assert.Same(
                    second,
                    Assert.IsType<MenuItem>(contextMenu.Items[1]).DataContext
                );

                parent.Items.Remove(first);
                DrainDispatcher();
                Assert.Same(
                    second,
                    Assert.IsType<MenuItem>(Assert.Single(contextMenu.Items)).DataContext
                );

                contextMenu.IsOpen = false;
                DrainDispatcher(TimeSpan.FromMilliseconds(200));
                Assert.Empty(contextMenu.Items);

                parent.IsSubmenuOpen = true;
                DrainDispatcher();
                Assert.Same(
                    second,
                    Assert.IsType<MenuItem>(Assert.Single(contextMenu.Items)).DataContext
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
    public void OpeningDescendantMenuState_OpensItsRequiredRootContextMenu()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
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
    public void NativeSubmenuHeader_ExpandsWithoutInvokingItsNavigationTarget()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = false,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var parent = new NavigationViewItem();
            var child = new NavigationViewItem(typeof(Border));
            var leaf = new NavigationViewItem("Leaf", typeof(Border));
            child.Items.Add(leaf);
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
                ContextMenu parentContextMenu = FindSubmenuContextMenu(parent);
                Assert.True(parentContextMenu.IsOpen);

                var childMenuItem = Assert.IsType<MenuItem>(Assert.Single(parentContextMenu.Items));
                var leafMenuItem = Assert.IsType<MenuItem>(Assert.Single(childMenuItem.Items));
                Assert.Null(childMenuItem.Command);

                childMenuItem.IsSubmenuOpen = true;
                DrainDispatcher();

                // A MenuItem with children is a native SubmenuHeader. WPF opens it but does not run
                // a command, even when the source NavigationViewItem also has a navigation target.
                Assert.NotSame(child, navigationView.SelectedItem);
                Assert.True(child.IsSubmenuOpen);
                Assert.True(parent.IsSubmenuOpen);
                Assert.True(parentContextMenu.IsOpen);

                Assert.NotNull(leafMenuItem.Command);
                leafMenuItem.Command.Execute(leafMenuItem.CommandParameter);
                Assert.Same(leaf, navigationView.SelectedItem);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void ContextMenuCascade_UsesNativeRootMenuCapture()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = false,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
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
                ContextMenu parentContextMenu = FindSubmenuContextMenu(parent);
                Assert.True(parentContextMenu.IsOpen);
                var childMenuItem = Assert.IsType<MenuItem>(Assert.Single(parentContextMenu.Items));
                Assert.True(childMenuItem.IsSubmenuOpen);
                Assert.Same(parentContextMenu, Mouse.Captured);

                parent.IsSubmenuOpen = false;
                DrainDispatcher();

                Assert.False(parent.IsSubmenuOpen);
                Assert.False(child.IsSubmenuOpen);
                Assert.NotSame(parentContextMenu, Mouse.Captured);

                parent.IsSubmenuOpen = true;
                DrainDispatcher();
                Assert.Same(parentContextMenu, Mouse.Captured);

                // WPF MenuBase ends menu mode when an unrelated control takes capture.
                Assert.True(window.CaptureMouse());
                Assert.False(parent.IsSubmenuOpen);
                Assert.Same(window, Mouse.Captured);
                window.ReleaseMouseCapture();

                parent.IsSubmenuOpen = true;
                DrainDispatcher();
                Assert.Same(parentContextMenu, Mouse.Captured);

                window.Close();
                windowClosed = true;
                DrainDispatcher();
                Assert.NotSame(parentContextMenu, Mouse.Captured);
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
    public void NativeMenuItems_MirrorSubmenuStateBackToTheirSources()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var root = new NavigationViewItem();
            var child = new NavigationViewItem();
            var grandchild = new NavigationViewItem();
            grandchild.Items.Add(new NavigationViewItem());
            child.Items.Add(grandchild);
            root.Items.Add(child);
            navigationView.MenuItems.Add(root);
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

                root.IsSubmenuOpen = true;
                DrainDispatcher();
                ContextMenu contextMenu = FindSubmenuContextMenu(root);
                var childMenuItem = Assert.IsType<MenuItem>(Assert.Single(contextMenu.Items));
                var grandchildMenuItem = Assert.IsType<MenuItem>(Assert.Single(childMenuItem.Items));
                var childProvider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
                    new System.Windows.Automation.Peers.MenuItemAutomationPeer(childMenuItem)
                       .GetPattern(System.Windows.Automation.Peers.PatternInterface.ExpandCollapse)
                );
                var grandchildProvider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
                    new System.Windows.Automation.Peers.MenuItemAutomationPeer(grandchildMenuItem)
                       .GetPattern(System.Windows.Automation.Peers.PatternInterface.ExpandCollapse)
                );

                childProvider.Expand();
                DrainDispatcher();
                grandchildProvider.Expand();
                DrainDispatcher();

                Assert.True(root.IsSubmenuOpen);
                Assert.True(child.IsSubmenuOpen);
                Assert.True(grandchild.IsSubmenuOpen);

                childProvider.Collapse();
                DrainDispatcher();

                Assert.True(root.IsSubmenuOpen);
                Assert.False(child.IsSubmenuOpen);
                Assert.False(grandchild.IsSubmenuOpen);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    [Fact]
    public void ClosingLeftPaneReplacesInlinePresenterAndRetainsTreeState()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = true,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
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
                Assert.True(FindSubmenuContextMenu(parent).IsOpen);
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
    public void CollapsedLeftPane_ProjectedDescendantIgnoresStaleInlineState()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
                IsPaneOpen = false,
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.NavigationView)]),
            };
            var grandchild = new NavigationViewItem { Header = "Grandchild" };
            var child = new NavigationViewItem
            {
                Header = "Child",
                ItemsSource = new object[] { grandchild },
            };
            var parent = new NavigationViewItem
            {
                Header = "Parent",
                ItemsSource = new object[] { child },
            };
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

                // A realized direct child now receives presentation context from its root even
                // though navigation identity is maintained independently from the container.
                Assert.False(child.IsPaneOpen);
                Assert.Equal(NavigationViewPaneDisplayMode.Left, child.PaneDisplayMode);
                InvokeClick(parent);
                DrainDispatcher();

                ContextMenu contextMenu = FindSubmenuContextMenu(parent);
                var projectedChild = Assert.IsType<MenuItem>(Assert.Single(contextMenu.Items));
                Assert.True(contextMenu.IsOpen);

                projectedChild.IsSubmenuOpen = true;
                DrainDispatcher();

                Assert.True(projectedChild.IsSubmenuOpen);
                Assert.True(child.IsSubmenuOpen);
                _ = projectedChild.ApplyTemplate();
                var popup = Assert.IsType<Popup>(
                    projectedChild.Template.FindName("Popup", projectedChild)
                );
                Assert.True(popup.IsOpen);
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
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView();
            var icon = new FluentIcon(SymbolRegular.Home24);
            var parent = new NavigationViewItem { Style = itemStyle };
            parent.Icon = icon;
            var child = new NavigationViewItem { Style = itemStyle };

            parent.Items.Add(child);
            AttachToNavigationView(parent, navigationView);
            var window = new Window
            {
                Content = parent,
                Width = 200D,
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
                child.Activate(navigationView);
                _ = parent.ApplyTemplate();

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
                _ = parent.ApplyTemplate();
                Assert.Null(parent.Template.FindName("PART_ChevronButton", parent));
                var headerButton = Assert.IsType<System.Windows.Controls.Button>(
                    parent.Template.FindName("PART_HeaderButton", parent)
                );
                _ = headerButton.ApplyTemplate();
                parent.UpdateLayout();
                Assert.True(parent.IsBranchActive);
                var iconPresenter = Assert.IsType<IconPresenter>(
                    headerButton.Template.FindName("IconPresenter", headerButton)
                );
                Assert.Equal(IconVariant.Filled, iconPresenter.Variant);
                Assert.Equal(IconVariant.Filled, icon.Variant);
                Assert.True(icon.Filled);

                parent.Deactivate(navigationView);
                Assert.True(icon.Filled);

                child.Deactivate(navigationView);
                Assert.False(icon.Filled);

                parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            }
            finally
            {
                window.Close();
            }
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
            new Uri("/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative)
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

    private static ContextMenu FindSubmenuContextMenu(NavigationViewItem item)
    {
        _ = item.ApplyTemplate();
        if (item.Template.FindName("PART_SubMenuContextMenu", item) is ContextMenu contextMenu)
        {
            return contextMenu;
        }

        throw new Xunit.Sdk.XunitException(
            "The active item template does not define PART_SubMenuContextMenu."
        );
    }

    private static bool ContainsNavigationViewItem(ItemsControl itemsControl)
    {
        foreach (object item in itemsControl.Items)
        {
            if (item is NavigationViewItem)
            {
                return true;
            }

            if (item is MenuItem menuItem && ContainsNavigationViewItem(menuItem))
            {
                return true;
            }
        }

        return false;
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

    private static void AttachToNavigationView(
        NavigationViewItem item,
        Wpf.Zen.Ui.Controls.NavigationView navigationView
    )
    {
        item.SetValue(NavigationParentProperty, navigationView);
        BindingOperations.SetBinding(
            item,
            PaneDisplayModeContextProperty,
            new Binding(nameof(Wpf.Zen.Ui.Controls.NavigationView.PaneDisplayMode))
            {
                Source = navigationView,
                Mode = BindingMode.OneWay,
            }
        );
        BindingOperations.SetBinding(
            item,
            IsPaneOpenContextProperty,
            new Binding(nameof(Wpf.Zen.Ui.Controls.NavigationView.IsPaneOpen))
            {
                Source = navigationView,
                Mode = BindingMode.OneWay,
            }
        );
        item.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
    }

    private sealed class ExtensibleNavigationViewItem : NavigationViewItem
    {
        public (bool IsActive, bool IsParentBranchActive)? ActivatedState { get; private set; }

        public (bool IsActive, bool IsParentBranchActive)? DeactivatedState { get; private set; }

        public (bool OldValue, bool NewValue, bool CurrentValue)? ExpandedState { get; private set; }

        public (bool OldValue, bool NewValue, bool CurrentValue)? SubmenuState { get; private set; }

        public bool WasExpandedWhenInvoked { get; private set; }

        protected override void OnActivated(Wpf.Zen.Ui.Controls.NavigationView navigationView)
        {
            ActivatedState = (
                IsActive,
                NavigationViewItemParent is NavigationViewItem parent && parent.IsDescendantActive
            );
        }

        protected override void OnDeactivated(Wpf.Zen.Ui.Controls.NavigationView navigationView)
        {
            DeactivatedState = (
                IsActive,
                NavigationViewItemParent is NavigationViewItem parent && parent.IsDescendantActive
            );
        }

        protected override void OnInvoked()
        {
            WasExpandedWhenInvoked = IsExpanded;
        }

        protected override void OnIsExpandedChanged(bool oldValue, bool newValue)
        {
            ExpandedState = (oldValue, newValue, IsExpanded);
        }

        protected override void OnIsSubmenuOpenChanged(bool oldValue, bool newValue)
        {
            SubmenuState = (oldValue, newValue, IsSubmenuOpen);
        }
    }

    private sealed class InspectableNavigationView : Wpf.Zen.Ui.Controls.NavigationView
    {
        public bool ContainsPageKey(string key) => PageKeyIndex.ContainsKey(key);
    }

    private sealed class FixedStyleSelector(Style style) : StyleSelector
    {
        public override Style SelectStyle(object item, DependencyObject container) => style;
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }
}
