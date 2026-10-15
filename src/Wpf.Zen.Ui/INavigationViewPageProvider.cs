// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Defines an optional page factory for container-free navigation scopes.
/// </summary>
/// <remarks>
/// Applications using <c>Wpf.Zen.Ui.DependencyInjection</c> do not register this interface. The DI
/// navigation factory automatically activates unregistered concrete pages and injects their
/// registered
/// constructor dependencies. Implement this contract only when a container-free scope needs custom
/// creation rules or existing page instances.
/// </remarks>
public interface INavigationViewPageProvider
{
    /// <summary>
    /// Retrieves a page of the specified type.
    /// </summary>
    /// <param name="pageType">The type of the page to retrieve.</param>
    /// <remarks>
    /// Implementations never return <see langword="null"/>: a page is either produced or an
    /// exception
    /// is thrown.
    /// </remarks>
    /// <returns>An instance of the specified page type.</returns>
    NavigationPageLease GetPage(Type pageType);
}
