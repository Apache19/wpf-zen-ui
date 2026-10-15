// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Owns one navigation host session, coordinating stop and asynchronous completion for
/// container-free and DI usage.
/// </summary>
/// <remarks>
/// For normal shutdown, await DisposeAsync before Dispatcher shutdown.
/// Lifecycle callbacks use RequestStop; the caller owns external providers and the root container.
/// </remarks>
public sealed class NavigationScope : INavigationScope
{
    private readonly INavigationHost             _host;
    private readonly NavigationService           _navigation;
    private readonly INavigationViewPageProvider _pageProvider;
    private readonly NavigationSession           _session;

    internal NavigationScope(
        INavigationHost             host,
        NavigationService           navigation,
        INavigationViewPageProvider pageProvider,
        IServiceProvider?           services         = null,
        string?                     id               = null,
        Func<Task>?                 releaseResources = null,
        Action?                     unregister       = null)
    {
        _host         = host;
        _navigation   = navigation;
        _pageProvider = pageProvider;
        _session      = new NavigationSession(host, navigation, releaseResources, unregister);
        navigation.SetSession(_session);
        Navigation = navigation;
        Services   = services ?? new NavigationScopeServiceProvider(navigation, pageProvider);
        Id         = id;
    }

    /// <inheritdoc />
    public string? Id { get; }

    /// <inheritdoc />
    public INavigationService Navigation { get; }

    /// <inheritdoc />
    public IServiceProvider Services { get; }

    /// <inheritdoc />
    public NavigationScopeState State => _session.State;

    /// <inheritdoc />
    public Task Completion
    {
        get
        {
            _session.VerifyCanWait();
            return _session.Completion;
        }
    }

    /// <summary>
    /// Attaches a container-free scope on the current Dispatcher, creating pages through
    /// parameterless constructors by default.
    /// </summary>
    /// <param name="host">The host owning pages, history, and visual projections.</param>
    /// <param name="pageProvider">
    /// An optional external page factory that the scope does not dispose.
    /// </param>
    /// <returns>
    /// An independent session whose service becomes permanently invalid after stopping.
    /// </returns>
    public static INavigationScope Attach(
        INavigationHost host, INavigationViewPageProvider? pageProvider = null)
    {
        VerifyAttachmentAccess(host);

        var provider   = pageProvider ?? ActivatorNavigationViewPageProvider.Instance;
        var navigation = new NavigationService(host);
        var scope      = new NavigationScope(host, navigation, provider);

        try
        {
            scope.AttachHost();
            return scope;
        }
        catch (Exception exception)
        {
            scope.RollbackAttachment(exception);
            throw;
        }
    }

    /// <inheritdoc />
    public void RequestStop()
    {
        _session.RequestStop();
    }

    /// <summary>
    /// Requests stop without blocking the Dispatcher; synchronously completed failures propagate
    /// directly.
    /// </summary>
    /// <remarks>
    /// Observe asynchronous cleanup through Completion. Application shutdown should use
    /// DisposeAsync; this method may return
    /// before disposal and never blocks the UI thread waiting for unfinished work.
    /// </remarks>
    public void Dispose()
    {
        RequestStop();

        Task completion = _session.Completion;

        if (completion.IsCompleted)
        {
            completion.GetAwaiter().GetResult();
        }
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _session.VerifyCanWait();
        RequestStop();
        return _session.Completion;
    }

    /// <summary>
    /// Acquires rollback responsibility before invoking the external host, undoing attachment even
    /// after partial success followed by failure.
    /// </summary>
    internal void AttachHost()
    {
        _session.BeginAttachment();
        _host.AttachNavigationScope(_navigation, _pageProvider);
        _session.CompleteAttachment();
    }

    internal void RollbackAttachment(Exception failure)
    {
        _session.Failures.Add(failure);

        RequestStop();

        if (_session.Completion.IsCompleted)
        {
            _session.Completion.GetAwaiter().GetResult();
        }

        throw new NavigationAttachmentException(failure, _session.Completion);
    }

    internal static void VerifyAttachmentAccess(INavigationHost host)
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        if (!host.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException("A navigation scope must attach on its host Dispatcher.");
        }

        if (host.Dispatcher.HasShutdownStarted || host.Dispatcher.HasShutdownFinished)
        {
            throw new InvalidOperationException("The navigation Dispatcher is shutting down.");
        }
    }

    private sealed class NavigationScopeServiceProvider(
        INavigationService          navigation,
        INavigationViewPageProvider pageProvider) : IServiceProvider
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