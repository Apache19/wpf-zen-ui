// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading;
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
    private readonly object _disposeGate = new();
    private Task? _disposeTask;

    /// <inheritdoc />
    public string? Id { get; } = id;

    /// <inheritdoc />
    public INavigationService Navigation { get; } = navigation;

    /// <inheritdoc />
    public IServiceProvider Services => serviceScope.ServiceProvider;

    /// <inheritdoc />
    /// <remarks>
    /// This method initiates cleanup without blocking a foreign thread on the WPF dispatcher. A
    /// cancellation-compliant entry callback normally completes during detachment, preserving
    /// immediate service disposal on the dispatcher. Use <see cref="DisposeAsync"/> to await an
    /// uncooperative or genuinely asynchronous callback before its scoped services are released.
    /// </remarks>
    public void Dispose()
    {
        Task disposeTask = BeginDispose();
        if (disposeTask.IsCompleted)
        {
            disposeTask.GetAwaiter().GetResult();
        }
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        return BeginDispose();
    }

    /// <summary>Creates or returns the one cleanup operation shared by both disposal APIs.</summary>
    private Task BeginDispose()
    {
        lock (_disposeGate)
        {
            if (_disposeTask is not null)
            {
                return _disposeTask;
            }

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            _disposeTask = completion.Task;
            _ = CompleteDisposeAsync(completion);
            return _disposeTask;
        }
    }

    /// <summary>Publishes synchronous and asynchronous cleanup failures through one stable task.</summary>
    private async Task CompleteDisposeAsync(TaskCompletionSource<bool> completion)
    {
        try
        {
            await DisposeCoreAsync().ConfigureAwait(false);
            completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    /// <summary>Detaches the host, drains entry callbacks, then releases container services.</summary>
    private async Task DisposeCoreAsync()
    {
        if (Id is not null)
        {
            router.Unregister(Id, Navigation);
        }

        try
        {
            if (host.Dispatcher.CheckAccess())
            {
                await host.DetachNavigationScope(Navigation).ConfigureAwait(false);
            }
            else if (!host.Dispatcher.HasShutdownStarted && !host.Dispatcher.HasShutdownFinished)
            {
                Task detachTask = await host.Dispatcher
                    .InvokeAsync(() => host.DetachNavigationScope(Navigation))
                    .Task
                    .ConfigureAwait(false);

                await detachTask.ConfigureAwait(false);
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
