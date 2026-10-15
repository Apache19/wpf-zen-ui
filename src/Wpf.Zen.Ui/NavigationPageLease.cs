// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace Wpf.Zen.Ui;

/// <summary>
/// Declares final disposal ownership for a page and optional model; navigation manages lease
/// lifetime after factory return.
/// </summary>
/// <remarks>
/// Borrowed is for caller-owned or container-owned objects. Owned transfers objects created by the
/// factory.
/// An owned lease must represent unique ownership; never create conflicting owned leases for the
/// same live object.
/// </remarks>
public sealed class NavigationPageLease
{
    private readonly Func<Task>? _release;
    private readonly object      _gate = new();

    private Task? _completion;

    private NavigationPageLease(FrameworkElement page, Func<Task>? release)
    {
        Page     = page ?? throw new ArgumentNullException(nameof(page));
        _release = release;
    }

    /// <summary>
    /// Gets the created or borrowed page root; DataContext ownership is not inferred.
    /// </summary>
    public FrameworkElement Page { get; }

    /// <summary>
    /// Gets whether this lease owns final disposal responsibility.
    /// </summary>
    public bool IsOwned => _release is not null;

    /// <summary>
    /// Creates a borrowed lease for container-registered or caller-supplied pages, leaving disposal
    /// to their owner.
    /// </summary>
    /// <param name="page">The page whose lifetime is managed by an external owner.</param>
    /// <returns>A lease participating only in navigation instance retention.</returns>
    public static NavigationPageLease Borrowed(FrameworkElement page)
    {
        return new NavigationPageLease(page, null);
    }

    /// <summary>
    /// Transfers final disposal of the page and optional model, disposing the page first and
    /// deduplicating by reference.
    /// </summary>
    /// <param name="page">The page created directly by the factory.</param>
    /// <param name="model">
    /// An optional factory-created model; do not transfer container-owned models.
    /// </param>
    /// <returns>An owned lease disposing exactly once, preferring asynchronous disposal.</returns>
    public static NavigationPageLease Owned(FrameworkElement page, object? model = null)
    {
        return new NavigationPageLease(page, () => ReleaseObjectsAsync(page, model));
    }

    /// <summary>
    /// Transfers a custom release strategy, such as a page child container; it owns all its
    /// resources and runs once.
    /// </summary>
    /// <param name="page">The page to display.</param>
    /// <param name="releaseAsync">
    /// The complete release operation executed on the page Dispatcher.
    /// </param>
    /// <returns>A lease using the specified release strategy.</returns>
    public static NavigationPageLease Owned(FrameworkElement page, Func<Task> releaseAsync)
    {
        if (releaseAsync is null)
        {
            throw new ArgumentNullException(nameof(releaseAsync));
        }

        return new NavigationPageLease(page, releaseAsync);
    }

    /// <summary>
    /// Performs final disposal. Repeated calls share one task; navigation normally calls this after
    /// the final retainer leaves.
    /// </summary>
    /// <returns>The stable task preserving disposal exceptions.</returns>
    public Task DisposeAsync()
    {
        lock (_gate)
        {
            if (_completion is not null)
            {
                return _completion;
            }

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            _completion = completion.Task;

            _ = CompleteReleaseAsync(completion);

            return _completion;
        }
    }

