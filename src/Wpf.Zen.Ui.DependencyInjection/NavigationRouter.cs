// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wpf.Zen.Ui;

namespace Wpf.Zen.Ui.DependencyInjection;

/// <summary>Maintains weak, explicitly named routes to live navigation scopes.</summary>
internal sealed class NavigationRouter : INavigationRouter
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, WeakReference<INavigationService>> _services = [];

    /// <inheritdoc />
    public NavigationResult Navigate(string hostId, NavigationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return TryGetService(hostId, out INavigationService? service)
            ? service!.Navigate(request)
            : NavigationResult.HostNotFound;
    }

    /// <inheritdoc />
    public Task<NavigationResult> NavigateAsync(
        string hostId,
        NavigationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return TryGetService(hostId, out INavigationService? service)
            ? service!.NavigateAsync(request, cancellationToken)
            : Task.FromResult(NavigationResult.HostNotFound);
    }

    internal void Register(string hostId, INavigationService service)
    {
        ValidateHostId(hostId);
        if (service is null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        lock (_syncRoot)
        {
            if (
                _services.TryGetValue(hostId, out WeakReference<INavigationService>? reference)
                && reference.TryGetTarget(out _)
            )
            {
                throw new InvalidOperationException(
                    $"A live navigation scope is already registered with id '{hostId}'."
                );
            }

            _services[hostId] = new WeakReference<INavigationService>(service);
        }
    }

    internal void Unregister(string hostId, INavigationService service)
    {
        lock (_syncRoot)
        {
            if (
                _services.TryGetValue(hostId, out WeakReference<INavigationService>? reference)
                && reference.TryGetTarget(out INavigationService? registeredService)
                && ReferenceEquals(registeredService, service)
            )
            {
                _services.Remove(hostId);
            }
        }
    }

    private bool TryGetService(string hostId, out INavigationService? service)
    {
        ValidateHostId(hostId);
        lock (_syncRoot)
        {
            if (
                _services.TryGetValue(hostId, out WeakReference<INavigationService>? reference)
                && reference.TryGetTarget(out service)
            )
            {
                return true;
            }

            _services.Remove(hostId);
            service = null;
            return false;
        }
    }

    private static void ValidateHostId(string hostId)
    {
        if (string.IsNullOrWhiteSpace(hostId))
        {
            throw new ArgumentException("A navigation host id cannot be empty.", nameof(hostId));
        }
    }
}
