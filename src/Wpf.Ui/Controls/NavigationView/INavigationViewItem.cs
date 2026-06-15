// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library
   Copyright(c) Microsoft Corporation.All rights reserved. */

using System.Collections;
using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents the container for an item in a NavigationView control.
/// </summary>
public interface INavigationViewItem
{
    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    object? Content { get; set; }

    /// <summary>
    /// Gets or sets the icon displayed in the menu item.
    /// </summary>
    IconElement? Icon { get; set; }

    /// <summary>
    /// Gets the collection of child menu items.
    /// </summary>
    IList MenuItems { get; }

    /// <summary>
    /// Gets or sets an object source used to generate child menu items.
    /// </summary>
    object? MenuItemsSource { get; set; }

    /// <summary>
    /// Gets a value indicating whether the current element is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the child menu items are expanded.
    /// </summary>
    bool IsExpanded { get; internal set; }

    /// <summary>
    /// Gets or sets the type of page to navigate to.
    /// </summary>
    Type? TargetPageType { get; set; }

    /// <summary>
    /// Gets or sets the optional variant that distinguishes this destination from other items targeting the same page type.
    /// </summary>
    string? TargetPageVariant { get; set; }

    InfoBadge? InfoBadge { get; set; }

    /// <summary>
    /// Gets or sets the caching characteristics for the navigation destination.
    /// </summary>
    NavigationCacheMode NavigationCacheMode { get; set; }

    /// <summary>
    /// Gets or sets the control template.
    /// </summary>
    ControlTemplate? Template { get; set; }

    /// <summary>
    /// Gets or sets the parent item when this item belongs to a navigation hierarchy.
    /// </summary>
    INavigationViewItem? NavigationViewItemParent { get; internal set; }

    /// <summary>
    /// Adds or removes a click event handler.
    /// </summary>
    [Category("Behavior")]
    event RoutedEventHandler Click;

    internal bool IsMenuElement { get; set; }

    /// <summary>
    /// Activates the item.
    /// </summary>
    void Activate(INavigationView navigationView);

    /// <summary>
    /// Deactivates the item.
    /// </summary>
    void Deactivate(INavigationView navigationView);
}
