// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Owns the page provider, contextual navigation service, and optional container services associated with one
/// navigation host.
/// </summary>
/// <remarks>
/// Dispose the scope when its containing window, tab, WinForms control, or other lifetime owner is
/// permanently closed. WPF's Unloaded event is not a reliable lifetime boundary because ordinary
/// template changes and visual-tree reparenting can raise it temporarily.
/// </remarks>
public interface INavigationScope : IDisposable
{
    /// <summary>Gets the optional id used for explicit cross-host routing.</summary>
    string? Id { get; }

    /// <summary>Gets the navigation service bound to this scope's host.</summary>
    INavigationService Navigation { get; }

    /// <summary>
    /// Gets the service provider associated with this host. Container-free scopes expose a minimal provider containing
    /// their navigation service and page provider.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Asynchronously detaches the host, waits for page-exit callbacks, and disposes scoped services.
    /// </summary>
    /// <returns>A task that represents complete navigation-scope disposal.</returns>
    Task DisposeAsync();
}

/// <summary>Creates independently disposable navigation scopes.</summary>
public interface INavigationScopeFactory
{
    /// <summary>Attaches a new service scope to a navigation host.</summary>
    /// <param name="host">The host that will own navigation state.</param>
    /// <param name="id">An optional unique id for explicit cross-host routing.</param>
    /// <returns>The attached navigation scope.</returns>
    INavigationScope Attach(INavigationHost host, string? id = null);
}
