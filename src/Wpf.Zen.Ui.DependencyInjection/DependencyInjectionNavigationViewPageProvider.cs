// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Wpf.Zen.Ui.DependencyInjection;

/// <summary>
/// Resolves registered pages or activates unregistered pages from one navigation host's DI scope.
/// </summary>
/// <remarks>
/// This type is an implementation detail of <see cref="NavigationScopeFactory"/> and is not
/// registered in the application container. Explicit page registrations retain their lifetimes.
/// Unregistered concrete pages use <see cref="ActivatorUtilities"/> to inject constructor
/// dependencies from the navigation scope.
/// </remarks>
internal sealed class DependencyInjectionNavigationViewPageProvider(IServiceProvider serviceProvider)
    : INavigationViewPageProvider
{
    /// <inheritdoc />
    public NavigationPageLease GetPage(Type pageType)
    {
        if (pageType is null)
        {
            throw new ArgumentNullException(nameof(pageType));
        }

        // Registered pages retain container ownership; factory-created pages transfer ownership
        // through their lease. Both resolve dependencies from the current navigation scope.
        var registered = serviceProvider.GetService(pageType);
        if (registered is FrameworkElement registeredPage)
        {
            return NavigationPageLease.Borrowed(registeredPage);
        }

        var created = ActivatorUtilities.CreateInstance(serviceProvider, pageType);
        var page = (FrameworkElement)created;
        return NavigationPageLease.Owned(page);
    }
}
