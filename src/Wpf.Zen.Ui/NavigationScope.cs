// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Creates a navigation scope without requiring a dependency-injection container.
/// </summary>
/// <remarks>
/// This is the container-free counterpart of <see cref="INavigationScopeFactory"/>. Both paths attach the same
/// immutable navigation service to the host and therefore provide identical page context, request parameters,
/// history, cache, and detachment behavior. The only difference is page construction and ownership of application
/// services: this scope uses an activator or a caller-supplied page provider and does not dispose that provider.
/// </remarks>
public sealed class NavigationScope : INavigationScope
{
    private readonly object _disposeSyncRoot = new();
    private readonly INavigationHost _host;
    private Task? _disposeTask;

    private NavigationScope(
        INavigationHost host,
        INavigationService navigation,
        INavigationViewPageProvider pageProvider
    )
    {
        _host = host;
        Navigation = navigation;
        Services = new NavigationScopeServiceProvider(navigation, pageProvider);
    }

    /// <inheritdoc />
    public string? Id => null;

    /// <inheritdoc />
    public INavigationService Navigation { get; }

    /// <inheritdoc />
    /// <remarks>
    /// A container-free scope exposes a minimal provider containing its <see cref="INavigationService"/> and
    /// <see cref="INavigationViewPageProvider"/>. Other service types return <see langword="null"/>.
    /// </remarks>
    public IServiceProvider Services { get; }

    /// <summary>Attaches a container-free navigation scope to a host.</summary>
    /// <param name="host">The navigation surface that will own history, selection, and page cache.</param>
    /// <param name="pageProvider">
    /// An optional page factory. When omitted, pages are created through their parameterless constructors.
    /// The returned scope does not take ownership of or dispose a caller-supplied provider.
    /// </param>
    /// <returns>A scope whose navigator and page context are bound exclusively to <paramref name="host"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The call is made from another dispatcher, or the host already has an attached scope.
    /// </exception>
    public static INavigationScope Attach(
        INavigationHost host,
        INavigationViewPageProvider? pageProvider = null
    )
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

        INavigationViewPageProvider effectivePageProvider =
            pageProvider ?? ActivatorNavigationViewPageProvider.Instance;
        var navigation = new NavigationService(host);
        host.AttachNavigationScope(navigation, effectivePageProvider);

        return new NavigationScope(host, navigation, effectivePageProvider);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Task disposeTask = DisposeAsync();

        // Navigation-aware pages may perform real asynchronous exit work. Blocking the UI thread
        // here could deadlock their continuation, so synchronous disposal initiates that work and
        // observes it only when it has already completed. Await DisposeAsync when the caller needs
        // a strict completion boundary.
        if (disposeTask.IsCompleted)
        {
            disposeTask.GetAwaiter().GetResult();
        }
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        lock (_disposeSyncRoot)
        {
            return _disposeTask ??= DisposeCoreAsync();
        }
    }

    private Task DisposeCoreAsync()
    {
        Task detachTask;
        if (_host.Dispatcher.CheckAccess())
        {
            detachTask = _host.DetachNavigationScopeAsync(Navigation);
        }
        else if (!_host.Dispatcher.HasShutdownStarted && !_host.Dispatcher.HasShutdownFinished)
        {
            detachTask = _host.Dispatcher.Invoke(
                () => _host.DetachNavigationScopeAsync(Navigation)
            );
        }
        else
        {
            // Once the owning Dispatcher has shut down, WPF state can no longer be touched. There
            // are no container-owned services in this scope, so graceful degradation is complete.
            detachTask = Task.CompletedTask;
        }

        return detachTask;
    }

    private sealed class NavigationScopeServiceProvider(
        INavigationService navigation,
        INavigationViewPageProvider pageProvider
    ) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(INavigationService))
            {
                return navigation;
            }

            return serviceType == typeof(INavigationViewPageProvider) ? pageProvider : null;
        }
    }
}
