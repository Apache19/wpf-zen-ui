// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Extensions.DependencyInjection;
using Wpf.Zen.Ui;

namespace Wpf.Zen.Ui.DependencyInjection;

/// <summary>
/// Resolves explicitly registered pages or activates unregistered pages from the dependency-injection
/// scope owned by one navigation host.
/// </summary>
/// <remarks>
/// This type is an implementation detail of <see cref="NavigationScopeFactory"/> and is deliberately
/// not registered in the application container. Page activation is navigation infrastructure, not an
/// application service that every application must configure. Explicit page registrations are still
/// respected, while an unregistered concrete page is created with <see cref="ActivatorUtilities"/> so
/// its registered constructor dependencies can be injected.
/// </remarks>
internal sealed class DependencyInjectionNavigationViewPageProvider(IServiceProvider serviceProvider)
    : INavigationViewPageProvider
{
    /// <inheritdoc />
    public object? GetPage(Type pageType)
    {
        if (pageType is null)
        {
            throw new ArgumentNullException(nameof(pageType));
        }

        // GetServiceOrCreateInstance preserves explicit registrations (including factories and
        // lifetimes), but does not force ordinary WPF pages to be listed in IServiceCollection.
        // The scoped provider is essential: constructor dependencies and INavigationService must
        // belong to the NavigationView that owns the resulting FrameworkElement.
        return ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, pageType);
    }
}
