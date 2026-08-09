// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library
   Copyright(c) Microsoft Corporation.All rights reserved. */

using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using Wpf.Zen.Ui.Animations;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents a container that enables navigation of app content. It has a header, a view for the main content, and a menu pane for navigation commands.
/// </summary>
public interface INavigationView
{
    /// <summary>
    /// Gets or sets the header content.
    /// </summary>
    object? Header { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Header"/> visibility.
    /// </summary>
    Visibility HeaderVisibility { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the header is always visible.
    /// </summary>
    bool AlwaysShowHeader { get; set; }

    /// <summary>
    /// Gets the collection of menu items displayed in the NavigationView.
    /// </summary>
    IList MenuItems { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="MenuItems"/> contains at least one item.
    /// </summary>
    bool HasMenuItems { get; }

    /// <summary>
    /// Gets or sets an object source used to generate the content of the NavigationView menu.
    /// </summary>
    object? MenuItemsSource { get; set; }

    /// <summary>
    /// Gets the list of objects to be used as navigation items in the footer menu.
    /// </summary>
    IList FooterMenuItems { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="FooterMenuItems"/> contains at least one item.
    /// </summary>
    bool HasFooterMenuItems { get; }

    /// <summary>
    /// Gets or sets the object that represents the navigation items to be used in the footer menu.
    /// </summary>
    object? FooterMenuItemsSource { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the top separators is visible.
    /// </summary>
    bool IsTopSeparatorVisible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the footer separators is visible.
    /// </summary>
    bool IsFooterSeparatorVisible { get; set; }

    /// <summary>
    /// Gets the selected source item.
    /// </summary>
    /// <remarks>
    /// For an ItemsSource-driven menu this is the original object supplied by the application,
    /// not the generated <see cref="NavigationViewItem"/> container.
    /// </remarks>
    object? SelectedItem { get; }

    /// <summary>
    /// Gets the current hierarchical navigation path as the original source objects.
    /// </summary>
    /// <remarks>
    /// The collection instance remains stable and can be assigned directly to a
    /// <see cref="ItemsControl.ItemsSource"/>. Generated navigation containers are never exposed
    /// through this API.
    /// </remarks>
    ReadOnlyObservableCollection<object> BreadcrumbItems { get; }

    /// <summary>
    /// Gets or sets a UI element that is shown at the top of the control, below the pane if PaneDisplayMode is Top.
    /// </summary>
    object? ContentOverlay { get; set; }

    /// <summary>
    /// Gets a value indicating whether the back button is enabled or disabled.
    /// </summary>
    bool IsBackEnabled { get; }

    /// <summary>
    /// Gets or sets the legacy browser-navigation input that is stopped at this control boundary.
    /// The default value is <see cref="NavigationViewInput.None"/>.
    /// </summary>
    NavigationViewInput SuppressedNavigationInput { get; set; }

    /// <summary>
    /// Gets or sets the policy that controls whether the back button is visible.
    /// The default value is <see cref="NavigationViewBackButtonVisible.Auto"/>, which displays the
    /// button only while the navigation journal can go back.
    /// </summary>
    NavigationViewBackButtonVisible IsBackButtonVisible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the toggle button is visible.
    /// </summary>
    bool IsPaneToggleVisible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the NavigationView pane is expanded to its full width.
    /// </summary>
    bool IsPaneOpen { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the pane is shown.
    /// </summary>
    bool IsPaneVisible { get; set; }

    /// <summary>
    /// Gets or sets the width of the pane when <see cref="PaneDisplayMode"/> is
    /// <see cref="NavigationViewPaneDisplayMode.Left"/> and the pane is open.
    /// </summary>
    double LeftPaneOpenWidth { get; set; }

    /// <summary>
    /// Gets or sets the width of the pane when <see cref="PaneDisplayMode"/> is
    /// <see cref="NavigationViewPaneDisplayMode.Left"/> and the pane is compact.
    /// </summary>
    double LeftPaneCompactWidth { get; set; }

    /// <summary>
    /// Gets the width retained for the Left pane when another
    /// <see cref="NavigationViewPaneDisplayMode"/> is active.
    /// </summary>
    double LeftPaneWidth { get; }

    /// <summary>
    /// Gets or sets the content for the pane header.
    /// </summary>
    object? PaneHeader { get; set; }

    /// <summary>
    /// Gets or sets the label adjacent to the menu icon when the NavigationView pane is open.
    /// </summary>
    string? PaneTitle { get; set; }

    /// <summary>
    /// Gets or sets the content for the pane footer.
    /// </summary>
    object? PaneFooter { get; set; }

    /// <summary>
    /// Gets or sets a value that specifies how the pane and content areas of a NavigationView are being shown.
    /// <para>It is not the same PaneDisplayMode as in WinUi.</para>
    /// </summary>
    NavigationViewPaneDisplayMode PaneDisplayMode { get; set; }

    /// <summary>
    /// Gets or sets the title bar whose leading content slot can host navigation commands.
    /// </summary>
    TitleBar? TitleBar { get; set; }

    /// <summary>
    /// Gets or sets an AutoSuggestBox to be displayed in the NavigationView.
    /// </summary>
    AutoSuggestBox? AutoSuggestBox { get; set; }

    /// <summary>
    /// Gets or sets an BreadcrumbBar that is in <see cref="Header"/>.
    /// </summary>
    BreadcrumbBar? BreadcrumbBar { get; set; }

    /// <summary>
    /// Gets or sets the default style applied throughout the primary menu item hierarchy.
    /// </summary>
    Style? MenuItemContainerStyle { get; set; }

    /// <summary>
    /// Gets or sets custom style-selection logic used throughout the primary menu hierarchy.
    /// </summary>
    StyleSelector? MenuItemContainerStyleSelector { get; set; }

    /// <summary>
    /// Gets or sets the default style applied throughout the footer menu item hierarchy.
    /// </summary>
    Style? FooterMenuItemContainerStyle { get; set; }

    /// <summary>
    /// Gets or sets custom style-selection logic used throughout the footer menu hierarchy.
    /// </summary>
    StyleSelector? FooterMenuItemContainerStyleSelector { get; set; }

    /// <summary>
    /// Gets or sets the data template used to display primary menu items.
    /// </summary>
    DataTemplate? MenuItemTemplate { get; set; }

    /// <summary>
    /// Gets or sets custom template-selection logic for primary menu items.
    /// </summary>
    DataTemplateSelector? MenuItemTemplateSelector { get; set; }

    /// <summary>
    /// Gets or sets the data template used to display footer menu items.
    /// </summary>
    DataTemplate? FooterMenuItemTemplate { get; set; }

    /// <summary>
    /// Gets or sets custom template-selection logic for footer menu items.
    /// </summary>
    DataTemplateSelector? FooterMenuItemTemplateSelector { get; set; }

    /// <summary>
    /// Gets or sets a value deciding how long the effect of the transition between the pages should take.
    /// </summary>
    int TransitionDuration { get; set; }

    /// <summary>
    /// Gets or sets type of <see cref="INavigationView"/> transitions during navigation.
    /// </summary>
    Transition Transition { get; set; }

    /// <summary>
    /// Gets or sets margin for a Frame of <see cref="INavigationView"/>
    /// </summary>
    Thickness FrameMargin { get; set; }

    /// <summary>
    /// Occurs when the NavigationView pane is opened.
    /// </summary>
    event TypedEventHandler<NavigationView, RoutedEventArgs> PaneOpened;

    /// <summary>
    /// Occurs when the NavigationView pane is closed.
    /// </summary>
    event TypedEventHandler<NavigationView, RoutedEventArgs> PaneClosed;

    /// <summary>
    /// Occurs when the pane display mode changes.
    /// </summary>
    event TypedEventHandler<NavigationView, RoutedEventArgs> PaneDisplayModeChanged;

    /// <summary>
    /// Occurs when the currently selected item changes.
    /// </summary>
    event TypedEventHandler<NavigationView, RoutedEventArgs> SelectionChanged;

    /// <summary>
    /// Occurs when an item in the menu receives an interaction such as a click or tap.
    /// </summary>
    event TypedEventHandler<NavigationView, RoutedEventArgs> ItemInvoked;

    /// <summary>
    /// Occurs when the back button receives an interaction such as a click or tap.
    /// </summary>
    event TypedEventHandler<NavigationView, RoutedEventArgs> BackRequested;

    /// <summary>
    /// Occurs when a new navigation is requested
    /// </summary>
    event TypedEventHandler<NavigationView, NavigatingCancelEventArgs> Navigating;

    /// <summary>
    /// Occurs when navigated to page
    /// </summary>
    event TypedEventHandler<NavigationView, NavigatedEventArgs> Navigated;

    /// <summary>
    /// Gets a value indicating whether there is at least one entry in back navigation history.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Synchronously navigates current navigation Frame to the
    /// given Element.
    /// </summary>
    bool Navigate(Type pageType, object? dataContext = null);

    /// <summary>
    /// Synchronously navigates current navigation Frame to the
    /// given Element.
    /// </summary>
    bool Navigate(string pageIdOrTargetTag, object? dataContext = null);

    /// <summary>
    /// Synchronously adds an element to the navigation stack and navigates current navigation Frame to the
    /// </summary>
    bool NavigateWithHierarchy(Type pageType, object? dataContext = null);

    /// <summary>
    /// Replaces the contents of the navigation frame, without changing the currently selected item or triggering an <see cref="SelectionChanged"/>.
    /// </summary>
    bool ReplaceContent(Type pageTypeToEmbed);

    /// <summary>
    /// Replaces the contents of the navigation frame, without changing the currently selected item or triggering an <see cref="SelectionChanged"/>.
    /// </summary>
    bool ReplaceContent(UIElement pageInstanceToEmbed, object? dataContext = null);

    /// <summary>
    /// Navigates the NavigationView to the next journal entry.
    /// </summary>
    /// <returns><see langword="true"/> if successfully navigated forward, otherwise <see langword="false"/>.</returns>
    bool GoForward();

    /// <summary>
    /// Navigates the NavigationView to the previous journal entry.
    /// </summary>
    /// <returns><see langword="true"/> if successfully navigated backward, otherwise <see langword="false"/>.</returns>
    bool GoBack();

    /// <summary>
    /// Clears the NavigationView history.
    /// </summary>
    void ClearJournal();

    /// <summary>
    /// Allows you to assign to the NavigationView a special service responsible for retrieving the page instances.
    /// </summary>
    void SetPageProviderService(INavigationViewPageProvider navigationViewPageProvider);

    /// <summary>
    /// Allows you to assign a general <see cref="IServiceProvider"/> to the NavigationView that will be used to retrieve page instances and view models.
    /// </summary>
    void SetServiceProvider(IServiceProvider serviceProvider);
}
