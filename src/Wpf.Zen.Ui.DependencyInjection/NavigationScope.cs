// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading.Tasks;
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
    private readonly object _disposeSyncRoot = new();
    private Task? _disposeTask;

    /// <inheritdoc />
    public string? Id { get; } = id;

    /// <inheritdoc />
    public INavigationService Navigation { get; } = navigation;

    /// <inheritdoc />
    public IServiceProvider Services => serviceScope.ServiceProvider;

    /// <inheritdoc />
    public void Dispose()
    {
        Task disposeTask = DisposeAsync();

        // Most pages complete their exit notification synchronously. If a page performs genuinely
        // asynchronous cleanup, blocking the UI thread here could deadlock its continuation. In
        // that case disposal continues in the background; callers requiring a strict completion
        // boundary should await DisposeAsync instead.
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP007:Don't dispose injected",
        Justification = "The factory transfers ownership of this child IServiceScope to NavigationScope specifically for disposal here."
    )]
    private async Task DisposeCoreAsync()
    {
        if (Id is not null)
        {
            router.Unregister(Id, Navigation);
        }

        try
        {
            Task detachTask;
            if (host.Dispatcher.CheckAccess())
            {
                detachTask = host.DetachNavigationScopeAsync(Navigation);
            }
            else if (!host.Dispatcher.HasShutdownStarted && !host.Dispatcher.HasShutdownFinished)
            {
                detachTask = host.Dispatcher.Invoke(
                    () => host.DetachNavigationScopeAsync(Navigation)
                );
            }
            else
            {
                detachTask = Task.CompletedTask;
            }

            await detachTask.ConfigureAwait(false);
        }
        finally
        {
            serviceScope.Dispose();
        }
    }
}
