// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Wpf.Zen.Ui.DependencyInjection;

/// <summary>
/// Creates a child container per attachment and transfers container and route cleanup to the shared
/// session coordinator.
/// </summary>
internal sealed class NavigationScopeFactory(IServiceScopeFactory serviceScopeFactory, NavigationRouter router)
    : INavigationScopeFactory
{
    /// <inheritdoc />
    public INavigationScope Attach(INavigationHost host, string? id = null)
    {
        NavigationScope.VerifyAttachmentAccess(host);

        var serviceScope = serviceScopeFactory.CreateAsyncScope();

        NavigationScope? scope = null;

        try
        {
            var navigation = serviceScope.ServiceProvider.GetRequiredService<ScopedNavigationService>();
            navigation.Attach(host);

            var provider = new DependencyInjectionNavigationViewPageProvider(serviceScope.ServiceProvider);
            scope = new NavigationScope(
                host,
                navigation,
                provider,
                serviceScope.ServiceProvider,
                id,
                () => serviceScope.DisposeAsync().AsTask(),
                () =>
                {
                    if (id is not null)
                    {
                        router.Unregister(id, navigation);
                    }
                }
            );

            if (id is not null)
            {
                router.Register(id, navigation);
            }

            scope.AttachHost();
            return scope;
        }
        catch (Exception exception)
        {
            if (scope is not null)
            {
                scope.RollbackAttachment(exception);
            }

            // Provides a complete asynchronous rollback handle if service resolution fails before
            // the shared coordinator exists.
            Task cleanup = ReleaseFailedCreationAsync(serviceScope, exception);
            if (cleanup.IsCompleted)
            {
                cleanup.GetAwaiter().GetResult();
            }

            throw new NavigationAttachmentException(exception, cleanup);
        }
    }

    private static async Task ReleaseFailedCreationAsync(AsyncServiceScope scope, Exception failure)
    {
        var failures = new NavigationFailures();
        failures.Add(failure);
        try
        {
            await scope.DisposeAsync();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        failures.ThrowIfAny();
    }
}