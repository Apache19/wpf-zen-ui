// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Extensions.DependencyInjection;
using Wpf.Zen.Ui;

namespace Wpf.Zen.Ui.DependencyInjection;

/// <summary>Creates a dependency-injection scope for every attached navigation host.</summary>
internal sealed class NavigationScopeFactory(
    IServiceScopeFactory serviceScopeFactory,
    NavigationRouter router
) : INavigationScopeFactory
{
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP001:Dispose created",
        Justification = "Ownership of the created IServiceScope is transferred to the returned NavigationScope; every failure path disposes it locally."
    )]
    public INavigationScope Attach(INavigationHost host, string? id = null)
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        if (!host.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "A navigation scope must be attached on the navigation host's dispatcher."
            );
        }

        IServiceScope serviceScope = serviceScopeFactory.CreateScope();
        ScopedNavigationService? navigationService = null;
        bool hostAttached = false;
        bool routeRegistered = false;

        try
        {
            navigationService = serviceScope.ServiceProvider
               .GetRequiredService<ScopedNavigationService>();
            navigationService.Attach(host);

            INavigationViewPageProvider pageProvider = serviceScope.ServiceProvider
               .GetRequiredService<INavigationViewPageProvider>();

            // Reserve an explicit route before mutating the host. A duplicate id then fails without
            // requiring asynchronous detachment of an otherwise valid host and page lifecycle.
            if (id is not null)
            {
                router.Register(id, navigationService);
                routeRegistered = true;
            }

            host.AttachNavigationScope(navigationService, pageProvider);
            hostAttached = true;

            return new NavigationScope(
                id,
                host,
                navigationService,
                serviceScope,
                router
            );
        }
        catch
        {
            if (routeRegistered && id is not null && navigationService is not null)
            {
                router.Unregister(id, navigationService);
            }

            if (hostAttached && navigationService is not null)
            {
                host.DetachNavigationScopeAsync(navigationService).GetAwaiter().GetResult();
            }

            serviceScope.Dispose();
            throw;
        }
    }
}