    private async Task CompleteReleaseAsync(TaskCompletionSource<bool> completion)
    {
        try
        {
            if (_release is not null)
            {
                Dispatcher dispatcher = Page.Dispatcher;

                if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    throw new InvalidOperationException("Page disposal requires its live Dispatcher.");
                }

                if (dispatcher.CheckAccess())
                {
                    await ReleaseOnDispatcherAsync();
                }
                else
                {
                    Task release = await dispatcher.InvokeAsync(ReleaseOnDispatcherAsync).Task.ConfigureAwait(false);
                    await release.ConfigureAwait(false);
                }
            }

            completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    /// <summary>
    /// Establishes an explicit WPF context for host-initiated disposal, preserving UI affinity
    /// during asynchronous cleanup.
    /// </summary>
    private async Task ReleaseOnDispatcherAsync()
    {
        SynchronizationContext? previous = SynchronizationContext.Current;

        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(Page.Dispatcher)
        );

        try
        {
            await _release!();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static async Task ReleaseObjectsAsync(object page, object? model)
    {
        var failures = new NavigationFailures();

        await ReleaseObjectAsync(page, failures);

        if (model is not null && !ReferenceEquals(page, model))
        {
            await ReleaseObjectAsync(model, failures);
        }

        failures.ThrowIfAny();
    }

    private static async Task ReleaseObjectAsync(object resource, NavigationFailures failures)
    {
        try
        {
            if (resource is IAsyncDisposable asynchronous)
            {
                await asynchronous.DisposeAsync();
            }
            else if (resource is IDisposable synchronous)
            {
                synchronous.Dispose();
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }
}

/// <summary>
/// Combines request, display, cache, and activation retention of one instance, releasing it after
/// the final retainer leaves.
/// </summary>
internal sealed class NavigationPageInstance
{
    private readonly NavigationSession            _session;
    private readonly NavigationWorkRegistry.Lease _work;

    private int _references = 1;

    internal NavigationPageInstance(
        NavigationSession session, NavigationPageLease lease, NavigationWorkRegistry.Lease work)
    {
        _session = session;
        Lease    = lease;
        _work    = work;
    }

    internal NavigationPageLease Lease      { get; }
    internal NavigationSession   Session    => _session;
    internal FrameworkElement    Page       => Lease.Page;
    internal bool                IsReleased => Volatile.Read(ref _references) == 0;

    internal void Retain()
    {
        Page.Dispatcher.VerifyAccess();

        if (IsReleased)
        {
            throw new ObjectDisposedException(nameof(NavigationPageInstance));
        }

        _references++;
    }

    internal void Release()
    {
        Page.Dispatcher.VerifyAccess();

        if (_references <= 0)
        {
            throw new InvalidOperationException("A page retention was released more than once.");
        }

        _references--;
        if (_references == 0)
        {
            _ = CompleteReleaseAsync();
        }
    }

    private async Task CompleteReleaseAsync()
    {
        try
        {
            using var callback = new NavigationCallbackContext(_session);
            await Lease.DisposeAsync();
        }
        catch (Exception exception)
        {
            _session.Failures.Add(exception);
        }
        finally
        {
            NavigationPageOwnership.CompleteRelease(this);
            _work.Dispose();
        }
    }
}

/// <summary>
/// Validates unique session ownership and disposal state by page reference; weak keys track
/// instance ownership without collecting global work.
/// </summary>
internal static class NavigationPageOwnership
{
    private static readonly ConditionalWeakTable<FrameworkElement, Stamp> Owners = new();
    private static readonly object                                        Gate   = new();

    internal static NavigationPageInstance Retain(
        NavigationSession            session,
        Dispatcher                   dispatcher,
        NavigationPageLease          lease,
        NavigationWorkRegistry.Lease work)
    {
        lock (Gate)
        {
            var stamp = Owners.GetValue(lease.Page, _ => new Stamp());
            if (stamp.Current is { } existing)
            {
                work.Dispose();

                if (!ReferenceEquals(existing.Session, session) || existing.IsReleased)
                {
                    throw new InvalidOperationException("The page is still retained by another activation or session.");
                }

                if (!ReferenceEquals(existing.Lease, lease) && lease.IsOwned)
                {
                    throw new InvalidOperationException("A page instance cannot have conflicting ownership leases.");
                }

                existing.Retain();
                return existing;
            }

            if (stamp.WasOwned)
            {
                work.Dispose();
                throw new ObjectDisposedException(
                    lease.Page.GetType().Name,
                    "An owned page cannot be reused after final disposal."
                );
            }

            if (!ReferenceEquals(lease.Page.Dispatcher, dispatcher))
            {
                _ = ReleaseInvalidPageAsync(session, lease, work);
                throw new InvalidOperationException("The page must belong to its navigation host Dispatcher.");
            }

            var instance = new NavigationPageInstance(session, lease, work);

            stamp.Current  = instance;
            stamp.WasOwned = lease.IsOwned;

            return instance;
        }
    }

    /// <summary>
    /// Removes strong references after disposal, retaining only a reuse prohibition for owned
    /// instances so old sessions can be collected.
    /// </summary>
    internal static void CompleteRelease(NavigationPageInstance instance)
    {
        lock (Gate)
        {
            if (Owners.TryGetValue(instance.Page, out var stamp)
                && ReferenceEquals(stamp.Current, instance))
            {
                stamp.Current = null;
            }
        }
    }

    /// <summary>
    /// Attempts disposal on the original Dispatcher and relinquishes work when a factory supplies
    /// an owned object from the wrong Dispatcher.
    /// </summary>
    private static async Task ReleaseInvalidPageAsync(
        NavigationSession session, NavigationPageLease lease, NavigationWorkRegistry.Lease work)
    {
        try
        {
            using var callback = new NavigationCallbackContext(session);
            await lease.DisposeAsync();
        }
        catch (Exception failure)
        {
            session.Failures.Add(failure);
        }
        finally
        {
            work.Dispose();
        }
    }

    private sealed class Stamp
    {
        internal NavigationPageInstance? Current  { get; set; }
        internal bool                    WasOwned { get; set; }
    }
}