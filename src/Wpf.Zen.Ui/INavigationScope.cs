// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Owns work and owned pages for one host session and coordinates final disposal of an optional DI
/// child container.
/// </summary>
/// <remarks>
/// Await DisposeAsync while the Dispatcher is available before final host shutdown. Synchronous
/// Dispose only requests stop;
/// it does not guarantee disposal on return. Visual Unloaded ends activation rather than the scope
/// lifetime.
/// Container-free scopes borrow the caller's provider; DI scopes own only the child container they
/// create.
/// </remarks>
public interface INavigationScope : IDisposable
{
    /// <summary>
    /// Gets the session state; cleanup failures still terminate the session and propagate through
    /// completion.
    /// </summary>
    NavigationScopeState State { get; }

    /// <summary>
    /// Gets stable stop completion, including work draining, error delivery, and owned resource
    /// disposal.
    /// </summary>
    /// <remarks>
    /// Reading this property does not request stop. Lifecycle callbacks must not await completion
    /// that includes themselves.
    /// </remarks>
    Task Completion { get; }

    /// <summary>
    /// Immediately closes admission and starts asynchronous cleanup. Safe and idempotent in
    /// lifecycle callbacks.
    /// </summary>
    void RequestStop();

    /// <summary>
    /// Gets the optional id used for explicit cross-host routing.
    /// </summary>
    string? Id { get; }

    /// <summary>
    /// Gets the navigation service bound to this scope's host.
    /// </summary>
    INavigationService Navigation { get; }

    /// <summary>
    /// Gets the service provider associated with this host. Container-free scopes expose a minimal
    /// provider containing
    /// their navigation service and page provider.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Requests stop and awaits initialization, registered operations, exit, error delivery, and
    /// asynchronous owned resource disposal.
    /// </summary>
    /// <returns>The shared completion task, preserving all unhandled cleanup exceptions.</returns>
    /// <exception cref="InvalidOperationException">
    /// Called within this session's lifecycle or registered operation callback; these callbacks
    /// should use RequestStop.
    /// </exception>
    Task DisposeAsync();
}

/// <summary>
/// Creates independently disposable navigation scopes.
/// </summary>
public interface INavigationScopeFactory
{
    /// <summary>
    /// Attaches a new service scope to a navigation host.
    /// </summary>
    /// <param name="host">The host that will own navigation state.</param>
    /// <param name="id">An optional unique id for explicit cross-host routing.</param>
    /// <returns>The attached navigation scope.</returns>
    INavigationScope Attach(INavigationHost host, string? id = null);
}
