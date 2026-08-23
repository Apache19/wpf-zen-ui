// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Zen.Ui;

namespace Wpf.Zen.Ui.DependencyInjection;

/// <summary>Owns one attached host and its dependency-injection scope.</summary>
internal sealed class NavigationScope(
    string? id,
    INavigationHost host,
    INavigationService navigation,
    IServiceScope serviceScope,
    NavigationRouter router
) : INavigationScope
{
    private int _isDisposed;

    /// <inheritdoc />
    public string? Id { get; } = id;

    /// <inheritdoc />
    public INavigationService Navigation { get; } = navigation;

    /// <inheritdoc />
    public IServiceProvider Services => serviceScope.ServiceProvider;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        if (Id is not null)
        {
            router.Unregister(Id, Navigation);
        }

        try
        {
            if (host.Dispatcher.CheckAccess())
            {
                host.DetachNavigationScope(Navigation);
            }
            else if (!host.Dispatcher.HasShutdownStarted && !host.Dispatcher.HasShutdownFinished)
            {
                host.Dispatcher.Invoke(() => host.DetachNavigationScope(Navigation));
            }
        }
        finally
        {
            DisposeServiceScope();
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP007:Don't dispose injected",
        Justification = "The factory transfers ownership of this child IServiceScope to NavigationScope specifically for disposal here."
    )]
    private void DisposeServiceScope() => serviceScope.Dispose();
}
