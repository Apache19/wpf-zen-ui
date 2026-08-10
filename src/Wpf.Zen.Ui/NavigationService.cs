// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Threading;

namespace Wpf.Zen.Ui;

/// <summary>Forwards navigation operations to one immutable navigation host.</summary>
public class NavigationService : INavigationService
{
    private INavigationHost? _host;

    static NavigationService()
    {
        LibraryInitializer.EnsureInitialized();
    }

    /// <summary>Initializes a new instance of the <see cref="NavigationService"/> class for infrastructure that immediately knows its host.</summary>
    /// <param name="host">The only navigation host this service will control.</param>
    internal NavigationService(INavigationHost host)
    {
        AttachHost(host);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationService"/> class for scope infrastructure.
    /// </summary>
    /// <remarks>
    /// Derived scope infrastructure must call <see cref="AttachHost"/> exactly once before exposing
    /// the service. The protected constructor exists because the host control is known only when a
    /// runtime navigation scope is attached, not when the application's root container is built.
    /// </remarks>
    protected NavigationService() { }

    /// <inheritdoc />
    public bool CanGoBack
    {
        get
        {
            INavigationHost host = GetHost();
            VerifyAccess(host);
            return host.CanGoBack;
        }
    }

    /// <inheritdoc />
    public NavigationResult Navigate(NavigationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        INavigationHost host = GetHost();
        VerifyAccess(host);
        return host.Navigate(request);
    }

    /// <inheritdoc />
    public NavigationResult Navigate(Type pageType, object? parameter = null) =>
        Navigate(NavigationRequest.To(pageType, parameter));

    /// <inheritdoc />
    public NavigationResult Navigate(string pageIdOrTargetTag, object? parameter = null) =>
        Navigate(NavigationRequest.To(pageIdOrTargetTag, parameter));

    /// <inheritdoc />
    public NavigationResult NavigateWithHierarchy(Type pageType, object? parameter = null) =>
        Navigate(NavigationRequest.To(pageType, parameter, NavigationMode.Hierarchical));

    /// <inheritdoc />
    public NavigationResult GoBack()
    {
        INavigationHost host = GetHost();
        VerifyAccess(host);
        return host.RequestGoBack();
    }

    /// <inheritdoc />
    public void ClearHistory()
    {
        INavigationHost host = GetHost();
        VerifyAccess(host);
        host.ClearNavigationHistory();
    }

    /// <inheritdoc />
    public Task<NavigationResult> NavigateAsync(
        NavigationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        INavigationHost host = GetHost();
        if (host.Dispatcher.CheckAccess())
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(host.Navigate(request));
        }

        return host.Dispatcher
           .InvokeAsync(() => host.Navigate(request), DispatcherPriority.Normal, cancellationToken)
           .Task;
    }

    /// <inheritdoc />
    public Task<NavigationResult> GoBackAsync(CancellationToken cancellationToken = default)
    {
        INavigationHost host = GetHost();
        if (host.Dispatcher.CheckAccess())
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(host.RequestGoBack());
        }

        return host.Dispatcher
           .InvokeAsync(host.RequestGoBack, DispatcherPriority.Normal, cancellationToken)
           .Task;
    }

    /// <summary>Binds an infrastructure-created service to its host exactly once.</summary>
    /// <param name="host">The immutable destination host.</param>
    /// <exception cref="InvalidOperationException">The service was already bound.</exception>
    protected void AttachHost(INavigationHost host)
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        if (_host is not null)
        {
            throw new InvalidOperationException("A navigation service cannot change its host after attachment.");
        }

        _host = host;
    }

    private INavigationHost GetHost() =>
        _host
        ?? throw new InvalidOperationException(
            "The navigation service has not been attached to a navigation host."
        );

    private static void VerifyAccess(INavigationHost host)
    {
        if (!host.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "Synchronous navigation must run on the navigation host's dispatcher. Use the asynchronous API when calling from another thread."
            );
        }
    }
}
