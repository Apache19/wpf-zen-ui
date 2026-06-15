// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace Wpf.Ui;

/// <summary>
/// Provides application-level access to a navigation control.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to the default destination for the specified page type.
    /// </summary>
    bool Navigate(Type pageType);

    /// <summary>
    /// Navigates to the default destination for the specified page type.
    /// </summary>
    bool Navigate(Type pageType, object? dataContext);

    /// <summary>
    /// Navigates to the destination identified by <paramref name="navigationKey"/>.
    /// </summary>
    bool Navigate(NavigationKey navigationKey);

    /// <summary>
    /// Navigates to the destination identified by <paramref name="navigationKey"/>.
    /// </summary>
    bool Navigate(NavigationKey navigationKey, object? dataContext);

    /// <summary>
    /// Adds the default destination for the specified page type to the navigation hierarchy and navigates to it.
    /// </summary>
    bool NavigateWithHierarchy(Type pageType);

    /// <summary>
    /// Adds the default destination for the specified page type to the navigation hierarchy and navigates to it.
    /// </summary>
    bool NavigateWithHierarchy(Type pageType, object? dataContext);

    /// <summary>
    /// Adds the destination identified by <paramref name="navigationKey"/> to the navigation hierarchy and navigates to it.
    /// </summary>
    bool NavigateWithHierarchy(NavigationKey navigationKey);

    /// <summary>
    /// Adds the destination identified by <paramref name="navigationKey"/> to the navigation hierarchy and navigates to it.
    /// </summary>
    bool NavigateWithHierarchy(NavigationKey navigationKey, object? dataContext);

    /// <summary>
    /// Provides direct access to the control responsible for navigation.
    /// </summary>
    INavigationView GetNavigationControl();

    /// <summary>
    /// Attaches the control responsible for navigation.
    /// </summary>
    void SetNavigationControl(INavigationView navigation);

    /// <summary>
    /// Navigates to the previous journal entry.
    /// </summary>
    bool GoBack();
}
