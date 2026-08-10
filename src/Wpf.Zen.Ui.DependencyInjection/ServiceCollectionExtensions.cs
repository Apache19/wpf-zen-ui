// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wpf.Zen.Ui;

namespace Wpf.Zen.Ui.DependencyInjection;

/// <summary>Registers navigation services with host-scoped dependencies and view-model lifetimes.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the factory, router, and scoped navigator required by navigation.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <returns>The same collection so registrations can be chained.</returns>
    /// <remarks>
    /// Calling this method does not create an application-global current navigation target. Each
    /// call to <see cref="INavigationScopeFactory.Attach"/> creates a child dependency-injection
    /// scope and binds its navigator exactly once. Concrete pages do not need to be registered: the
    /// scope factory activates them and injects their registered constructor dependencies. An explicit
    /// page registration remains useful when an application needs a factory or replacement instance;
    /// NavigationView's cache then decides whether that instance is retained for that host.
    /// </remarks>
    public static IServiceCollection AddNavigation(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddScoped<ScopedNavigationService>();

        // The lifetime is an architectural invariant, not an optional default. Retaining a legacy
        // singleton INavigationService registration would let pages resolve a different navigator
        // from the one attached to their host, recreating the cross-window bug this API prevents.
        // The contextual navigator is always owned by the child scope created for the host.
        services.RemoveAll<INavigationService>();
        services.AddScoped<INavigationService>(
            provider => provider.GetRequiredService<ScopedNavigationService>()
        );
        services.TryAddSingleton<NavigationRouter>();
        services.TryAddSingleton<INavigationRouter>(
            provider => provider.GetRequiredService<NavigationRouter>()
        );
        services.TryAddSingleton<INavigationScopeFactory, NavigationScopeFactory>();

        return services;
    }
}
