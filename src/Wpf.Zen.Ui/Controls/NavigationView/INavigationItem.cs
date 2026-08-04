// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Describes the presentation-independent data required by <see cref="NavigationView"/> to
/// navigate, search, and construct a breadcrumb path.
/// </summary>
/// <remarks>
/// Implement this interface on a view-model item when an application wants
/// <see cref="NavigationView.MenuItemsSource"/> and
/// <see cref="NavigationView.FooterMenuItemsSource"/> to contain ordinary data rather than
/// <see cref="NavigationViewItem"/> controls. The source object remains the public item identity:
/// it is returned by <see cref="INavigationView.SelectedItem"/> and is forwarded to a connected
/// <see cref="BreadcrumbBar"/>. Item templates and container styles remain responsible for visual
/// presentation.
/// </remarks>
public interface INavigationItem
{
    /// <summary>
    /// Gets the stable identifier used by the navigation journal and breadcrumb navigation.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets or sets the content used by the default textual presentation and breadcrumb path.
    /// </summary>
    /// <remarks>
    /// A page can update this value through <see cref="NavigationView.HeaderContentProperty"/>.
    /// Custom data templates receive the complete source item and do not have to display this
    /// property.
    /// </remarks>
    object? Content { get; set; }

    /// <summary>Gets the data or visual content presented in the item's icon slot.</summary>
    /// <remarks>
    /// Prefer a presentation-independent value, such as <see cref="FluentIconSource"/> or an
    /// application view-model, when the item can appear in multiple presenters. WPF data templates
    /// then create one visual instance per presenter without cloning a <see cref="FrameworkElement"/>.
    /// </remarks>
    object? Icon { get; }

    /// <summary>Gets the optional tag used by <see cref="NavigationView.Navigate(string, object?)"/>.</summary>
    string? TargetPageTag { get; }

    /// <summary>Gets the page type activated by this item, or <see langword="null"/> for a group.</summary>
    Type? TargetPageType { get; }

    /// <summary>Gets the caching policy used when the target page is created.</summary>
    NavigationCacheMode NavigationCacheMode { get; }

    /// <summary>Gets the child data collection used to build the semantic navigation hierarchy.</summary>
    IEnumerable? Children { get; }

    /// <summary>
    /// Gets the text used by the navigation search surface. A <see langword="null"/> value derives
    /// text from <see cref="Content"/>; an empty value excludes the item from search.
    /// </summary>
    string? SearchText { get; }
}
