// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Threading;

namespace Wpf.Zen.Ui;

/// <summary>
/// Forwards navigation operations to one immutable navigation host.
/// </summary>
public class NavigationService : INavigationService
{
    private INavigationHost?   _host;
    private NavigationSession? _session;

    internal NavigationSession? Session => _session;

    /// <summary>
    /// Binds the service to one session; after stopping it cannot rebind or control a new session
    /// on the same host.
    /// </summary>
    internal void SetSession(NavigationSession session)
    {
        if (_session is not null)
        {
            throw new InvalidOperationException("A navigator already belongs to a session.");
        }

        _session = session;
    }

    static NavigationService()
    {
        LibraryInitializer.EnsureInitialized();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationService"/> class for infrastructure
    /// that immediately knows its host.
    /// </summary>
    /// <param name="host">The only navigation host this service will control.</param>
    internal NavigationService(INavigationHost host)
    {
        AttachHost(host);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationService"/> class for scope
    /// infrastructure.
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
    public bool CanGoForward
    {
        get
        {
            INavigationHost host = GetHost();
            VerifyAccess(host);
            return host.CanGoForward;
        }
    }

    /// <inheritdoc />
    public INavigationState State => GetHost().NavigationState;

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
    public NavigationResult Navigate(NavigationTarget target)
    {
        var request = NavigationRequest.To(target);
        return Navigate(request);
    }

    /// <inheritdoc />
    public NavigationResult Navigate(BreadcrumbSegment segment)
    {
        if (segment is null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        return segment.Target is { } target ? Navigate(target) : NavigationResult.Rejected;
    }

    /// <inheritdoc />
    public NavigationResult Navigate(Type pageType, object? parameter = null)
    {
        var request = NavigationRequest.To(pageType, parameter);
        return Navigate(request);
    }

    /// <inheritdoc />
    public NavigationResult Navigate(string pageIdOrTargetTag, object? parameter = null)
    {
        var request = NavigationRequest.To(pageIdOrTargetTag, parameter);
        return Navigate(request);
    }

    /// <inheritdoc />
    public NavigationResult GoBack()
    {
        INavigationHost host = GetHost();
        VerifyAccess(host);
        return host.RequestGoBack();
    }

    /// <inheritdoc />
    public NavigationResult GoForward()
    {
        INavigationHost host = GetHost();
        VerifyAccess(host);
        return host.RequestGoForward();
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
        NavigationRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return DispatchRequestAsync(host => host.Navigate(request, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task<NavigationResult> GoBackAsync(CancellationToken cancellationToken = default)
    {
        return DispatchRequestAsync(host => host.RequestGoBack(cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task<NavigationResult> GoForwardAsync(CancellationToken cancellationToken = default)
    {
        return DispatchRequestAsync(host => host.RequestGoForward(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Rechecks session identity on Dispatcher execution and awaits actual Frame commit rather than
    /// acceptance alone.
    /// </summary>
    private async Task<NavigationResult> DispatchRequestAsync(
        Func<INavigationHost, NavigationResult> submit, CancellationToken cancellationToken)
    {
        INavigationHost host = GetHost();
        if (cancellationToken.IsCancellationRequested)
        {
            return new NavigationResult(NavigationResultStatus.Cancelled);
        }

        NavigationResult result;
        if (host.Dispatcher.CheckAccess())
        {
            result = submit(GetHost());
        }
        else
        {
            try
            {
                result = await host.Dispatcher.InvokeAsync(
                    () => submit(GetHost()), DispatcherPriority.Normal, cancellationToken).Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new NavigationResult(NavigationResultStatus.Cancelled);
            }
        }

        return await result.Completion.ConfigureAwait(false);
    }

    /// <summary>
    /// Binds an infrastructure-created service to its host exactly once.
    /// </summary>
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

    /// <summary>
    /// Validates admission on the caller thread and Dispatcher execution; queueing does not extend
    /// an old service's lifetime.
    /// </summary>
    private INavigationHost GetHost()
    {
        if (_session?.IsStopping == true)
        {
            throw new ObjectDisposedException(nameof(NavigationService), "The navigation session has stopped.");
        }

        return _host ?? throw new InvalidOperationException(
            "The navigation service has not been attached to a navigation host."
        );
    }

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