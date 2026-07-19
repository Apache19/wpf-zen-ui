// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library
   Copyright(c) Microsoft Corporation.All rights reserved. */

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents the container for an item in a NavigationView control.
/// </summary>
public interface INavigationViewItem
{
    /// <summary>
    /// Gets the unique identifier that allows the item to be located in the navigation.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets or sets the header displayed by the navigation item.
    /// </summary>
    object? Header { get; set; }

    /// <summary>
    /// Gets or sets the icon displayed beside the item header.
    /// </summary>
    IconElement? Icon { get; set; }

    /// <summary>
    /// Gets a value indicating whether the current element is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets or sets the unique tag used by the parent navigation system for the purpose of searching and navigating.
    /// </summary>
    string TargetPageTag { get; set; }

    /// <summary>
    /// Gets or sets the type of the page to be navigated. (Should be derived from <see cref="FrameworkElement"/>).
    /// </summary>
    Type? TargetPageType { get; set; }

    /// <summary>Gets or sets the optional badge displayed in the item header.</summary>
    InfoBadge? InfoBadge { get; set; }

    /// <summary>
    /// Gets or sets the caching characteristics for a page involved in a navigation.
    /// </summary>
    NavigationCacheMode NavigationCacheMode { get; set; }

    /// <summary>
    /// Gets or sets the control template used to present the item header and its child items.
    /// </summary>
    ControlTemplate? Template { get; set; }

    /// <summary>
    /// Gets the semantic parent when this item is contained by another navigation item.
    /// </summary>
    INavigationViewItem? NavigationViewItemParent { get; }

    internal bool IsMenuElement { get; set; }

    /// <summary>
    /// Marks the item active in the specified navigation view and updates its visual state.
    /// </summary>
    void Activate(INavigationView navigationView);

    /// <summary>
    /// Marks the item inactive in the specified navigation view and updates its visual state.
    /// </summary>
    void Deactivate(INavigationView navigationView);
}
